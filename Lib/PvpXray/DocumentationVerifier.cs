using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PvpXray
{
    static class DocumentationVerifier
    {
        public static readonly string[] Checks = { "PVP-60-1", "PVP-61-1" };
        static List<PathVerifier.Entry> PathEntries(IReadOnlyList<string> paths) =>
            paths.Select(p => new PathVerifier.Entry(p)).ToList();

        static List<string> GetTopLevelDirectories(IReadOnlyList<string> paths) =>
            PathEntries(paths).Select(e => e.DirectoryWithCase.Split('/')[0])
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct().ToList();

        static bool HasMultipleDocumentationDirs(this List<string> paths) =>
            paths.Count(p => p.ToLowerInvariant().Contains("documentation")) > 1;

        static bool MissingRootUnityDocumentationDir(this List<string> paths) =>
            !(paths.Contains("Documentation~") || paths.Contains(".Documentation~"));

        static List<string> GetDocumentationMarkdownFiles(IReadOnlyList<string> paths) =>
            paths.Where(path => Regex.IsMatch(path, @"^\.?Documentation~/[^/]*\.md$")).ToList();

        static bool HasTooLittleContent(this string contents) => contents.Length < 10;

        public static void Run(Verifier.Context context)
        {
            var topLevelDirectories = GetTopLevelDirectories(context.Files);

            if (topLevelDirectories.HasMultipleDocumentationDirs()) context.AddError("PVP-60-1", "Only one documentation directory is permitted per package");
            if (topLevelDirectories.MissingRootUnityDocumentationDir()) context.AddError("PVP-60-1", "A folder named \"Documentation~\" is required to be present at the root of the package");

            var markdownFiles = GetDocumentationMarkdownFiles(context.Files);

            if (markdownFiles.Count == 0) context.AddError("PVP-60-1", "Documentation~ folder must contain markdown documentation files");

            foreach (var path in markdownFiles.Where(p => context.ReadFileToString(p).HasTooLittleContent()))
            {
                context.AddError("PVP-61-1", $"{path}: Documentation Markdown files must have at least 10 characters of content");
            }
        }
    }
}
