using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PvpXray
{
    class MetaFileVerifierV3 : Verifier.IChecker
    {
        const string k_Check = "PVP-26-3";
        const string k_MetaExtension = ".meta";

        public static string[] Checks { get; } = { k_Check };
        public static int PassCount => 0;

        // Derive directories from file paths. This assumes that there are no empty directories.
        internal static List<PathEntry> GetFileAndDirectoryEntries(IReadOnlyList<PathEntry> fileEntries)
        {
            var entries = fileEntries.ToList();
            var seenDirectories = new HashSet<string>();
            var pathBuilder = new StringBuilder(PathVerifier.MaxPathLength);
            foreach (var fileEntry in fileEntries)
            {
                if (fileEntry.Components.Length > 1)
                {
                    var componentsWithCase = fileEntry.PathWithCase.Split('/');
                    for (var length = 1; length < componentsWithCase.Length; length++)
                    {
                        pathBuilder.Append(componentsWithCase[0]);
                        for (var index = 1; index < length; index++)
                        {
                            pathBuilder.Append('/');
                            pathBuilder.Append(componentsWithCase[index]);
                        }
                        var directoryPath = pathBuilder.ToStringAndReset();
                        if (seenDirectories.Add(directoryPath))
                        {
                            entries.Add(new PathEntry(directoryPath, default, isDirectory: true));
                        }
                    }
                }
            }
            return entries;
        }

        public MetaFileVerifierV3(Verifier.Context context)
        {
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            // We need to know about directories since folder assets also have corresponding meta files.
            var entries = GetFileAndDirectoryEntries(context.PathEntries);

            var minVersion = context.ManifestPermitInvalidJson["unity"].IfPresent?.String;
            int i;
            var targetUnityRequiresMetaFilesInPluginDirs = minVersion == null || (
                (i = minVersion.IndexOf('.')) != -1 &&
                int.TryParse(minVersion.SpanOrSubstring(0, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) &&
                int.TryParse(minVersion.SpanOrSubstring(i + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor) &&
                (major < 2020 || (major <= 2021 && minor < 3) || (major == 2022 && minor < 2))
            );

            Dictionary<string, PathEntry> entriesByPath;
            try
            {
                entriesByPath = entries.ToDictionary(e => e.Path);
            }
            catch (ArgumentException)
            {
                throw new Verifier.FailAllException("case collision in package paths");
            }

            foreach (var entry in entries)
            {
                // ignore all files inside plugin directories, unless targeting old Unity version
                if (!targetUnityRequiresMetaFilesInPluginDirs && entry.IsInsidePluginDirectory) continue;

                if (entry.HasExtension(k_MetaExtension))
                {
                    // ignore .meta files inside hidden directories (like Samples~)
                    if (entry.Components.Length > 1 && new PathEntry(entry.DirectoryWithCase, default, isDirectory: true).IsHiddenLegacy) continue;

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
                        else if (assetEntry.IsHiddenLegacy)
                        {
                            context.AddError(k_Check, $"{entry.PathWithCase}: Meta file for hidden asset");
                        }
                        else if (assetEntry.HasExtension(k_MetaExtension))
                        {
                            context.AddError(k_Check, $"{entry.PathWithCase}: Meta file for asset with meta file extension");
                        }
                    }
                }
                else if (!entry.IsHiddenLegacy)
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
