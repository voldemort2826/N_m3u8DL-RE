namespace N_m3u8DL_RE.Config
{
    /// <summary>
    /// Control some logic through environment variables
    /// </summary>
    public static class EnvConfigKey
    {
        /// <summary>
        /// When this value is 1, the m4s file is not deleted after the PNG is generated in the graphic subtitle processing logic
        /// </summary>
        public const string ReKeepImageSegments = "RE_KEEP_IMAGE_SEGMENTS";

        /// <summary>
        /// Control the specific ffmpeg command line when PipeMux is enabled
        /// </summary>
        public const string ReLivePipeOptions = "RE_LIVE_PIPE_OPTIONS";

        /// <summary>
        /// Control the generation directory of named pipes in non-Windows environments when PipeMux is enabled
        /// </summary>
        public const string ReLivePipeTmpDir = "RE_LIVE_PIPE_TMP_DIR";
    }
}