using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PvpXray
{
    static class XrayUtils
    {
        // New in .NET 7, but until then.
        class UnreachableException : Exception
        {
            public UnreachableException(Exception innerException = null)
                : base("A situation that should never occur nevertheless occurred.", innerException)
            {
            }
        }

        /// Return sub-span or substring, depending on .NET version, to avoid needless allocations.
        /// Note that Span values CANNOT be compared using Equals or operator==.
        /// (SequenceEqual can be used, though for String that allocates and performs poorly.)
#if NET5_0_OR_GREATER
        public static ReadOnlySpan<char> SpanOrSubstring(this string s, int start) => s.AsSpan(start);
        public static ReadOnlySpan<char> SpanOrSubstring(this string s, int start, int length) => s.AsSpan(start, length);
#else
        public static string SpanOrSubstring(this string s, int start) => s.Substring(start);
        public static string SpanOrSubstring(this string s, int start, int length) => s.Substring(start, length);
#endif

        public static readonly Regex Sha1Regex = new Regex("^[0-9a-f]{40}$");

        /// Convert byte array to lowercase hex string.
#if NET5_0_OR_GREATER
        public static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
#else
        public static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
#endif

        /// Read and dispose stream, return SHA-1 hash as a hex string.
        public static string Sha1(Stream stream)
        {
            using (var hasher = SHA1.Create())
            using (stream)
            {
                return Hex(hasher.ComputeHash(stream));
            }
        }

        /// Return SHA-1 hash of byte array as a hex string.
        public static string Sha1(byte[] buffer, int length)
        {
#if NET5_0_OR_GREATER
            Span<byte> digest = stackalloc byte[20];
            SHA1.HashData(buffer.AsSpan(0, length), digest);
            return Hex(digest);
#else
            using (var hasher = SHA1.Create())
            {
                return Hex(hasher.ComputeHash(buffer, 0, length));
            }
#endif
        }

        public static string Sha1(byte[] buffer) => Sha1(buffer, buffer.Length);
        public static string Sha1(string text) => Sha1(Utf8Strict.GetBytes(text));

        /// Return SHA-256 hash of byte array as a hex string.
        public static string Sha256(byte[] buffer, int length)
        {
#if NET5_0_OR_GREATER
            Span<byte> digest = stackalloc byte[32];
            SHA256.HashData(buffer.AsSpan(0, length), digest);
            return Hex(digest);
#else
            using (var hasher = SHA256.Create())
            {
                return Hex(hasher.ComputeHash(buffer, 0, length));
            }
#endif
        }

        public static string Sha256(byte[] buffer) => Sha256(buffer, buffer.Length);
        public static string Sha256(string text) => Sha256(Utf8Strict.GetBytes(text));

        /// Version of Encoding.UTF8 that throws on encoding errors instead of
        /// letting them pass silently with a replacement character.
        /// This affects conversions TO string only (Encoding.GetString), not
        /// vice versa (GetBytes etc.), where Encoding.UTF8 remains acceptable.
        public static readonly UTF8Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// Decode UTF-8 bytes to .NET string, silently discarding UTF-8 BOM
        /// if present and replacing invalid UTF-8 sequences with '�'.
        public static string DecodeUtf8Lax(byte[] bytes)
        {
            const char replacementCharacter = '\ufffd';

            if (bytes.Length > MaxUtf8BytesForString) throw new ArgumentException("Input too long for conversion to string", nameof(bytes));

            // Silently discard UTF-8 BOM if present.
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var start = hasBom ? 3 : 0;
            var length = hasBom ? bytes.Length - 3 : bytes.Length;

            try
            {
                // Attempt to decode UTF-8 string.
                return Utf8Strict.GetString(bytes, start, length);
            }
            catch (DecoderFallbackException)
            {
                // Decode UTF-8 string replacing invalid UTF-8 sequences with replacement characters.
                var s = Encoding.UTF8.GetString(bytes, start, length);

                // The number of replacement characters resulting from an invalid UTF-8 sequence is implementation specific.
                // For consistency, collapse consecutive replacement character substrings to a single code point.
                var sb = new StringBuilder();
                var i = 0;
                while (i < s.Length)
                {
                    var next = s.IndexOf(replacementCharacter, i);
                    if (next == -1) break;

                    sb.Append(s, i, next - i);
                    sb.Append(replacementCharacter);

                    i = next + 1;
                    while (i < s.Length && s[i] == replacementCharacter)
                    {
                        i++;
                    }
                }
                sb.Append(s, i, s.Length - i);

                return sb.ToString();
            }
            catch (ArgumentException e)
            {
                throw new UnreachableException(e);
            }
        }

        /// Reading streams into a byte array is limited by the maximum byte[]
        /// length. For Mono (and thus, Unity) the limit is Int32.MaxValue
        /// (2147483647) bytes (tested on 2019.2.21f1 and 2023.2.0a5), but .NET
        /// Core (and thus, upm-pvp xray) is limited to only 2147483591 bytes.
        /// For consistency, use this constant to always enforce the lower of
        /// the two limits.
        public const int MaxByteArrayLength = 2147483591;

        /// .NET has an undocumented (probably runtime dependent) size limit
        /// for strings at a little less than 2^30 chars. As we're reading
        /// UTF-8, a char can take between 1 and 4 bytes. For expediency and
        /// consistency, just assume the worst case (1 byte per character,
        /// i.e. ASCII) and use a fixed limit of 1e9 bytes.
        public const int MaxUtf8BytesForString = 1_000_000_000;

        // Stream.ReadExactly is a .NET Core 7 built-in, but until then...
        /// Read entire contents of buffer from stream with no intermediate
        /// buffering (unlike Stream.CopyTo(MemoryStream)).
        /// Throws IOException if stream ends before buffer is fully read.
        public static void ReadExactly(Stream stream, byte[] buffer)
        {
            for (var offset = 0; offset < buffer.Length;)
            {
                var nRead = stream.Read(buffer, offset, buffer.Length - offset);
                if (nRead < 1) throw new IOException($"Unexpected EOF at offset {offset} of {buffer.Length}");
                offset += nRead;
            }
        }

        /// Read stream as a UTF-8 string (in strict mode), and dispose stream.
        public static string ReadToString(Stream stream)
        {
            using (var sr = new StreamReader(stream, Utf8Strict, detectEncodingFromByteOrderMarks: false))
            {
                return sr.ReadToEnd();
            }
        }

        /// Retrieve Stream contents as a byte array, as efficiently as possible.
        /// May return a reference to internal buffers; caller MUST NOT modify
        /// array contents. (IReadOnlyList doesn't give the desired performance,
        /// and can't use ReadOnlySpan while we need to support Unity 2021.2.)
        public static void GetStreamArray(Stream stream, out byte[] array, out int length)
        {
            // If we know the stream length and position, we can do an exact read.
            if (stream.CanSeek)
            {
                var sPosition = stream.Position;
                var sLength = stream.Length;

                // If MemoryStream (but not a subclass!) we may be able to
                // query the buffer directly and avoid any copying.
                if (sPosition == 0 &&
                    stream.GetType() == typeof(MemoryStream) &&
                    ((MemoryStream)stream).TryGetBuffer(out var arraySegment) &&
                    arraySegment.Offset == 0)
                {
                    array = arraySegment.Array;
                    length = arraySegment.Count;
                    stream.Position = length;
                    return;
                }

                var lengthLong = sLength - sPosition;
                if (lengthLong > MaxByteArrayLength)
                {
                    throw new IOException($"Stream contents too large ({lengthLong} bytes) to fit in array");
                }

                length = (int)lengthLong;
                array = new byte[length];
                ReadExactly(stream, array);
                return;
            }

            // Otherwise, proceed with naive path.
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            array = ms.GetBuffer();
            length = (int)ms.Length;
        }
    }
}
