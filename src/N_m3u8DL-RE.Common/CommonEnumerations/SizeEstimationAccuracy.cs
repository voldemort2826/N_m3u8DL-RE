namespace N_m3u8DL_RE.Common.CommonEnumerations
{
    /// <summary>
    /// Indicates the accuracy level of size estimation
    /// </summary>
    public enum SizeEstimationAccuracy
    {
        /// <summary>
        /// Size from EXT-X-BYTERANGE tags (most accurate)
        /// </summary>
        Precise,
        /// <summary>
        /// Size from HTTP HEAD requests (accurate)
        /// </summary>
        HttpHead,
        /// <summary>
        /// Size from HTTP HEAD sampling (good estimate)
        /// </summary>
        HttpHeadSampled,
        /// <summary>
        /// Size from bitrate calculation (rough estimate)
        /// </summary>
        BitrateEstimate
    }
}
