using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;
using N_m3u8DL_RE.Common.Util;
using N_m3u8DL_RE.Entity;

namespace N_m3u8DL_RE.Util
{
    internal static class DownloadUtil
    {
        private static readonly HttpClient AppHttpClient = HTTPUtil.AppHttpClient;
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

        // Adaptive buffer sizing based on connection speed
        private static int GetOptimalBufferSize(SpeedContainer speedContainer, long? contentLength = null)
        {
            // Base buffer size
            int baseSize = 64 * 1024; // 64KB base

            // Adapt based on speed (bytes per second)
            if (speedContainer.NowSpeed > 0)
            {
                // For high-speed connections, use larger buffers
                if (speedContainer.NowSpeed > 50 * 1024 * 1024) // > 50MB/s
                {
                    return Math.Min(1024 * 1024, baseSize * 16); // Max 1MB
                }
                else if (speedContainer.NowSpeed > 10 * 1024 * 1024) // > 10MB/s
                {
                    return baseSize * 8; // 512KB
                }
                else if (speedContainer.NowSpeed > 1024 * 1024) // > 1MB/s
                {
                    return baseSize * 4; // 256KB
                }
                else
                {
                    return baseSize * 2; // 128KB
                }
            }

            // For small files, don't use oversized buffers
            return contentLength.HasValue && contentLength.Value < baseSize ? Math.Max(8192, (int)contentLength.Value) : baseSize;
        }

        private static async Task<DownloadResult> CopyFileAsync(string sourceFile, string path, SpeedContainer speedContainer, long? fromPosition = null, long? toPosition = null)
        {
            using FileStream inputStream = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream outputStream = new(path, FileMode.OpenOrCreate);
            _ = inputStream.Seek(fromPosition ?? 0L, SeekOrigin.Begin);

            long totalBytes = (toPosition ?? inputStream.Length) - inputStream.Position;

            // Use streaming approach for all file copies
            const int bufferSize = 256 * 1024; // 256KB for file operations
            byte[] buffer = BufferPool.Rent(bufferSize);

            try
            {
                long totalRead = 0;
                int bytesRead;

                while (totalRead < totalBytes && (bytesRead = await inputStream.ReadAsync(
                    buffer.AsMemory(0, (int)Math.Min(bufferSize, totalBytes - totalRead)))) > 0)
                {
                    await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    _ = speedContainer.Add(bytesRead);
                    totalRead += bytesRead;
                }

                return new DownloadResult()
                {
                    ActualContentLength = outputStream.Length,
                    ActualFilePath = path
                };
            }
            finally
            {
                BufferPool.Return(buffer);
            }
        }

        public static async Task<DownloadResult> DownloadToFileAsync(string url, string path, SpeedContainer speedContainer, CancellationTokenSource cancellationTokenSource, Dictionary<string, string>? headers = null, long? fromPosition = null, long? toPosition = null)
        {
            Logger.Debug(ResString.Fetch + url);

            // Handle special URL schemes
            if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                string file = new Uri(url).LocalPath;
                return await CopyFileAsync(file, path, speedContainer, fromPosition, toPosition);
            }
            if (url.StartsWith("base64://", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = Convert.FromBase64String(url[9..]);
                await File.WriteAllBytesAsync(path, bytes, cancellationTokenSource.Token);
                return new DownloadResult()
                {
                    ActualContentLength = bytes.Length,
                    ActualFilePath = path,
                };
            }
            if (url.StartsWith("hex://", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = HexUtil.HexToBytes(url[6..]);
                await File.WriteAllBytesAsync(path, bytes, cancellationTokenSource.Token);
                return new DownloadResult()
                {
                    ActualContentLength = bytes.Length,
                    ActualFilePath = path,
                };
            }

            using HttpRequestMessage request = new(HttpMethod.Get, new Uri(url));
            if (fromPosition != null || toPosition != null)
            {
                request.Headers.Range = new(fromPosition, toPosition);
            }

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> item in headers)
                {
                    _ = request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                }
            }
            Logger.Debug(request.Headers.ToString());

            try
            {
                using HttpResponseMessage response = await AppHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationTokenSource.Token);

