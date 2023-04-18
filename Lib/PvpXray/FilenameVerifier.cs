using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PvpXray
{
    internal static class FilenameVerifierExtensions
    {
        static readonly byte[] ForbiddenControlChars = {
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09,
            0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19,
            0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
            0x7f,
            0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
            0x8A, 0x8B, 0x8C, 0x8D, 0x8E, 0x8F,
            0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99,
            0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F,
        };

        static readonly string[] ForbiddenFileNames = {
            "aux", "clock$", "com0", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "con", "lpt0", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9", "nul", "prn"
        };

        private static readonly char[] ForbiddenAsciiCharacters = { '<', '>', ':', '"', '|', '?', '*' };

        public static bool HasForbiddenControlCharacters(this string fileName)
        {
            var fileNameBytes = Encoding.ASCII.GetBytes(fileName);
            foreach (var controlChar in ForbiddenControlChars)
            {
                if (fileNameBytes.Contains(controlChar))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsNotUnicodeNormalizationFormC(this string fileName)
        {
            try
            {
                return fileName.IsNormalized() == false;
            }
            catch (ArgumentException)
            {
                // do nothing.
                // Only interested if it is not Form C - not if it is invalid input
            }
            return false;
        }

        public static bool HasInvalidUTF16SurrogateSequence(this string fileName)
        {
            // On .NET Core, we could simply do fileName.IsNormalized() to check this;
            // but Mono's implementation does not detect invalid UTF-16.
            for (var i = 0; i < fileName.Length; ++i)
            {
                var c = fileName[i];

                // Low surrogate not immediately following low surrogate?
                if (char.IsLowSurrogate(c)) return true;

                if (char.IsHighSurrogate(c))
                {
                    // High surrogate not followed by low surrogate?
                    if (i == fileName.Length - 1 || !char.IsLowSurrogate(fileName[i + 1])) return true;

                    // OK; skip the high surrogate.
                    ++i;
                }
            }
            return false;
        }

        public static bool HasForbiddenAsciiChars(this string fileName)
        {
            foreach (var asciiChar in ForbiddenAsciiCharacters)
            {
                if (fileName.Contains(asciiChar))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasSegmentsWithForbiddenName(this string path)
        {
            var lowercasePath = ConvertToAsciiLowercase(path);
            var pathSegments = lowercasePath.Split('/');
            foreach (var segment in pathSegments)
            {
                foreach (var forbiddenName in ForbiddenFileNames)
                {
                    if (segment.Split('.').First() == forbiddenName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static string ConvertToAsciiLowercase(string path)
        {
            return new string(
                path.Select(c => c >= 'A' && c <= 'Z' ? (char)((int)c + 32) : c).ToArray()
                );
        }

        public static bool HasSegmentsEndingInPeriod(this string path)
        {
            var segments = path.Split('/');
            foreach (var segment in segments)
            {
                if (segment.EndsWithOrdinal("."))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasSegmentsStartingWithSpace(this string path)
        {
            var segments = path.Split('/');
            foreach (var segment in segments)
            {
                if (segment.StartsWithOrdinal(" "))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasSegmentsEndingWithSpace(this string path)
        {
            var segments = path.Split('/');
            foreach (var segment in segments)
            {
                if (segment.EndsWithOrdinal(" "))
                {
                    return true;
                }
            }
            return false;
        }
    }

    class FilenameVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[] { "PVP-70-1", "PVP-71-1", "PVP-72-1" };

        public static int PassCount => 1;

        readonly Verifier.IContext m_Context;

        public FilenameVerifier(Verifier.IContext context)
        {
            m_Context = context;
            CheckCollidingPaths(m_Context);
        }

        string GetFilename(string filePath)
        {
            return filePath.Split('/').Last();
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            var path = file.Path;
            if (path.HasForbiddenControlCharacters())
                m_Context.AddError("PVP-70-1", $"{path}: control character");
            if (path.IsNotUnicodeNormalizationFormC())
                m_Context.AddError("PVP-70-1", $"{path}: not in Unicode Normalization Form C");
            if (path.HasInvalidUTF16SurrogateSequence())
                m_Context.AddError("PVP-70-1", $"{path}: invalid UTF-16 surrogate sequence");
            if (path.HasSegmentsEndingInPeriod())
                m_Context.AddError("PVP-71-1", $"{path}: forbidden trailing period");
            if (path.HasSegmentsStartingWithSpace())
                m_Context.AddError("PVP-71-1", $"{path}: forbidden leading space");
            if (path.HasSegmentsEndingWithSpace())
                m_Context.AddError("PVP-71-1", $"{path}: forbidden trailing space");
            if (path.HasForbiddenAsciiChars())
                m_Context.AddError("PVP-71-1", $"{path}: forbidden character");
            if (path.HasSegmentsWithForbiddenName())
                m_Context.AddError("PVP-71-1", $"{path}: reserved device filename");
        }

        public void Finish()
        {

        }

        static void CheckCollidingPaths(Verifier.IContext context)
        {
            var entries = new Dictionary<string, List<string>>(context.Files.Count);

            void AddEntry(string path, string lowerCasePath)
            {
                if (entries.TryGetValue(lowerCasePath, out var entry))
                {
                    // Is it a directory prefix that we've seen (with this casing) before? If so, we're done.
                    if (entry[0] == path && lowerCasePath[lowerCasePath.Length - 1] == '/')
                    {
                        return;
                    }

                    if (!entry.Contains(path))
                    {
                        entry.Add(path);
                    }
                }
                else
                {
                    entries[lowerCasePath] = new List<string> { path };
                }
            }

            foreach (var path in context.Files)
            {
                var lowerCasePath = new string(path.Select(c => c >= 'A' && c <= 'Z' ? (char)((int)c + 32) : c).ToArray());

                // Add full path.
                AddEntry(path, lowerCasePath);

                // Add directory prefixes.
                for (var i = 0; i < path.Length; ++i)
                {
                    if (path[i] == '/')
                    {
                        AddEntry(path.Substring(0, i + 1), lowerCasePath.Substring(0, i + 1));
                    }
                }
            }

            foreach (var kv in entries.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (kv.Value.Count == 1) continue;
                foreach (var path in kv.Value)
                {
                    context.AddError("PVP-72-1", path);
                }
            }
        }
    }
}
