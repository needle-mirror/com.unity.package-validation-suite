using System;
using System.Collections.Generic;
using System.IO;

namespace PvpXray
{
    public interface IPackage
    {
        IReadOnlyList<string> Files { get; }
        Stream Open(string filename);
    }

    public class FileSystemPackage : IPackage
    {
        readonly string m_RootPrefix;
        public IReadOnlyList<string> Files { get; }

        public FileSystemPackage(string rootPath)
        {
            var isWindows = Path.DirectorySeparatorChar != Path.AltDirectorySeparatorChar;
            var files = new List<string>();
            rootPath = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);
            m_RootPrefix = rootPath + Path.DirectorySeparatorChar;

            // Impressively, Directory.EnumerateFiles doesn't throw if path is not an existing directory.
            if (!Directory.Exists(rootPath))
            {
                throw new DirectoryNotFoundException(rootPath);
            }

            foreach (var path in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
            {
                if (!path.StartsWithOrdinal(m_RootPrefix))
                {
                    throw new InvalidOperationException($"EnumerateFiles returned invalid result '{path}' for root '{m_RootPrefix}'");
                }

                var relativePath = path.Substring(m_RootPrefix.Length);
                if (isWindows)
                {
                    relativePath = relativePath.Replace('\\', '/');
                }
                files.Add(relativePath);
            }

            Files = files.AsReadOnly();
        }

        public Stream Open(string filename)
        {
            return new FileStream(m_RootPrefix + filename, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
