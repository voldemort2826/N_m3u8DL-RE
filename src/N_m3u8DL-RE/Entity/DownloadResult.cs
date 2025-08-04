namespace N_m3u8DL_RE.Entity
{
    internal sealed class DownloadResult
    {
        public bool Success => (ActualContentLength != null && RespContentLength != null) ? (RespContentLength == ActualContentLength) : (ActualContentLength != null);
        public long? RespContentLength { get; set; }
        public long? ActualContentLength { get; set; }
        public bool ImageHeader { get; set; }  // Image header
        public bool GzipHeader { get; set; }  // GZip compression
        public required string ActualFilePath { get; set; }
    }
}