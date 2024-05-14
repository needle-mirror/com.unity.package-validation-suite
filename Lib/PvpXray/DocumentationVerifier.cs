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
        public static string[] Checks => new[] { "PVP-60-1", "PVP-61-1" };
        public static int PassCount => 1;

        static List<string> GetTopLevelDirectories(IReadOnlyList<PathEntry> paths) =>
            paths.Select(e => e.DirectoryWithCase.Split('/')[0])
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct().ToList();

        static List<string> GetDocumentationMarkdownFiles(IReadOnlyList<string> paths) =>
            paths.Where(path => Regex.IsMatch(path, @"^\.?Documentation~/[^/]*\.md$")).ToList();

        readonly Verifier.Context m_Context;
        readonly List<string> m_MarkdownFiles;

        public DocumentationVerifier(Verifier.Context context)
        {
            m_Context = context;

            var topLevelDirectories = GetTopLevelDirectories(context.PathEntries);

            if (topLevelDirectories.HasMultipleDocumentationDirs()) context.AddError("PVP-60-1", "Only one documentation directory is permitted per package");
            if (topLevelDirectories.MissingRootUnityDocumentationDir()) context.AddError("PVP-60-1", "A folder named \"Documentation~\" is required to be present at the root of the package");

            m_MarkdownFiles = GetDocumentationMarkdownFiles(context.Files);

            if (m_MarkdownFiles.Count == 0) context.AddError("PVP-60-1", "Documentation~ folder must contain markdown documentation files");
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
