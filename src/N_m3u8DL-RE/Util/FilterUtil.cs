using System.Text.RegularExpressions;

using N_m3u8DL_RE.Common.CommonEnumerations;
using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;
using N_m3u8DL_RE.Common.Util;
using N_m3u8DL_RE.Entity;

using Spectre.Console;

namespace N_m3u8DL_RE.Util
{
    public static class FilterUtil
    {
        public static List<StreamSpec> DoFilterKeep(IEnumerable<StreamSpec> lists, StreamFilter? filter)
        {
            if (filter == null)
            {
                return [];
            }

            IEnumerable<StreamSpec> inputs = lists.Where(_ => true);
            if (filter.GroupIdReg != null)
            {
                inputs = inputs.Where(i => i.GroupId != null && filter.GroupIdReg.IsMatch(i.GroupId));
            }

            if (filter.LanguageReg != null)
            {
                inputs = inputs.Where(i => i.Language != null && filter.LanguageReg.IsMatch(i.Language));
            }

            if (filter.NameReg != null)
            {
                inputs = inputs.Where(i => i.Name != null && filter.NameReg.IsMatch(i.Name));
            }

            if (filter.CodecsReg != null)
            {
                inputs = inputs.Where(i => i.Codecs != null && filter.CodecsReg.IsMatch(i.Codecs));
            }

            if (filter.ResolutionReg != null)
            {
                inputs = inputs.Where(i => i.Resolution != null && filter.ResolutionReg.IsMatch(i.Resolution));
            }

            if (filter.FrameRateReg != null)
            {
                inputs = inputs.Where(i => i.FrameRate != null && filter.FrameRateReg.IsMatch($"{i.FrameRate}"));
            }

            if (filter.ChannelsReg != null)
            {
                inputs = inputs.Where(i => i.Channels != null && filter.ChannelsReg.IsMatch(i.Channels));
            }

            if (filter.VideoRangeReg != null)
            {
                inputs = inputs.Where(i => i.VideoRange != null && filter.VideoRangeReg.IsMatch(i.VideoRange));
            }

            if (filter.UrlReg != null)
            {
                inputs = inputs.Where(i => i.Url != null && filter.UrlReg.IsMatch(i.Url));
            }

            if (filter.SegmentsMaxCount != null && inputs.All(i => i.SegmentsCount > 0))
            {
                inputs = inputs.Where(i => i.SegmentsCount < filter.SegmentsMaxCount);
            }

            if (filter.SegmentsMinCount != null && inputs.All(i => i.SegmentsCount > 0))
            {
                inputs = inputs.Where(i => i.SegmentsCount > filter.SegmentsMinCount);
            }

            if (filter.PlaylistMinDur != null)
            {
                inputs = inputs.Where(i => i.Playlist?.TotalDuration > filter.PlaylistMinDur);
            }

            if (filter.PlaylistMaxDur != null)
            {
                inputs = inputs.Where(i => i.Playlist?.TotalDuration < filter.PlaylistMaxDur);
            }

            if (filter.BandwidthMin != null)
            {
                inputs = inputs.Where(i => i.Bandwidth >= filter.BandwidthMin);
            }

            if (filter.BandwidthMax != null)
            {
                inputs = inputs.Where(i => i.Bandwidth <= filter.BandwidthMax);
            }

            if (filter.Role.HasValue)
            {
                inputs = inputs.Where(i => i.Role == filter.Role);
            }

            string bestNumberStr = filter.For.Replace("best", "");
            string worstNumberStr = filter.For.Replace("worst", "");

            if (filter.For == "best" && inputs.Any())
            {
                inputs = [.. inputs.Take(1)];
            }
            else if (filter.For == "worst" && inputs.Any())
            {
                inputs = [.. inputs.TakeLast(1)];
            }
            else if (int.TryParse(bestNumberStr, out int bestNumber) && inputs.Any())
            {
                inputs = [.. inputs.Take(bestNumber)];
            }
            else if (int.TryParse(worstNumberStr, out int worstNumber) && inputs.Any())
            {
                inputs = [.. inputs.TakeLast(worstNumber)];
            }

            return [.. inputs];
        }

        public static List<StreamSpec> DoFilterDrop(IEnumerable<StreamSpec> lists, StreamFilter? filter)
        {
            if (filter == null)
            {
                return [.. lists];
            }

            IEnumerable<StreamSpec> inputs = lists.Where(_ => true);
            List<StreamSpec> selected = DoFilterKeep(lists, filter);

            inputs = inputs.Where(i => selected.All(s => s.ToString() != i.ToString()));

            return [.. inputs];
        }

