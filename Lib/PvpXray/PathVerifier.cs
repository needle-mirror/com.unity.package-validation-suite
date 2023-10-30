using System;
using System.Collections.Generic;
using System.Linq;

namespace PvpXray
{
    class PathVerifier : Verifier.IChecker
    {
        // REMEMBER: Checks must not be changed once added. Any modifications must be implemented as a NEW check.
        // Note: These path validations are all run on LOWERCASE paths (unless explicitly using "XxxWithCase").
        // Use only Ordinal string comparisons (and Invariant transforms); see StringExtensions.cs.
        static readonly (string, Func<PathEntry, bool>)[] k_SinglePathValidations = {
            // PVP-21-1: No JPEG image assets (US-0110)
            ("PVP-21-1", e => e.HasComponent("documentation~", "tests") || !e.HasExtension(".jpg", ".jpeg")),

            // PVP-22-1: File paths may not exceed 140 characters (US-0113)
            ("PVP-22-1", e => e.Path.Length <= 140),

            // PVP-23-1: Restricted filename extensions (US-0115)
            ("PVP-23-1", e => !e.HasExtension(".bat", ".bin", ".com", ".csh", ".dom", ".exe", ".jse", ".msi", ".msp", ".mst", ".ps1", ".vb", ".vbe", ".vbs", ".vbscript", ".vs", ".vsd", ".vsh")),

            // PVP-24-1: Restricted filenames (US-0115)
            ("PVP-24-1", e => !e.HasFilename("assetstoretools.dll", "assetstoretoolsextra.dll", "droidsansmono.ttf", "csc.rsp")),

            // PVP-25-1: Unapproved filenames (US-0115)
            ("PVP-25-1", e => e.Filename != "standard assets" && !e.Filename.StartsWithOrdinal("standard assets.") && !e.HasExtension(".unitypackage", ".zip", ".rar", ".lib", ".dll", ".js")),

            // PVP-33-1: No filenames that ought to never appear in a package
            ("PVP-33-1", e =>
                !e.HasFilename(
                    ".ds_store", ".volumeicon.icns", ".apdisk", ".localized", // macOS
                    ".appcollector.yaml", ".appcollector.yml", "appcollector.yaml", "appcollector.yml", // AppCollector
                    ".buginfo", // Buginfo
                    ".editorconfig", // EditorConfig
                    ".eslintrc.js", ".eslintrc.cjs", ".eslintrc.yaml", ".eslintrc.yml", ".eslintrc.json", ".eslintignore", "eslint.config.js", // ESLint
                    ".gitattributes", ".gitignore", ".gitmodules", // Git
                    ".gitlab-ci.yml", // GitLab
                    ".hgeol", ".hgignore", ".hgsub", ".hgsubstate", ".hgtags", // Mercurial
                    ".npmignore", ".npmrc", "npm-debug.log", // npm
                    ".readme - external.md", "readme - external.md", // Package Starter Kit meta docs ("external instructions for partners")
                    ".repoconfig", // unity-meta formatting tools
                    ".sample.json", ".tests.json", "build.bat", "build.sh", // UPM-CI
                    "catalog-info.yaml", "catalog-info.yml", // Backstage
                    "codecov.yml", // Codecov
                    "codeowners", "issue_template.md", "pull_request_template.md", // GitHub
                    ".lock-wscript", "config.gypi", // Node.js
                    "qareport.md", // Internal quality report
                    "renovate.json", "renovate.json5", ".renovaterc", ".renovaterc.json", ".renovaterc.json5", // Renovate
                    "sonarqube.analysis.xml", // SonarQube
                    "testrunneroptions.json") // UTR
                && e.Path != "contributing.md" // Contribution document rarely makes sense outside repository context.
                && !e.Filename.StartsWithOrdinal("._") // macOS
                && !e.Filename.StartsWithOrdinal(".wafpickle-") // Node.js
                && !e.HasExtension(
                        ".api", // API file
                        ".orig", // Merge backup file
                        ".swp") // Vim swap file
                && !e.HasDirectoryComponent(
                    ".build_script", "upm-ci~", // UPM-CI
                    ".documentrevisions-v100", ".spotlight-v100", ".temporaryitems", ".trash", ".trashes", ".fseventsd", "__macosx", // macOS
                    ".editor", // unity-downloader-cli
                    ".git", // Git
                    ".github", // GitHub
                    ".gitlab", // GitLab
                    ".hg", ".hglf", // Mercurial
                    ".idea", ".vs", ".vscode", // IDEs
                    ".svn", // Subversion
                    ".yamato", // Yamato
                    "cvs") // CVS
                && !e.Path.StartsWithOrdinal("node_modules/") // Node.js
            ),

            // PVP-34-1: No file paths matching `*.zip*` glob
            ("PVP-34-1", e => !e.Path.Contains(".zip")),

            // PVP-62-1: index.md filename must be spelled in lowercase
            ("PVP-62-1", e => e.Filename != "index.md" || e.Components[0] != "documentation~" || e.PathWithCase.EndsWithOrdinal("index.md")),
        };

        // REMEMBER: Checks must not be changed once added. Any modifications must be implemented as a NEW check.
        // Note: These path validations are run against all file paths in the package at once,
        // e.g. to check for the existence of a certain file.
        static readonly (string, Predicate<IEnumerable<string>>, string)[] k_AllPathsValidations =
        {
            // PVP-28-1: Must have .signature file (US-0134)
            ("PVP-28-1", paths => paths.Contains(".signature"), "Missing .signature file"),
            // PVP-50-1: Must have README.md file
            ("PVP-50-1", paths => paths.Contains("README.md"), "Missing README.md file"),
        };

        public static string[] Checks =>
            k_SinglePathValidations.Select(v => v.Item1)
            .Concat(k_AllPathsValidations.Select(v => v.Item1))
            .ToArray();

        public static int PassCount => 0;

        public PathVerifier(Verifier.Context context)
        {
            foreach (var path in context.Files)
            {
                var entry = new PathEntry(path);
                foreach (var (check, isValid) in k_SinglePathValidations)
                {
                    if (!isValid(entry))
                    {
                        context.AddError(check, path);
                    }
                }
            }

            foreach (var (check, isValid, error) in k_AllPathsValidations)
            {
                if (!isValid(context.Files))
                {
                    context.AddError(check, error);
                }
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            throw new InvalidOperationException();
        }

        public void Finish()
        {
        }
    }
}
