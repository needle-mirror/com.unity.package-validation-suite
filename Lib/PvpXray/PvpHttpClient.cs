using System;
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
            : base($"HTTP request for URL {url} failed: {inner.Message}", inner)
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
        readonly string m_UserAgent;

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

        public PvpHttpClient(string userAgent)
        {
            m_UserAgent = userAgent;
        }

        public Stream GetStream(string url, out int status)
        {
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
                        try
                        {
                            (status, body) = Task.Run(async () =>
                            {
                                // ReSharper disable AccessToDisposedClosure -- Task.Run blocks until we're done
                                // ReSharper disable AccessToModifiedClosure
                                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                                return ((int)response.StatusCode, await response.Content.ReadAsStreamAsync());
                            }).GetAwaiter().GetResult();
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
                        // Retry on HTTP server error.
                        if (status >= 500 && status < 600 && nRetries > 0)
                        {
                            Thread.Sleep(retryDelayMs);
                            continue;
                        }

                        // Success: pass responsibility for disposing HttpClient onto caller.
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
