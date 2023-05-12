using System;
using System.IO;
using System.Text;

namespace PvpXray
{
    static class PackageFileExtensions
    {
        public static Json ReadAsJson(this Verifier.PackageFile file)
        {
            var text = file.ReadToString();

            if (text.StartsWithOrdinal("\ufeff"))
            {
                throw new Verifier.FailAllException($"{file.Path}: file contains UTF-8 BOM");
            }

            try
            {
                return new Json(text);
            }
            catch (SimpleJsonException)
            {
                throw new Verifier.FailAllException($"{file.Path}: file is not valid JSON");
            }
        }

        public static string ReadToString(this Verifier.PackageFile file)
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

            try
            {
                // Decode without .NET's magic BOM handling
                return Encoding.UTF8.GetString(file.Content);
            }
            catch (ArgumentException)
            {
                throw new Verifier.FailAllException($"{file.Path}: file could not be read as UTF-8 text");
            }
        }
    }
}
