using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Util;

namespace N_m3u8DL_RE.Util
{
    internal static class LargeSingleFileSplitUtil
    {
        private sealed class Clip
        {
            public required int Index;
            public required long From;
            public required long To; // inclusive; use -1 to indicate "until end"
        }

        /// <summary>
        /// Large file slicing processing
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="playlist"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static async Task<List<MediaSegment>?> SplitUrlAsync(MediaSegment segment, Playlist? playlist, Dictionary<string, string> headers)
        {
            string url = segment.Url;
            if (!await CanSplitAsync(url, headers))
            {
                return null;
            }

            // Already a ranged segment; do not split
            if (segment.StartRange != null)
            {
                return null;
            }

            long fileSize = await GetFileSizeAsync(url, headers);
            if (fileSize <= 0)
            {
                return null;
            }

            List<MediaSegment> splitSegments = [];
            int outIndex = 0;

            // 1) Add indexRange first if present
            long? indexStart = playlist?.IndexRangeStart;
            long? indexEnd = playlist?.IndexRangeEnd;
            if (indexStart != null && indexEnd != null && indexStart <= indexEnd && indexEnd < fileSize)
            {
                long length = indexEnd.Value - indexStart.Value + 1;
                splitSegments.Add(new MediaSegment()
                {
                    Index = outIndex++,
                    Url = url,
                    StartRange = indexStart.Value,
                    ExpectLength = length,
                    EncryptInfo = segment.EncryptInfo,
                });
            }

            // 2) Remainder after init and index, in 5MB chunks
            long remainderStart = 0;

            // Skip init (downloaded elsewhere)
            long initEnd = -1;
            if (playlist?.MediaInit?.StartRange != null && playlist.MediaInit.ExpectLength != null)
            {
                initEnd = playlist.MediaInit.StartRange.Value + playlist.MediaInit.ExpectLength.Value - 1;
            }

            if (indexEnd != null)
            {
                remainderStart = Math.Max(remainderStart, indexEnd.Value + 1);
            }
            remainderStart = Math.Max(remainderStart, initEnd + 1);

            if (remainderStart < fileSize)
            {
                List<Clip> clips = GetAllClips(fileSize, remainderStart, 5 * 1024 * 1024);
                foreach (Clip clip in clips)
                {
                    splitSegments.Add(new MediaSegment()
                    {
                        Index = outIndex++,
                        Url = url,
                        StartRange = clip.From,
                        ExpectLength = clip.To - clip.From + 1,
                        EncryptInfo = segment.EncryptInfo,
                    });
                }
            }

            return splitSegments;
        }

        public static async Task<bool> CanSplitAsync(string url, Dictionary<string, string> headers)
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Head, url);
                foreach (KeyValuePair<string, string> header in headers)
                {
                    _ = request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                HttpResponseMessage response = (await HTTPUtil.AppHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)).EnsureSuccessStatusCode();
                bool supportsRangeRequests = response.Headers.Contains("Accept-Ranges");

                return supportsRangeRequests;
            }
            catch (Exception ex)
            {
                Logger.DebugMarkUp(ex.Message);
                return false;
            }
        }

        private static async Task<long> GetFileSizeAsync(string url, Dictionary<string, string> headers)
        {
            using HttpRequestMessage httpRequestMessage = new(HttpMethod.Head, url);
            foreach (KeyValuePair<string, string> header in headers)
            {
                _ = httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            HttpResponseMessage response = (await HTTPUtil.AppHttpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead)).EnsureSuccessStatusCode();
            long totalSizeBytes = response.Content.Headers.ContentLength ?? 0;

            return totalSizeBytes;
        }

        // Build inclusive byte ranges starting at startOffset, in perSize chunks
        private static List<Clip> GetAllClips(long fileSize, long startOffset, int perSize)
        {
            List<Clip> clips = [];
            if (startOffset >= fileSize)
            {
                return clips;
            }

            long pos = startOffset;
            int idx = 0;

            while (pos < fileSize)
            {
                long end = pos + perSize - 1;
                if (end < fileSize - 1)
                {
                    clips.Add(new Clip
                    {
                        Index = idx++,
                        From = pos,
                        To = end
                    });
                    pos = end + 1;
                }
                else
                {
                    // last chunk to exact EOF (closed range)
                    clips.Add(new Clip
                    {
                        Index = idx++,
                        From = pos,
                        To = fileSize - 1
                    });
                    break;
                }
            }

            return clips;
        }
    }
}