                // Ignore single-byte off-by-one at EOF that returns 403
                if (response.StatusCode == HttpStatusCode.Forbidden && request.Headers.Range != null)
                {
                    RangeItemHeaderValue? r = request.Headers.Range.Ranges.FirstOrDefault();
                    if (r != null && r.From.HasValue && r.To.HasValue && r.From.Value == r.To.Value)
                    {
                        await File.WriteAllBytesAsync(path, [], cancellationTokenSource.Token);
                        return new DownloadResult()
                        {
                            ActualContentLength = 0,
                            RespContentLength = 0,
                            ActualFilePath = path,
                        };
                    }
                }

                // Handle redirects (this logic could be moved to HTTPUtil.DoGetAsync)
                if (((int)response.StatusCode).ToString(CultureInfo.InvariantCulture).StartsWith("30", StringComparison.OrdinalIgnoreCase))
                {
                    HttpResponseHeaders respHeaders = response.Headers;
                    Logger.Debug(respHeaders.ToString());
                    if (respHeaders.Location != null)
                    {
                        string redirectedUrl = respHeaders.Location.IsAbsoluteUri
                            ? respHeaders.Location.AbsoluteUri
                            : new Uri(new Uri(url), respHeaders.Location).AbsoluteUri;
                        return await DownloadToFileAsync(redirectedUrl, path, speedContainer, cancellationTokenSource, headers, fromPosition, toPosition);
                    }
                }

                _ = response.EnsureSuccessStatusCode();
                long? contentLength = response.Content.Headers.ContentLength;
                if (speedContainer.SingleSegment)
                {
                    speedContainer.ResponseLength = contentLength;
                }

                using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: Math.Min(1024 * 1024, GetOptimalBufferSize(speedContainer, contentLength))); // Optimize FileStream buffer
                using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationTokenSource.Token);

                // Use adaptive buffer sizing
                int bufferSize = GetOptimalBufferSize(speedContainer, contentLength);
                byte[] buffer = BufferPool.Rent(bufferSize);

                try
                {
                    bool imageHeader = false;
                    bool gZipHeader = false;
                    bool firstRead = true;

                    // Improved rate limiting variables
                    DateTime lastRateLimitCheck = DateTime.UtcNow;
                    const int rateLimitCheckInterval = 100; // Check every 100ms

                    int size;
                    while ((size = await responseStream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationTokenSource.Token)) > 0)
                    {
                        _ = speedContainer.Add(size);
                        await stream.WriteAsync(buffer.AsMemory(0, size), cancellationTokenSource.Token);

                        // Detect headers on first read
                        if (firstRead)
                        {
                            imageHeader = ImageHeaderUtil.IsImageHeader(buffer);
                            gZipHeader = size >= 2 && buffer[0] == 0x1f && buffer[1] == 0x8b;
                            firstRead = false;
                        }

                        // Improved rate limiting - check less frequently
                        DateTime now = DateTime.UtcNow;
                        if ((now - lastRateLimitCheck).TotalMilliseconds >= rateLimitCheckInterval)
                        {
                            if (speedContainer.Downloaded > speedContainer.SpeedLimit)
                            {
                                // Calculate appropriate delay based on overage
                                long overage = speedContainer.Downloaded - speedContainer.SpeedLimit;
                                int delayMs = Math.Min(1000, (int)(overage * 1000 / Math.Max(1, speedContainer.NowSpeed)));
                                if (delayMs > 0)
                                {
                                    await Task.Delay(delayMs, cancellationTokenSource.Token);
                                }
                            }
                            lastRateLimitCheck = now;
                        }
                    }

                    return new DownloadResult()
                    {
                        ActualContentLength = stream.Length,
                        RespContentLength = contentLength,
                        ActualFilePath = path,
                        ImageHeader = imageHeader,
                        GzipHeader = gZipHeader
                    };
                }
                finally
                {
                    BufferPool.Return(buffer);
                }
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationTokenSource.Token)
            {
                _ = speedContainer.ResetLowSpeedCount();
                throw new TimeoutException($"Download speed too slow! Current speed is below the minimum threshold. URL: {url}");
            }
        }
    }
}
