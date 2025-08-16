using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

using N_m3u8DL_RE.Common.CommonEnumerations;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;
using N_m3u8DL_RE.Common.Util;
using N_m3u8DL_RE.Entity;
using N_m3u8DL_RE.Enumerations;
using N_m3u8DL_RE.Util;

namespace N_m3u8DL_RE.CommandLine
{
    internal static partial class CommandInvoker
    {
#if BUILD_DATE
        public const string VERSION_INFO = $"N_m3u8DL-RE (Beta) build" + BUILD_DATE;
#else
        // Fallback for when BUILD_DATE is not defined (local dev builds)
        public static readonly string VERSION_INFO = $"N_m3u8DL-RE (Beta) build {DateTime.UtcNow.AddHours(8):yyyyMMdd}";
#endif

        [GeneratedRegex("((best|worst)\\d*|all)")]
        private static partial Regex ForStrRegex();
        [GeneratedRegex(@"(\d*)-(\d*)")]
        private static partial Regex RangeRegex();
        [GeneratedRegex(@"([\d\\.]+)(M|K)")]
        private static partial Regex SpeedStrRegex();

        private static readonly Argument<string> Input = new("input") { Description = ResString.CmdInput };
        private static readonly Option<string?> TmpDir = new("--tmp-dir") { Description = ResString.CmdTmpDir };
        private static readonly Option<string?> SaveDir = new("--save-dir") { Description = ResString.CmdSaveDir };
        private static readonly Option<string?> SaveName = new("--save-name") { Description = ResString.CmdSaveName, CustomParser = ParseSaveName };
        private static readonly Option<string?> SavePattern = new("--save-pattern") { Description = ResString.CmdSavePattern, DefaultValueFactory = (_) => "<SaveName>_<Id>_<Codecs>_<Language>_<Ext>" };
        private static readonly Option<string?> LogFilePath = new("--log-file-path") { Description = ResString.CmdLogFilePath, CustomParser = ParseFilePath };
        private static readonly Option<string?> UILanguage = new Option<string?>(name: "--ui-language") { Description = ResString.CmdUiLanguage }.AcceptOnlyFromAmong("en-US", "zh-CN", "zh-TW");
        private static readonly Option<string?> UrlProcessorArgs = new("--urlprocessor-args") { Description = ResString.CmdUrlProcessorArgs };
        private static readonly Option<string[]?> Keys = new("--key") { Description = ResString.CmdKeys, Arity = ArgumentArity.OneOrMore, AllowMultipleArgumentsPerToken = false };
        private static readonly Option<string> KeyTextFile = new("--key-text-file") { Description = ResString.CmdKeyText };
        private static readonly Option<Dictionary<string, string>> Headers = new("-H", "--header") { Description = ResString.CmdHeader, CustomParser = ParseHeaders, Arity = ArgumentArity.OneOrMore, AllowMultipleArgumentsPerToken = false };
        private static readonly Option<LogLevel> LogLevel = new("--log-level") { Description = ResString.CmdLogLevel, DefaultValueFactory = (_) => Common.Log.LogLevel.INFO };
        private static readonly Option<SubtitleFormat> SubtitleFormat = new("--sub-format") { Description = ResString.CmdSubFormat, DefaultValueFactory = (_) => Enumerations.SubtitleFormat.SRT };
        private static readonly Option<bool> DisableUpdateCheck = new("--disable-update-check") { Description = ResString.CmdDisableUpdateCheck, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> AutoSelect = new("--auto-select") { Description = ResString.CmdAutoSelect, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> SubOnly = new("--sub-only") { Description = ResString.CmdSubOnly, DefaultValueFactory = (_) => false };
        private static readonly Option<int> ThreadCount = new("--thread-count") { Description = ResString.CmdThreadCount, DefaultValueFactory = (_) => Environment.ProcessorCount };
        private static readonly Option<int> DownloadRetryCount = new("--download-retry-count") { Description = ResString.CmdDownloadRetryCount, DefaultValueFactory = (_) => 3 };
        private static readonly Option<double> HttpRequestTimeout = new("--http-request-timeout") { Description = ResString.CmdHttpRequestTimeout, DefaultValueFactory = (_) => 100 };
        private static readonly Option<bool> SkipMerge = new("--skip-merge") { Description = ResString.CmdSkipMerge, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> SkipDownload = new("--skip-download") { Description = ResString.CmdSkipDownload, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> NoDateInfo = new("--no-date-info") { Description = ResString.CmdNoDateInfo, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> BinaryMerge = new("--binary-merge") { Description = ResString.CmdBinaryMerge, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> UseFFmpegConcatDemuxer = new("--use-ffmpeg-concat-demuxer") { Description = ResString.CmdUseFFmpegConcatDemuxer, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> DelAfterDone = new("--del-after-done") { Description = ResString.CmdDelAfterDone, DefaultValueFactory = (_) => true };
        private static readonly Option<bool> AutoSubtitleFix = new("--auto-subtitle-fix") { Description = ResString.CmdSubtitleFix, DefaultValueFactory = (_) => true };
        private static readonly Option<bool> CheckSegmentsCount = new("--check-segments-count") { Description = ResString.CmdCheckSegmentsCount, DefaultValueFactory = (_) => true };
        private static readonly Option<bool> WriteMetaJson = new("--write-meta-json") { Description = ResString.CmdWriteMetaJson, DefaultValueFactory = (_) => true };
        private static readonly Option<bool> AppendUrlParams = new("--append-url-params") { Description = ResString.CmdAppendUrlParams, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> MP4RealTimeDecryption = new("--mp4-real-time-decryption") { Description = ResString.CmdMP4RealTimeDecryption, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> UseShakaPackager = new("--use-shaka-packager") { Description = ResString.CmdUseShakaPackager, DefaultValueFactory = (_) => false, Hidden = true };
        private static readonly Option<DecryptEngine> DecryptionEngine = new("--decryption-engine") { Description = ResString.CmdDecryptionEngine, DefaultValueFactory = (_) => DecryptEngine.FFMPEG };
        private static readonly Option<bool> ForceAnsiConsole = new("--force-ansi-console") { Description = ResString.CmdForceAnsiConsole };
        private static readonly Option<bool> NoAnsiColor = new("--no-ansi-color") { Description = ResString.CmdNoAnsiColor };
        private static readonly Option<string?> DecryptionBinaryPath = new("--decryption-binary-path") { Description = ResString.CmdDecryptionBinaryPath, HelpName = "PATH" };
        private static readonly Option<string?> FFmpegBinaryPath = new("--ffmpeg-binary-path") { Description = ResString.CmdFfmpegBinaryPath, HelpName = "PATH" };
        private static readonly Option<string?> BaseUrl = new("--base-url") { Description = ResString.CmdBaseUrl };
        private static readonly Option<bool> ConcurrentDownload = new("-mt", "--concurrent-download") { Description = ResString.CmdConcurrentDownload, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> NoLog = new("--no-log") { Description = ResString.CmdNoLog, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> AllowHlsMultiExtMap = new("--allow-hls-multi-ext-map") { Description = ResString.CmdAllowHlsMultiExtMap, DefaultValueFactory = (_) => false };
        private static readonly Option<string[]?> AdKeywords = new("--ad-keyword") { Description = ResString.CmdAdKeyword, HelpName = "REG" };
        private static readonly Option<long?> MaxSpeed = new("-R", "--max-speed") { Description = ResString.CmdMaxSpeed, CustomParser = ParseSpeedLimit, HelpName = "SPEED" };


        // Proxy options
        private static readonly Option<bool> UseSystemProxy = new("--use-system-proxy") { Description = ResString.CmdUseSystemProxy, DefaultValueFactory = (_) => true };
        private static readonly Option<WebProxy?> CustomProxy = new("--custom-proxy") { Description = ResString.CmdCustomProxy, CustomParser = ParseProxy, HelpName = "URL" };

        // Only download part of the segment
        private static readonly Option<CustomRange?> CustomRange = new("--custom-range") { Description = ResString.CmdCustomRange, CustomParser = ParseCustomRange, HelpName = "RANGE" };


        // morehelp
        private static readonly Option<string?> MoreHelp = new("--morehelp") { Description = ResString.CmdMoreHelp, HelpName = "OPTION" };

        // Custom KEY etc.
        private static readonly Option<EncryptMethod?> CustomHLSMethod = new("--custom-hls-method") { Description = ResString.CmdCustomHLSMethod, HelpName = "METHOD" };
        private static readonly Option<byte[]?> CustomHLSKey = new("--custom-hls-key") { Description = ResString.CmdCustomHLSKey, CustomParser = ParseHLSCustomKey, HelpName = "FILE|HEX|BASE64" };
        private static readonly Option<byte[]?> CustomHLSIv = new("--custom-hls-iv") { Description = ResString.CmdCustomHLSIv, CustomParser = ParseHLSCustomKey, HelpName = "FILE|HEX|BASE64" };

        // Task start time
        private static readonly Option<DateTime?> TaskStartAt = new("--task-start-at") { Description = ResString.CmdTaskStartAt, CustomParser = ParseStartTime, HelpName = "yyyyMMddHHmmss" };


        // Live related
        private static readonly Option<bool> LivePerformAsVod = new("--live-perform-as-vod") { Description = ResString.CmdLivePerformAsVod, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> LiveRealTimeMerge = new("--live-real-time-merge") { Description = ResString.CmdLiveRealTimeMerge, DefaultValueFactory = (_) => false };
        private static readonly Option<bool> LiveKeepSegments = new("--live-keep-segments") { Description = ResString.CmdLiveKeepSegments, DefaultValueFactory = (_) => true };
        private static readonly Option<bool> LivePipeMux = new("--live-pipe-mux") { Description = ResString.CmdLivePipeMux, DefaultValueFactory = (_) => false };
        private static readonly Option<TimeSpan?> LiveRecordLimit = new("--live-record-limit") { Description = ResString.CmdLiveRecordLimit, CustomParser = ParseLiveLimit, HelpName = "HH:mm:ss" };
        private static readonly Option<int?> LiveWaitTime = new("--live-wait-time") { Description = ResString.CmdLiveWaitTime, HelpName = "SEC" };
        private static readonly Option<int> LiveTakeCount = new("--live-take-count") { Description = ResString.CmdLiveTakeCount, DefaultValueFactory = (_) => 16, HelpName = "NUM" };
        private static readonly Option<bool> LiveFixVttByAudio = new("--live-fix-vtt-by-audio") { Description = ResString.CmdLiveFixVttByAudio, DefaultValueFactory = (_) => false };


        // Complex command line
        private static readonly Option<MuxOptions?> MuxAfterDone = new("-M", "--mux-after-done") { Description = ResString.CmdMuxAfterDone, CustomParser = ParseMuxAfterDone, HelpName = "OPTIONS" };
        private static readonly Option<List<OutputFile>> MuxImports = new("--mux-import") { Description = ResString.CmdMuxImport, CustomParser = ParseImports, Arity = ArgumentArity.OneOrMore, AllowMultipleArgumentsPerToken = false, HelpName = "OPTIONS" };
        private static readonly Option<StreamFilter?> VideoFilter = new("-sv", "--select-video") { Description = ResString.CmdSelectVideo, CustomParser = ParseStreamFilter, HelpName = "OPTIONS" };
        private static readonly Option<StreamFilter?> AudioFilter = new("-sa", "--select-audio") { Description = ResString.CmdSelectAudio, CustomParser = ParseStreamFilter, HelpName = "OPTIONS" };
        private static readonly Option<StreamFilter?> SubtitleFilter = new("-ss", "--select-subtitle") { Description = ResString.CmdSelectSubtitle, CustomParser = ParseStreamFilter, HelpName = "OPTIONS" };

        private static readonly Option<StreamFilter?> DropVideoFilter = new("-dv", "--drop-video") { Description = ResString.CmdDropVideo, CustomParser = ParseStreamFilter, HelpName = "OPTIONS" };
        private static readonly Option<StreamFilter?> DropAudioFilter = new("-da", "--drop-audio") { Description = ResString.CmdDropAudio, CustomParser = ParseStreamFilter, HelpName = "OPTIONS" };
        private static readonly Option<StreamFilter?> DropSubtitleFilter = new("-ds", "--drop-subtitle") { Description = ResString.CmdDropSubtitle, CustomParser = ParseStreamFilter, HelpName = "OPTIONS" };

        /// <summary>
        /// Parse download speed limit
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static long? ParseSpeedLimit(ArgumentResult result)
        {
            string input = result.Tokens[0].Value.ToUpperInvariant();
            try
            {
                Regex reg = SpeedStrRegex();
                if (!reg.IsMatch(input))
                {
                    throw new ArgumentException($"Invalid speed limit format: {input}. " +
                        "Please use e.g. '10M' (megabytes/sec) or '500K' (kilobytes/sec).");
                }

                Match match = reg.Match(input);
                double number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (number <= 0)
                {
                    throw new ArgumentException("Speed limit must be greater than 0.");
                }

                string unit = match.Groups[2].Value;
                return unit == "M"
                    ? (long)(number * 1024 * 1024)
                    : (long)(number * 1024);
            }
            catch (Exception ex)
            {
                result.AddError($"Error parsing speed limit: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse user-defined download range
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static CustomRange? ParseCustomRange(ArgumentResult result)
        {
            string input = result.Tokens[0].Value;
            // Supported types: 0-100; 01:00:00-02:30:00; -300; 300-; 05:00-; -03:00;
            try
            {
                if (string.IsNullOrEmpty(input))
                {
                    return null;
                }

                string[] arr = input.Split('-');
                if (arr.Length != 2)
                {
                    throw new ArgumentException("Bad format!");
                }

                if (input.Contains(':'))
                {
                    return new CustomRange()
                    {
                        InputStr = input,
                        StartSec = arr[0] == "" ? 0 : OtherUtil.ParseDur(arr[0]).TotalSeconds,
                        EndSec = arr[1] == "" ? double.MaxValue : OtherUtil.ParseDur(arr[1]).TotalSeconds,
                    };
                }

                if (RangeRegex().IsMatch(input))
                {
                    string left = RangeRegex().Match(input).Groups[1].Value;
                    string right = RangeRegex().Match(input).Groups[2].Value;
                    return new CustomRange()
                    {
                        InputStr = input,
                        StartSegIndex = left == "" ? 0 : long.Parse(left, CultureInfo.InvariantCulture),
                        EndSegIndex = right == "" ? long.MaxValue : long.Parse(right, CultureInfo.InvariantCulture),
                    };
                }

                throw new ArgumentException("Bad format!");
            }
            catch (Exception ex)
            {
                result.AddError($"error in parse CustomRange: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse user agent
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static WebProxy? ParseProxy(ArgumentResult result)
        {
            string input = result.Tokens[0].Value;
            try
            {
                if (string.IsNullOrEmpty(input))
                {
                    return null;
                }

                Uri uri = new(input);
                WebProxy proxy = new(uri, true);
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    string[] infos = uri.UserInfo.Split(':');
                    proxy.Credentials = new NetworkCredential(infos.First(), infos.Last());
                }
                return proxy;
            }
            catch (Exception ex)
            {
                result.AddError($"error in parse proxy: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse custom KEY
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static byte[]? ParseHLSCustomKey(ArgumentResult result)
        {
            string input = result.Tokens[0].Value;
            try
            {
                return string.IsNullOrEmpty(input)
                    ? null
                    : File.Exists(input)
                    ? File.ReadAllBytes(input)
                    : HexUtil.TryParseHexString(input, out byte[]? bytes) ? bytes : Convert.FromBase64String(input);
            }
            catch (Exception)
            {
                result.AddError($"error in parse hls custom key: {input}");
                return null;
            }
        }

        /// <summary>
        /// Parse live recording duration limit
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static TimeSpan? ParseLiveLimit(ArgumentResult result)
        {
            string input = result.Tokens[0].Value;
            try
            {
                return OtherUtil.ParseDur(input);
            }
            catch (Exception)
            {
                result.AddError($"error in parse LiveRecordLimit: {input}");
                return null;
            }
        }

        /// <summary>
        /// Parse task start time
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static DateTime? ParseStartTime(ArgumentResult result)
        {
            string input = result.Tokens[0].Value;
            try
            {
                CultureInfo provider = CultureInfo.InvariantCulture;
                return DateTime.ParseExact(input, "yyyyMMddHHmmss", provider);
            }
            catch (Exception)
            {
                result.AddError($"error in parse TaskStartTime: {input}");
                return null;
            }
        }

        private static string? ParseSaveName(ArgumentResult result)
        {
            string input = result.Tokens[0].Value;
            string newName = OtherUtil.GetValidFileName(input);
            if (string.IsNullOrEmpty(newName))
            {
                result.AddError("Invalid save name!");
                return null;
            }
            return newName;
        }

        private static string? ParseFilePath(ArgumentResult result)
        {
            string input = result.Tokens[0].Value;
            string path;
            try
            {
                path = Path.GetFullPath(input);
            }
            catch (Exception e)
            {
                result.AddError($"Invalid log path!, Reason: {e.Message}");
                return null;
            }
            string? dir = Path.GetDirectoryName(path);
            string filename = Path.GetFileName(path);
            string newName = OtherUtil.GetValidFileName(filename);
            if (string.IsNullOrEmpty(newName))
            {
                result.AddError("Invalid log file name!");
                return null;
            }
            return Path.Combine(dir!, newName);
        }

        /// <summary>
        /// Stream filter
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static StreamFilter? ParseStreamFilter(ArgumentResult result)
        {
            StreamFilter streamFilter = new();
            string input = result.Tokens[0].Value;
            ComplexParamParser p = new(input);


            // Target range
            string forStr;
            if (input == ForStrRegex().Match(input).Value)
            {
                forStr = input;
            }
            else
            {
                forStr = p.GetValue("for") ?? "best";
                if (forStr != ForStrRegex().Match(forStr).Value)
                {
                    result.AddError($"for={forStr} not valid");
                    return null;
                }
            }
            streamFilter.For = forStr;

            string? id = p.GetValue("id");
            if (!string.IsNullOrEmpty(id))
            {
                streamFilter.GroupIdReg = new Regex(id);
            }

            string? lang = p.GetValue("lang");
            if (!string.IsNullOrEmpty(lang))
            {
                streamFilter.LanguageReg = new Regex(lang);
            }

            string? name = p.GetValue("name");
            if (!string.IsNullOrEmpty(name))
            {
                streamFilter.NameReg = new Regex(name);
            }

            string? codecs = p.GetValue("codecs");
            if (!string.IsNullOrEmpty(codecs))
            {
                streamFilter.CodecsReg = new Regex(codecs);
            }

            string? res = p.GetValue("res");
            if (!string.IsNullOrEmpty(res))
            {
                streamFilter.ResolutionReg = new Regex(res);
            }

            string? frame = p.GetValue("frame");
            if (!string.IsNullOrEmpty(frame))
            {
                streamFilter.FrameRateReg = new Regex(frame);
            }

            string? channel = p.GetValue("channel");
            if (!string.IsNullOrEmpty(channel))
            {
                streamFilter.ChannelsReg = new Regex(channel);
            }

            string? range = p.GetValue("range");
            if (!string.IsNullOrEmpty(range))
            {
                streamFilter.VideoRangeReg = new Regex(range);
            }

            string? url = p.GetValue("url");
            if (!string.IsNullOrEmpty(url))
            {
                streamFilter.UrlReg = new Regex(url);
            }

            string? segsMin = p.GetValue("segsMin");
            if (!string.IsNullOrEmpty(segsMin))
            {
                streamFilter.SegmentsMinCount = long.Parse(segsMin, CultureInfo.InvariantCulture);
            }

            string? segsMax = p.GetValue("segsMax");
            if (!string.IsNullOrEmpty(segsMax))
            {
                streamFilter.SegmentsMaxCount = long.Parse(segsMax, CultureInfo.InvariantCulture);
            }

            string? plistDurMin = p.GetValue("plistDurMin");
            if (!string.IsNullOrEmpty(plistDurMin))
            {
                streamFilter.PlaylistMinDur = OtherUtil.ParseSeconds(plistDurMin);
            }

            string? plistDurMax = p.GetValue("plistDurMax");
            if (!string.IsNullOrEmpty(plistDurMax))
            {
                streamFilter.PlaylistMaxDur = OtherUtil.ParseSeconds(plistDurMax);
            }

            string? bwMin = p.GetValue("bwMin");
            if (!string.IsNullOrEmpty(bwMin))
            {
                streamFilter.BandwidthMin = int.Parse(bwMin, CultureInfo.InvariantCulture) * 1000;
            }

            string? bwMax = p.GetValue("bwMax");
            if (!string.IsNullOrEmpty(bwMax))
            {
                streamFilter.BandwidthMax = int.Parse(bwMax, CultureInfo.InvariantCulture) * 1000;
            }

            string? role = p.GetValue("role");
            if (Enum.TryParse(role, true, out RoleType roleType))
            {
                streamFilter.Role = roleType;
            }

            return streamFilter;
        }

        /// <summary>
        /// Split header
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static Dictionary<string, string> ParseHeaders(ArgumentResult result)
        {
            string[] array = [.. result.Tokens.Select(t => t.Value)];
            return OtherUtil.SplitHeaderArrayToDic(array);
        }

        /// <summary>
        /// Parse external files imported by mux
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static List<OutputFile> ParseImports(ArgumentResult result)
        {
            List<OutputFile> imports = [];

            foreach (Token item in result.Tokens)
            {
                ComplexParamParser p = new(item.Value);
                string path = p.GetValue("path") ?? item.Value; // If not obtained, use the entire string as path
                string? lang = p.GetValue("lang");
                string? name = p.GetValue("name");
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    result.AddError("path empty or file not exists!");
                    return imports;
                }
                imports.Add(new OutputFile()
                {
                    Index = 999,
                    FilePath = path,
                    LangCode = lang,
                    Description = name
                });
            }

            return imports;
        }

        /// <summary>
        /// Parse mux options
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static MuxOptions? ParseMuxAfterDone(ArgumentResult result)
        {
            string v = result.Tokens[0].Value;
            ComplexParamParser p = new(v);
            // Mux format
            string format = p.GetValue("format") ?? v.Split(':')[0]; // If not obtained, use the string before : as format
            bool parseResult = Enum.TryParse(format.ToUpperInvariant(), out MuxFormat muxFormat);
            if (!parseResult)
            {
                result.AddError($"format={format} not valid");
                return null;
            }
            // Muxer
            string muxer = p.GetValue("muxer") ?? "ffmpeg";
            if (muxer is not "ffmpeg" and not "mkvmerge")
            {
                result.AddError($"muxer={muxer} not valid");
                return null;
            }
            // Muxer path
            string bin_path = p.GetValue("bin_path") ?? "auto";
            if (string.IsNullOrEmpty(bin_path))
            {
                result.AddError($"bin_path={bin_path} not valid");
                return null;
            }
            // Whether to delete
            string keep = p.GetValue("keep") ?? "false";
            if (keep is not "true" and not "false")
            {
                result.AddError($"keep={keep} not valid");
                return null;
            }
            // Whether to ignore subtitles
            string skipSub = p.GetValue("skip_sub") ?? "false";
            if (skipSub is not "true" and not "false")
            {
                result.AddError($"skip_sub={keep} not valid");
                return null;
            }
            // Conflict detection
            if (muxer == "mkvmerge" && format == "mp4")
            {
                result.AddError("mkvmerge can not do mp4");
                return null;
            }
            return new MuxOptions()
            {
                UseMkvmerge = muxer == "mkvmerge",
                MuxFormat = muxFormat,
                KeepFiles = keep == "true",
                SkipSubtitle = skipSub == "true",
                BinPath = bin_path == "auto" ? null : bin_path
            };
        }

        public static async Task<int> InvokeArgs(string[] args, Func<MyOption, Task> action)
        {
            List<string> argList = [.. args];
            int index = -1;
            if ((index = argList.IndexOf("--morehelp")) >= 0 && argList.Count > index + 1)
            {
                string option = argList[index + 1];
                string msg = option switch
                {
                    "mux-after-done" => ResString.CmdMuxAfterDoneHelp,
                    "mux-import" => ResString.CmdMuxImportHelp,
                    "select-video" => ResString.CmdSelectVideoHelp,
                    "select-audio" => ResString.CmdSelectAudioHelp,
                    "select-subtitle" => ResString.CmdSelectSubtitleHelp,
                    "custom-range" => ResString.CmdCustomRangeHelp,
                    _ => $"Option=\"{option}\" not found"
                };
                Console.WriteLine($"More Help:\r\n\r\n  --{option}\r\n\r\n" + msg);
                Environment.Exit(0);
            }

#pragma warning disable IDE0028 // Collection initialization can be simplified
            // This one cannot be simplified
            RootCommand rootCommand = new(VERSION_INFO)
#pragma warning restore IDE0028
            {
                Input, TmpDir, SaveDir, SaveName, LogFilePath, BaseUrl, ThreadCount, DownloadRetryCount, HttpRequestTimeout, ForceAnsiConsole, NoAnsiColor,AutoSelect, SkipMerge, SkipDownload, CheckSegmentsCount,
                BinaryMerge, UseFFmpegConcatDemuxer, DelAfterDone, NoDateInfo, NoLog, WriteMetaJson, AppendUrlParams, ConcurrentDownload, Headers, SubOnly, SubtitleFormat, AutoSubtitleFix,
                FFmpegBinaryPath,
                LogLevel, UILanguage, UrlProcessorArgs, Keys, KeyTextFile, DecryptionEngine, DecryptionBinaryPath, UseShakaPackager, MP4RealTimeDecryption,
                MaxSpeed,
                MuxAfterDone,
                CustomHLSMethod, CustomHLSKey, CustomHLSIv, UseSystemProxy, CustomProxy, CustomRange, TaskStartAt,
                LivePerformAsVod, LiveRealTimeMerge, LiveKeepSegments, LivePipeMux, LiveFixVttByAudio, LiveRecordLimit, LiveWaitTime, LiveTakeCount,
                MuxImports, VideoFilter, AudioFilter, SubtitleFilter, DropVideoFilter, DropAudioFilter, DropSubtitleFilter, AdKeywords, DisableUpdateCheck, AllowHlsMultiExtMap, MoreHelp
                // SavePattern
            };

            rootCommand.TreatUnmatchedTokensAsErrors = true;
            rootCommand.SetAction(async (parseResult, token) =>
            {
                MyOption myOption = new()
                {
                    Input = parseResult.GetValue(Input) ?? string.Empty,
                    ForceAnsiConsole = parseResult.GetValue(ForceAnsiConsole),
                    NoAnsiColor = parseResult.GetValue(NoAnsiColor),
                    LogLevel = parseResult.GetValue(LogLevel),
                    AutoSelect = parseResult.GetValue(AutoSelect),
                    DisableUpdateCheck = parseResult.GetValue(DisableUpdateCheck),
                    SkipMerge = parseResult.GetValue(SkipMerge),
                    BinaryMerge = parseResult.GetValue(BinaryMerge),
                    UseFFmpegConcatDemuxer = parseResult.GetValue(UseFFmpegConcatDemuxer),
                    DelAfterDone = parseResult.GetValue(DelAfterDone),
                    AutoSubtitleFix = parseResult.GetValue(AutoSubtitleFix),
                    CheckSegmentsCount = parseResult.GetValue(CheckSegmentsCount),
                    SubtitleFormat = parseResult.GetValue(SubtitleFormat),
                    SubOnly = parseResult.GetValue(SubOnly),
                    TmpDir = parseResult.GetValue(TmpDir),
                    SaveDir = parseResult.GetValue(SaveDir),
                    SaveName = parseResult.GetValue(SaveName),
                    LogFilePath = parseResult.GetValue(LogFilePath),
                    ThreadCount = parseResult.GetValue(ThreadCount),
                    UILanguage = parseResult.GetValue(UILanguage),
                    SkipDownload = parseResult.GetValue(SkipDownload),
                    WriteMetaJson = parseResult.GetValue(WriteMetaJson),
                    AppendUrlParams = parseResult.GetValue(AppendUrlParams),
                    SavePattern = parseResult.GetValue(SavePattern),
                    Keys = parseResult.GetValue(Keys),
                    UrlProcessorArgs = parseResult.GetValue(UrlProcessorArgs),
                    MP4RealTimeDecryption = parseResult.GetValue(MP4RealTimeDecryption),
                    UseShakaPackager = parseResult.GetValue(UseShakaPackager),
                    DecryptionEngine = parseResult.GetValue(DecryptionEngine),
                    DecryptionBinaryPath = parseResult.GetValue(DecryptionBinaryPath),
                    FFmpegBinaryPath = parseResult.GetValue(FFmpegBinaryPath),
                    KeyTextFile = parseResult.GetValue(KeyTextFile),
                    DownloadRetryCount = parseResult.GetValue(DownloadRetryCount),
                    HttpRequestTimeout = parseResult.GetValue(HttpRequestTimeout),
                    BaseUrl = parseResult.GetValue(BaseUrl),
                    MuxImports = parseResult.GetValue(MuxImports),
                    ConcurrentDownload = parseResult.GetValue(ConcurrentDownload),
                    VideoFilter = parseResult.GetValue(VideoFilter),
                    AudioFilter = parseResult.GetValue(AudioFilter),
                    SubtitleFilter = parseResult.GetValue(SubtitleFilter),
                    DropVideoFilter = parseResult.GetValue(DropVideoFilter),
                    DropAudioFilter = parseResult.GetValue(DropAudioFilter),
                    DropSubtitleFilter = parseResult.GetValue(DropSubtitleFilter),
                    LiveRealTimeMerge = parseResult.GetValue(LiveRealTimeMerge),
                    LiveKeepSegments = parseResult.GetValue(LiveKeepSegments),
                    LiveRecordLimit = parseResult.GetValue(LiveRecordLimit),
                    TaskStartAt = parseResult.GetValue(TaskStartAt),
                    LivePerformAsVod = parseResult.GetValue(LivePerformAsVod),
                    LivePipeMux = parseResult.GetValue(LivePipeMux),
                    LiveFixVttByAudio = parseResult.GetValue(LiveFixVttByAudio),
                    UseSystemProxy = parseResult.GetValue(UseSystemProxy),
                    CustomProxy = parseResult.GetValue(CustomProxy),
                    CustomRange = parseResult.GetValue(CustomRange),
                    LiveWaitTime = parseResult.GetValue(LiveWaitTime),
                    LiveTakeCount = parseResult.GetValue(LiveTakeCount),
                    NoDateInfo = parseResult.GetValue(NoDateInfo),
                    NoLog = parseResult.GetValue(NoLog),
                    AllowHlsMultiExtMap = parseResult.GetValue(AllowHlsMultiExtMap),
                    AdKeywords = parseResult.GetValue(AdKeywords),
                    MaxSpeed = parseResult.GetValue(MaxSpeed),
                };

                if (string.IsNullOrWhiteSpace(myOption.Input))
                {
                    Logger.Error("Error: input (URL or file) is required.");
                    return;
                }

                if (parseResult.GetValue(CustomHLSMethod) is not null)
                {
                    myOption.CustomHLSMethod = parseResult.GetValue(CustomHLSMethod);
                }

                if (parseResult.GetValue(CustomHLSKey) is not null)
                {
                    myOption.CustomHLSKey = parseResult.GetValue(CustomHLSKey);
                }

                if (parseResult.GetValue(CustomHLSIv) is not null)
                {
                    myOption.CustomHLSIv = parseResult.GetValue(CustomHLSIv);
                }

                Dictionary<string, string>? parsedHeaders = parseResult.GetValue(Headers);
                if (parsedHeaders != null)
                {
                    myOption.Headers = parsedHeaders;
                }


                // Priority is given to user-selected language
                if (myOption.UILanguage != null)
                {
                    CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(myOption.UILanguage);
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(myOption.UILanguage);
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(myOption.UILanguage);
                }

                // Mux settings
                MuxOptions? muxAfterDoneValue = parseResult.GetValue(MuxAfterDone);
                if (muxAfterDoneValue != null)
                {
                    myOption.MuxAfterDone = true;
                    myOption.MuxOptions = muxAfterDoneValue;
                    if (!muxAfterDoneValue.UseMkvmerge)
                    {
                        myOption.MkvmergeBinaryPath = muxAfterDoneValue.BinPath;
                    }
                    else
                    {
                        myOption.FFmpegBinaryPath ??= muxAfterDoneValue.BinPath;
                    }
                }
                else
                {
                    myOption.MuxAfterDone = false;
                }

                await action(myOption);
            });

            ParserConfiguration config = new()
            {
                EnablePosixBundling = false,
            };

            try
            {
                ParseResult parseResult = rootCommand.Parse(args, config);
                return await parseResult.InvokeAsync();
            }
            catch (Exception ex)
            {
                try { Console.CursorVisible = true; } catch { }
                string msg = Logger.LogLevel == Common.Log.LogLevel.DEBUG ? ex.ToString() : ex.Message;
#if DEBUG
                msg = ex.ToString();
#endif
                Logger.Error(msg);
                Thread.Sleep(3000);
                Environment.Exit(1);
                return 1;
            }
        }
    }
}
