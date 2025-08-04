using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;

namespace N_m3u8DL_RE.Util
{
    internal static class SubtitleUtil
    {
        /// <summary>
        /// Write image subtitle PNG files
        /// </summary>
        /// <param name="finalVtt"></param>
        /// <param name="tmpDir">Temporary directory</param>
        /// <returns></returns>
        public static async Task TryWriteImagePngsAsync(WebVttSub? finalVtt, string tmpDir)
        {
            if (finalVtt != null && finalVtt.Cues.Any(v => v.Payload.StartsWith("Base64::", StringComparison.OrdinalIgnoreCase)))
            {
                Logger.WarnMarkUp(ResString.ProcessImageSub);
                int i = 0;
                foreach (SubCue? img in finalVtt.Cues.Where(v => v.Payload.StartsWith("Base64::", StringComparison.OrdinalIgnoreCase)))
                {
                    string name = $"{i++}.png";
                    string dest = "";
                    for (; File.Exists(dest = Path.Combine(tmpDir, name)); name = $"{i++}.png")
                    {
                        ;
                    }

                    string base64 = img.Payload[8..];
                    await File.WriteAllBytesAsync(dest, Convert.FromBase64String(base64));
                    img.Payload = name;
                }
            }
        }
    }
}