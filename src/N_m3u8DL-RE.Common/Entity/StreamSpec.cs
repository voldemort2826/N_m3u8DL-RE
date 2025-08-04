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

        public int SegmentsCount => Playlist != null ? Playlist.MediaParts.Sum(x => x.MediaSegments.Count) : 0;

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

            // Calculate duration
            if (Playlist != null)
            {
                double total = Playlist.TotalDuration;
                returnStr += " | ~" + GlobalUtil.FormatTime((int)total);
            }

            return returnStr.TrimEnd().TrimEnd('|').TrimEnd();
        }
    }
}