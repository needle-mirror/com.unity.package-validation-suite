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
            if (file.Size > XrayUtils.MaxUtf8BytesForString)
            {
                throw new Verifier.FailAllException($"{file.Path}: file is too large to read as UTF-8 text");
            }

            return XrayUtils.DecodeUtf8Lax(file.Content);
        }

        /// Deprecated. Do not use in new checks.
        public static string ReadToStringLegacy(this Verifier.PackageFile file)
        {
            if (file.Size > XrayUtils.MaxUtf8BytesForString)
            {
                throw new Verifier.FailAllException($"{file.Path}: file is too large to read as UTF-8 text");
            }

            // Preserve UTF-8 BOM, but replace invalid UTF-8 sequences with replacement characters.
            return Encoding.UTF8.GetString(file.Content);
        }
    }
}