        public static List<StreamSpec> SelectStreams(IEnumerable<StreamSpec> lists)
        {
            List<StreamSpec> streamSpecs = [.. lists];
            if (streamSpecs.Count == 1)
            {
                return [.. streamSpecs];
            }

            // Basic streams
            List<StreamSpec> basicStreams = [.. streamSpecs.Where(x => x.MediaType == null)];
            // Optional audio tracks
            List<StreamSpec> audios = [.. streamSpecs.Where(x => x.MediaType == MediaType.AUDIO)];
            // Optional subtitle tracks
            List<StreamSpec> subs = [.. streamSpecs.Where(x => x.MediaType == MediaType.SUBTITLES)];

            MultiSelectionPrompt<StreamSpec> prompt = new MultiSelectionPrompt<StreamSpec>()
                    .Title(ResString.PromptTitle)
                    .UseConverter(x =>
                    {
                        return x.Name != null && x.Name.StartsWith("__", StringComparison.OrdinalIgnoreCase) ? $"[darkslategray1]{x.Name[2..]}[/]" : x.ToString().EscapeMarkup().RemoveMarkup();
                    })
                    .Required()
                    .PageSize(10)
                    .MoreChoicesText(ResString.PromptChoiceText)
                    .InstructionsText(ResString.PromptInfo)
                ;

            // Default selection
            StreamSpec first = streamSpecs.First();
            _ = prompt.Select(first);

            if (basicStreams.Count != 0)
            {
                _ = prompt.AddChoiceGroup(new StreamSpec() { Name = "__Basic" }, basicStreams);
            }

            if (audios.Count != 0)
            {
                _ = prompt.AddChoiceGroup(new StreamSpec() { Name = "__Audio" }, audios);
                // Default audio track
                if (first.AudioId != null)
                {
                    _ = prompt.Select(audios.First(a => a.GroupId == first.AudioId));
                }
            }
            if (subs.Count != 0)
            {
                _ = prompt.AddChoiceGroup(new StreamSpec() { Name = "__Subtitle" }, subs);
                // Default subtitle track
                if (first.SubtitleId != null)
                {
                    _ = prompt.Select(subs.First(s => s.GroupId == first.SubtitleId));
                }
            }

            // If no stream is selected at this time, select one automatically
            _ = prompt.Select(basicStreams.Concat(audios).Concat(subs).First());

            // Multiple selection
            List<StreamSpec> selectedStreams = CustomAnsiConsole.Console.Prompt(prompt);

            // Display total download size
            DisplayTotalDownloadSize(selectedStreams);

            return selectedStreams;
        }

        /// <summary>
        /// Used for live streaming. Align the start of each track.
        /// </summary>
        /// <param name="selectedSteams"></param>
        /// <param name="takeLastCount"></param>
        public static void SyncStreams(List<StreamSpec> selectedSteams, int takeLastCount = 15)
        {
            // Synchronize by Date
            if (selectedSteams.All(x => x.Playlist!.MediaParts[0].MediaSegments.All(x => x.DateTime != null)))
            {
                DateTime? minDate = selectedSteams.Max(s =>
                {
                    static DateTime? selector(MediaSegment s)
                    {
                        return s.DateTime;
                    }

                    return s.Playlist!.MediaParts[0].MediaSegments.Min(selector);
                })!;
                foreach (StreamSpec item in selectedSteams)
                {
                    foreach (MediaPart part in item.Playlist!.MediaParts)
                    {
                        // Second-level synchronization, ignore milliseconds
                        part.MediaSegments = [.. part.MediaSegments.Where(s => s.DateTime!.Value.Ticks / TimeSpan.TicksPerSecond >= minDate.Value.Ticks / TimeSpan.TicksPerSecond)];
                    }
                }
            }
            else // Synchronize by index
            {
                long minIndex = selectedSteams.Max(s =>
                {
                    static long selector(MediaSegment s)
                    {
                        return s.Index;
                    }
                    return s.Playlist!.MediaParts[0].MediaSegments.Min(selector);
                });
                foreach (StreamSpec item in selectedSteams)
                {
                    foreach (MediaPart part in item.Playlist!.MediaParts)
                    {
                        part.MediaSegments = [.. part.MediaSegments.Where(s => s.Index >= minIndex)];
                    }
                }
            }

            // Take the latest N segments
            if (selectedSteams.Any(x => x.Playlist!.MediaParts[0].MediaSegments.Count > takeLastCount))
            {
                int skipCount = selectedSteams.Min(x => x.Playlist!.MediaParts[0].MediaSegments.Count) - takeLastCount + 1;
                if (skipCount < 0)
                {
                    skipCount = 0;
                }

                foreach (StreamSpec item in selectedSteams)
                {
                    foreach (MediaPart part in item.Playlist!.MediaParts)
                    {
                        part.MediaSegments = [.. part.MediaSegments.Skip(skipCount)];
                    }
                }
            }
        }

