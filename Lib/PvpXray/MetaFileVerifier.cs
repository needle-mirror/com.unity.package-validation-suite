using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PvpXray
{
    class MetaFileVerifier : Verifier.IChecker
    {
        const string k_MetaExtension = ".meta";

        public static string[] Checks => new[] { "PVP-26-1", "PVP-26-2" };
        public static int PassCount => 0;

        public MetaFileVerifier(Verifier.IContext context)
        {
            // We need to know about directories since folder assets also have corresponding meta files.
            var entries = GetFileAndDirectoryEntries(context.Files);

            // PVP-26-1 reports issues with meta files inside hidden directories whereas PVP-26-2 does not.
            void AddError(string error, bool insideHiddenDirectory)
            {
                context.AddError("PVP-26-1", error);
                if (!insideHiddenDirectory)
                {
                    context.AddError("PVP-26-2", error);
                }
            }

            ValidateMetaFiles(entries, AddError);
        }

        // Derive directories from file paths. This assumes that there are no empty directories.
        internal static List<PathEntry> GetFileAndDirectoryEntries(IEnumerable<string> files)
        {
            var fileEntries = files.Select(path => new PathEntry(path)).ToList();
            var entries = fileEntries.ToList();

            var seenDirectories = new HashSet<string>();
            var pathBuilder = new StringBuilder();
            foreach (var fileEntry in fileEntries)
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
                            entries.Add(new PathEntry(directoryPath, isDirectory: true));
                        }
                    }
                }
            }

            return entries;
        }

        static void ValidateMetaFiles(List<PathEntry> entries, Action<string, bool> addError)
        {
            foreach (var entry in entries)
            {
                var directoryWithCase = entry.DirectoryWithCase;
                var insideHiddenDirectory = directoryWithCase != "" && new PathEntry(directoryWithCase).IsHiddenLegacy;

                if (entry.HasExtension(k_MetaExtension))
                {
                    if (entry.IsDirectory)
                    {
                        addError($"{entry.PathWithCase}: Directory with meta file extension", insideHiddenDirectory);
                    }
                    else
                    {
                        var assetPath = entry.Path.Substring(0, entry.Path.Length - k_MetaExtension.Length);
                        var assetEntry = entries.FirstOrDefault(e => e.Path == assetPath);
                        if (assetEntry == null)
                        {
                            addError($"{entry.PathWithCase}: Meta file without corresponding asset", insideHiddenDirectory);
                        }
                        else if (assetEntry.IsHiddenLegacy)
                        {
                            addError($"{entry.PathWithCase}: Meta file for hidden asset", insideHiddenDirectory);
                        }
                        else if (assetEntry.HasExtension(k_MetaExtension))
                        {
                            addError($"{entry.PathWithCase}: Meta file for asset with meta file extension", insideHiddenDirectory);
                        }
                    }
                }
                else if (!entry.IsHiddenLegacy)
                {
                    var metaPath = entry.Path + k_MetaExtension;
                    var metaEntry = entries.FirstOrDefault(e => e.Path == metaPath);
                    if (metaEntry == null)
                    {
                        addError($"{entry.PathWithCase}: Asset without corresponding meta file", insideHiddenDirectory);
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
