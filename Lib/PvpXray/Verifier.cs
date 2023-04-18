using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

namespace PvpXray
{
    public interface IPackageFile
    {
        string Path { get; }
        long Size { get; }
        Stream Content { get; }
    }

    public class VerifierContext
    {
        public List<string> Files { get; set; }
        public List<long> Sizes { get; set; }
        public byte[] Manifest { get; set; }
        public IPvpHttpClient HttpClient { get; set; }

        public VerifierContext()
        {
        }

        internal VerifierContext(IEnumerable<IPackageFile> files, IPvpHttpClient httpClient)
        {
            Files = new List<string>();
            Sizes = new List<long>();
            HttpClient = httpClient;

            foreach (var file in files)
            {
                Files.Add(file.Path);
                Sizes.Add(file.Size);

                if (file.Path == "package.json")
                {
                    Manifest = new byte[file.Size];
                    file.Content.CopyTo(new MemoryStream(Manifest));
                }
            }
        }
    }

    public sealed class CheckResult
    {
        public string SkipReason { get; internal set; }
        public List<string> Errors { get; internal set; }

        internal CheckResult()
        {
        }
    }

    public class Verifier
    {
        // Wrapper around public IPackageFile interface that reads file content into a buffer on demand
        // such that multiple IChecker implementations can read the same file in the same pass.
        internal class PackageFile
        {
            readonly IPackageFile m_File;
            byte[] m_Content;

            public PackageFile(IPackageFile file)
            {
                m_File = file;
                Path = file.Path;
                Size = file.Size;
            }

            public string Path { get; }
            public long Size { get; }

            public byte[] Content
            {
                get
                {
                    if (m_Content == null)
                    {
                        // Attempting to read the entire file into a byte array imposes a limit on the maximum supported file size.
                        // For .NET Core (and thus, upm-pvp xray) the limit is 2147483591 bytes.
                        // For Mono (and thus, Unity) the limit is Int32.MaxValue (2147483647) bytes (tested on 2019.2.21f1 and 2023.2.0a5).
                        // For consistency, always enforce the lower of the two limits.
                        const long maximumSize = 2147483591;
                        if (Size > maximumSize)
                        {
                            throw new InvalidOperationException($"Reading content of files bigger than {maximumSize} bytes is not supported");
                        }

                        m_Content = new byte[Size];
                        m_File.Content.CopyTo(new MemoryStream(m_Content));
                    }

                    return m_Content;
                }
            }
        }

        internal interface IChecker
        {
            void CheckItem(PackageFile file, int passIndex);
            void Finish();
        }

        // If some precondition fails, ALL dependent checks must be marked
        // as failed. This exception may be used to do so.
        internal class FailAllException : Exception
        {
            public FailAllException(string message) : base(message) { }
        }

        internal interface IContext
        {
            IReadOnlyList<string> Files { get; }
            Json Manifest { get; }
            void AddError(string checkId, string error);
            void Skip(string checkId, string reason);
            bool DirectoryExists(string path);
        }

        class Context : IContext
        {
            // Checks implemented directly in Context.
            public static string[] ContextChecks => new[] { "PVP-100-1" };

            public IReadOnlyList<string> Files { get; }
            public Json Manifest => m_Manifest ?? throw new FailAllException(m_ManifestError);
            public Dictionary<string, CheckResult> Results { get; }

            readonly HashSet<string> m_CurrentBatchChecks;
            readonly Json m_Manifest;
            readonly string m_ManifestError;
            readonly HashSet<string> m_PreviousErrors;

            public Context(VerifierContext verifierContext)
            {
                Results = new Dictionary<string, CheckResult>();
                foreach (var check in Checks)
                {
                    Results.Add(check, new CheckResult
                    {
                        Errors = new List<string>(),
                        SkipReason = null,
                    });
                }

                m_CurrentBatchChecks = new HashSet<string>();
                m_PreviousErrors = new HashSet<string>();
                Files = verifierContext.Files;

                try
                {
                    var text = Encoding.UTF8.GetString(verifierContext.Manifest);

                    // UTF-8 BOM is unwelcome, but we can proceed with verification.
                    if (text.StartsWithOrdinal("\ufeff"))
                    {
                        Results["PVP-100-1"].Errors.Add("manifest file contains UTF-8 BOM");
                        text = text.Substring(1);
                    }

                    m_Manifest = new Json(text);
                    m_ManifestError = null;
                }
                catch (Exception e)
                {
                    m_Manifest = null;
                    m_ManifestError = e is JsonException ? "package.json manifest is not valid JSON" : "package.json manifest could not be read";
                    Results["PVP-100-1"].Errors.Add(m_ManifestError);
                }
            }

            public void AddError(string checkId, string error)
            {
                // deduplicate errors when exactly identical
                if (m_PreviousErrors.Add($"{checkId}: {error}"))
                {
                    if (!m_CurrentBatchChecks.Contains(checkId))
                    {
                        throw new InvalidOperationException($"batch tried to add error with undeclared check ID {checkId}: {error}");
                    }

                    var result = Results[checkId];
                    if (result.SkipReason == null)
                    {
                        result.Errors.Add(error);
                    }
                }
            }

