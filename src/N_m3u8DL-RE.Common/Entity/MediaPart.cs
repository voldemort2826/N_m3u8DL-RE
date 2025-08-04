namespace N_m3u8DL_RE.Common.Entity
{
    /// <summary>
    /// Process EXT-X-DISCONTINUITY
    /// </summary>
    public class MediaPart
    {
        public List<MediaSegment> MediaSegments { get; set; } = [];
    }
}