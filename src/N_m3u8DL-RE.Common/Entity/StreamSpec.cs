using N_m3u8DL_RE.Common.CommonEnumerations;
using N_m3u8DL_RE.Common.Util;

using Spectre.Console;

namespace N_m3u8DL_RE.Common.Entity
{
    public class StreamSpec
    {
        public MediaType? MediaType { get; set; }
        public string? GroupId { get; set; }
        public string? Language { get; set; }
        public string? Name { get; set; }
        public Choice? Default { get; set; }

        // Skipped segment duration due to user selection
        public double? SkippedDuration { get; set; }

        // MSS information
        public MSSData? MSSData { get; set; }

        // Basic information
        public int? Bandwidth { get; set; }
        public string? Codecs { get; set; }
        public string? Resolution { get; set; }
        public double? FrameRate { get; set; }
        public string? Channels { get; set; }
        public string? Extension { get; set; }

        // Dash
        public RoleType? Role { get; set; }

        // Additional information - color gamut
        public string? VideoRange { get; set; }
        // Additional information - characteristics
        public string? Characteristics { get; set; }
        // Publication time (only needed for MPD)
        public DateTime? PublishTime { get; set; }

        // External track GroupId (subsequent search for corresponding track information)
        public string? AudioId { get; set; }
        public string? VideoId { get; set; }
        public string? SubtitleId { get; set; }

        public string? PeriodId { get; set; }

        /// <summary>
        /// URL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Original URL
        /// </summary>
        public string OriginalUrl { get; set; } = string.Empty;

        public Playlist? Playlist { get; set; }

        /// <summary>
        /// Cached estimated download size in bytes
        /// </summary>
        public long? EstimatedSize { get; set; }

        /// <summary>
        /// Indicates the accuracy level of the size estimation
        /// </summary>
        public SizeEstimationAccuracy? EstimationAccuracy { get; set; }

        public int SegmentsCount => Playlist != null ? Playlist.MediaParts.Sum(x => x.MediaSegments.Count) : 0;

        /// <summary>
        /// Estimates the total download size using HTTP HEAD requests for accuracy
        /// </summary>
        /// <param name="headers">HTTP headers to use for requests</param>
        /// <param name="maxConcurrency">Maximum concurrent HEAD requests (default: 5)</param>
        /// <param name="timeoutSeconds">Timeout for each HEAD request (default: 10)</param>
        /// <returns>Estimated size in bytes, or 0 if estimation fails</returns>
        public async Task<long> EstimateDownloadSizeAsync(
            Dictionary<string, string>? headers = null,
            int maxConcurrency = 5,
            int timeoutSeconds = 10,
            double successThreshold = 0.7,
            int maxSegmentsToCheck = 0)
        {
            // Return cached value if it's accurate enough
            if (EstimatedSize.HasValue && (EstimationAccuracy == SizeEstimationAccuracy.Precise ||
                                           EstimationAccuracy == SizeEstimationAccuracy.HttpHead))
            {
                return EstimatedSize.Value;
            }

            if (Playlist == null)
            {
                EstimatedSize = 0;
                EstimationAccuracy = SizeEstimationAccuracy.BitrateEstimate;
                return 0;
            }

            try
            {
                // Method 1: Use ExpectLength from segments if available (most accurate)
                long totalExpectedLength = Playlist.MediaParts
                    .SelectMany(part => part.MediaSegments)
                    .Where(segment => segment.ExpectLength.HasValue)
                    .Sum(segment => segment.ExpectLength!.Value);

                if (totalExpectedLength > 0)
                {
                    EstimatedSize = totalExpectedLength;
                    EstimationAccuracy = SizeEstimationAccuracy.Precise;
                    return totalExpectedLength;
                }

                // Method 2: HTTP HEAD requests for precise size
                List<MediaSegment> allSegments = [.. Playlist.MediaParts
                    .SelectMany(part => part.MediaSegments)
                    .Where(segment => !string.IsNullOrEmpty(segment.Url))];

                if (allSegments.Count == 0)
                {
                    return EstimateSizeFromBitrate();
                }

                // Limit segments to check if specified
                List<MediaSegment> segmentsToCheck = maxSegmentsToCheck > 0 && maxSegmentsToCheck < allSegments.Count
                    ? HTTPUtil.SampleEvenly(allSegments, maxSegmentsToCheck)
                    : allSegments;

                // Use semaphore to limit concurrent requests
                using SemaphoreSlim semaphore = new(maxConcurrency, maxConcurrency);
                IEnumerable<Task<long>> tasks = segmentsToCheck.Select(async segment =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        return await HTTPUtil.GetContentLengthAsync(segment.Url, headers, timeoutSeconds);
                    }
                    finally
                    {
                        _ = semaphore.Release();
                    }
                });

