using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PvpXray
{
    static class MetaFileValidations
    {
        // Augment Entry type to also represent directories.
        class Entry
        {
            public PathValidations.Entry FileEntry;
            public bool IsDirectory;
        }

        const string k_MetaExtension = ".meta";

        public static readonly string[] Checks = { "PVP-26-1", "PVP-26-2" };

        // Derive directories from file paths. This assumes that there are no empty directories.
        static List<Entry> GetFileAndDirectoryEntries(IEnumerable<string> files)
        {
            var fileEntries = files.Select(path => new PathValidations.Entry(path)).ToList();
            var entries = fileEntries.Select(fileEntry => new Entry
            {
                FileEntry = fileEntry,
                IsDirectory = false,
            }).ToList();

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
                            entries.Add(new Entry
                            {
                                FileEntry = new PathValidations.Entry(directoryPath),
                                IsDirectory = true,
                            });
                        }
                    }
                }
            }

            return entries;
        }

        static void ValidateMetaFiles(List<Entry> entries, Action<string, bool> addError)
        {
            foreach (var entry in entries)
            {
                var directoryWithCase = entry.FileEntry.DirectoryWithCase;
                var insideHiddenDirectory = directoryWithCase != "" && new PathValidations.Entry(directoryWithCase).IsHidden;

                if (entry.FileEntry.HasExtension(k_MetaExtension))
                {
                    if (entry.IsDirectory)
                    {
                        addError($"{entry.FileEntry.PathWithCase}: Directory with meta file extension", insideHiddenDirectory);
                    }
                    else
                    {
                        var assetPath = entry.FileEntry.Path.Substring(0, entry.FileEntry.Path.Length - k_MetaExtension.Length);
                        var assetEntry = entries.FirstOrDefault(e => e.FileEntry.Path == assetPath);
                        if (assetEntry == null)
                        {
                            addError($"{entry.FileEntry.PathWithCase}: Meta file without corresponding asset", insideHiddenDirectory);
                        }
                        else if (assetEntry.FileEntry.IsHidden)
                        {
                            addError($"{entry.FileEntry.PathWithCase}: Meta file for hidden asset", insideHiddenDirectory);
                        }
                        else if (assetEntry.FileEntry.HasExtension(k_MetaExtension))
                        {
                            addError($"{entry.FileEntry.PathWithCase}: Meta file for asset with meta file extension", insideHiddenDirectory);
                        }
                    }
                }
                else if (!entry.FileEntry.IsHidden)
                {
                    var metaPath = entry.FileEntry.Path + k_MetaExtension;
                    var metaEntry = entries.FirstOrDefault(e => e.FileEntry.Path == metaPath);
                    if (metaEntry == null)
                    {
                        addError($"{entry.FileEntry.PathWithCase}: Asset without corresponding meta file", insideHiddenDirectory);
                    }
                }
            }
        }

        public static void Run(Validator.Context context)
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
    }
}
