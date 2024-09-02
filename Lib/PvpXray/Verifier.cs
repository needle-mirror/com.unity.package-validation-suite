using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PvpXray
{
    interface IPackageFile
    {
        string Path { get; }
        long Size { get; }
        Stream Content { get; }
    }

    class MemoryPackageFile : IPackageFile, IDisposable
    {
        readonly Stream m_Content;
        bool m_Disposed;

        public MemoryPackageFile(string path, byte[] content)
            : this(path, content, content.Length)
        {
        }

        public MemoryPackageFile(string path, byte[] content, int size)
        {
            Path = path;
            Size = size;
            m_Content = new ThrowOnDisposedStream(new MemoryStream(content, 0, size), preventSeek: true);
        }

        public string Path { get; }
        public long Size { get; }

        public Stream Content
        {
            get
            {
                if (m_Disposed) throw new ObjectDisposedException(GetType().FullName, "cannot get Content stream");
                return m_Content;
            }
        }

        public void Dispose()
        {
            m_Disposed = true;
            m_Content.Dispose();
        }
    }

    class VerifierContext
    {
        public class PublishSetPackage
        {
            internal Verifier.PackageBaseline Baseline;
            internal readonly PackageId Id;
            public byte[] Manifest { get; }

            public PublishSetPackage(string sha1, byte[] manifest)
            {
                if (!XrayUtils.Sha1Regex.IsMatch(sha1)) throw new ArgumentException(nameof(sha1));

                // Convert the manifest to string in "relaxed" mode; strict validation will happen elsewhere.
                var manifestText = XrayUtils.DecodeUtf8Lax(manifest);

                Baseline = new Verifier.PackageBaseline
                {
                    Sha1 = sha1,
                };
                Manifest = manifest;

                try
                {
                    Baseline.Manifest = new Json(manifestText, null, permitInvalidJson: true);
                    Id = new PackageId(Baseline.Manifest);
                }
                catch (SimpleJsonException)
                {
                }
            }
        }

        // Mandatory context (package under test)
        public List<string> Files { get; set; }
        public List<long> Sizes { get; set; }
        public byte[] Manifest { get; set; }

        // Optional context
        public Func<string, string> GetXrayEnv { get; set; }
        public IPvpHttpClient HttpClient { get; set; }
        public List<PublishSetPackage> PublishSet { get; set; }

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
                    file.Content.ReadExactly(Manifest);
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
        public const byte V2 = 0x04;

        static readonly Dictionary<string, int> k_IndexByLowerExtension;
        static readonly string[] k_Canonical;
        static readonly byte[] k_Flags;

        static FileExt()
        {
            var rawData = new[] {
                ( "", 0 ),
                ( ".asmdef", V2 | TextFile ),
                ( ".asmref", V2 | TextFile ),
                ( ".cginc", V2 | TextFile ),
                ( ".compute", V2 | TextFile ),
                ( ".cpp", V2 | TextFile ),
                ( ".cs", V2 | TextFile ),
                ( ".h", V2 | TextFile ),
                ( ".hlsl", V2 | TextFile ),
                ( ".java", V2 | TextFile ),
                ( ".js", V2 | TextFile ),
                ( ".json", V2 | TextFile ),
                ( ".m", V2 | TextFile ),
                ( ".md", V2 | TextFile ),
                ( ".mm", V2 | TextFile ),
                ( ".plist", V2 | TextFile ),
                ( ".py", V2 | TextFile ),
                ( ".shader", V2 | TextFile ),
                ( ".txt", V2 | TextFile ),
                ( ".uss", V2 | TextFile ),
                ( ".uxml", V2 | TextFile ),
                ( ".yaml", V2 | TextFile ),
                ( ".yml", V2 | TextFile ),

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

    class PathEntry
    {
        readonly string m_Extension;

        public string[] Components { get; }
        public string Filename { get; }
        public bool IsDirectory { get; } // usually false, as by default, directories are not enumerated
        /// <summary>Do not use (except for backwards compatibility). Incorrectly treats plugin directories directories as importable.</summary>
        public bool IsHiddenLegacy { get; }
        /// <summary>Do not use (except for backwards compatibility). Does not account for samples. Prefer <see cref="IsAsset"/>.</summary>
        public bool IsHiddenLegacy2 { get; }
        /// <summary>For specialized use only; prefer simply using <see cref="IsAsset"/>.</summary>
        public bool IsInsidePluginDirectory { get; }
        /// <summary>Is this a (visible) regular asset (not inside Samples~)? Prefer <see cref="IsAsset"/>.</summary>
        public bool IsRegularAsset { get; }
        /// <summary>Is this a (visible) asset inside Samples~? Prefer <see cref="IsAsset"/>.</summary>
        public bool IsSampleAsset { get; }
        public string Path { get; }
        public string PathWithCase { get; }

        public string DirectoryWithCase => Components.Length == 1 ? "" : PathWithCase.Substring(0, PathWithCase.Length - Filename.Length - 1);
        public StringSlice FilenameWithCase => PathWithCase.Slice(PathWithCase.Length - Filename.Length);
        public StringSlice FilenameWithCaseNoExtension => PathWithCase.Slice(PathWithCase.Length - Filename.Length, PathWithCase.Length - m_Extension.Length);

        public PathEntry(string path, Verifier.AssetVisibility assetVisibility, bool isDirectory = false)
        {
            // Note: avoid .NET Path APIs here; they are poorly documented and may have platform-specific quirks.

            IsDirectory = isDirectory;
            Path = path.ToLowerInvariant();
            PathWithCase = path;
            Components = Path.Split('/');
            Filename = Components[Components.Length - 1];

            var i = Filename.LastIndexOf('.');
            // 'i > 0' because the extension of ".gitignore" is not ".gitignore".
            m_Extension = i > 0 ? Filename.Substring(i) : "";

            IsHiddenLegacy = !IsDirectory && m_Extension == ".tmp"; // updated below

            var samplePrefixLevels = assetVisibility.SingleTopLevelSample ? 1 : 2;
            IsSampleAsset = Components.Length > samplePrefixLevels && Components[0] == "samples~"; // updated below

            for (i = 0; i < Components.Length; i++)
            {
                var name = Components[i];
                // Files are considered "hidden" (and not imported by the asset pipeline) subject
                // to the patterns given here: https://docs.unity3d.com/Manual/SpecialFolders.html
                // (Implementation appears to be in Runtime/VirtualFileSystem/LocalFileSystem.h)
                if (name[0] == '.' || name[name.Length - 1] == '~' || name == "cvs")
                {
                    IsHiddenLegacy = true;
                    if (i >= samplePrefixLevels) IsSampleAsset = false;
                }

                // As of 2023.1.0a24 and corresponding backports (UUM-9421), Unity will ignore
                // files inside directories with certain file extensions IF a plugin has been
                // registered for that path. Whether files are "hidden" or not can thus no longer
                // be determined from the path alone, but depends on the exact Unity patch version
                // and runtime plugin config.
                // But for PVP, we assume that such paths are always plugins. For details, see:
                // - https://github.cds.internal.unity3d.com/unity/unity/pull/19042
                // - PluginImporter::GetLoadableDirectoryExtensionTypes
                if (!IsInsidePluginDirectory && i != Components.Length - 1)
                {
                    // Note: we always ignore the last component (regardless of isDirectory), as
                    // the plugin directory itself is still an asset (only its content is ignored).
                    IsInsidePluginDirectory =
                        name.EndsWithOrdinal(".androidlib") ||
                        name.EndsWithOrdinal(".bundle") ||
                        name.EndsWithOrdinal(".framework") ||
                        name.EndsWithOrdinal(".plugin");

                    if (IsInsidePluginDirectory && !assetVisibility.TargetUnityImportsPluginDirs && i >= samplePrefixLevels) IsSampleAsset = false;
                }
            }

            IsHiddenLegacy2 = IsHiddenLegacy || (IsInsidePluginDirectory && !assetVisibility.TargetUnityImportsPluginDirsLegacy);
            IsRegularAsset = !(IsHiddenLegacy || (IsInsidePluginDirectory && !assetVisibility.TargetUnityImportsPluginDirs));
        }

        public bool IsAsset(bool includeSamples) => includeSamples ? IsSampleAsset || IsRegularAsset : IsRegularAsset;

        public bool HasComponent(params string[] components) => Components.Any(components.Contains);
        public bool HasDirectoryComponent(params string[] components) => Components.Take(Components.Length - 1).Any(components.Contains);
        public bool HasExtension(string extension) => m_Extension == extension;
        public bool HasExtension(params string[] extensions) => extensions.Contains(m_Extension);
        public bool HasFilename(params string[] filenames) => filenames.Contains(Filename);
    }

    struct UnityMajor : IComparable<UnityMajor>, IEquatable<UnityMajor>
    {
        static readonly Regex k_ValidMajor = new Regex(@"^(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})$");
        public static readonly UnityMajor Any = new UnityMajor { X = -1, Y = 0, Version = "" };

        public string Version { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        public UnityMajor(string version)
        {
            Version = version ?? throw new ArgumentNullException();
            var m = k_ValidMajor.Match(version);
            if (!m.Success) throw new ArgumentException();
            X = int.Parse(m.Groups[1].Value);
            Y = int.Parse(m.Groups[2].Value);
        }

        public int CompareTo(UnityMajor other) => X != other.X ? X - other.X : Y - other.Y;
        public bool Equals(UnityMajor other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is UnityMajor other && Equals(other);
        public override int GetHashCode() => X ^ (Y << 16);
        public override string ToString() => Version;

        public static UnityMajor FromFull(string version)
        {
            var i = version.IndexOf('.', version.IndexOf('.') + 1);
            if (i <= 0) throw new ArgumentException();
            return new UnityMajor(version.Substring(0, i));
        }

        /// Note this can construct minors larger than what's possible
        /// from a string input, i.e. "X.1000000000" and later.
        public UnityMajor Next => new UnityMajor { X = X, Y = Y + 1, Version = $"{X}.{Y + 1}" };
    }

    public class Verifier
    {
        // Wrapper around public IPackageFile interface that reads file content into a buffer on demand
        // such that multiple IChecker implementations can read the same file in the same pass.
        internal class PackageFile
        {
            readonly IPackageFile m_File;
            byte[] m_Content;

            public PackageFile(IPackageFile file, AssetVisibility assetVisibility)
            {
                m_File = file;
                Entry = new PathEntry(file.Path, assetVisibility, isDirectory: false);
                Path = file.Path;
                Size = file.Size;

                FileExt.GetFileExtension(Path, out var suffix, out var isProperExtension);
                Suffix = new FileExt(suffix);
                Extension = isProperExtension ? Suffix : new FileExt("");
            }

            public string Path { get; }
            public PathEntry Entry { get; }
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
                        m_File.Content.ReadExactly(m_Content);
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
            public static SkipAllException InvalidBaseline => new SkipAllException("invalid_baseline");
        }

        internal struct PackageBaseline
        {
            public Json Manifest;
            public string Sha1;
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

            internal CheckerSet(Type checker)
                : this(new List<CheckerMeta> { new CheckerMeta(checker) })
            {
            }

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

        // Context that determines which files count as assets and which are
        // hidden from the asset pipeline.
        internal struct AssetVisibility
        {
            public bool SingleTopLevelSample;
            public bool TargetUnityImportsPluginDirs;
            public bool TargetUnityImportsPluginDirsLegacy;
        }

        internal class Context
        {
            public readonly AssetVisibility AssetVisibility;
            public Func<string, string> GetXrayEnv { get; }
            public IReadOnlyList<string> Files { get; }
            public bool IsLegacyCheckerEmittingLegacyJsonErrors { get; set; }
            public IReadOnlyList<PathEntry> PathEntries { get; }
            public IPvpHttpClient HttpClient => m_HttpClient ?? throw new SkipAllException("offline_requested");
            // note difference: the legacy `ManifestPermitInvalidJson` throws FailAllException, whereas
            // the new `Manifest` throws SimpleJsonException as if it had actually just attempted to parse the manifest.
            public Json Manifest => m_Manifest ?? throw new SimpleJsonException(m_ManifestJsonError, null) { PackageFilePath = "package.json" };
            public Json ManifestPermitInvalidJson => m_ManifestPermitInvalidJson ?? throw new FailAllException(m_ManifestErrorPermitInvalidJson);
            public List<(string, string)> ManifestContextErrors { get; } = new List<(string, string)>();

            readonly HashSet<string> m_CurrentBatchChecks;
            readonly IPvpHttpClient m_HttpClient;
            readonly Json m_Manifest;
            readonly Json m_ManifestPermitInvalidJson;
            readonly string m_ManifestJsonError;
            readonly string m_ManifestErrorPermitInvalidJson;
            readonly HashSet<string> m_PreviousErrors;
            readonly Dictionary<string, Dictionary<string, object>> m_ProductionRegistryVersions = new Dictionary<string, Dictionary<string, object>>();
            readonly ResultFileStub m_ResultFile;
            readonly Dictionary<PackageId, PackageBaseline> m_PublishSetBaselines = new Dictionary<PackageId, PackageBaseline>();
            readonly Dictionary<string, HashSet<PackageId>> m_EditorManifestPackages = new Dictionary<string, HashSet<PackageId>>();
            List<UnityMajor> m_SupportedEditorsBaseline;
            string m_SupportedEditorsBaselineSkipReason;

            public Context(VerifierContext verifierContext, CheckerSet checkerSet, ResultFileStub resultFile)
            {
                foreach (var pkg in verifierContext.PublishSet ?? Enumerable.Empty<VerifierContext.PublishSetPackage>())
                {
                    if (pkg.Baseline.Manifest == null)
                    {
                        // Ignore entries in the publish set with invalid JSON in their manifests.
                        continue;
                    }

                    try
                    {
                        m_PublishSetBaselines.Add(pkg.Id, pkg.Baseline);
                    }
                    catch (ArgumentException)
                    {
                        // Collision in package ID = ambiguous publish set. Give up on doing anything with it.
                        m_PublishSetBaselines = null;
                        break;
                    }
                }

                m_ResultFile = resultFile;
                foreach (var check in checkerSet.Checks)
                {
                    m_ResultFile.Results.Add(check, new CheckResult
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

                if (verifierContext.Manifest == null)
                {
                    m_ManifestJsonError = "manifest file not present";
                    m_ManifestErrorPermitInvalidJson = "package.json manifest could not be read";
                    ManifestContextErrors.Add(("PVP-100-1", m_ManifestErrorPermitInvalidJson));
                    ManifestContextErrors.Add(("PVP-100-2", "package.json: could not be read"));
                }
                else
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

                    try
                    {
                        try
                        {
                            m_Manifest = m_ManifestPermitInvalidJson = new Json(text, "package.json", permitInvalidJson: false);
                            AssetVisibility.SingleTopLevelSample = HasSingleTopLevelSample(m_Manifest);
                            AssetVisibility.TargetUnityImportsPluginDirs = AssetVisibility.TargetUnityImportsPluginDirsLegacy = GetTargetUnityImportsPluginDirs(m_Manifest);
                        }
                        catch (SimpleJsonException e)
                        {
                            m_ManifestJsonError = e.Message;
                            m_ManifestPermitInvalidJson = new Json(text, "package.json", permitInvalidJson: true);
                            AssetVisibility.TargetUnityImportsPluginDirsLegacy = GetTargetUnityImportsPluginDirs(m_ManifestPermitInvalidJson);
                        }
                    }
                    catch (SimpleJsonException e)
                    {
                        m_ManifestErrorPermitInvalidJson = "package.json manifest is not valid JSON";
                        ManifestContextErrors.Add(("PVP-100-1", m_ManifestErrorPermitInvalidJson));
                        ManifestContextErrors.Add(("PVP-100-2", e.LegacyFullMessage));
                    }
                }

                PathEntries = Files.Select(p => new PathEntry(p, AssetVisibility)).ToList();
            }

            internal static bool HasSingleTopLevelSample(Json manifest)
            {
                try
                {
                    var hasTopLevelSample = false;
                    foreach (var sample in manifest["samples"].Elements)
                    {
                        if (hasTopLevelSample) return false; // multiple samples, can't be single top-level
                        hasTopLevelSample = sample["path"].String.Equals("samples~", StringComparison.OrdinalIgnoreCase);
                        if (!hasTopLevelSample) return false;
                    }
                    return hasTopLevelSample;
                }
                catch (SimpleJsonException)
                {
                    return false;
                }
            }

            static bool GetTargetUnityImportsPluginDirs(Json manifest)
            {
                try
                {
                    var minVersion = manifest["unity"].IfPresent?.String;
                    int i;
                    // The asset pipeline in Unity 2019.*, 2020.1, 2020.2, 2021.1, 2021.2, 2022.1
                    // doesn't ignore Loadable Plugin Directories. Cf. PathEntry.IsHidden.
                    return minVersion == null || (
                        (i = minVersion.IndexOf('.')) != -1 &&
                        XrayUtils.TryParseUint(minVersion.SpanOrSubstring(0, i), out var major) &&
                        XrayUtils.TryParseUint(minVersion.SpanOrSubstring(i + 1), out var minor) &&
                        (major < 2020 || (major <= 2021 && minor < 3) || (major == 2022 && minor < 2))
                    );
                }
                catch (SimpleJsonException)
                {
                    return false;
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

                    var result = m_ResultFile.Results[checkId];
                    if (result.SkipReason == null)
                    {
                        result.Errors.Add(error);
                    }
                }
            }

            public void AddError(string[] checkIds, string error)
            {
                foreach (var checkId in checkIds) AddError(checkId, error);
            }

            static bool CanFetchProductionRegistryVersions(string packageName)
            {
                // Modules are only ever built-in, don't attempt queries for those.
                // Also refuse to look up dependencies with invalid package names, as this could
                // lead to HTTP requests for undesirable URLs.
                return !packageName.StartsWithOrdinal(PackageId.UnityModuleNamePrefix) && PackageId.ValidName.IsMatch(packageName);
            }

            /// Look up NPM metadata for package name, which caller must ensure satisfies CanFetchProductionRegistryVersions.
            /// Returns null on 404.
            Dictionary<string, object> FetchProductionRegistryVersions(string packageName)
            {
                if (m_ProductionRegistryVersions.TryGetValue(packageName, out var versions)) return versions;

                var url = "https://packages.unity.com/" + packageName;
                var resp = HttpClient.GetCheckStatus(url, 200, 404);
                if (resp.Status != 404)
                {
                    try
                    {
                        var json = new Json(resp.GetBaselineString(), null);
                        versions = json["versions"].RawObject;
                    }
                    catch (SimpleJsonException)
                    {
                        throw SkipAllException.InvalidBaseline;
                    }
                }

                m_ProductionRegistryVersions[packageName] = versions;
                return versions;
            }

            public void QuerySemVerProduction(string packageName, ref SemVerQuery query)
            {
                if (!CanFetchProductionRegistryVersions(packageName)) return;

                var sb = new StringBuilder(100);
                sb.Append("semver:production:");
                sb.Append(packageName);
                sb.Append(SemVerQuery.OperatorNames[(int)query.Operator]);
                query.RefVersion.AppendTo(sb);
                var baselineKey = sb.ToString();

                var versions = FetchProductionRegistryVersions(packageName);
                if (versions != null)
                {
                    foreach (var kv in versions) query.Consider(kv.Key);
                }

                foreach (var kv in m_PublishSetBaselines) query.Consider(kv.Key.Version);

                m_ResultFile.Baselines[baselineKey] = query.BestVersion == null ? "null" : $"\"{query.BestVersion}\"";
            }

            public void Skip(string checkId, string reason)
            {
                if (!m_CurrentBatchChecks.Contains(checkId))
                {
                    throw new InvalidOperationException($"batch tried to skip undeclared check ID {checkId}: {reason}");
                }

                var result = m_ResultFile.Results[checkId];
                if (result.SkipReason == null)
                {
                    result.Errors.Clear();
                    result.SkipReason = reason;
                }
            }

            public void Skip(string[] checkIds, string reason)
            {
                foreach (var checkId in checkIds) Skip(checkId, reason);
            }

            void SetBaseline(string id, string json)
            {
                if (m_ResultFile.Baselines.TryGetValue(id, out var existing))
                {
                    if (existing != json)
                        throw new InvalidOperationException($"multiple baseline values with same ID '{id}': {existing}, {json}");
                }
                else
                {
                    m_ResultFile.Baselines[id] = json;
                }
            }

            public void SetBlobBaseline(string id, PvpHttpResponse response)
            {
                var hash = XrayUtils.Sha256(response.Buffer, response.Length);
                SetBaseline($"blob:{id}", $"\"{hash}\"");
            }

            public bool TryFetchPackageBaseline(PackageId package, out PackageBaseline baseline)
            {
                baseline = default;

                // Don't even emit a "null" baseline for modules and other ineligible package names.
                if (!CanFetchProductionRegistryVersions(package.Name)) return false;

                if (m_PublishSetBaselines == null) throw SkipAllException.InvalidBaseline;
                if (m_PublishSetBaselines.TryGetValue(package, out baseline))
                {
                    SetBaseline($"pkg:{package}", $"\"{baseline.Sha1}\"");
                    return true;
                }

                var versions = FetchProductionRegistryVersions(package.Name);
                if (versions != null && versions.TryGetValue(package.Version, out var registryManifest))
                {
                    baseline.Manifest = new Json(registryManifest, null);
                    try
                    {
                        baseline.Sha1 = baseline.Manifest["dist"]["shasum"].String;
                    }
                    catch (SimpleJsonException)
                    {
                        throw SkipAllException.InvalidBaseline;
                    }

                    if (!XrayUtils.Sha1Regex.IsMatch(baseline.Sha1))
                        throw SkipAllException.InvalidBaseline;

                    SetBaseline($"pkg:{package}", $"\"{baseline.Sha1}\"");
                    return true;
                }

                SetBaseline($"pkg:{package}", "null");
                return false;
            }

            public bool TryFetchEditorManifestBaseline(string editorVersion, out IReadOnlyCollection<PackageId> editorManifestPackages)
            {
                if (!m_EditorManifestPackages.TryGetValue(editorVersion, out var packages))
                {
                    var url = "https://pkgprom-api.ds.unity3d.com/internal-api/editor-manifest/"
                        + WebUtility.UrlEncode(editorVersion);
                    var resp = HttpClient.GetCheckStatus(url, 200, 404);
                    if (resp.Status == 404)
                    {
                        SetBaseline($"blob:editor_manifest:{editorVersion}", "null");
                    }
                    else
                    {
                        SetBlobBaseline($"editor_manifest:{editorVersion}", resp);

                        var editorManifestJson = resp.GetBaselineString();
                        try
                        {
                            var json = new Json(editorManifestJson, null);
                            packages = new HashSet<PackageId>();
                            foreach (var pkg in json["packages"].Members)
                            {
                                var version = pkg["version"];
                                if (version.IsPresent)
                                {
                                    packages.Add(new PackageId(pkg.Key, version.String));
                                }
                            }
                        }
                        catch (SimpleJsonException)
                        {
                            throw SkipAllException.InvalidBaseline;
                        }
                    }

                    m_EditorManifestPackages[editorVersion] = packages;
                }

                editorManifestPackages = packages;
                return packages != null;
            }

            public bool HasSupportedEditorsInRange(UnityMajor startInclusive, UnityMajor endExclusive)
            {
                if (m_SupportedEditorsBaselineSkipReason != null)
                    throw new SkipAllException(m_SupportedEditorsBaselineSkipReason);

                if (m_SupportedEditorsBaseline == null)
                {
                    try
                    {
                        const string url = "https://pkgprom-api.ds.unity3d.com/internal-api/supported-editors";
                        var response = HttpClient.GetCheckStatus(url).GetBaselineString();
                        try
                        {
                            var json = new Json(response, null);
                            m_SupportedEditorsBaseline = new List<UnityMajor>(8);
                            foreach (var e in json.Elements)
                            {
                                m_SupportedEditorsBaseline.Add(UnityMajor.FromFull(e["latestUnityRelease"]["version"].String));
                            }

                            m_SupportedEditorsBaseline.Sort();
                            for (var i = 1; i < m_SupportedEditorsBaseline.Count; i++)
                            {
                                if (m_SupportedEditorsBaseline[i].Equals(m_SupportedEditorsBaseline[i - 1]))
                                    m_SupportedEditorsBaseline.RemoveAt(i--);
                            }
                        }
                        catch (ArgumentException) // invalid UnityMajor string
                        {
                            throw SkipAllException.InvalidBaseline;
                        }
                        catch (SimpleJsonException)
                        {
                            throw SkipAllException.InvalidBaseline;
                        }
                    }
                    catch (SkipAllException e)
                    {
                        m_SupportedEditorsBaselineSkipReason = e.Message;
                        throw;
                    }
                }

                // Now, determine if any supported majors are in range, and emit baseline.
                StringBuilder inRange = null;
                foreach (var major in m_SupportedEditorsBaseline)
                {
                    if (startInclusive.CompareTo(major) <= 0 && major.CompareTo(endExclusive) < 0)
                    {
                        if (inRange == null) inRange = new StringBuilder(m_SupportedEditorsBaseline.Count * 10).Append('[');
                        else inRange.Append(',');
                        inRange.AppendAsJson(major.Version);
                    }
                }

                SetBaseline($"supported_editors:{startInclusive}:{endExclusive}", inRange?.Append(']').ToString() ?? "[]");
                return inRange != null;
            }

            /// Determine if the specified path exists in package and is a directory. Note that
            /// IPackage never tracks empty directories, so this will return false in that case.
            public bool DirectoryExists(string path)
            {
                path += "/";
                return Files.Any(p => p.StartsWithOrdinal(path));
            }

            public void RunBatch(string[] checks, Action action, bool isLegacyCheckerEmittingLegacyJsonErrors)
            {
                IsLegacyCheckerEmittingLegacyJsonErrors = isLegacyCheckerEmittingLegacyJsonErrors;
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
                    AddError(checks, e.Message);
                }
                catch (PvpHttpException)
                {
                    Skip(checks, "network_error");
                }
                catch (SimpleJsonException e) when (e.PackageFilePath != null)
                {
                    AddError(checks, IsLegacyCheckerEmittingLegacyJsonErrors ? e.LegacyFullMessage : e.FullMessage);
                }
                catch (YamlParseException e) when (e.PackageFilePath != null)
                {
                    AddError(checks, e.FullMessage);
                }
                catch (YamlAccessException e) when (e.PackageFilePath != null)
                {
                    AddError(checks, e.FullMessage);
                }
                catch (SkipAllException e)
                {
                    Skip(checks, e.Message);
                }
            }
        }

        struct CheckerEntry
        {
            public IChecker Instance;
            public CheckerMeta Meta;
            public bool IsLegacyCheckerEmittingLegacyJsonErrors;
        }

        readonly Context m_Context;
        readonly List<CheckerEntry> m_Checkers;
        readonly int m_PassCount;
        int m_PassIndex;
        readonly ResultFileStub m_ResultFile;

        internal Verifier(VerifierContext verifierContext, CheckerSet checkerSet)
        {
            m_ResultFile = new ResultFileStub();
            m_Context = new Context(verifierContext, checkerSet, m_ResultFile);
            m_Checkers = new List<CheckerEntry>();
            m_PassCount = checkerSet.PassCount;

            var parameterTypes = new[] { typeof(Context) };
            var parameterValues = new object[] { m_Context };
            foreach (var meta in checkerSet.Checkers)
            {
                var constructor = meta.Type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null)
                    ?? throw new NotImplementedException($"{meta.Type} is missing required public constructor with Verifier.Context parameter");
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

                m_Context.RunBatch(meta.Checks, CreateChecker, false);
                // If constructor threw PvpHttpException, SkipAllException or
                // FailAllException, checker will be null here, and we won't be
                // calling CheckItem or Finish on the checker.
                if (checker != null)
                {
                    // Finish checker immediately if it requires no passes so
                    // it can be garbage collected.
                    if (meta.PassCount == 0)
                    {
                        m_Context.RunBatch(meta.Checks, checker.Finish, m_Context.IsLegacyCheckerEmittingLegacyJsonErrors);
                    }
                    else
                    {
                        m_Checkers.Add(new CheckerEntry
                        {
                            Instance = checker,
                            Meta = meta,
                            IsLegacyCheckerEmittingLegacyJsonErrors = m_Context.IsLegacyCheckerEmittingLegacyJsonErrors,
                        });
                    }
                }
            }
        }

        /// file.Content must be disposed by the caller and is assumed to be a read-only
        /// non-seekable stream that is only valid for the duration of this method call.
        internal void CheckItem(IPackageFile file)
        {
            if (m_PassIndex == m_PassCount)
            {
                throw new InvalidOperationException("All passes already finished");
            }

            var checkerFile = new PackageFile(file, m_Context.AssetVisibility);

            foreach (var entry in m_Checkers)
            {
                m_Context.RunBatch(entry.Meta.Checks, () => entry.Instance.CheckItem(checkerFile, m_PassIndex), entry.IsLegacyCheckerEmittingLegacyJsonErrors);
            }
        }

        internal void FinishPass()
        {
            m_PassIndex++;

            // Finish checkers that have completed all their passes, and clear
            // their references so they can be garbage collected.
            for (var i = m_Checkers.Count - 1; i >= 0; i--)
            {
                var entry = m_Checkers[i];
                if (entry.Meta.PassCount <= m_PassIndex)
                {
                    m_Context.RunBatch(entry.Meta.Checks, entry.Instance.Finish, entry.IsLegacyCheckerEmittingLegacyJsonErrors);
                    m_Checkers.RemoveAt(i);
                }
            }
        }

        internal ResultFileStub Result
        {
            get
            {
                if (m_Checkers.Count != 0)
                {
                    throw new InvalidOperationException("Unfinished checkers remain");
                }

                return m_ResultFile;
            }
        }

        internal static ResultFileStub OneShot(VerifierContext context, CheckerSet checkerSet, IEnumerable<IPackageFile> files)
        {
            var verifier = new Verifier(context, checkerSet);

            for (var passIndex = 0; passIndex < checkerSet.PassCount; passIndex++)
            {
                foreach (var file in files)
                {
                    verifier.CheckItem(file);
                }

                verifier.FinishPass();
            }

            return verifier.Result;
        }
    }
}
