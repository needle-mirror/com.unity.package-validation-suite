using System;
using System.Collections.Generic;
using System.Linq;

namespace PureFileValidationPvp
{
    static class PathValidations
    {
        public class Entry
        {
            readonly string m_Extension;

            public string[] Components { get; }
            public string Filename { get; }
            public bool IsHidden { get; }
            public string Path { get; }
            public string PathWithCase { get; }

            public string DirectoryWithCase => Components.Length == 1 ? "" : PathWithCase.Substring(0, PathWithCase.Length - Filename.Length - 1);
            public string FilenameWithCase => PathWithCase.Substring(PathWithCase.Length - Filename.Length);

            public Entry(string path)
            {
                // Note: avoid .NET Path APIs here; they are poorly documented and may have platform-specific quirks.

                Path = path.ToLowerInvariant();
                PathWithCase = path;
                Components = Path.Split('/');
                Filename = Components[Components.Length - 1];

                var i = Filename.LastIndexOf('.');
                // 'i > 0' because the extension of ".gitignore" is not ".gitignore".
                m_Extension = i > 0 ? Filename.Substring(i) : "";

                // Files are considered "hidden" (and not imported by the asset pipeline) subject
                // to the patterns given here: https://docs.unity3d.com/Manual/SpecialFolders.html
                // (Implementation appears to be in Runtime/VirtualFileSystem/LocalFileSystem.h)
                IsHidden = Components.Any(name => name[0] == '.' || name[name.Length - 1] == '~' || name == "cvs")
                    || m_Extension == ".tmp";
            }

            public bool HasComponent(params string[] components) => Components.Any(components.Contains);
            public bool HasExtension(params string[] extensions) => extensions.Contains(m_Extension);
            public bool HasFilename(params string[] filenames) => filenames.Contains(Filename);
        }

        // REMEMBER: Checks must not be changed once added. Any modifications must be implemented as a NEW check.
        // Note: These path validations are all run on LOWERCASE paths (unless explicitly using "XxxWithCase").
        // Use only Ordinal string comparisons (and Invariant transforms); see StringExtensions.cs.
        static readonly (string, Func<Entry, bool>)[] k_SinglePathValidations = {
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
        };

        public static readonly string[] Checks =
            k_SinglePathValidations.Select(v => v.Item1)
            .Concat(k_AllPathsValidations.Select(v => v.Item1))
            .ToArray();

        public static void Run(Validator.Context context)
        {
            foreach (var path in context.Files)
            {
                var entry = new Entry(path);
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
    }
}
