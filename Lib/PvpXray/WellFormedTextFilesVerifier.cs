using System;

namespace PvpXray
{
    class WellFormedTextFilesVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[] {
            "PVP-120-1", "PVP-121-1", "PVP-122-1", "PVP-123-1", "PVP-124-1", "PVP-125-1",
            "PVP-120-2", "PVP-121-2", "PVP-122-2", "PVP-123-2", "PVP-124-2", "PVP-125-2",
        };
        public static int PassCount => 1;

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
            // Ignore empty files.
            var size = file.Size;
            if (size == 0) return;

            // Ignore files without known "text file" filename extensions.
            var isV1Eligible = file.Suffix.HasFlags(FileExt.V1, FileExt.TextFile) && file.Suffix.IsCanonical;
            var isV2Eligible = file.Extension.HasFlags(FileExt.V1 | FileExt.V2, FileExt.TextFile);
            if (!isV1Eligible && !isV2Eligible) return;

            var failures = 0;
            const int pvp120 = 1 << 0; // File is not well-formed UTF-8.
            const int pvp121 = 1 << 1; // Carriage Return appears in file.
            const int pvp122 = 1 << 2; // Horizontal Tab appears in file.
            const int pvp123 = 1 << 3; // Control code (not including Horizontal Tab, Line Feed, and Carriage Return) appears in file.
            const int pvp124 = 1 << 4; // Trailing Space or Horizontal Tab appears in file.
            const int pvp125 = 1 << 5; // UTF-8 byte order mark sequence appears (anywhere) in file.

            var content = file.Content;

            // Check for invalid UTF-8 encoding.
            try
            {
                _ = XrayUtils.Utf8Strict.GetCharCount(content);
            }
            catch (ArgumentException)
            {
                failures |= pvp120;
            }

            for (var i = 0; i < size; i++)
            {
                var b = content[i];
                if (b == '\r')
                {
                    failures |= pvp121;
                }
                else if (b == '\t' || b == ' ')
                {
                    if (b == '\t')
                    {
                        failures |= pvp122;
                    }

                    if (i == size - 1 || content[i + 1] == '\n' || content[i + 1] == '\r')
                    {
                        failures |= pvp124;
                    }
                }
                else if (b == k_UTF8ByteOrderMark1)
                {
                    if (i < size - 2 && content[i + 1] == k_UTF8ByteOrderMark2 && content[i + 2] == k_UTF8ByteOrderMark3)
                    {
                        failures |= pvp125;
                    }
                }
                // Control codes values are 0 through 31 and additionally 127.
                // We want to exclude Horizontal Tab, Line Feed, and Carriage Return.
                // b cannot be Horizontal Tab (9) nor Carriage Return (13) at this point.
                else if (b == 127 || b <= 31 && b != '\n')
                {
                    failures |= pvp123;
                }
            }

            var path = file.Path;
            for (var i = 0; i < 6; ++i)
            {
                if ((failures & (1 << i)) == 0) continue;

                // Add PVP-*-1 error (if eligible).
                if (isV1Eligible) m_Context.AddError(Checks[i], path);
                // Add PVP-*-2 error (if eligible).
                if (isV2Eligible) m_Context.AddError(Checks[i + 6], path);
            }
        }

        public void Finish()
        {
        }
    }
}
