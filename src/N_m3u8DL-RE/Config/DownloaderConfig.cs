using N_m3u8DL_RE.CommandLine;

namespace N_m3u8DL_RE.Config
{
    internal sealed class DownloaderConfig
    {
        public required MyOption MyOptions { get; set; }

        /// <summary>
        /// The folder name generated in the previous step
        /// </summary>
        public required string DirPrefix { get; set; }
        /// <summary>
        /// File name template
        /// </summary>
        public string? SavePattern { get; set; }
        /// <summary>
        /// Check the file size of the response header and the actual size
        /// </summary>
        public bool CheckContentLength { get; set; } = true;
        /// <summary>
        /// Request header
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = [];
    }
}