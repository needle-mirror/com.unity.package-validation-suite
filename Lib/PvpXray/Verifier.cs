using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

namespace PvpXray
{
    interface IPackageFile
    {
        string Path { get; }
        long Size { get; }
        Stream Content { get; }
    }

    class VerifierContext
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
                    XrayUtils.ReadExactly(file.Content, Manifest);
                }
            }
        }
    }

    class ResultFileStub
    {
        public Dictionary<string, string> Baselines { get; } = new Dictionary<string, string>();
        public Dictionary<string, CheckResult> Results { get; } = new Dictionary<string, CheckResult>();
    }

    sealed class CheckResult
    {
        public string SkipReason { get; internal set; }
        public List<string> Errors { get; internal set; }
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
                        if (Size > XrayUtils.MaxByteArrayLength)
                        {
                            throw new FailAllException($"Cannot read file {Path} which, at {Size} bytes, exceeds the limit of {XrayUtils.MaxByteArrayLength} bytes");
                        }

                        m_Content = new byte[Size];
                        XrayUtils.ReadExactly(m_File.Content, m_Content);
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

        // If some precondition fails, ALL dependent checks must be marked
        // as failed. This exception may be used to do so.
        internal class SkipAllException : Exception
        {
            public SkipAllException(string reason) : base(reason) { }
        }

        internal interface IContext
        {
            IReadOnlyList<string> Files { get; }
            IPvpHttpClient HttpClient { get; }
            Json Manifest { get; }
            void AddError(string checkId, string error);
            void Skip(string checkId, string reason);
            bool DirectoryExists(string path);
            void SetBlobBaseline(string name, string hash);
        }

        class Context : IContext
        {
            // Checks implemented directly in Context.
            public static string[] ContextChecks => new[] { "PVP-100-1", "PVP-100-2" };

            public IReadOnlyList<string> Files { get; }
            public IPvpHttpClient HttpClient => m_HttpClient ?? throw new SkipAllException("offline_requested");
            public Json Manifest => m_Manifest ?? throw new FailAllException(m_ManifestError);
            public ResultFileStub ResultFile { get; }

            readonly HashSet<string> m_CurrentBatchChecks;
            readonly IPvpHttpClient m_HttpClient;
            readonly Json m_Manifest;
            readonly string m_ManifestError;
            readonly HashSet<string> m_PreviousErrors;

            public Context(VerifierContext verifierContext)
            {
                ResultFile = new ResultFileStub();
                foreach (var check in Checks)
                {
                    ResultFile.Results.Add(check, new CheckResult
                    {
                        Errors = new List<string>(),
                        SkipReason = null,
                    });
                }

                m_CurrentBatchChecks = new HashSet<string>();
                m_PreviousErrors = new HashSet<string>();
                Files = verifierContext.Files;
                m_HttpClient = verifierContext.HttpClient;

                try
                {
                    string text;
                    try
                    {
                        text = XrayUtils.Utf8Strict.GetString(verifierContext.Manifest);
                    }
                    catch (DecoderFallbackException)
                    {
                        // If strict decoding fails, decode with replacement.
                        text = Encoding.UTF8.GetString(verifierContext.Manifest);

                        // If Encoding.UTF8 also throws, the following error is
                        // intentionally not added (but an error is added below).
                        ResultFile.Results["PVP-100-2"].Errors.Add("package.json: contains invalid UTF-8");
                    }

                    // UTF-8 BOM is unwelcome, but we can proceed with verification.
                    if (text.StartsWithOrdinal("\ufeff"))
                    {
                        ResultFile.Results["PVP-100-1"].Errors.Add("manifest file contains UTF-8 BOM");
                        ResultFile.Results["PVP-100-2"].Errors.Add("package.json: contains UTF-8 BOM");
                        text = text.Substring(1);
                    }

                    m_Manifest = new Json(text);
                    m_ManifestError = null;
                }
                catch (Exception e)
                {
                    m_Manifest = null;
                    m_ManifestError = e is SimpleJsonException ? "package.json manifest is not valid JSON" : "package.json manifest could not be read";
                    ResultFile.Results["PVP-100-1"].Errors.Add(m_ManifestError);
                    ResultFile.Results["PVP-100-2"].Errors.Add(e is SimpleJsonException ? $"package.json: {e.Message}" : "package.json: could not be read");
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

                    var result = ResultFile.Results[checkId];
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

                var result = ResultFile.Results[checkId];
                if (result.SkipReason == null)
                {
                    result.Errors.Clear();
                    result.SkipReason = reason;
                }
            }

            public void SetBlobBaseline(string id, string hash)
            {
                if (hash.Length != 64) throw new ArgumentException("invalid blob hash");
                try
                {
                    ResultFile.Baselines.Add($"blob:{id}", $"\"{hash}\"");
                }
                catch (ArgumentException)
                {
                    throw new InvalidOperationException($"blob baseline with same ID set multiple times: {id}");
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
                catch (PvpHttpException)
                {
                    foreach (var check in Checks)
                    {
                        Skip(check, "network_error");
                    }
                }
                catch (SkipAllException e)
                {
                    foreach (var check in checks)
                    {
                        Skip(check, e.Message);
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

        internal Verifier(VerifierContext verifierContext)
        {
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
                // If constructor threw PvpHttpException, SkipAllException or
                // FailAllException, checker will be null here, and we won't be
                // calling CheckItem or Finish on the checker.
                if (checker != null)
                {
                    m_Checkers.Add(type, (checker, checks, passCount));
                }
            }
        }

        /// file.Content must be disposed by the caller and is assumed to be a read-only
        /// non-seekable stream that is only valid for the duration of this method call.
        internal void CheckItem(IPackageFile file, int passIndex)
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

        internal ResultFileStub Finish()
        {
            foreach (var (checker, checks, _) in m_Checkers.Values)
            {
                m_Context.RunBatch(checks, checker.Finish);
            }

            return m_Context.ResultFile;
        }

        internal static ResultFileStub OneShot(IEnumerable<IPackageFile> files, IPvpHttpClient httpClient)
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
