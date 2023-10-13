using System;
using System.Globalization;
using System.Linq;

namespace PvpXray
{
    class MetaFileVerifierV3 : Verifier.IChecker
    {
        const string k_Check = "PVP-26-3";
        const string k_MetaExtension = ".meta";

        public static string[] Checks => new[] { k_Check };
        public static int PassCount => 0;

        public MetaFileVerifierV3(Verifier.IContext context)
        {
            // We need to know about directories since folder assets also have corresponding meta files.
            var entries = MetaFileVerifier.GetFileAndDirectoryEntries(context.Files);

            var minVersion = context.Manifest["unity"].IfPresent?.String;
            int i;
            var targetUnityRequiresMetaFilesInPluginDirs = minVersion == null || (
                (i = minVersion.IndexOf('.')) != -1 &&
                int.TryParse(minVersion.Substring(0, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) &&
                int.TryParse(minVersion.Substring(i + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor) &&
                (major < 2020 || (major <= 2021 && minor < 3) || (major == 2022 && minor < 2))
            );

            var entriesByPath = entries.ToDictionary(e => e.Path);
            foreach (var entry in entries)
            {
                // ignore all files inside plugin directories, unless targeting old Unity version
                if (!targetUnityRequiresMetaFilesInPluginDirs && entry.IsInsidePluginDirectory) continue;

                if (entry.HasExtension(k_MetaExtension))
                {
                    // ignore .meta files inside hidden directories (like Samples~)
                    if (entry.Components.Length > 1 && new PathEntry(entry.DirectoryWithCase, isDirectory: true).IsHidden) continue;

                    if (entry.IsDirectory)
                    {
                        context.AddError(k_Check, $"{entry.PathWithCase}: Directory with meta file extension");
                    }
                    else
                    {
                        var assetPath = entry.Path.Substring(0, entry.Path.Length - k_MetaExtension.Length);
                        if (!entriesByPath.TryGetValue(assetPath, out var assetEntry))
                        {
                            context.AddError(k_Check, $"{entry.PathWithCase}: Meta file without corresponding asset");
                        }
                        else if (assetEntry.IsHidden)
                        {
                            context.AddError(k_Check, $"{entry.PathWithCase}: Meta file for hidden asset");
                        }
                        else if (assetEntry.HasExtension(k_MetaExtension))
                        {
                            context.AddError(k_Check, $"{entry.PathWithCase}: Meta file for asset with meta file extension");
                        }
                    }
                }
                else if (!entry.IsHidden)
                {
                    var metaPath = entry.Path + k_MetaExtension;
                    if (!entriesByPath.ContainsKey(metaPath))
                    {
                        context.AddError(k_Check, $"{entry.PathWithCase}: Asset without corresponding meta file");
                    }
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
