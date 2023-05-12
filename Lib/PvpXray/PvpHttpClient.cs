using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PvpXray
{
    public class PvpHttpException : Exception
    {
        public string Url { get; }

        public PvpHttpException(string url, Exception inner)
            : this(url, inner.Message, inner) { }

        public PvpHttpException(string url, string message, Exception inner = null)
            : base($"HTTP request for URL {url} failed: {message}", inner)
        {
            Url = url;
        }
    }

    public interface IPvpHttpClient
    {
        /// Perform a HTTP GET request to the given URL, returning the
        /// HTTP status code and the response body as a stream.
        Stream GetStream(string url, out int status);
    }

    public static class PvpHttpClientExtensions
    {
        /// Perform a HTTP GET request to the given URL, returning the
        /// HTTP status and response body (decoded as UTF-8) as a string.
        public static string GetString(this IPvpHttpClient self, string url, out int status)
        {
            try
            {
                using (var sr = new StreamReader(
                    self.GetStream(url, out status),
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (Exception e) when (!(e is PvpHttpException))
            {
                throw new PvpHttpException(url, e);
            }
        }
    }

    /// Default IPvpHttpClient implementation, with automatic retry logic.
    public class PvpHttpClient : IPvpHttpClient
    {
        // A wrapper Stream that disposes both its underlying Stream and the
        // HttpClient. The ever decorative .NET type system has no distinct
        // type for a read-only, non-seekable stream, so most methods here
        // throw exceptions.
        class HttpStream : Stream
        {
            readonly HttpClient m_Client;
            readonly Stream m_Stream;

            public HttpStream(HttpClient client, Stream stream)
            {
                m_Client = client;
                m_Stream = stream;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    m_Stream.Dispose();
                    m_Client.Dispose();
                }
            }

            public override void Flush() { /* no-op */ }
            public override int ReadByte() => m_Stream.ReadByte();
            public override int Read(byte[] buffer, int offset, int count) => m_Stream.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        }

        struct CacheEntry
        {
            public readonly byte[] Data;
            public readonly int Length;
            public readonly int Status;

            static int CheckLen(long length)
                => length <= XrayUtils.MaxByteArrayLength ? (int)length
                    : throw new IOException($"HTTP response too large to cache: {length} bytes");

            public CacheEntry(int status, long? length, Stream stream)
            {
                Status = status;
                if (length.HasValue) // Fast path for when we know exact size
                {
                    Length = CheckLen(length.Value);
                    Data = new byte[Length];
                    XrayUtils.ReadExactly(stream, Data);
                }
                else
                {
                    var ms = new MemoryStream();
                    stream.CopyTo(ms);

                    // MemoryStream will usually have thrown by now, but on Mono, we might make it to this point.
                    Length = CheckLen(ms.Length);
                    Data = ms.GetBuffer();
                }
            }

            public MemoryStream GetStream() => new MemoryStream(Data, index: 0, count: Length, writable: false, publiclyVisible: true);
        }

        readonly string m_UserAgent;
        readonly Dictionary<string, CacheEntry> m_Cache;

        public PvpHttpClient(string userAgent) : this(userAgent, false) { }

        public PvpHttpClient(string userAgent, bool cache)
        {
            m_UserAgent = userAgent;
            m_Cache = cache ? new Dictionary<string, CacheEntry>() : null;
        }

        public Stream GetStream(string url, out int status)
        {
            if (m_Cache != null && m_Cache.TryGetValue(url, out var entry))
            {
                status = entry.Status;
                return entry.GetStream();
            }

            // Perform 4 retries (5 total attempts) with intervals 10ms, 100ms, 1s, 10s.
            var retryDelayMs = 10;
            for (var nRetries = 4; ; --nRetries, retryDelayMs *= 10)
            {
                // We create a new HttpClient for every request (thus getting
                // no connection pooling), since reliability is infinitely more
                // important than performance, and we don't trust HttpClient
                // across the dozen different .NET runtimes and versions this
                // code might run on. (There are e.g. known bugs in its timeout
                // logic in several .NET runtimes, which we may have to work
                // around in the future, as was already done in Stevedore.)
                var client = new HttpClient();
                try
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;
                    client.Timeout = TimeSpan.FromMinutes(1);

                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.Add("User-Agent", m_UserAgent);

                        Stream body;
                        long? contentLength;
                        try
                        {
                            (status, contentLength, body) = Task.Run(async () =>
                            {
                                // ReSharper disable AccessToDisposedClosure -- Task.Run blocks until we're done
                                // ReSharper disable AccessToModifiedClosure
                                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                                return (
                                    (int)response.StatusCode,
                                    response.Content.Headers.ContentLength,
                                    await response.Content.ReadAsStreamAsync()
                                );
                            }).GetAwaiter().GetResult();

                            // Retry on HTTP server error.
                            if (status >= 500 && status < 600 && nRetries > 0)
                            {
                                Thread.Sleep(retryDelayMs);
                                continue;
                            }

                            // If caching the response, read entire thing into memory now and return stream wrapping it.
                            if (m_Cache != null)
                            {
                                var newEntry = m_Cache[url] = new CacheEntry(status, contentLength, body);
                                return newEntry.GetStream();
                            }
                        }
                        catch (Exception e)
                        {
                            // Retry on network error.
                            if (nRetries > 0)
                            {
                                Thread.Sleep(retryDelayMs);
                                continue;
                            }

                            throw new PvpHttpException(url, e);
                        }

                        // If streaming response, pass responsibility for disposing HttpClient onto caller.
                        var result = new HttpStream(client, body);
                        client = null;
                        return result;
                    }
                }
                finally
                {
                    client?.Dispose();
                }
            }
        }
    }
}
