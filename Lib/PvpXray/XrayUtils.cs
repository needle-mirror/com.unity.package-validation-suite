using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

#if NET5_0_OR_GREATER
using SpanOrString = System.ReadOnlySpan<char>;
using SpanOrArraySegment = System.ReadOnlySpan<byte>;
#else
using SpanOrString = System.String;
using SpanOrArraySegment = System.ArraySegment<byte>;
#endif

namespace PvpXray
{
    /// New in .NET Standard 2.1, which is only partially available in Unity 2018.4.
    static class NetStandard21Compat
    {
        // Avoid allocation for the sole single-char separator used in PvpXray (as of this writing).
        static readonly char[] k_ArrayOfSlash = { '/' };

        public static bool Contains(this string self, char value) => self.IndexOf(value) != -1;
        public static bool EndsWith(this string self, char value) => self.Length != 0 && self[self.Length - 1] == value;
        public static string[] Split(this string self, char separator, StringSplitOptions options = StringSplitOptions.None)
            => self.Split(separator == '/' ? k_ArrayOfSlash : new[] { separator }, options);
        public static bool StartsWith(this string self, char value) => self.Length != 0 && self[0] == value;

        public static int GetByteCount(this Encoding self, string s, int index, int count)
        {
            if (s == null || index < 0 || count < 0 || index > s.Length - count) throw new ArgumentException();
            unsafe
            {
                fixed (char* p = s)
                {
                    return self.GetByteCount(p + index, count);
                }
            }
        }

        public static V GetValueOrDefault<K, V>(this Dictionary<K, V> dictionary, K key)
            => dictionary.TryGetValue(key, out var value) ? value : default;
    }

    /// New in .NET 7, but until then.
    static class Net7Compat
    {
        public class UnreachableException : Exception
        {
            public UnreachableException(Exception innerException = null)
                : base("A situation that should never occur nevertheless occurred.", innerException)
            {
            }
        }

        public static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';
        public static bool IsAsciiHexDigit(char c) => IsAsciiDigit(c) || (c | 32) >= 'a' && (c | 32) <= 'f';
        public static bool IsAsciiLetter(char c) => (uint)((c | 32) - 97) <= 25U;
        public static bool IsAsciiLetterOrDigit(char c) => IsAsciiDigit(c) || IsAsciiLetter(c);

        /// Read entire contents of buffer from stream with no intermediate
        /// buffering (unlike Stream.CopyTo(MemoryStream)).
        /// Throws IOException if stream ends before buffer is fully read.
        // (In .NET 7, this is a native Stream method, taking precedence over extension methods.)
        public static void ReadExactly(this Stream stream, byte[] buffer)
        {
            for (var offset = 0; offset < buffer.Length;)
            {
                var nRead = stream.Read(buffer, offset, buffer.Length - offset);
                if (nRead < 1) throw new IOException($"Unexpected EOF at offset {offset} of {buffer.Length}");
                offset += nRead;
            }
        }
    }

    static class XrayUtils
    {
        public static StringBuilder AppendAsJson(this StringBuilder sb, string str)
        {
            Yaml.Encode(str, sb);
            return sb;
        }

        internal static bool IsUnicodeCharacter(char c) => c < 0xD800 || (c > 0xDFFF && c < 0xFDD0) || (c > 0xFDEF && c < 0xFFFE);
        public static bool IsValidUnicode(SpanOrString s)
        {
            // .NET strings are naive char[] wrappers and may be invalid UTF-16.
            // The following codepoints are not valid Unicode (ranges inclusive):
            // U+D800 - U+DFFF (when outside a valid surrogate pair)
            // U+FDD0 - U+FDEF (non-characters)
            // U+xFFFE - U+xFFFF for all planes 'x' 0x0 through 0x10 (non-characters)
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (IsUnicodeCharacter(c)) continue;

                var isHighSurrogate = c < 0xDC00;
                if (isHighSurrogate && ++i < s.Length)
                {
                    var c2 = s[i];
                    if (char.IsLowSurrogate(c2) && ((c & 0x3f) != 0x3f || c2 < 0xDC00 + 0x3fe)) continue;
                }
                return false;
            }
            return true;
        }

