using System;
using System.Text;

namespace PvpXray
{
    class WellFormedTextFilesVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[] { "PVP-120-1", "PVP-121-1", "PVP-122-1", "PVP-123-1", "PVP-124-1", "PVP-125-1" };
        public static int PassCount => 1;

        static string[] k_TextFileExtensions = new string[]
        {
            ".cginc",
            ".compute",
            ".cpp",
            ".cs",
            ".h",
            ".hlsl",
            ".js",
            ".json",
            ".md",
            ".py",
            ".shader",
            ".txt",
            ".uss",
            ".uxml",
            ".yaml",
            ".yml",
        };

        const byte k_UTF8ByteOrderMark1 = 0xef;
        const byte k_UTF8ByteOrderMark2 = 0xbb;
        const byte k_UTF8ByteOrderMark3 = 0xbf;

        readonly Verifier.IContext m_Context;

        public WellFormedTextFilesVerifier(Verifier.IContext context)
        {
            m_Context = context;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            // Ignore files without known "text file" filename extensions.
            var isTextFile = false;
            var path = file.Path;
            for (var i = 0; i < k_TextFileExtensions.Length; i++)
            {
                if (path.EndsWithOrdinal(k_TextFileExtensions[i]))
                {
                    isTextFile = true;
                    break;
                }
            }
            if (!isTextFile) return;

            // Ignore empty files.
            var size = file.Size;
            if (size == 0) return;

            // Check for invalid UTF-8 encoding.
            var content = file.Content;
            try
            {
                _ = XrayUtils.Utf8Strict.GetCharCount(content);
            }
            catch (ArgumentException)
            {
                m_Context.AddError("PVP-120-1", path);
            }

            var pvp121 = false; // Carriage Return appears in file.
            var pvp122 = false; // Horizontal Tab appears in file.
            var pvp123 = false; // Control code (not including Horizontal Tab, Line Feed, and Carriage Return) appears in file.
            var pvp124 = false; // Trailing Space or Horizontal Tab appears in file.
            var pvp125 = false; // UTF-8 byte order mark sequence appears (anywhere) in file.

            for (var i = 0; i < size; i++)
            {
                var b = content[i];
                if (b == '\r')
                {
                    pvp121 = true;
                }
                else if (b == '\t' || b == ' ')
                {
                    if (b == '\t')
                    {
                        pvp122 = true;
                    }

                    if (i == size - 1 || content[i + 1] == '\n' || content[i + 1] == '\r')
                    {
                        pvp124 = true;
                    }
                }
                else if (b == k_UTF8ByteOrderMark1)
                {
                    if (i < size - 2 && content[i + 1] == k_UTF8ByteOrderMark2 && content[i + 2] == k_UTF8ByteOrderMark3)
                    {
                        pvp125 = true;
                    }
                }
                // Control codes values are 0 through 31 and additionally 127.
                // We want to exclude Horizontal Tab, Line Feed, and Carriage Return.
                // b cannot be Horizontal Tab (9) nor Carriage Return (13) at this point.
                else if (b == 127 || b <= 31 && b != '\n')
                {
                    pvp123 = true;
                }
            }

            if (pvp121) m_Context.AddError("PVP-121-1", path);
            if (pvp122) m_Context.AddError("PVP-122-1", path);
            if (pvp123) m_Context.AddError("PVP-123-1", path);
            if (pvp124) m_Context.AddError("PVP-124-1", path);
            if (pvp125) m_Context.AddError("PVP-125-1", path);
        }

        public void Finish()
        {
        }
    }
}
