namespace N_m3u8DL_RE.Common.Entity
{
    public class Playlist
    {
        // Corresponding Url information
        public string Url { get; set; } = string.Empty;
        // Whether it is live
        public bool IsLive { get; set; }
        // Live refresh interval in milliseconds (default 15 seconds)
        public double RefreshIntervalMs { get; set; } = 15000;
        // Total duration of all segments
        public double TotalDuration => MediaParts.Sum(x =>
        {
            static double selector(MediaSegment m)
            {
                return m.Duration;
            }

            return x.MediaSegments.Sum(selector);
        });

        // Longest duration of all segments
        public double? TargetDuration { get; set; }
        // INIT information
        public MediaSegment? MediaInit { get; set; }
        // Segment information
        public List<MediaPart> MediaParts { get; set; } = [];
    }
}