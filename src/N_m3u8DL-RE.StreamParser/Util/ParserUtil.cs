using System.Globalization;
using System.Text.RegularExpressions;

using N_m3u8DL_RE.StreamParser.Constants;

namespace N_m3u8DL_RE.StreamParser.Util
{
    public static partial class ParserUtil
    {
        [GeneratedRegex(@"\$Number%([^$]+)d\$")]
        private static partial Regex VarsNumberRegex();

        /// <summary>
        /// Get parameters from the following text
        /// #EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=2149280,CODECS="mp4a.40.2,avc1.64001f",RESOLUTION=1280x720,NAME="720"
        /// </summary>
        /// <param name="line">The line of text to be parsed</param>
        /// <param name="key">If empty, get all characters after the first English colon</param>
        /// <returns></returns>
        public static string GetAttribute(string line, string key = "")
        {
            line = line.Trim();
            if (key == "")
            {
                return line[(line.IndexOf(':') + 1)..];
            }

            string result = string.Empty;

            int index;
            if ((index = line.IndexOf(key + "=\"", StringComparison.Ordinal)) > -1)
            {
                int startIndex = index + (key + "=\"").Length;
                int endIndex = startIndex + line[startIndex..].IndexOf('\"');
                result = line[startIndex..endIndex];
            }
            else if ((index = line.IndexOf(key + "=", StringComparison.Ordinal)) > -1)
            {
                int startIndex = index + (key + "=").Length;
                int endIndex = startIndex + line[startIndex..].IndexOf(',');
                result = endIndex >= startIndex ? line[startIndex..endIndex] : line[startIndex..];
            }

            return result;
        }

        /// <summary>
        /// Extract from the following text
        /// <n>[@<o>]
        /// </summary>
        /// <param name="input"></param>
        /// <returns>n(length) o(start)</returns>
        public static (long, long?) GetRange(string input)
        {
            string[] t = input.Split('@');
            return t.Length switch
            {
                <= 0 => (0, null),
                1 => (Convert.ToInt64(t[0], CultureInfo.InvariantCulture), null),
                2 => (Convert.ToInt64(t[0], CultureInfo.InvariantCulture), Convert.ToInt64(t[1], CultureInfo.InvariantCulture)),
                _ => (0, null)
            };
        }

        /// <summary>
        /// Get StartRange, ExpectLength information from a string like 100-300
        /// </summary>
        /// <param name="range"></param>
        /// <returns>StartRange, ExpectLength</returns>
        public static (long, long) ParseRange(string range)
        {
            long start = Convert.ToInt64(range.Split('-')[0], CultureInfo.InvariantCulture);
            long end = Convert.ToInt64(range.Split('-')[1], CultureInfo.InvariantCulture);
            return (start, end - start + 1);
        }

        /// <summary>
        /// MPD SegmentTemplate replacement
        /// </summary>
        /// <param name="text"></param>
        /// <param name="keyValuePairs"></param>
        /// <returns></returns>
        public static string ReplaceVars(string text, Dictionary<string, object?> keyValuePairs)
        {
            foreach (KeyValuePair<string, object?> item in keyValuePairs)
            {
                if (text.Contains(item.Key))
                {
                    text = text.Replace(item.Key, item.Value!.ToString());
                }
            }

            // Process special form numbers, such as $Number%05d$
            Regex regex = VarsNumberRegex();
            if (regex.IsMatch(text) && keyValuePairs.TryGetValue(DASHTags.TemplateNumber, out object? keyValuePair))
            {
                foreach (Match m in regex.Matches(text))
                {
                    text = text.Replace(m.Value, keyValuePair?.ToString()?.PadLeft(Convert.ToInt32(m.Groups[1].Value, CultureInfo.InvariantCulture), '0'));
                }
            }

            return text;
        }

        /// <summary>
        /// Concatenate Baseurl and RelativeUrl
        /// </summary>
        /// <param name="baseurl">Baseurl</param>
        /// <param name="url">RelativeUrl</param>
        /// <returns></returns>
        public static string CombineURL(string baseurl, string url)
        {
            if (string.IsNullOrEmpty(baseurl))
            {
                return url;
            }

            Uri uri1 = new(baseurl);  // Here you can directly pass the complete URL
            Uri uri2 = new(uri1, url);
            url = uri2.ToString();

            return url;
        }
    }
}