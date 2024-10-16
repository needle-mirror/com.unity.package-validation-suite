using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class AssemblyDefinitionVerifierV2 : Verifier.IChecker
    {
        const string k_Extension = ".asmdef";
        static readonly Regex k_AssemblyNamePattern = new Regex(@"^[0-9A-Z][0-9A-Za-z]*(\.[0-9A-Z][0-9A-Za-z]*)*$");

        public static string[] Checks { get; } = {
            "PVP-130-2", // .asmdef file name should match the assembly name (US-0038)
            "PVP-131-2", // Assembly names must start with 'Unity' for ApiUpdater coverage
            "PVP-132-2", // Assembly names must follow naming convention (US-0038)
            "PVP-133-2", // .asmdef files must be located in appropriate folder
        };

        public static int PassCount => 1;

        readonly Verifier.Context m_Context;
        readonly bool m_IsUnityPackage;

        public AssemblyDefinitionVerifierV2(Verifier.Context context)
        {
            m_Context = context;

            try
            {
                m_IsUnityPackage = m_Context.Manifest["name"].String.StartsWithOrdinal("com.unity.");
            }
            catch (SimpleJsonException e)
            {
                m_Context.AddError("PVP-131-2", e.FullMessage);
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            var entry = file.Entry;
            if (!entry.HasExtension(k_Extension) || !entry.IsAsset(includeSamples: true)) return;

            var json = file.ReadAsJsonLax();
            var assemblyName = json["name"].String;

            if (!entry.FilenameWithCaseNoExtension.Equals(assemblyName))
            {
                m_Context.AddError("PVP-130-2", $"{file.Path}: assembly name does not match filename: {assemblyName}");
            }

            if (!k_AssemblyNamePattern.IsMatch(assemblyName))
            {
                m_Context.AddError("PVP-132-2", $"{file.Path}: assembly name does not follow naming convention: {assemblyName}");
            }

            if (!entry.IsSampleAsset)
            {
                if (m_IsUnityPackage && !assemblyName.StartsWithOrdinal("Unity"))
                {
                    m_Context.AddError("PVP-131-2", $"{file.Path}: name of Unity package assembly does not start with \"Unity\": {assemblyName}");
                }

                var actualPathPrefix = GetPathPrefix(file.Path);
                var actualNameSuffix = GetNameSuffix(assemblyName);
                try
                {
                    var kind = GetAssemblyKind(json);
                    var required = k_AssemblyKindReqs[(int)kind];

                    if (!actualNameSuffix.Equals(required.Suffix) && (required.SuffixAlt == null || !actualNameSuffix.Equals(required.SuffixAlt)))
                    {
                        var permitExceptionally = assemblyName.StartsWithOrdinal("UnityEditor.")
                            && required.Suffix.TryStripPrefix(".Editor", out var remainingSuffix)
                            && actualNameSuffix.Equals(remainingSuffix); // "" or ".Tests"
                        if (!permitExceptionally)
                            m_Context.AddError("PVP-132-2", GetAffixErrorMsg(file.Path, "name of ", required.Adjective, actualNameSuffix, "end with", required.Suffix, required.SuffixAlt, null));
                    }

                    if (!actualPathPrefix.Equals(required.PathPrefix))
                    {
                        m_Context.AddError("PVP-133-2", GetAffixErrorMsg(file.Path, "", required.Adjective, actualPathPrefix, "be located in", required.PathPrefix, null, " (in the package root)"));
                    }
                }
                catch (SimpleJsonException e)
                {
                    m_Context.AddError("PVP-132-2", e.FullMessage);
                    m_Context.AddError("PVP-133-2", e.FullMessage);
                }
            }
        }

        public void Finish() { }

        static string GetAffixErrorMsg(string filePath, string preAdjective, string adjective, StringSlice actual, string requirementKind, string expected, string expectedAlt, string emptyActualAppendix)
        {
            var sb = new StringBuilder(256);
            sb.Append($"{filePath}: {preAdjective}{adjective} assembly should {requirementKind} '{expected}'");
            if (expectedAlt == "") sb.Append(" or no suffix"); // 'expectedAlt' is only used for suffixes
            else if (expectedAlt != null) sb.Append($" or '{expectedAlt}'");
            if (actual.Length != 0) sb.Append($", not '{actual}'");
            else sb.Append(emptyActualAppendix);
            return sb.ToString();
        }

        internal enum AssemblyKind
        {
            EditorAssembly = 0x00,
            EditorTestAssembly = 0x01,
            RuntimeAssembly = 0x02,
            RuntimeTestAssembly = 0x03,
        }

        struct Req
        {
            public string Adjective;
            public string PathPrefix;
            public string Suffix;
            public string SuffixAlt;
        }

        static readonly Req[] k_AssemblyKindReqs = {
            new Req { Adjective = "editor",       PathPrefix = "Editor/",        Suffix = ".Editor", SuffixAlt = null },
            new Req { Adjective = "editor test",  PathPrefix = "Tests/Editor/",  Suffix = ".Editor.Tests", SuffixAlt = null },
            new Req { Adjective = "runtime",      PathPrefix = "Runtime/",       Suffix = ".Runtime", SuffixAlt = "" },
            new Req { Adjective = "runtime test", PathPrefix = "Tests/Runtime/", Suffix = ".Runtime.Tests", SuffixAlt = ".Tests" },
        };

        // The concept of a "test assembly" has, historically, been a nebulous concept.
        // Here, we operate with four main criteria, any one of which is sufficient:
        //     P1: "TestAssemblies" (case sensitive) in "optionalUnityReferences"
        //     P2: "UnityEngine.TestRunner" (case sensitive) in "references"
        //      or "GUID:27619889b8ba8c24980f49ee34dbb44a" (insensitive) in "references"
        //     P3: "UnityEditor.TestRunner" (case sensitive) in "references"
        //      or "GUID:0acc523941302664db1f4e527237feb3" (insensitive) in "references"
        //     P4: "nunit.framework.dll" (case sensitive) in "precompiledReferences"
        //
        // Use in shadow packages (per 2024-10-12), ignoring asmdef paths containing "~/":
        //     1731 -- -- -- --
        //      423 -- P2 P3 P4
        //      394 P1 -- -- --
        //       65 -- -- -- P4
        //       62 -- P2 -- P4
        //       18 P1 -- -- P4
        //       10 -- P2 P3 --
        //        8 -- -- P3 P4
        //        1 -- P2 -- --
        //        1 -- -- P3 --
        //
        // P1 is an older style since superseded by P2/P3, as evidenced by P2/P3 not
        // working in 2018.4, and the lack of overlap in usage of the two styles.
        //
        // The legacy PVS API docs validation relies on Utilities.IsTestAssembly,
        // which does use another test assembly criteria:
        //     P5: "UNITY_INCLUDE_TESTS" (case sensitive) in "defineConstraints"
        // but on its own that only adds 8 more asmdef files, with all but one such
        // assembly containing test utilities or example code snippets (and no actual
        // tests). While skipping API docs validation for such assemblies makes sense,
        // they should not have a ".Tests" suffix or "Tests/" prefix.
        static bool IsTestReference(object reference)
        {
            var r = (string)reference;
            return r == "UnityEngine.TestRunner"
                || r == "UnityEditor.TestRunner"
                || r.EqualsIgnoreCase("GUID:27619889b8ba8c24980f49ee34dbb44a")
                || r.EqualsIgnoreCase("GUID:0acc523941302664db1f4e527237feb3");
        }

        internal static AssemblyKind GetAssemblyKind(Json asmdef)
        {
            // Always validate all three JSON properties, even if we find a target string early.
            var optionalUnityReferences = asmdef["optionalUnityReferences"].ArrayOfStringIfPresent;
            var precompiledReferences = asmdef["precompiledReferences"].ArrayOfStringIfPresent;
            var references = asmdef["references"].ArrayOfStringIfPresent;

            var isTestAssembly = optionalUnityReferences.Contains("TestAssemblies") ||
                precompiledReferences.Contains("nunit.framework.dll") ||
                references.Any(IsTestReference);

            // An "editor" assembly is one that references no runtime platforms (only Editor).
            // (If both includePlatforms and excludePlatforms are empty, that means "all platforms".)
            // Platform names are not case sensitive (tested in 2018.4.36f1).
            var includePlatforms = asmdef["includePlatforms"].ArrayOfStringIfPresent;
            var excludePlatforms = asmdef["excludePlatforms"].ArrayOfStringIfPresent;
            if (includePlatforms.Count != 0 && excludePlatforms.Count != 0)
                throw new SimpleJsonException(".includePlatforms, .excludePlatforms: both cannot be non-empty", null) { PackageFilePath = asmdef.PackageFilePath };
            var isEditorAssembly = includePlatforms.Count == 1 && "Editor".EqualsIgnoreCase((string)includePlatforms[0]);

            var kind = isEditorAssembly ? AssemblyKind.EditorAssembly : AssemblyKind.RuntimeAssembly;
            if (isTestAssembly) kind += 1;
            return kind;
        }

        /// GetPathPrefix/GetNameSuffix looks for these affixes, continuing until
        /// they reach a component not listed here. This allows us to catch things
        /// like invalid affix use even when no suffix is permitted, e.g.
        /// "name of runtime assembly should end with '.Runtime' or no suffix, not '.Editor'"
        ///
        /// Also includes several invalid affixes, to enable errors like
        /// "name of runtime test assembly should end with '.Runtime.Tests' or '.Tests', not '.RuntimeTests'"
        static readonly string[] k_AffixStrings = {
            "editor", "editortest", "editortests",
            "runtime", "runtimetest", "runtimetests",
            "test", "tests",
        };

        internal static StringSlice GetPathPrefix(string path)
        {
            var start = 0;
            int i;
            while ((i = path.IndexOf('/', start)) != -1 && k_AffixStrings.TryIndexOfIgnoreCase(path.Slice(start, i), out _)) start = i + 1;
            return path.Slice(0, start);
        }

        internal static StringSlice GetNameSuffix(string name)
        {
            var end = name.Length;
            int i;
            while ((i = name.LastIndexOf('.', end - 1)) != -1 && k_AffixStrings.TryIndexOfIgnoreCase(name.Slice(i + 1, end), out _)) end = i;
            return name.Slice(end);
        }
    }
}