                // Execute all HEAD requests concurrently
                long[] sizes = await Task.WhenAll(tasks);
                long totalSize = sizes.Where(size => size > 0).Sum();

                // Check success rate
                int successfulRequests = sizes.Count(size => size > 0);
                double actualSuccessRate = (double)successfulRequests / segmentsToCheck.Count;

                if (actualSuccessRate >= successThreshold)
                {
                    // Extrapolate to full size if we sampled
                    if (segmentsToCheck.Count < allSegments.Count)
                    {
                        double scaleFactor = (double)allSegments.Count / segmentsToCheck.Count;
                        totalSize = (long)(totalSize * scaleFactor);
                        EstimationAccuracy = SizeEstimationAccuracy.HttpHeadSampled;
                    }
                    else
                    {
                        // If some requests failed, estimate their size based on average
                        if (successfulRequests < allSegments.Count)
                        {
                            long averageSize = totalSize / successfulRequests;
                            int failedRequests = allSegments.Count - successfulRequests;
                            totalSize += averageSize * failedRequests;
                        }
                        EstimationAccuracy = SizeEstimationAccuracy.HttpHead;
                    }

                    EstimatedSize = totalSize;
                    return totalSize;
                }

                // Method 3: Fallback to bitrate calculation
                return EstimateSizeFromBitrate();
            }
            catch
            {
                // If HTTP HEAD requests fail, fallback to bitrate calculation
                return EstimateSizeFromBitrate();
            }
        }

        /// <summary>
        /// Estimates size from bitrate and duration (fallback method)
        /// </summary>
        private long EstimateSizeFromBitrate()
        {
            if (Bandwidth.HasValue && Playlist != null && Playlist.TotalDuration > 0)
            {
                long estimatedBytes = (long)(Bandwidth.Value / 8.0 * Playlist.TotalDuration);
                EstimatedSize = estimatedBytes;
                EstimationAccuracy = SizeEstimationAccuracy.BitrateEstimate;
                return estimatedBytes;
            }

            EstimatedSize = 0;
            EstimationAccuracy = SizeEstimationAccuracy.BitrateEstimate;
            return 0;
        }

        /// <summary>
        /// Synchronous method that returns cached estimated size or estimates from bitrate
        /// </summary>
        public long EstimateDownloadSize()
        {
            return EstimatedSize ?? EstimateSizeFromBitrate();
        }

        /// <summary>
        /// Gets a user-friendly accuracy description
        /// </summary>
        public string GetEstimationAccuracyDescription()
        {
            return EstimationAccuracy switch
            {
                SizeEstimationAccuracy.Precise => "Precise (from playlist)",
                SizeEstimationAccuracy.HttpHead => "Accurate (HTTP HEAD)",
                SizeEstimationAccuracy.HttpHeadSampled => "Good estimate (sampled)",
                SizeEstimationAccuracy.BitrateEstimate => "Rough estimate (bitrate)",
                _ => "Unknown"
            };
        }

        public string ToShortString()
        {
            string encStr = string.Empty;

            string prefixStr;
            string returnStr;
            if (MediaType == CommonEnumerations.MediaType.AUDIO)
            {
                prefixStr = $"[deepskyblue3]Aud[/] {encStr}";
                string d = $"{GroupId} | {(Bandwidth != null ? (Bandwidth / 1000) + " Kbps" : "")} | {Name} | {Codecs} | {Language} | {(Channels != null ? Channels + "CH" : "")} | {Role}";
                returnStr = d.EscapeMarkup();
            }
            else if (MediaType == CommonEnumerations.MediaType.SUBTITLES)
            {
                prefixStr = $"[deepskyblue3_1]Sub[/] {encStr}";
                string d = $"{GroupId} | {Language} | {Name} | {Codecs} | {Role}";
                returnStr = d.EscapeMarkup();
            }
            else
            {
                prefixStr = $"[aqua]Vid[/] {encStr}";
                string d = $"{Resolution} | {Bandwidth / 1000} Kbps | {GroupId} | {FrameRate} | {Codecs} | {VideoRange} | {Role}";
                returnStr = d.EscapeMarkup();
            }

            returnStr = prefixStr + returnStr.Trim().Trim('|').Trim();
            while (returnStr.Contains("|  |"))
            {
                returnStr = returnStr.Replace("|  |", "|");
            }

            return returnStr.TrimEnd().TrimEnd('|').TrimEnd();
        }

        public string ToShortShortString()
        {
            string encStr = string.Empty;

            string prefixStr;
            string returnStr;
            if (MediaType == CommonEnumerations.MediaType.AUDIO)
            {
                prefixStr = $"[deepskyblue3]Aud[/] {encStr}";
                string d = $"{(Bandwidth != null ? (Bandwidth / 1000) + " Kbps" : "")} | {Name} | {Language} | {(Channels != null ? Channels + "CH" : "")} | {Role}";
                returnStr = d.EscapeMarkup();
            }
            else if (MediaType == CommonEnumerations.MediaType.SUBTITLES)
            {
                prefixStr = $"[deepskyblue3_1]Sub[/] {encStr}";
                string d = $"{Language} | {Name} | {Codecs} | {Role}";
                returnStr = d.EscapeMarkup();
            }
            else
            {
                prefixStr = $"[aqua]Vid[/] {encStr}";
                string d = $"{Resolution} | {Bandwidth / 1000} Kbps | {FrameRate} | {VideoRange} | {Role}";
                returnStr = d.EscapeMarkup();
            }

            returnStr = prefixStr + returnStr.Trim().Trim('|').Trim();
            while (returnStr.Contains("|  |"))
            {
                returnStr = returnStr.Replace("|  |", "|");
            }

            return returnStr.TrimEnd().TrimEnd('|').TrimEnd();
        }

        public override string ToString()
        {
            string prefixStr = "";
            string returnStr = "";
            string encStr = string.Empty;
            string segmentsCountStr = SegmentsCount == 0 ? "" : (SegmentsCount > 1 ? $"{SegmentsCount} Segments" : $"{SegmentsCount} Segment");

            // Add encryption flag
            if (Playlist != null && Playlist.MediaParts.Any(m => m.MediaSegments.Any(s => s.EncryptInfo.Method != EncryptMethod.NONE)))
            {
                IEnumerable<EncryptMethod> ms = Playlist.MediaParts.SelectMany(m => m.MediaSegments.Select(s => s.EncryptInfo.Method)).Where(e => e != EncryptMethod.NONE).Distinct();
                encStr = $"[red]*{string.Join(",", ms).EscapeMarkup()}[/] ";
            }

            if (MediaType == CommonEnumerations.MediaType.AUDIO)
            {
                prefixStr = $"[deepskyblue3]Aud[/] {encStr}";
                string d = $"{GroupId} | {(Bandwidth != null ? (Bandwidth / 1000) + " Kbps" : "")} | {Name} | {Codecs} | {Language} | {(Channels != null ? Channels + "CH" : "")} | {segmentsCountStr} | {Role}";
                returnStr = d.EscapeMarkup();
            }
            else if (MediaType == CommonEnumerations.MediaType.SUBTITLES)
            {
                prefixStr = $"[deepskyblue3_1]Sub[/] {encStr}";
                string d = $"{GroupId} | {Language} | {Name} | {Codecs} | {Characteristics} | {segmentsCountStr} | {Role}";
                returnStr = d.EscapeMarkup();
            }
            else
            {
                prefixStr = $"[aqua]Vid[/] {encStr}";
                string d = $"{Resolution} | {Bandwidth / 1000} Kbps | {GroupId} | {FrameRate} | {Codecs} | {VideoRange} | {segmentsCountStr} | {Role}";
                returnStr = d.EscapeMarkup();
            }

            returnStr = prefixStr + returnStr.Trim().Trim('|').Trim();
            while (returnStr.Contains("|  |"))
            {
                returnStr = returnStr.Replace("|  |", "|");
            }

            // Calculate duration and estimated size
            if (Playlist != null)
            {
                double total = Playlist.TotalDuration;
                returnStr += " | ~" + GlobalUtil.FormatTime((int)total);

                // Add estimated size calculation
                long estimatedSize = EstimateDownloadSize();
                if (estimatedSize > 0)
                {
                    returnStr += " | ~" + GlobalUtil.FormatFileSize(estimatedSize);
                }
            }

            return returnStr.TrimEnd().TrimEnd('|').TrimEnd();
        }
    }
}