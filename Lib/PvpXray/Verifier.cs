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
        public Func<string, string> GetXrayEnv { get; set; }
        public List<string> Files { get; set; }
        public List<long> Sizes { get; set; }
        public byte[] Manifest { get; set; }
        public IPvpHttpClient HttpClient { get; set; }

        public VerifierContext()
        {
        }

        internal VerifierContext(IEnumerable<IPackageFile> files)
        {
            Files = new List<string>();
            Sizes = new List<long>();

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

    struct FileExt
    {
        // "TextFile" refers only to file types developers are likely to edit
        // by hand, and which should therefore be checked for well-formedness.
        public const byte TextFile = 0x01;
        public const byte UnityYaml = 0x02; // Unity assets serialized as YAML with "%YAML " prefix in text mode.
        public const byte V1 = 0x04;
        public const byte V2 = 0x08;

        static readonly Dictionary<string, int> k_IndexByLowerExtension;
        static readonly string[] k_Canonical;
        static readonly byte[] k_Flags;

        static FileExt()
        {
            var rawData = new[] {
                ( "", 0 ),
                ( ".asmdef", V2 | TextFile ),
                ( ".asmref", V2 | TextFile ),
                ( ".cginc", V1 | TextFile ),
                ( ".compute", V1 | TextFile ),
                ( ".cpp", V1 | TextFile ),
                ( ".cs", V1 | TextFile ),
                ( ".h", V1 | TextFile ),
                ( ".hlsl", V1 | TextFile ),
                ( ".java", V2 | TextFile ),
                ( ".js", V1 | TextFile ),
                ( ".json", V1 | TextFile ),
                ( ".m", V2 | TextFile ),
                ( ".md", V1 | TextFile ),
                ( ".mm", V2 | TextFile ),
                ( ".plist", V2 | TextFile ),
                ( ".py", V1 | TextFile ),
                ( ".shader", V1 | TextFile ),
                ( ".txt", V1 | TextFile ),
                ( ".uss", V1 | TextFile ),
                ( ".uxml", V1 | TextFile ),
                ( ".yaml", V1 | TextFile ),
                ( ".yml", V1 | TextFile ),

                // Various common file extensions (20+ occurrences across a large sample of packages).
                ( ".aar", V2 ),
                ( ".dll", V2 ),
                ( ".exr", V2 ),
                ( ".fbx", V2 ),
                ( ".gif", V2 ),
                ( ".jpeg", V2 ),
                ( ".jpg", V2 ),
                ( ".mov", V2 ),
                ( ".mp3", V2 ),
                ( ".pdb", V2 ),
                ( ".png", V2 ),
                ( ".po", V2 ),
                ( ".psd", V2 ),
                ( ".so", V2 ),
                ( ".strings", V2 ),
                ( ".svg", V2 ),
                ( ".tga", V2 ),
                ( ".tgz", V2 ),
                ( ".tif", V2 ),
                ( ".tiff", V2 ),
                ( ".ttf", V2 ),
                ( ".wav", V2 ),
                ( ".xml", V2 ),

                // Unity YAML extensions as of 2319b08a8846 (pre-2023.3.0a1)
                // from Modules/AssetPipelineEditor/Public/NativeFormatImporterExtensions.h
                ( ".anim", V2 | UnityYaml ),
                ( ".animset", V2 | UnityYaml ),
                ( ".asset", V2 | UnityYaml ),
                ( ".blendtree", V2 | UnityYaml ),
                ( ".brush", V2 | UnityYaml ),
                ( ".buildreport", V2 | UnityYaml ),
                ( ".colors", V2 | UnityYaml ),
                ( ".controller", V2 | UnityYaml ),
                ( ".cubemap", V2 | UnityYaml ),
                ( ".curves", V2 | UnityYaml ),
                ( ".curvesNormalized", V2 | UnityYaml ),
                ( ".flare", V2 | UnityYaml ),
                ( ".fontsettings", V2 | UnityYaml ),
                ( ".giparams", V2 | UnityYaml ),
                ( ".gradients", V2 | UnityYaml ),
                ( ".guiskin", V2 | UnityYaml ),
                ( ".ht", V2 | UnityYaml ),
                ( ".lighting", V2 | UnityYaml ),
                ( ".mask", V2 | UnityYaml ),
                ( ".mat", V2 | UnityYaml ),
                ( ".mesh", V2 | UnityYaml ),
                ( ".mixer", V2 | UnityYaml ),
                ( ".overrideController", V2 | UnityYaml ),
                ( ".particleCurves", V2 | UnityYaml ),
                ( ".particleCurvesSigned", V2 | UnityYaml ),
                ( ".particleDoubleCurves", V2 | UnityYaml ),
                ( ".particleDoubleCurvesSigned", V2 | UnityYaml ),
                ( ".physicMaterial", V2 | UnityYaml ),
                ( ".physicsMaterial2D", V2 | UnityYaml ),
                ( ".playable", V2 | UnityYaml ),
                ( ".preset", V2 | UnityYaml ),
                ( ".renderTexture", V2 | UnityYaml ),
                ( ".scenetemplate", V2 | UnityYaml ),
                ( ".shadervariants", V2 | UnityYaml ),
                ( ".signal", V2 | UnityYaml ),
                ( ".spriteatlas", V2 | UnityYaml ),
                ( ".state", V2 | UnityYaml ),
                ( ".statemachine", V2 | UnityYaml ),
                ( ".terrainlayer", V2 | UnityYaml ),
                ( ".texture2D", V2 | UnityYaml ),
                ( ".transition", V2 | UnityYaml ),
                ( ".webCamTexture", V2 | UnityYaml ),

                // Other Unity YAML extensions, not listed in NativeFormatImporterExtensions.h.
                ( ".meta", V2 ), // Always serialized as YAML but without "%YAML " prefix.
                ( ".prefab", V2 | UnityYaml ),
                ( ".unity", V2 | UnityYaml ),
                ( ".vfxoperator", V2 | UnityYaml ),
                ( ".wlt", V2 | UnityYaml ),

                // Other Unity extensions, not using YAML serialization
                ( ".shadergraph", V2 ),
                ( ".shadersubgraph", V2 ),
                ( ".unitypackage", V2 ),
            };

            k_IndexByLowerExtension = new Dictionary<string, int>(rawData.Length);
            k_Canonical = new string[rawData.Length];
            k_Flags = new byte[rawData.Length];
            for (var i = 0; i < rawData.Length; ++i)
            {
                var (canonical, flags) = rawData[i];
                // index 0 is reserved for "extension not found".
                if (i != 0) k_IndexByLowerExtension.Add(canonical.ToLowerInvariant(), i);
                k_Canonical[i] = canonical;
                k_Flags[i] = (byte)flags;
            }
        }

        public static void GetFileExtension(string path, out string suffix, out bool isProperExtension)
        {
            for (var i = path.Length - 1; i >= 0; --i)
            {
                if (path[i] == '.')
                {
                    suffix = path.Substring(i);
                    isProperExtension = i > 0 && path[i - 1] != '/';
                    return;
                }

                if (path[i] == '/') break;
            }
            suffix = "";
            isProperExtension = false;
        }

        public FileExt(string extension)
        {
            Raw = extension;
            if (extension.Length == 0 || !k_IndexByLowerExtension.TryGetValue(extension.ToLowerInvariant(), out m_Index)) m_Index = 0;
        }

        readonly int m_Index;

        /// File extension with the leading dot, or blank if none.
        public string Raw { get; }

        /// File extension with canonical capitalization.
        public string Canonical => k_Canonical[m_Index];

        public bool IsCanonical => Raw == Canonical;

        public bool HasFlags(byte flagSet)
        {
            return (k_Flags[m_Index] & flagSet) != 0;
        }

        public bool HasFlags(byte flagSet1, byte flagSet2)
        {
            var flags = k_Flags[m_Index];
            return (flags & flagSet1) != 0 && (flags & flagSet2) != 0;
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

                FileExt.GetFileExtension(Path, out var suffix, out var isProperExtension);
                Suffix = new FileExt(suffix);
                Extension = isProperExtension ? Suffix : new FileExt("");
            }

            public string Path { get; }
            public long Size { get; }

            /// For legacy reasons, we track both file extensions and file suffixes.
            /// ".gitignore" has no file extension, but it has a suffix.
            /// _Usually_ the Extension is what should be used.
            public FileExt Extension { get; }
            public FileExt Suffix { get; }

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
            Func<string, string> GetXrayEnv { get; }
            List<(string, string)> ManifestContextErrors { get; }
            IReadOnlyList<string> Files { get; }
            IPvpHttpClient HttpClient { get; }
            Json Manifest { get; }
            void AddError(string checkId, string error);
            void Skip(string checkId, string reason);
            bool DirectoryExists(string path);
            void SetBlobBaseline(string name, string hash);
        }

        internal struct CheckerMeta
        {
            public readonly Type Type;
            public readonly string[] Checks;
            public readonly int PassCount;

            public CheckerMeta(Type type)
            {
                Type = type;
                Checks = InvokePublicStaticPropertyGetter<string[]>(type, "Checks");
                PassCount = InvokePublicStaticPropertyGetter<int>(type, "PassCount");
            }

            static T InvokePublicStaticPropertyGetter<T>(Type type, string name)
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty, null, typeof(T), Array.Empty<Type>(), null);
                if (property == null || property.GetMethod == null) throw new NotImplementedException($"{type} is missing required public static {typeof(T)} {name} property getter");
                return (T)property.GetMethod.Invoke(null, null);
            }
        }

        internal class CheckerSet
        {
            public static readonly CheckerSet PvsCheckers;

            static CheckerSet()
            {
                var allCheckers = typeof(Verifier).Assembly.GetTypes()
                    .Where(t => !t.IsAbstract && typeof(IChecker).IsAssignableFrom(t))
                    .Select(t => new CheckerMeta(t))
                    .ToList();

                PvsCheckers = new CheckerSet(allCheckers);
            }

            internal readonly List<CheckerMeta> Checkers;
            internal readonly string[] Checks;
            internal readonly int PassCount;

            public CheckerSet(List<CheckerMeta> checkers)
            {
                Checkers = checkers;
                PassCount = 0;

                var checkCount = 0;
                foreach (var checker in checkers)
                {
                    if (checker.PassCount > PassCount) PassCount = checker.PassCount;
                    checkCount += checker.Checks.Length;
                }

                Checks = checkers.SelectMany(c => c.Checks).Distinct().ToArray();
                if (checkCount != Checks.Length) throw new InvalidOperationException("internal error: check ID repeated in one or more ICheckers");
            }
        }

        class Context : IContext
        {
            public Func<string, string> GetXrayEnv { get; }
            public IReadOnlyList<string> Files { get; }
            public IPvpHttpClient HttpClient => m_HttpClient ?? throw new SkipAllException("offline_requested");
            public Json Manifest => m_Manifest ?? throw new FailAllException(m_ManifestError);
            public ResultFileStub ResultFile { get; }
            public List<(string, string)> ManifestContextErrors { get; } = new List<(string, string)>();

            readonly HashSet<string> m_CurrentBatchChecks;
            readonly IPvpHttpClient m_HttpClient;
            readonly Json m_Manifest;
            readonly string m_ManifestError;
            readonly HashSet<string> m_PreviousErrors;

            public Context(VerifierContext verifierContext, CheckerSet checkerSet)
            {
                ResultFile = new ResultFileStub();
                foreach (var check in checkerSet.Checks)
                {
                    ResultFile.Results.Add(check, new CheckResult
                    {
                        Errors = new List<string>(),
                        SkipReason = null,
                    });
                }

                m_CurrentBatchChecks = new HashSet<string>();
                m_PreviousErrors = new HashSet<string>();
                GetXrayEnv = verifierContext.GetXrayEnv ?? Environment.GetEnvironmentVariable;
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
                        ManifestContextErrors.Add(("PVP-100-2", "package.json: contains invalid UTF-8"));
                    }

                    // UTF-8 BOM is unwelcome, but we can proceed with verification.
                    if (text.StartsWithOrdinal("\ufeff"))
                    {
                        ManifestContextErrors.Add(("PVP-100-1", "manifest file contains UTF-8 BOM"));
                        ManifestContextErrors.Add(("PVP-100-2", "package.json: contains UTF-8 BOM"));
                        text = text.Substring(1);
                    }

                    m_Manifest = new Json(text, "package.json");
                    m_ManifestError = null;
                }
                catch (Exception e)
                {
                    m_Manifest = null;
                    m_ManifestError = e is SimpleJsonException ? "package.json manifest is not valid JSON" : "package.json manifest could not be read";
                    ManifestContextErrors.Add(("PVP-100-1", m_ManifestError));
                    ManifestContextErrors.Add(("PVP-100-2", e is SimpleJsonException sje ? sje.FullMessage : "package.json: could not be read"));
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
                    foreach (var check in checks)
                    {
                        Skip(check, "network_error");
                    }
                }
                catch (SimpleJsonException e) when (e.PackageFilePath != null)
                {
                    foreach (var check in checks)
                    {
                        AddError(check, e.FullMessage);
                    }
                }
                catch (YamlParseException e) when (e.PackageFilePath != null)
                {
                    foreach (var check in checks)
                    {
                        AddError(check, e.FullMessage);
                    }
                }
                catch (YamlAccessException e) when (e.PackageFilePath != null)
                {
                    foreach (var check in checks)
                    {
                        AddError(check, e.FullMessage);
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

        public static string[] Checks => CheckerSet.PvsCheckers.Checks;
        public static int PassCount => CheckerSet.PvsCheckers.PassCount;

        readonly Context m_Context;
        readonly List<(IChecker, CheckerMeta)> m_Checkers;

        internal Verifier(VerifierContext verifierContext, CheckerSet checkerSet = null)
        {
            checkerSet = checkerSet ?? CheckerSet.PvsCheckers;
            m_Context = new Context(verifierContext, checkerSet);
            m_Checkers = new List<(IChecker, CheckerMeta)>();

            var parameterTypes = new[] { typeof(IContext) };
            var parameterValues = new object[] { m_Context };
            foreach (var meta in checkerSet.Checkers)
            {
                var constructor = meta.Type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null)
                    ?? throw new NotImplementedException($"{meta.Type} is missing required public constructor with Verifier.IContext parameter");
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

                m_Context.RunBatch(meta.Checks, CreateChecker);
                // If constructor threw PvpHttpException, SkipAllException or
                // FailAllException, checker will be null here, and we won't be
                // calling CheckItem or Finish on the checker.
                if (checker != null)
                {
                    m_Checkers.Add((checker, meta));
                }
            }
        }

        /// file.Content must be disposed by the caller and is assumed to be a read-only
        /// non-seekable stream that is only valid for the duration of this method call.
        internal void CheckItem(IPackageFile file, int passIndex)
        {
            var checkerFile = new PackageFile(file);

            foreach (var (checker, meta) in m_Checkers)
            {
                if (passIndex < meta.PassCount)
                {
                    m_Context.RunBatch(meta.Checks, () => checker.CheckItem(checkerFile, passIndex));
                }
            }
        }

        internal ResultFileStub Finish()
        {
            foreach (var (checker, meta) in m_Checkers)
            {
                m_Context.RunBatch(meta.Checks, checker.Finish);
            }

            return m_Context.ResultFile;
        }

        internal static ResultFileStub OneShot(IEnumerable<IPackageFile> files, IPvpHttpClient httpClient, CheckerSet checkerSet = null, Func<string, string> getXrayEnv = null)
        {
            checkerSet = checkerSet ?? CheckerSet.PvsCheckers;
            var context = new VerifierContext(files)
            {
                GetXrayEnv = getXrayEnv,
                HttpClient = httpClient,
            };
            var verifier = new Verifier(context, checkerSet);

            for (var passIndex = 0; passIndex < checkerSet.PassCount; passIndex++)
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