        /// Creates string from StringBuilder (starting at given index, default 0),
        /// then resets Length to the index (default: Clear entirely).
        public static string ToStringAndReset(this StringBuilder sb, int index = 0)
        {
            var result = sb.ToString(index, sb.Length - index);
            sb.Length = index;
            return result;
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

        /// Search for 'separator'. If found, returns SpanOrString preceding it, and else return input as SpanOrString.
        public static SpanOrString SplitLeft(this string self, char separator)
        {
            var i = self.IndexOf(separator);
            return i == -1 ? self : self.SpanOrSubstring(0, i);
        }

        public static readonly Regex Sha1Regex = new Regex("^[0-9a-f]{40}$");

        /// Convert byte array to lowercase hex string.
        public static string Hex(SpanOrArraySegment bytes)
#if NET5_0_OR_GREATER
            => Convert.ToHexString(bytes).ToLowerInvariant();
#else
            => BitConverter.ToString(bytes.Array, bytes.Offset, bytes.Count).Replace("-", "").ToLowerInvariant();
#endif
        public static string Hex(byte[] bytes) => Hex(new SpanOrArraySegment(bytes));

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
        public static string Sha1(SpanOrArraySegment bytes)
        {
#if NET5_0_OR_GREATER
            Span<byte> digest = stackalloc byte[20];
            SHA1.HashData(bytes, digest);
            return Hex(digest);
#else
            using (var hasher = SHA1.Create())
            {
                return Hex(hasher.ComputeHash(bytes.Array, bytes.Offset, bytes.Count));
            }
#endif
        }

        public static string Sha1(byte[] buffer) => Sha1(new SpanOrArraySegment(buffer));
        public static string Sha1(string text) => Sha1(Utf8Strict.GetBytes(text));

        /// Return SHA-256 hash of byte array as a hex string.
        public static string Sha256(SpanOrArraySegment bytes)
        {
#if NET5_0_OR_GREATER
            Span<byte> digest = stackalloc byte[32];
            SHA256.HashData(bytes, digest);
            return Hex(digest);
#else
            using (var hasher = SHA256.Create())
            {
                return Hex(hasher.ComputeHash(bytes.Array, bytes.Offset, bytes.Count));
            }
#endif
        }

        public static string Sha256(byte[] buffer) => Sha256(new SpanOrArraySegment(buffer));
        public static string Sha256(string text) => Sha256(Utf8Strict.GetBytes(text));

        /// Strict parsing of non-negative integers (consisting of ASCII digits only, no leading zeros).
        public static bool TryParseUint(SpanOrString text, out uint result)
            => uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out result)
                && (text.Length < 2 || text[0] != '0');

        /// Version of Encoding.UTF8 that throws on encoding errors instead of
        /// letting them pass silently with a replacement character.
        /// This affects conversions TO string only (Encoding.GetString), not
        /// vice versa (GetBytes etc.), where Encoding.UTF8 remains acceptable.
        public static readonly UTF8Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// Decode UTF-8 bytes to .NET string, silently discarding UTF-8 BOM
        /// if present and replacing invalid UTF-8 sequences with '�'.
        public static string DecodeUtf8Lax(byte[] bytes) => DecodeUtf8Lax(new ArraySegment<byte>(bytes));
        public static string DecodeUtf8Lax(ArraySegment<byte> bytes)
        {
            const char replacementCharacter = '\ufffd';

            if (bytes.Count > MaxUtf8BytesForString) throw new ArgumentException("Input too long for conversion to string", nameof(bytes));

            // Silently discard UTF-8 BOM if present.
            var hasBom = bytes.Count >= 3 && bytes.Array[bytes.Offset] == 0xEF && bytes.Array[bytes.Offset + 1] == 0xBB && bytes.Array[bytes.Offset + 2] == 0xBF;
            if (hasBom) bytes = new ArraySegment<byte>(bytes.Array, bytes.Offset + 3, bytes.Count - 3);

            try
            {
                // Attempt to decode UTF-8 string.
                return Utf8Strict.GetString(bytes.Array, bytes.Offset, bytes.Count);
            }
            catch (DecoderFallbackException)
            {
                // Decode UTF-8 string replacing invalid UTF-8 sequences with replacement characters.
                var s = Encoding.UTF8.GetString(bytes.Array, bytes.Offset, bytes.Count);

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
                throw new Net7Compat.UnreachableException(e);
            }
        }

        /// Reading streams into a byte array is limited by the maximum byte[]
        /// length. For Mono (and thus, Unity) the limit is Int32.MaxValue
        /// (2147483647) bytes (tested on 2019.2.21f1 and 2023.2.0a5), but .NET
        /// Core (and thus, upm-pvp xray) is limited to only 2147483591 bytes
        /// (Array.MaxLength). For consistency, use this constant to always
        /// enforce the lower of the two limits.
        public const int MaxByteArrayLength = 2147483591;

        /// .NET has an undocumented (probably runtime dependent) size limit
        /// for strings at a little less than 2^30 chars. As we're reading
        /// UTF-8, a char can take between 1 and 4 bytes. For expediency and
        /// consistency, just assume the worst case (1 byte per character,
        /// i.e. ASCII) and use a fixed limit of 1e9 bytes.
        public const int MaxUtf8BytesForString = 1_000_000_000;

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
                stream.ReadExactly(array);
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
