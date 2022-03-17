using System.Security.Cryptography;
using System.Text;

namespace Unity.APIComparison.Framework
{
    static class StringExtensions
    {
        public static string ComputeHash(this string self)
        {
            using (var hasher = MD5.Create())
            {
                byte[] data = hasher.ComputeHash(Encoding.UTF8.GetBytes(self));
                var sb = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                {
                    sb.Append(data[i].ToString("x2"));
                }

                return sb.ToString().ToUpperInvariant();
            }
        }
    }
}
