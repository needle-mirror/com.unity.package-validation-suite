using System;
using System.Linq;

namespace PureFileValidationPvp
{
    static class PathValidations
    {
        class Entry
        {
            readonly string m_Extension;

            public string[] Components { get; }
            public string Filename { get; }
            public bool IsHidden { get; }
            public string Path { get; }
            public string PathWithCase { get; }

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
        static readonly (string, Func<Entry, bool>)[] k_PathValidations = {
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

        public static readonly string[] Checks = k_PathValidations.Select(v => v.Item1).ToArray();

        public static void Run(IPackage package, Action<string, string> addError)
        {
            foreach (var path in package.Files)
            {
                var entry = new Entry(path);
                foreach (var (check, isValid) in k_PathValidations)
                {
                    if (!isValid(entry))
                    {
                        addError(check, path);
                    }
                }
            }
        }
    }
}
