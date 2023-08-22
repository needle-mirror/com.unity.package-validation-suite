using System;
using System.Text;

namespace PvpXray
{
    static class PackageFileExtensions
    {
        const char k_ReplacementCharacter = '\ufffd';

        /// Read file.Content as a JSON document, silently discarding UTF-8 BOM
        /// if present and replacing invalid UTF-8 sequences in JSON string
        /// values with '�'. Other invalid UTF-8 sequences will cause
        /// SimpleJsonException to be raised, as will JSON syntax errors.
        /// If a IChecker fails to handle a SimpleJsonException raised by this
        /// method, or later by the returned Json object, all checker checks
        /// are marked as failed (as with a FailAllException).
        public static Json ReadAsJsonLax(this Verifier.PackageFile file)
            => new Json(file.ReadToStringLax(), file.Path);

        /// Read file.Content as a (simplified) YAML document, silently
        /// discarding UTF-8 BOM if present and replacing invalid UTF-8
        /// sequences in YAML string values with '�'. Other invalid UTF-8
        /// sequences will cause YamlParseException to be raised, as will YAML
        /// syntax errors.
        /// If a IChecker fails to handle a YamlParseException raised by this
        /// method, or later fails to handle a YamlAccessException raised the
        /// returned Yaml object, all checker checks are marked as failed (as
        /// with a FailAllException).
        public static Yaml ReadAsYamlLax(this Verifier.PackageFile file)
            => new Yaml(file.ReadToStringLax(), file.Path);

        /// Read file.Content as an UTF-8 string, silently discarding UTF-8 BOM
        /// if present and replacing invalid UTF-8 sequences with '�'.
        /// Throws FailAllException only if file is too large (> 1 GB).
        public static string ReadToStringLax(this Verifier.PackageFile file)
        {
            // .NET has an undocumented (probably runtime dependent) size limit
            // for strings at a little less than 2^30 chars. As we're reading
            // UTF-8, a char can take between 1 and 4 bytes. For expediency and
            // consistency, just assume the worst case (1 byte per character,
            // i.e. ASCII) and use a fixed limit of 1e9 bytes.
            if (file.Size > 1_000_000_000)
            {
                throw new Verifier.FailAllException($"{file.Path}: file is too large to read as UTF-8 text");
            }

            // Silently discard UTF-8 BOM if present.
            var bytes = file.Content;
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var start = hasBom ? 3 : 0;
            var length = hasBom ? bytes.Length - 3 : bytes.Length;

            try
            {
                // Attempt to decode UTF-8 string.
                return XrayUtils.Utf8Strict.GetString(bytes, start, length);
            }
            catch (ArgumentException)
            {
                // Decode UTF-8 string replacing invalid UTF-8 sequences with replacement characters.
                var s = Encoding.UTF8.GetString(bytes, start, length);

                // The number of replacement characters resulting from an invalid UTF-8 sequence is implementation specific.
                // For consistency, collapse consecutive replacement character substrings to a single code point.
                var sb = new StringBuilder();
                var i = 0;
                while (i < s.Length)
                {
                    var next = s.IndexOf(k_ReplacementCharacter, i);
                    if (next == -1) break;

                    sb.Append(s.Substring(i, next - i));
                    sb.Append(k_ReplacementCharacter);

                    i = next + 1;
                    while (i < s.Length && s[i] == k_ReplacementCharacter)
                    {
                        i++;
                    }
                };
                sb.Append(s.Substring(i, s.Length - i));

                return sb.ToString();
            }
        }

        /// Deprecated. Do not use in new checks.
        public static string ReadToStringLegacy(this Verifier.PackageFile file)
        {
            // .NET has an undocumented (probably runtime dependent) size limit
            // for strings at a little less than 2^30 chars. As we're reading
            // UTF-8, a char can take between 1 and 4 bytes. For expediency and
            // consistency, just assume the worst case (1 byte per character,
            // i.e. ASCII) and use a fixed limit of 1e9 chars.
            if (file.Size > 1_000_000_000)
            {
                throw new Verifier.FailAllException($"{file.Path}: file is too large to read as UTF-8 text");
            }

            // Preserve UTF-8 BOM, but replace invalid UTF-8 sequences with replacement characters.
            return Encoding.UTF8.GetString(file.Content);
        }
    }
}
