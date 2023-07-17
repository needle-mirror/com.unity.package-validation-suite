using System.IO;
using System.Text;

namespace PvpXray
{
    static class XrayUtils
    {
        /// Version of Encoding.UTF8 that throws on encoding errors instead of
        /// letting them pass silently with a replacement character.
        /// This affects conversions TO string only (Encoding.GetString), not
        /// vice versa (GetBytes etc.), where Encoding.UTF8 remains acceptable.
        public static readonly UTF8Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// Reading streams into a byte array is limited by the maximum byte[]
        /// length. For Mono (and thus, Unity) the limit is Int32.MaxValue
        /// (2147483647) bytes (tested on 2019.2.21f1 and 2023.2.0a5), but .NET
        /// Core (and thus, upm-pvp xray) is limited to only 2147483591 bytes.
        /// For consistency, use this constant to always enforce the lower of
        /// the two limits.
        public const int MaxByteArrayLength = 2147483591;

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