        /// <summary>
        /// Apply user-defined segment range
        /// </summary>
        /// <param name="selectedSteams"></param>
        /// <param name="customRange"></param>
        public static void ApplyCustomRange(List<StreamSpec> selectedSteams, CustomRange? customRange)
        {
            if (customRange == null)
            {
                return;
            }

            Logger.InfoMarkUp($"{ResString.CustomRangeFound}[Cyan underline]{customRange.InputStr}[/]");
            Logger.WarnMarkUp($"[darkorange3_1]{ResString.CustomRangeWarn}[/]");

            bool filterByIndex = customRange is { StartSegIndex: not null, EndSegIndex: not null };
            bool filterByTime = customRange is { StartSec: not null, EndSec: not null };

            if (!filterByIndex && !filterByTime)
            {
                Logger.ErrorMarkUp(ResString.CustomRangeInvalid);
                return;
            }

            foreach (StreamSpec stream in selectedSteams)
            {
                double skippedDur = 0d;
                if (stream.Playlist == null)
                {
                    continue;
                }

                foreach (MediaPart part in stream.Playlist.MediaParts)
                {
                    List<MediaSegment> newSegments = filterByIndex
                        ? [.. part.MediaSegments.Where(seg => seg.Index >= customRange.StartSegIndex && seg.Index <= customRange.EndSegIndex)]
                        : [.. part.MediaSegments.Where(seg => stream.Playlist.MediaParts.SelectMany(p => p.MediaSegments).Where(x => x.Index < seg.Index).Sum(x => x.Duration) >= customRange.StartSec
                                                                      && stream.Playlist.MediaParts.SelectMany(p => p.MediaSegments).Where(x => x.Index < seg.Index).Sum(x => x.Duration) <= customRange.EndSec)];
                    if (newSegments.Count > 0)
                    {
                        skippedDur += part.MediaSegments.Where(seg => seg.Index < newSegments.First().Index).Sum(x => x.Duration);
                    }

                    part.MediaSegments = newSegments;
                }
                stream.SkippedDuration = skippedDur;
            }
        }

        /// <summary>
        /// Clear ad segments based on user input
        /// </summary>
        /// <param name="selectedSteams"></param>
        /// <param name="keywords"></param>
        public static void CleanAd(List<StreamSpec> selectedSteams, string[]? keywords)
        {
            if (keywords == null)
            {
                return;
            }

            List<Regex> regList = [.. keywords.Select(s => new Regex(s))];
            foreach (Regex? reg in regList)
            {
                Logger.InfoMarkUp($"{ResString.CustomAdKeywordsFound}[Cyan underline]{reg}[/]");
            }

            foreach (StreamSpec stream in selectedSteams)
            {
                if (stream.Playlist == null)
                {
                    continue;
                }

                int countBefore = stream.SegmentsCount;

                foreach (MediaPart part in stream.Playlist.MediaParts)
                {
                    // No ad segment found
                    if (part.MediaSegments.All(x => regList.All(reg => !reg.IsMatch(x.Url))))
                    {
                        continue;
                    }
                    // Found ad segment, clean
                    part.MediaSegments = [.. part.MediaSegments.Where(x => regList.All(reg => !reg.IsMatch(x.Url)))];
                }

                // Clean up empty parts
                stream.Playlist.MediaParts = [.. stream.Playlist.MediaParts.Where(x => x.MediaSegments.Count > 0)];

                int countAfter = stream.SegmentsCount;

                if (countBefore != countAfter)
                {
                    Logger.WarnMarkUp("[grey]{} segments => {} segments[/]", countBefore, countAfter);
                }
            }
        }

        /// <summary>
        /// Displays total download size for selected streams
        /// </summary>
        /// <param name="selectedStreams">Selected streams</param>
        public static void DisplayTotalDownloadSize(List<StreamSpec> selectedStreams)
        {
            if (selectedStreams.Count == 0)
            {
                return;
            }

            // Calculate total size and group by accuracy
            long totalSize = 0;
            Dictionary<string, (long size, int count)> accuracyGroups = [];

            foreach (StreamSpec stream in selectedStreams)
            {
                long streamSize = stream.EstimateDownloadSize();
                totalSize += streamSize;

                if (streamSize > 0)
                {
                    string accuracy = stream.EstimationAccuracy?.ToString() ?? "Unknown";
                    if (accuracyGroups.TryGetValue(accuracy, out (long size, int count) value))
                    {
                        accuracyGroups[accuracy] = (value.size + streamSize, value.count + 1);
                    }
                    else
                    {
                        accuracyGroups[accuracy] = (streamSize, 1);
                    }
                }
            }

            if (totalSize > 0)
            {
                Logger.InfoMarkUp($"[bold cyan]Total estimated download size: {GlobalUtil.FormatFileSize(totalSize)}[/]");

                // Show accuracy breakdown if there are multiple types
                if (accuracyGroups.Count > 1)
                {
                    Logger.InfoMarkUp("[grey]Size estimation breakdown:[/]");
                    foreach ((string accuracy, (long size, int count)) in accuracyGroups.OrderByDescending(x => x.Value.size))
                    {
                        string percentage = totalSize > 0 ? $" ({(double)size / totalSize:P1})" : "";
                        Logger.InfoMarkUp($"[grey]  {accuracy}: {GlobalUtil.FormatFileSize(size)} from {count} stream(s){percentage}[/]");
                    }
                }
            }
            else
            {
                Logger.InfoMarkUp("[yellow]Total download size: Unable to estimate[/]");
            }
        }
    }
}