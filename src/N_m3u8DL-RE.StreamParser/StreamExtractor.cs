using System.Diagnostics.CodeAnalysis;

using N_m3u8DL_RE.Common.CommonEnumerations;
using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;
using N_m3u8DL_RE.Common.Util;
using N_m3u8DL_RE.StreamParser.Config;
using N_m3u8DL_RE.StreamParser.Constants;
using N_m3u8DL_RE.StreamParser.Extractor;

namespace N_m3u8DL_RE.StreamParser
{
    public class StreamExtractor(ParserConfig parserConfig)
    {
        public ExtractorType ExtractorType => extractor?.ExtractorType ?? throw new InvalidOperationException("Extractor not initialized");
        private IExtractor? extractor;
        private readonly ParserConfig parserConfig = parserConfig;
        private string? rawText;
        private static readonly SemaphoreSlim semaphore = new(1, 1);

        public Dictionary<string, string> RawFiles { get; set; } = []; // Store (file name, file content)

        public async Task LoadSourceFromUrlAsync(string url)
        {
            Logger.Info(ResString.LoadingUrl + url);
            if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                Uri uri = new(url);
                rawText = await File.ReadAllTextAsync(uri.LocalPath);
                parserConfig.OriginalUrl = parserConfig.Url = url;
            }
            else if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                parserConfig.OriginalUrl = url;
                (rawText, url) = await HTTPUtil.GetWebSourceAndNewUrlAsync(url, parserConfig.Headers);
                parserConfig.Url = url;
            }
            else if (File.Exists(url))
            {
                url = Path.GetFullPath(url);
                rawText = await File.ReadAllTextAsync(url);
                parserConfig.OriginalUrl = parserConfig.Url = new Uri(url).AbsoluteUri;
            }
            if (rawText != null)
            {
                rawText = rawText.Trim();
                LoadSourceFromText(rawText);
            }
            else
            {
                throw new InvalidOperationException("Failed to load content from URL");
            }
            LoadSourceFromText(rawText);
        }

        [MemberNotNull(nameof(this.rawText), nameof(extractor))]
        private void LoadSourceFromText(string rawText)
        {
            string rawType = "txt";
            rawText = rawText.Trim();
            this.rawText = rawText;
            if (rawText.StartsWith(HLSTags.ext_m3u, StringComparison.OrdinalIgnoreCase))
            {
                Logger.InfoMarkUp(ResString.MatchHLS);
                extractor = new HLSExtractor(parserConfig);
                rawType = "m3u8";
            }
            else if (rawText.Contains("</MPD>") && rawText.Contains("<MPD"))
            {
                Logger.InfoMarkUp(ResString.MatchDASH);
                // extractor = new DASHExtractor(parserConfig);
                extractor = new DASHExtractor2(parserConfig);
                rawType = "mpd";
            }
            else if (rawText.Contains("</SmoothStreamingMedia>") && rawText.Contains("<SmoothStreamingMedia"))
            {
                Logger.InfoMarkUp(ResString.MatchMSS);
                // extractor = new DASHExtractor(parserConfig);
                extractor = new MSSExtractor(parserConfig);
                rawType = "ism";
            }
            else if (rawText == ResString.ReLiveTs)
            {
                Logger.InfoMarkUp(ResString.MatchTS);
                extractor = new LiveTSExtractor(parserConfig);
            }
            else
            {
                throw new NotSupportedException(ResString.NotSupported);
            }

            RawFiles[$"raw.{rawType}"] = rawText;
        }

        /// <summary>
        /// Start parsing stream media information
        /// </summary>
        /// <returns></returns>
        public async Task<List<StreamSpec>> ExtractStreamsAsync()
        {
            if (extractor == null || rawText == null)
            {
                throw new InvalidOperationException("StreamExtractor not initialized. Call LoadSourceFromUrlAsync first.");
            }

            try
            {
                await semaphore.WaitAsync();
                Logger.Info(ResString.ParsingStream);
                return await extractor.ExtractStreamsAsync(rawText);
            }
            finally
            {
                _ = semaphore.Release();
            }
        }

        /// <summary>
        /// Fill media playlist information according to specification
        /// </summary>
        /// <param name="streamSpecs"></param>
        public async Task FetchPlayListAsync(List<StreamSpec> streamSpecs)
        {
            if (extractor == null)
            {
                throw new InvalidOperationException("StreamExtractor not initialized. Call LoadSourceFromUrlAsync first.");
            }
            try
            {
                await semaphore.WaitAsync();
                Logger.Info(ResString.ParsingStream);
                await extractor.FetchPlayListAsync(streamSpecs);
            }
            finally
            {
                _ = semaphore.Release();
            }
        }

        public async Task RefreshPlayListAsync(List<StreamSpec> streamSpecs)
        {
            if (extractor == null)
            {
                throw new InvalidOperationException("StreamExtractor not initialized. Call LoadSourceFromUrlAsync first.");
            }
            try
            {
                await semaphore.WaitAsync();
                _ = await RetryUtil.WebRequestRetryAsync(async () =>
                {
                    await extractor.RefreshPlayListAsync(streamSpecs);
                    return true;
                }, retryDelayMilliseconds: 1000, maxRetries: 5);
            }
            finally
            {
                _ = semaphore.Release();
            }
        }
    }
}