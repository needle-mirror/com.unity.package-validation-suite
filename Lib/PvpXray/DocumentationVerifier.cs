using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PvpXray
{
    static class DocumentationVerifierExtensions
    {
        public static bool HasMultipleDocumentationDirs(this List<string> paths) =>
            paths.Count(p => p.ToLowerInvariant().Contains("documentation")) > 1;

        public static bool MissingRootUnityDocumentationDir(this List<string> paths) =>
            !(paths.Contains("Documentation~") || paths.Contains(".Documentation~"));

        public static bool HasTooLittleContent(this string contents) => contents.Length < 10;
    }

    class DocumentationVerifier : Verifier.IChecker
    {
        static readonly string[] k_Pvp60_1 = { "PVP-60-1" };
        static readonly string[] k_Pvp60 = { "PVP-60-1", "PVP-60-2" }; // Compliant Documentation~ folder paths (US-0040)
        public static string[] Checks { get; } = k_Pvp60
            .Append("PVP-61-1") // Compliant Documentation~ file contents (US-0040)
            .ToArray();


        public static int PassCount => 1;

        static List<string> GetTopLevelDirectories(IReadOnlyList<PathEntry> paths) =>
            paths.Select(e => e.DirectoryWithCase.SplitLeft('/').ToString())
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct().ToList();

        static List<string> GetDocumentationMarkdownFiles(IReadOnlyList<string> paths) =>
            paths.Where(path => Regex.IsMatch(path, @"^\.?Documentation~/[^/]*\.md$")).ToList();

        readonly Verifier.Context m_Context;
        readonly List<string> m_MarkdownFiles;

        public DocumentationVerifier(Verifier.Context context)
        {
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            m_Context = context;

            var topLevelDirectories = GetTopLevelDirectories(context.PathEntries);

            if (topLevelDirectories.HasMultipleDocumentationDirs()) context.AddError(k_Pvp60, "Only one documentation directory is permitted per package");
            var missingRootUnityDocumentationDir = topLevelDirectories.MissingRootUnityDocumentationDir();
            if (missingRootUnityDocumentationDir) context.AddError(context.IsFeatureSetPackage ? k_Pvp60_1 : k_Pvp60, "A folder named \"Documentation~\" is required to be present at the root of the package");

            m_MarkdownFiles = GetDocumentationMarkdownFiles(context.Files);

            if (m_MarkdownFiles.Count == 0) context.AddError(context.IsFeatureSetPackage && missingRootUnityDocumentationDir ? k_Pvp60_1 : k_Pvp60, "Documentation~ folder must contain markdown documentation files");
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (!m_MarkdownFiles.Contains(file.Path)) return;

            if (file.ReadToStringLegacy().HasTooLittleContent())
            {
                m_Context.AddError("PVP-61-1", $"{file.Path}: Documentation Markdown files must have at least 10 characters of content");
            }
        }

        public void Finish()
        {
        }
    }
}
