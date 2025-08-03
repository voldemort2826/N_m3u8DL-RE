using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using N_m3u8DL_RE.Common.CommonEnumerations;
using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.JsonConverter;

namespace N_m3u8DL_RE.Common.Util
{
    public static class GlobalUtil
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = {
                new JsonStringEnumConverter<MediaType>(),
                new JsonStringEnumConverter<EncryptMethod>(),
                new JsonStringEnumConverter<ExtractorType>(),
                new BytesBase64Converter()
            }
        };
        private static readonly JsonContext Context = new(Options);

        public static string ConvertToJson<T>(T obj) where T : class
        {
            return obj switch
            {
                StreamSpec s => JsonSerializer.Serialize(s, Context.StreamSpec),
                IOrderedEnumerable<StreamSpec> ss => JsonSerializer.Serialize(ss, Context.IOrderedEnumerableStreamSpec),
                List<StreamSpec> sList => JsonSerializer.Serialize(sList, Context.ListStreamSpec),
                IEnumerable<MediaSegment> mList => JsonSerializer.Serialize(mList, Context.IEnumerableMediaSegment),
                _ => throw new NotSupportedException($"Type {typeof(T).Name} is not supported for JSON serialization")
            };
        }

        // backward compatibility
        public static string ConvertToJson(object o)
        {
            return ConvertToJson((dynamic)o);
        }

        public static string FormatFileSize(double fileSize)
        {
            return fileSize switch
            {
                < 0 => throw new ArgumentOutOfRangeException(nameof(fileSize)),
                >= 1024 * 1024 * 1024 => $"{fileSize / (1024 * 1024 * 1024):########0.00}GB",
                >= 1024 * 1024 => $"{fileSize / (1024 * 1024):####0.00}MB",
                >= 1024 => $"{fileSize / 1024:####0.00}KB",
                _ => $"{fileSize:####0.00}B"
            };
        }

        // 此函数用于格式化输出时长  
        public static string FormatTime(int time)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(time);
            TimeSpan ts = TimeSpan.FromSeconds(time);
            return ts.Hours > 0
                ? $"{ts.Hours:00}h{ts.Minutes:00}m{ts.Seconds:00}s"
                : $"{ts.Minutes:00}m{ts.Seconds:00}s";
        }

        /// <summary>
        /// 寻找可执行程序
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string? FindExecutable(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            string fileExt = OperatingSystem.IsWindows() ? ".exe" : "";
            string executableName = name + fileExt;

            List<string?> searchPaths =
            [
                // Add current directory
                Environment.CurrentDirectory,
            ];

            // Add process directory (with null check)
            string? processDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (processDir != null)
            {
                searchPaths.Add(processDir);
            }

            // Add PATH environment variable paths
            string[] envPath = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
            searchPaths.AddRange(envPath);

            return searchPaths
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => Path.Combine(p!, executableName))
                .FirstOrDefault(File.Exists);
        }
    }
}