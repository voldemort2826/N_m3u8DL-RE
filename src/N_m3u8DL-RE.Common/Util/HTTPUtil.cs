using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;

namespace N_m3u8DL_RE.Common.Util
{
    public static class HTTPUtil
    {
        public static readonly HttpClientHandler HttpClientHandler = new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
            MaxConnectionsPerServer = 1024,
        };

        public static readonly HttpClient AppHttpClient = new(HttpClientHandler)
        {
            Timeout = TimeSpan.FromSeconds(100),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };

        private static async Task<HttpResponseMessage> DoGetAsync(string url, Dictionary<string, string>? headers = null, int maxRedirects = 10, HashSet<string>? visitedUrls = null)
        {
            // Initialize visited URLs tracking on first call
            visitedUrls ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Check for redirect limit exceeded
            if (maxRedirects <= 0)
            {
                throw new InvalidOperationException($"Maximum redirect limit exceeded. Last URL: {url}");
            }

            // Check for redirect loop
            if (visitedUrls.Contains(url))
            {
                throw new InvalidOperationException($"Redirect loop detected. URL already visited: {url}");
            }

            // Add current URL to visited set
            _ = visitedUrls.Add(url);

            Logger.Debug(ResString.Fetch + url);
            using HttpRequestMessage webRequest = new(HttpMethod.Get, url);
            _ = webRequest.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            webRequest.Headers.CacheControl = CacheControlHeaderValue.Parse("no-cache");
            webRequest.Headers.Connection.Clear();
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> item in headers)
                {
                    _ = webRequest.Headers.TryAddWithoutValidation(item.Key, item.Value);
                }
            }
            Logger.Debug(webRequest.Headers.ToString());
            // Manually handle redirects to avoid loss of custom headers
            HttpResponseMessage webResponse = await AppHttpClient.SendAsync(webRequest, HttpCompletionOption.ResponseHeadersRead);
            if (webResponse.StatusCode is >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest)
            {
                HttpResponseHeaders respHeaders = webResponse.Headers;
                Logger.Debug(respHeaders.ToString());
                if (respHeaders.Location != null)
                {
                    string redirectedUrl = respHeaders.Location.IsAbsoluteUri
                        ? respHeaders.Location.AbsoluteUri
                        : new Uri(new Uri(url), respHeaders.Location).AbsoluteUri;

                    if (!string.Equals(redirectedUrl, url, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Extra($"Redirected => {redirectedUrl} (Redirects remaining: {maxRedirects - 1})");
                        webResponse.Dispose();
                        return await DoGetAsync(redirectedUrl, headers, maxRedirects - 1, visitedUrls);
                    }
                }
            }

            // Return the response
            _ = webResponse.EnsureSuccessStatusCode();
            return webResponse;
        }

        public static async Task<byte[]> GetBytesAsync(string url, Dictionary<string, string>? headers = null)
        {
            return url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                ? await File.ReadAllBytesAsync(new Uri(url).LocalPath)
                : await RetryUtil.WebRequestRetryAsync(async () =>
            {
                using HttpResponseMessage webResponse = await DoGetAsync(url, headers);

                // Get content length for pre-allocation if available
                long? contentLength = webResponse.Content.Headers.ContentLength;

                if (contentLength.HasValue && contentLength.Value > 100 * 1024 * 1024) // > 100MB
                {
                    // Use streaming for large files
                    using Stream stream = await webResponse.Content.ReadAsStreamAsync();
                    using MemoryStream memoryStream = new((int)contentLength.Value);
                    await stream.CopyToAsync(memoryStream);
                    return memoryStream.ToArray();
                }

                return await webResponse.Content.ReadAsByteArrayAsync();
            }) ?? throw new InvalidOperationException($"Failed to download bytes from {url} after all retry attempts");
        }

        /// <summary>
        /// Get the webpage source code
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static async Task<string> GetWebSourceAsync(string url, Dictionary<string, string>? headers = null)
        {
            return await RetryUtil.WebRequestRetryAsync(async () =>
            {
                using HttpResponseMessage webResponse = await DoGetAsync(url, headers);
                string htmlCode = await webResponse.Content.ReadAsStringAsync();
                Logger.Debug(htmlCode);
                return htmlCode;
            }) ?? throw new InvalidOperationException("Failed to get web source");
        }

        private static bool CheckMPEG2TS(HttpResponseMessage? webResponse)
        {
            if (webResponse?.Content.Headers.ContentType == null)
            {
                return false;
            }

            string? mediaType = webResponse.Content.Headers.ContentType.MediaType?.ToLowerInvariant();
            bool hasUnknownLength = webResponse.Content.Headers.ContentLength == null;

            string[] mpegTypes = ["video/ts", "video/mp2t", "video/mpeg", "application/octet-stream"];
            return hasUnknownLength && mpegTypes.Contains(mediaType);
        }

        /// <summary>
        /// Get the web page source code and the final resolved URL
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <returns>(Source Code, Final Resolved URL)</returns>
        public static async Task<(string, string)> GetWebSourceAndNewUrlAsync(string url, Dictionary<string, string>? headers = null)
        {
            return await RetryUtil.WebRequestRetryAsync(async () =>
            {
                using HttpResponseMessage webResponse = await DoGetAsync(url, headers);
                string htmlCode = CheckMPEG2TS(webResponse) ? ResString.ReLiveTs : await webResponse.Content.ReadAsStringAsync();
                Logger.Debug(htmlCode);

                // Get the final URL from the proper source - either from the request message or fallback to original
                Uri finalUrl = webResponse.RequestMessage?.RequestUri ?? new Uri(url);
                return (htmlCode, finalUrl.AbsoluteUri);
            });
        }

        public static async Task<string> GetPostResponseAsync(string Url, byte[] postData)
        {
            return await RetryUtil.WebRequestRetryAsync(async () =>
            {
                using HttpRequestMessage request = new(HttpMethod.Post, Url);
                _ = request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                _ = request.Headers.TryAddWithoutValidation("Content-Length", postData.Length.ToString(CultureInfo.InvariantCulture));
                request.Content = new ByteArrayContent(postData);
                using HttpResponseMessage webResponse = await AppHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                return await webResponse.Content.ReadAsStringAsync();
            }) ?? throw new InvalidOperationException($"Failed to post data to {Url} after all retry attempts");
        }

        /// <summary>
        /// Gets the content length of a URL using HTTP HEAD request
        /// </summary>
        /// <param name="url">URL to check</param>
        /// <param name="headers">Custom headers</param>
        /// <param name="timeoutSeconds">Timeout in seconds</param>
        /// <returns>Content length in bytes, or 0 if failed</returns>
        public static async Task<long> GetContentLengthAsync(string url, Dictionary<string, string>? headers = null, int timeoutSeconds = 10)
        {
            try
            {
                using CancellationTokenSource cts = new(TimeSpan.FromSeconds(timeoutSeconds));

                // Create dedicated HttpClient to avoid shared resource issues
                using HttpClient client = new(new HttpClientHandler()
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.All,
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                    MaxConnectionsPerServer = 1024,
                })
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds + 2)
                };

                using HttpRequestMessage request = new(HttpMethod.Head, url);

                // Add custom headers if provided
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                    {
                        _ = request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return response.Content.Headers.ContentLength ?? 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to get content length for {url}: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Sample items evenly distributed across a list
        /// </summary>
        /// <param name="allItems">All available items</param>
        /// <param name="sampleSize">Desired sample size</param>
        /// <returns>Sampled items</returns>
        public static List<T> SampleEvenly<T>(IList<T> allItems, int sampleSize)
        {
            if (allItems.Count <= sampleSize)
            {
                return [.. allItems];
            }

            List<T> sampleItems = [];
            double step = (double)allItems.Count / sampleSize;

            for (int i = 0; i < sampleSize; i++)
            {
                // Fixed: Clamp index to avoid out-of-range
                int index = Math.Min((int)(i * step), allItems.Count - 1);
                sampleItems.Add(allItems[index]);
            }

            return sampleItems;
        }
    }
}
