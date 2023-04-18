using PvpXray;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityEditor.PackageManager.ValidationSuite
{
    public class FileSystemPackage : IEnumerable<IPackageFile>
    {
        class PackageFile : IPackageFile, IDisposable
        {
            static readonly bool k_IsWindows = System.IO.Path.DirectorySeparatorChar != System.IO.Path.AltDirectorySeparatorChar;
            readonly FileInfo m_Info;
            ThrowOnDisposedStream m_Content;
            bool m_Disposed;

            public PackageFile(string rootPrefix, FileInfo info)
            {
                var path = info.FullName;
                if (!path.StartsWithOrdinal(rootPrefix))
                {
                    throw new InvalidOperationException($"EnumerateFiles returned invalid result '{path}' for root '{rootPrefix}'");
                }

                var relativePath = path.Substring(rootPrefix.Length);
                if (k_IsWindows)
                {
                    relativePath = relativePath.Replace('\\', '/');
                }

                m_Info = info;
                Path = relativePath;
                Size = info.Length;
            }

            public string Path { get; }
            public long Size { get; }

            public Stream Content
            {
                get
                {
                    if (m_Disposed) throw new ObjectDisposedException(GetType().FullName, "cannot get Content stream");
                    return m_Content = m_Content ?? new ThrowOnDisposedStream(m_Info.Open(FileMode.Open, FileAccess.Read, FileShare.Read), preventSeek: true);
                }
            }

            public void Dispose()
            {
                m_Disposed = true;
                m_Content?.Dispose();
            }
        }

        readonly string m_RootPrefix;
        readonly IEnumerable<FileInfo> m_Files;

        public FileSystemPackage(string rootPath)
        {
            rootPath = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);
            m_RootPrefix = rootPath + Path.DirectorySeparatorChar;
            m_Files = new DirectoryInfo(rootPath).EnumerateFiles("*.*", SearchOption.AllDirectories).ToList();
        }

        public IEnumerator<IPackageFile> GetEnumerator()
        {
            foreach (var info in m_Files)
            {
                using (var file = new PackageFile(m_RootPrefix, info))
                {
                    yield return file;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
