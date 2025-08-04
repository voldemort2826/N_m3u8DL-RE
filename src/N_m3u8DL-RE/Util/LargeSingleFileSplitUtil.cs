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
            public required long To;
        }

        /// <summary>
        /// Large file slicing processing
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static async Task<List<MediaSegment>?> SplitUrlAsync(MediaSegment segment, Dictionary<string, string> headers)
        {
            string url = segment.Url;
            if (!await CanSplitAsync(url, headers))
            {
                return null;
            }

            if (segment.StartRange != null)
            {
                return null;
            }

            long fileSize = await GetFileSizeAsync(url, headers);
            if (fileSize == 0)
            {
                return null;
            }

            List<Clip> allClips = GetAllClips(fileSize);
            List<MediaSegment> splitSegments = [];
            foreach (Clip clip in allClips)
            {
                splitSegments.Add(new MediaSegment()
                {
                    Index = clip.Index,
                    Url = url,
                    StartRange = clip.From,
                    ExpectLength = clip.To == -1 ? null : clip.To - clip.From + 1,
                    EncryptInfo = segment.EncryptInfo,
                });
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
            using HttpRequestMessage httpRequestMessage = new();
            httpRequestMessage.RequestUri = new(url);
            foreach (KeyValuePair<string, string> header in headers)
            {
                _ = httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            HttpResponseMessage response = (await HTTPUtil.AppHttpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead)).EnsureSuccessStatusCode();
            long totalSizeBytes = response.Content.Headers.ContentLength ?? 0;

            return totalSizeBytes;
        }

        // This function mainly handles the slicing download logic
        private static List<Clip> GetAllClips(long fileSize)
        {
            List<Clip> clips = [];
            int index = 0;
            long counter = 0;
            int perSize = 10 * 1024 * 1024;
            while (fileSize > 0)
            {
                Clip c = new()
                {
                    Index = index,
                    From = counter,
                    To = counter + perSize
                };
                // Not at the end
                if (fileSize - perSize > 0)
                {
                    fileSize -= perSize;
                    counter += perSize + 1;
                    index++;
                    clips.Add(c);
                }
                // Already at the end
                else
                {
                    c.To = -1;
                    clips.Add(c);
                    break;
                }
            }
            return clips;
        }
    }
}