            public void Skip(string checkId, string reason)
            {
                if (!m_CurrentBatchChecks.Contains(checkId))
                {
                    throw new InvalidOperationException($"batch tried to skip undeclared check ID {checkId}: {reason}");
                }

                var result = Results[checkId];
                if (result.SkipReason == null)
                {
                    result.Errors.Clear();
                    result.SkipReason = reason;
                }
            }

            /// Determine if the specified path exists in package and is a directory. Note that
            /// IPackage never tracks empty directories, so this will return false in that case.
            public bool DirectoryExists(string path)
            {
                path += "/";
                return Files.Any(p => p.StartsWithOrdinal(path));
            }

            public void RunBatch(string[] checks, Action action)
            {
                m_CurrentBatchChecks.Clear();
                m_CurrentBatchChecks.UnionWith(checks);

                // Since check IDs shouldn't overlap between batches, clear this for a bit of performance.
                m_PreviousErrors.Clear();

                try
                {
                    action();
                }
                catch (FailAllException e)
                {
                    foreach (var check in checks)
                    {
                        AddError(check, e.Message);
                    }
                }
            }
        }

        static readonly Dictionary<Type, (string[], int)> k_CheckerTypes;

        public static string[] Checks { get; }
        public static int PassCount { get; }

        readonly Context m_Context;
        readonly Dictionary<Type, (IChecker, string[], int)> m_Checkers;

        static Verifier()
        {
            k_CheckerTypes = new Dictionary<Type, (string[], int)>();
            var allChecks = new HashSet<string>(Context.ContextChecks);
            var maxPassCount = 0;

            foreach (var type in typeof(Verifier).Assembly.GetTypes().Where(t => typeof(IChecker).IsAssignableFrom(t) && !t.IsAbstract))
            {
                var checks = InvokePublicStaticPropertyGetter<string[]>(type, "Checks");
                var overlappingChecks = allChecks.Intersect(checks).ToList();
                if (overlappingChecks.Count != 0) throw new InvalidOperationException($"{type} declares overlapping check ids with other IChecker implementations: {string.Join(", ", overlappingChecks)}");
                allChecks.UnionWith(checks);

                var passCount = InvokePublicStaticPropertyGetter<int>(type, "PassCount");
                if (passCount > maxPassCount) maxPassCount = passCount;

                k_CheckerTypes.Add(type, (checks.ToArray(), passCount));
            }

            Checks = allChecks.ToArray();
            PassCount = maxPassCount;
        }

        static T InvokePublicStaticPropertyGetter<T>(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty, null, typeof(T), Array.Empty<Type>(), null);
            if (property == null || property.GetMethod == null) throw new NotImplementedException($"{type} is missing required public static {typeof(T)} {name} property getter");
            return (T)property.GetMethod.Invoke(null, null);
        }

        public Verifier(VerifierContext verifierContext)
        {
            // TODO: New checks that require access to a network resource must be skipped immediately with reason "offline_requested" if httpClient is null.
            // TODO: New checks that call httpClient.GetStream must be skipped with reason "network_error" if it throws PvpHttpException.

            m_Context = new Context(verifierContext);
            m_Checkers = new Dictionary<Type, (IChecker, string[], int)>();

            var parameterTypes = new[] { typeof(IContext) };
            var parameterValues = new object[] { m_Context };
            foreach (var entry in k_CheckerTypes)
            {
                var type = entry.Key;
                var checks = entry.Value.Item1;
                var passCount = entry.Value.Item2;

                var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null)
                    ?? throw new NotImplementedException($"{type} is missing required public constructor with Verifier.IContext parameter");
                IChecker checker = null;

                void CreateChecker()
                {
                    try
                    {
                        checker = (IChecker)constructor.Invoke(parameterValues);
                    }
                    catch (TargetInvocationException e) when (e.InnerException != null)
                    {
                        ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                    }
                }

                m_Context.RunBatch(checks, CreateChecker);
                if (checker != null)
                {
                    m_Checkers.Add(type, (checker, checks, passCount));
                }
            }
        }

        /// file.Content must be disposed by the caller and is assumed to be a read-only
        /// non-seekable stream that is only valid for the duration of this method call.
        public void CheckItem(IPackageFile file, int passIndex)
        {
            var checkerFile = new PackageFile(file);

            foreach (var (checker, checks, passCount) in m_Checkers.Values)
            {
                if (passIndex < passCount)
                {
                    m_Context.RunBatch(checks, () => checker.CheckItem(checkerFile, passIndex));
                }
            }
        }

        public Dictionary<string, CheckResult> Finish()
        {
            foreach (var (checker, checks, _) in m_Checkers.Values)
            {
                m_Context.RunBatch(checks, checker.Finish);
            }

            return m_Context.Results;
        }

        public static Dictionary<string, CheckResult> OneShot(IEnumerable<IPackageFile> files, IPvpHttpClient httpClient)
        {
            var context = new VerifierContext(files, httpClient);
            var verifier = new Verifier(context);

            for (var passIndex = 0; passIndex < PassCount; passIndex++)
            {
                foreach (var file in files)
                {
                    verifier.CheckItem(file, passIndex);
                }
            }

            return verifier.Finish();
        }
    }
}
