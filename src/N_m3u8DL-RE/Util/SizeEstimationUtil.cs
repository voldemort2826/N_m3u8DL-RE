using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Log;

namespace N_m3u8DL_RE.Util
{
    internal static class SizeEstimationUtil
    {
        /// <summary>
        /// Estimates download sizes for multiple streams concurrently
        /// </summary>
        public static async Task EstimateStreamSizesAsync(
            IEnumerable<StreamSpec> streams,
            Dictionary<string, string>? headers = null,
            int maxConcurrencyPerStream = 3,
            int timeoutSeconds = 10,
            double successThreshold = 0.7)
        {
            List<StreamSpec> streamList = [.. streams.Where(s => s.Playlist != null)];

            if (streamList.Count == 0)
            {
                return;
            }

            Logger.InfoMarkUp("[yellow]Estimating download sizes...[/]");

            IEnumerable<Task> tasks = streamList.Select(async stream =>
            {
                try
                {
                    _ = await stream.EstimateDownloadSizeAsync(headers, maxConcurrencyPerStream, timeoutSeconds, successThreshold);
                }
                catch (Exception ex)
                {
                    Logger.DebugMarkUp($"Failed to estimate size for stream {stream.GroupId}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);

            // Log accuracy summary
            Dictionary<string, int> accuracyCounts = streamList
                .GroupBy(s => s.EstimationAccuracy?.ToString() ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            Logger.InfoMarkUp("[green]Size estimation completed![/]");
            foreach ((string accuracyName, int count) in accuracyCounts)
            {
                Logger.InfoMarkUp($"  {accuracyName}: {count} streams");
            }
        }

        /// <summary>
        /// Estimates sizes using sampling for quick estimates
        /// </summary>
        public static async Task EstimateStreamSizesBySamplingAsync(
            IEnumerable<StreamSpec> streams,
            int sampleSize = 3,
            Dictionary<string, string>? headers = null,
            int timeoutSeconds = 10)
        {
            List<StreamSpec> streamList = [.. streams.Where(s => s.Playlist != null)];

            if (streamList.Count == 0)
            {
                return;
            }

            Logger.InfoMarkUp("[yellow]Sampling segments for size estimation...[/]");

            IEnumerable<Task> tasks = streamList.Select(async stream =>
            {
                try
                {
                    // Use sampleSize as maxSegmentsToCheck to limit sampling
                    _ = await stream.EstimateDownloadSizeAsync(
                        headers,
                        maxConcurrency: 3,
                        timeoutSeconds,
                        successThreshold: 0.5, // Lower threshold for sampling
                        maxSegmentsToCheck: sampleSize); // FIX: Use the sampleSize parameter
                }
                catch (Exception ex)
                {
                    Logger.DebugMarkUp($"Failed to sample stream {stream.GroupId}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);

            // Log accuracy summary
            Dictionary<string, int> accuracyCounts = streamList
                .GroupBy(s => s.EstimationAccuracy?.ToString() ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            Logger.InfoMarkUp("[green]Size sampling completed![/]");
            foreach ((string accuracyName, int count) in accuracyCounts)
            {
                Logger.InfoMarkUp($"{accuracyName}: {count} streams");
            }
        }
    }
}
