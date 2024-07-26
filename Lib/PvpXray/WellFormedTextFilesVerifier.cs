using System;
using System.Text;

namespace PvpXray
{
    class WellFormedTextFilesVerifier : Verifier.IChecker
    {
        public static string[] Checks { get; } = {
            "PVP-39-1",
            "PVP-120-2", "PVP-121-2", "PVP-122-2", "PVP-123-2", "PVP-124-2", "PVP-125-2",
            "PVP-200-1",
        };
        public static int PassCount => 1;

        const byte k_UTF8ByteOrderMark1 = 0xef;
        const byte k_UTF8ByteOrderMark2 = 0xbb;
        const byte k_UTF8ByteOrderMark3 = 0xbf;

        static byte[] k_TestAttrib = Encoding.ASCII.GetBytes("Test");
        static byte[] k_UnityTestAttrib = Encoding.ASCII.GetBytes("UnityTest");

        readonly Verifier.Context m_Context;

        public WellFormedTextFilesVerifier(Verifier.Context context)
        {
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            m_Context = context;
        }

        static bool StartsWithBytes(byte[] buffer, int offset, byte[] substring) // for lack of Span
        {
            if (offset + substring.Length > buffer.Length) return false;
            foreach (var b in substring)
            {
                if (b != buffer[offset++]) return false;
            }
            return true;
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            // Ignore empty files.
            var size = file.Size;
            if (size == 0) return;

            var content = file.Content;
            var isConsideredBinaryByGit = false;
            for (var i = 0; i < Math.Min(size, 8000); ++i)
            {
                if (file.Content[i] == (byte)'\0')
                {
                    isConsideredBinaryByGit = true;
                    break;
                }
            }

            // Ignore files without known "text file" filename extensions – except for Git conflict markers.
            var isV2Eligible = file.Extension.HasFlags(FileExt.V2, FileExt.TextFile);
            if (!isV2Eligible && isConsideredBinaryByGit) return;

            var isCSharp = !file.Entry.IsHidden && file.Extension.Canonical == ".cs";

            var failures = 0;
            const int pvp39 = 1 << 0; // Git conflict markers.
            const int pvp120 = 1 << 1; // File is not well-formed UTF-8.
            const int pvp121 = 1 << 2; // Carriage Return appears in file.
            const int pvp122 = 1 << 3; // Horizontal Tab appears in file.
            const int pvp123 = 1 << 4; // Control code (not including Horizontal Tab, Line Feed, and Carriage Return) appears in file.
            const int pvp124 = 1 << 5; // Trailing Space or Horizontal Tab appears in file.
            const int pvp125 = 1 << 6; // UTF-8 byte order mark sequence appears (anywhere) in file.
            const int pvp200 = 1 << 7; // Test attributes in C# code.

            if (isV2Eligible)
            {
                // Check for invalid UTF-8 encoding.
                try
                {
                    _ = XrayUtils.Utf8Strict.GetCharCount(content);
                }
                catch (ArgumentException)
                {
                    failures |= pvp120;
                }
            }

            var startOfLine = true;
            var startOfLineIndented = true;
            for (var i = 0; i < size; i++)
            {
                var b = content[i];
                if (b == '\n')
                {
                    startOfLine = startOfLineIndented = true;
                    continue;
                }
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
                // We want to exclude Horizontal Tab, Line Feed, and Carriage Return,
                // all of which are handled above.
                else if (b == 127 || b <= 31)
                {
                    failures |= pvp123;
                }
                else if (startOfLine && !isConsideredBinaryByGit && i + 7 < size && (b == '<' || b == '>') && content[i + 7] == ' ')
                {
                    var isConflictMarker = true;
                    for (var j = i + 6; j > i; --j)
                    {
                        if (content[j] != b)
                        {
                            isConflictMarker = false;
                            break;
                        }
                    }
                    if (isConflictMarker) failures |= pvp39;
                }
                else if (isCSharp && startOfLineIndented && b == '[' && (StartsWithBytes(content, i + 1, k_TestAttrib) || StartsWithBytes(content, i + 1, k_UnityTestAttrib)))
                {
                    failures |= pvp200;
                }

                startOfLine = false;
                if (b != '\t' && b != ' ') startOfLineIndented = false;
            }

            if (!isV2Eligible) failures &= pvp39;
            if (failures == 0) return;

            var path = file.Path;
            for (var i = 0; i < Checks.Length; ++i)
            {
                if ((failures & (1 << i)) == 0) continue;

                m_Context.AddError(Checks[i], path);
            }
        }

        public void Finish()
        {
        }
    }
}
