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
            catch (JsonException)
            {
                throw new Verifier.FailAllException($"{file.Path}: file is not valid JSON");
            }
        }

        public static string ReadToString(this Verifier.PackageFile file)
        {
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
