using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PvpXray
{
    public class PvpHttpException : Exception
    {
        public PvpHttpRequest Request { get; }
        public PvpHttpException(PvpHttpRequest request, Exception inner) : this(request, inner.Message, inner) { }
        public PvpHttpException(PvpHttpRequest request, string message, Exception inner = null)
            : base($"HTTP request failed: {request.Method} {request.Url}: {message}", inner)
        {
            Request = request;
        }
    }

    public struct PvpHttpRequest
    {
        public string Method;
        public string Url;

        public PvpHttpRequest(string method, string url)
        {
            Method = method;
            Url = url;
        }
        public static implicit operator PvpHttpRequest(string url) => new PvpHttpRequest("GET", url);

        internal void CheckStatus(int status, int expected)
        {
            if (status != expected)
            {
                throw new PvpHttpException(this, $"unexpected HTTP status {status}");
            }
        }
    }

    public interface IPvpHttpClient
    {
        /// <summary>Perform a HTTP GET request to the given URL. Does not check HTTP status.</summary>
        /// <exception cref="PvpHttpException">
        /// A network or protocol error occurred when sending the request or reading the response,
        /// or the response body exceeded <see cref="XrayUtils.MaxByteArrayLength"/> bytes.
        /// </exception>
        PvpHttpResponse Get(string url);
    }

    public static class PvpHttpClientExtensions
    {
        public static PvpHttpResponse GetCheckStatus(this IPvpHttpClient self, string url, int expected = 200) => self.GetCheckStatus(url, expected, expected);
        public static PvpHttpResponse GetCheckStatus(this IPvpHttpClient self, string url, int expected1, int expected2)
        {
            var resp = self.Get(url);
            if (resp.Status != expected1) new PvpHttpRequest("GET", url).CheckStatus(resp.Status, expected2);
            return resp;
        }
    }

    public struct PvpHttpResponse
    {
        public readonly byte[] Buffer;
        public readonly int Length;
        public readonly int Status;

        public ArraySegment<byte> Body => new ArraySegment<byte>(Buffer, 0, Length);

        public PvpHttpResponse(int status, byte[] buffer) : this(status, buffer, buffer.Length) { }
        public PvpHttpResponse(int status, byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
            Status = status;
        }

        /// <summary>Same as <see cref="GetString"/>, except throwing SkipAllException.InvalidBaseline on error.</summary>
        /// <exception cref="Verifier.SkipAllException">If the body is not valid UTF-8, or exceeds <see cref="XrayUtils.MaxUtf8BytesForString"/> bytes.</exception>
        public string GetBaselineString()
        {
            if (Length > XrayUtils.MaxUtf8BytesForString)
                throw Verifier.SkipAllException.InvalidBaseline;
            try
            {
                return XrayUtils.Utf8Strict.GetString(Buffer, 0, Length);
            }
            catch (DecoderFallbackException)
            {
                throw Verifier.SkipAllException.InvalidBaseline;
            }
        }

        /// <summary>Returns Body as a string, using strict UTF-8 decoding.</summary>
        /// <exception cref="PvpHttpException">If the body is not valid UTF-8, or exceeds <see cref="XrayUtils.MaxUtf8BytesForString"/> bytes.</exception>
        public string GetString(PvpHttpRequest request)
        {
            if (Length > XrayUtils.MaxUtf8BytesForString)
                throw new PvpHttpException(request, $"HTTP response too big to decode as string at {Length} bytes");
            try
            {
                return XrayUtils.Utf8Strict.GetString(Buffer, 0, Length);
            }
            catch (DecoderFallbackException e)
            {
                throw new PvpHttpException(request, e);
            }
        }
    }

    /// Default IPvpHttpClient implementation, with automatic retry logic.
    public class PvpHttpClient : IPvpHttpClient
    {
        // Many of these internals are reused by upm-pvp's UpmPvpHttp.

        internal static int CheckLen(PvpHttpRequest request, long length)
            => length <= XrayUtils.MaxByteArrayLength ? (int)length
                : throw new PvpHttpException(request, $"HTTP response too big at {length} bytes");

        internal static void CheckLikelyResponseExceedingByteArrayLimit(PvpHttpRequest request, MemoryStream ms, IOException e)
        {
            // An IOException (Unity 2018.4) or HttpRequestException wrapping an
            // IOException (.NET 8) may be caused by the response exceeding the
            // max capacity of the MemoryStream's backing byte[]. But short of
            // implementing our own MemoryStream, there's no reliable way to
            // determine that this is the case: An IOException could be any one
            // of a million things (most of which we just want to retry), the
            // exception message may be localized, and ms.Position points to the
            // position BEFORE the failing write, so that's not a reliable
            // indicator either. However, it can be used as a reasonable hint.
            // In .NET 8, HttpConnectionResponseContent.SerializeToStreamAsync
            // uses a fixed block size of just 8 kiB (so downloading a 2 GB file
            // involves up to 262144 virtual Stream.Read + Write calls…), while
            // Unity 2018.4 seems to use a read size of 16 kiB. So if Position
            // is close to the max array size, assume the likely error cause.
            if (e.Message.Contains("Stream was too long") || ms.Position > XrayUtils.MaxByteArrayLength - 16 * 1024)
                throw new PvpHttpException(request, "HTTP response too big");
        }

        internal static (PvpHttpResponse, HttpContentHeaders Headers) HttpExchange(
            PvpHttpRequest request, HttpClient client, HttpRequestMessage httpRequestMessage, MemoryStream body)
        {
            // ReSharper disable AccessToDisposedClosure -- Task.Run blocks until we're done
            // ReSharper disable AccessToModifiedClosure
            return Task.Run(() => HttpExchangeAsync(request, client, httpRequestMessage, body)).GetAwaiter().GetResult();
        }

        internal static async Task<(PvpHttpResponse, HttpContentHeaders Headers)> HttpExchangeAsync(
            PvpHttpRequest request, HttpClient client, HttpRequestMessage httpRequestMessage, MemoryStream body)
        {
            using (var response = await client.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead))
            {
                // Caution: The automatic decompression in some Unity versions removes
                // the Content-Encoding header but not the Content-Length header. We
                // must therefore treat Content-Length as a minimum size. (PETS-1462)
                // Conversely, in .NET Core, we have no size hint for compressed content.
                var size = CheckLen(request, response.Content.Headers.ContentLength.GetValueOrDefault(1024));

                body.SetLength(0); // Discard old data (to avoid copying it). Also sets Position = 0.
                if (body.Capacity < size) body.Capacity = size;

                try
                {
                    await response.Content.CopyToAsync(body);
                }
                catch (IOException e)
                {
                    CheckLikelyResponseExceedingByteArrayLimit(request, body, e);
                    throw;
                }
                catch (HttpRequestException e) when (e.InnerException is IOException inner)
                {
                    CheckLikelyResponseExceedingByteArrayLimit(request, body, inner);
                    throw;
                }

                return (
                    new PvpHttpResponse((int)response.StatusCode, body.GetBuffer(), CheckLen(request, body.Position)),
                    response.Content.Headers
                );
            }
        }

        readonly string m_UserAgent;
        readonly Dictionary<string, PvpHttpResponse> m_Cache;
        internal int InitialRetryDelayMs = 10;
        internal int TimeoutMs = 60_000;

        public PvpHttpClient(string userAgent, bool cache)
        {
            m_UserAgent = userAgent;
            m_Cache = cache ? new Dictionary<string, PvpHttpResponse>() : null;
        }

        public PvpHttpResponse Get(string url)
        {
            if (m_Cache != null && m_Cache.TryGetValue(url, out var entry)) return entry;

            var ms = new MemoryStream();

            // Perform 4 retries (5 total attempts) with intervals 10ms, 100ms, 1s, 10s.
            var retryDelayMs = InitialRetryDelayMs;
            for (var nRetries = 4; ; --nRetries, retryDelayMs *= 10)
            {
                // We create a new HttpClient for every request (thus getting
                // no connection pooling), since reliability is infinitely more
                // important than performance, and we don't trust HttpClient
                // across the dozen different .NET runtimes and versions this
                // code might run on. (There are e.g. known bugs in its timeout
                // logic in several .NET runtimes, which we may have to work
                // around in the future, as was already done in Stevedore.)
                using (var client = new HttpClient(new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                }))
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;
                    client.Timeout = TimeSpan.FromMilliseconds(TimeoutMs);

                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.Add("User-Agent", m_UserAgent);

                        PvpHttpResponse result;
                        try
                        {
                            (result, _) = HttpExchange(url, client, request, ms);

                            // Retry on HTTP server error.
                            if (result.Status >= 500 && result.Status < 600 && nRetries > 0)
                            {
                                Thread.Sleep(retryDelayMs);
                                continue;
                            }
                        }
                        catch (Exception e) when (!(e is PvpHttpException))
                        {
                            // Retry on network error.
                            if (nRetries > 0)
                            {
                                Thread.Sleep(retryDelayMs);
                                continue;
                            }

                            if (e is TaskCanceledException) throw new PvpHttpException(url, "Request timed out");
                            throw new PvpHttpException(url, e);
                        }

                        if (m_Cache != null) m_Cache[url] = result;
                        return result;
                    }
                }
            }
        }
    }

    class TestHttpClient : IEnumerable, IPvpHttpClient
    {
        readonly Action<string> m_Assert;
        readonly List<(string, int, byte[])> m_Expected = new List<(string, int, byte[])>();
        int m_ExpectedIndex;

        public TestHttpClient(Action<string> assert)
        {
            m_Assert = assert;
        }

        public bool IsFinished => m_ExpectedIndex >= m_Expected.Count;

        public void Add(string url, int status, byte[] response) => m_Expected.Add((url, status, response));
        public void Add(string url, int status, string response) => Add(url, status, Encoding.UTF8.GetBytes(response));

        public PvpHttpResponse Get(string url)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));

            var expectedUrl = IsFinished ? null : m_Expected[m_ExpectedIndex].Item1;
            if (url != expectedUrl)
            {
                m_Assert($"Unexpected request for {url}; expected {expectedUrl ?? "no more requests"}.");
                return new PvpHttpResponse(-1, Array.Empty<byte>());
            }

            var resp = m_Expected[m_ExpectedIndex++];
            return new PvpHttpResponse(status: resp.Item2, buffer: resp.Item3);
        }

        IEnumerator IEnumerable.GetEnumerator()
            => throw new NotImplementedException("IEnumerator only implemented to enable Add syntax");


        public void AssertFinished()
        {
            if (!IsFinished)
            {
                m_Assert($"Expected {m_Expected.Count - m_ExpectedIndex} further HTTP request(s), starting with: {m_Expected[m_ExpectedIndex].Item1}");
            }
        }
    }
}
