using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PvpXray
{
    class MetaFileVerifierV4 : Verifier.IChecker
    {
        const string k_Check = "PVP-26-4";
        const string k_MetaExtension = ".meta";

        public static string[] Checks => new[] { k_Check };
        public static int PassCount => 0;

        // Derive directories from file paths. This assumes that there are no empty directories.
        internal static List<PathEntry> GetFileAndDirectoryEntries(Verifier.Context context)
        {
            var entries = context.PathEntries.ToList();
            var seenDirectories = new HashSet<string>();
            var pathBuilder = new StringBuilder();
            foreach (var fileEntry in context.PathEntries)
            {
                if (fileEntry.Components.Length > 1)
                {
                    var componentsWithCase = fileEntry.PathWithCase.Split('/');
                    for (var length = 1; length < componentsWithCase.Length; length++)
                    {
                        pathBuilder.Clear();
                        pathBuilder.Append(componentsWithCase[0]);
                        for (var index = 1; index < length; index++)
                        {
                            pathBuilder.Append("/");
                            pathBuilder.Append(componentsWithCase[index]);
                        }
                        var directoryPath = pathBuilder.ToString();
                        if (seenDirectories.Add(directoryPath))
                        {
                            entries.Add(new PathEntry(directoryPath, context.TargetUnityImportsPluginDirs, isDirectory: true));
                        }
                    }
                }
            }
            return entries;
        }

        public MetaFileVerifierV4(Verifier.Context context)
        {
            // We need to know about directories since folder assets also have corresponding meta files.
            var entries = GetFileAndDirectoryEntries(context);

            var entriesByPath = entries.ToDictionary(e => e.PathWithCase);
            foreach (var entry in entries)
            {
                // ignore all files inside plugin directories, unless targeting old Unity version
                // (This enables packages to bridge the gap between Unity versions requiring
                // .meta files to be present inside plugin directories, and versions that don't.)
                if (!context.TargetUnityImportsPluginDirs && entry.IsInsidePluginDirectory) continue;

                if (entry.HasExtension(k_MetaExtension))
                {
                    // ignore .meta files inside hidden directories (like Samples~)
                    if (entry.Components.Length > 1 && new PathEntry(entry.DirectoryWithCase, context.TargetUnityImportsPluginDirs, isDirectory: true).IsHidden) continue;

                    if (entry.IsDirectory)
                    {
                        context.AddError(k_Check, $"{entry.PathWithCase}: Directory with meta file extension");
                    }
                    else
                    {
                        var assetPath = entry.PathWithCase.Substring(0, entry.PathWithCase.Length - k_MetaExtension.Length);
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
                    var metaPath = entry.PathWithCase + k_MetaExtension;
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
