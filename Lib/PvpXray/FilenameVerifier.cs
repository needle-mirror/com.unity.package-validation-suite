using System;
using System.Collections.Generic;
using System.Linq;

namespace PvpXray
{
    class FilenameVerifier : Verifier.IChecker
    {
        public static string[] Checks => new[] { "PVP-70-1", "PVP-71-1", "PVP-72-1", "PVP-73-1" };
        public static int PassCount => 1;

        readonly Verifier.Context m_Context;

        public FilenameVerifier(Verifier.Context context)
        {
            m_Context = context;
            CheckCollidingPaths(m_Context);
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            var path = file.Path;

            var hasForbiddenControlCharacters = false;
            var hasSegmentsEndingInPeriod = false;
            var hasSegmentsStartingWithSpace = false;
            var hasSegmentsEndingWithSpace = false;
            var hasForbiddenAsciiChars = false;

            // On .NET Core, we could simply do fileName.IsNormalized() to check bad
            // surrogates, but Mono's implementation does not detect invalid UTF-16.
            var hasInvalidUtf16SurrogateSequence = false;
            var isNotUnicodeNormalizationFormC = false;

            try
            {
                isNotUnicodeNormalizationFormC = !path.IsNormalized();
            }
            catch (ArgumentException)
            {
                // do nothing.
                // Only interested if it is not Form C - not if it is invalid input
            }

            // Note: using bit operators |, & instead of ||, && to avoid short-circuiting and reduce branching.
            var prev = '\0';
            for (var i = 0; i < path.Length; i++)
            {
                var c = path[i];
                hasForbiddenControlCharacters |= char.IsControl(c);
                hasSegmentsEndingInPeriod |= (c == '/') & (prev == '.');
                hasSegmentsEndingWithSpace |= (c == '/') & (prev == ' ');
                hasSegmentsStartingWithSpace |= (c == ' ') & (prev == '/' | i == 0);
                hasForbiddenAsciiChars |= (c == '<') | (c == '>') | (c == ':') | (c == '"') | (c == '|') | (c == '?') | (c == '*');

                // Low surrogate not immediately following high surrogate?
                hasInvalidUtf16SurrogateSequence |= char.IsLowSurrogate(c) & !char.IsHighSurrogate(prev);

                // High surrogate not immediately followed by low surrogate?
                hasInvalidUtf16SurrogateSequence |= char.IsHighSurrogate(prev) & !char.IsLowSurrogate(c);

                prev = c;
            }
            hasSegmentsEndingInPeriod |= prev == '.';
            hasSegmentsEndingWithSpace |= prev == ' ';
            // High surrogate at end of string?
            hasInvalidUtf16SurrogateSequence |= char.IsHighSurrogate(prev);

            if (hasForbiddenControlCharacters)
                m_Context.AddError("PVP-70-1", $"{path}: control character");
            if (isNotUnicodeNormalizationFormC)
                m_Context.AddError("PVP-70-1", $"{path}: not in Unicode Normalization Form C");
            if (hasInvalidUtf16SurrogateSequence)
                m_Context.AddError("PVP-70-1", $"{path}: invalid UTF-16 surrogate sequence");
            if (hasSegmentsEndingInPeriod)
                m_Context.AddError("PVP-71-1", $"{path}: forbidden trailing period");
            if (hasSegmentsStartingWithSpace)
                m_Context.AddError("PVP-71-1", $"{path}: forbidden leading space");
            if (hasSegmentsEndingWithSpace)
                m_Context.AddError("PVP-71-1", $"{path}: forbidden trailing space");
            if (hasForbiddenAsciiChars)
                m_Context.AddError("PVP-71-1", $"{path}: forbidden character");
            if (HasSegmentsWithForbiddenName(file.Entry))
                m_Context.AddError("PVP-71-1", $"{path}: reserved device filename");

            if (file.Extension.HasFlags(FileExt.V2) && !file.Extension.IsCanonical)
            {
                var i = path.LastIndexOf('/');
                var filename = path.Substring(i + 1, path.Length - file.Extension.Raw.Length - i - 1);
                m_Context.AddError("PVP-73-1", $"{path}: should be {filename}{file.Extension.Canonical}");
            }
        }

        public void Finish()
        {
        }

        static string ToAsciiLowercase(char[] chars)
        {
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c >= 'A' && c <= 'Z')
                {
                    chars[i] = (char)(c + 32);
                }
            }
            return new string(chars);
        }

        static readonly string[] k_ForbiddenFileNames = {
            "aux", "clock$", "com0", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "con", "lpt0", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9", "nul", "prn",
        };

        static bool HasSegmentsWithForbiddenName(PathEntry path)
        {
            foreach (var segment in path.Components)
            {
                var i = segment.IndexOf('.');
                if (i == -1) i = segment.Length;
                if (i < 3 || i > 6) continue;

                var name = segment.Slice(0, i);
                if (k_ForbiddenFileNames.TryIndexOf(name, out _)) return true;
            }
            return false;
        }

        static void CheckCollidingPaths(Verifier.Context context)
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
                var lowerCasePath = ToAsciiLowercase(path.ToCharArray());

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
