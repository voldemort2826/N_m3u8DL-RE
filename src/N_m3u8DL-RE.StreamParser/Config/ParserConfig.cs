using N_m3u8DL_RE.Common.CommonEnumerations;
using N_m3u8DL_RE.StreamParser.Processor;
using N_m3u8DL_RE.StreamParser.Processor.DASH;
using N_m3u8DL_RE.StreamParser.Processor.HLS;

namespace N_m3u8DL_RE.StreamParser.Config
{
    public class ParserConfig
    {
        public string Url { get; set; } = string.Empty;

        public string OriginalUrl { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public Dictionary<string, string> CustomParserArgs { get; } = [];

        public Dictionary<string, string> Headers { get; init; } = [];

        /// <summary>
        /// Content pre-processor. Call order is the same as the list order
        /// </summary>
        public IList<ContentProcessor> ContentProcessors { get; } = [new DefaultHLSContentProcessor(), new DefaultDASHContentProcessor()];

        /// <summary>
        /// Add segment URL pre-processor. Call order is the same as the list order
        /// </summary>
        public IList<UrlProcessor> UrlProcessors { get; } = [new DefaultUrlProcessor()];

        /// <summary>
        /// KEY parser. Call order is the same as the list order
        /// </summary>
        public IList<KeyProcessor> KeyProcessors { get; } = [new DefaultHLSKeyProcessor()];


        /// <summary>
        /// Custom encryption method
        /// </summary>
        public EncryptMethod? CustomMethod { get; set; }

        /// <summary>
        /// Custom decryption KEY
        /// </summary>
        public byte[]? CustomeKey { get; set; }

        /// <summary>
        /// Custom decryption IV
        /// </summary>
        public byte[]? CustomeIV { get; set; }

        /// <summary>
        /// When assembling the URL of the video segment, whether to add the parameters after the original URL
        /// For example, Base URL = "http://xxx.com/playlist.m3u8?hmac=xxx&token=xxx"
        /// Relative path = clip_01.ts
        /// If AppendUrlParams=false, get http://xxx.com/clip_01.ts
        /// If AppendUrlParams=true, get http://xxx.com/clip_01.ts?hmac=xxx&token=xxx
        /// </summary>
        public bool AppendUrlParams { get; set; }

        /// <summary>
        /// This parameter will be passed to the URL Processor
        /// </summary>
        public string? UrlProcessorArgs { get; set; }

        /// <summary>
        /// KEY retry count
        /// </summary>
        public int KeyRetryCount { get; set; } = 3;
    }
}