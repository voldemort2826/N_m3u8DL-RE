using System.Security.Cryptography;

using N_m3u8DL_RE.Common.CommonEnumerations;
using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Config;
using N_m3u8DL_RE.Crypto;
using N_m3u8DL_RE.Entity;
using N_m3u8DL_RE.Util;

using Spectre.Console;

namespace N_m3u8DL_RE.Downloader
{
    /// <summary>
    /// Simple downloader
    /// </summary>
    internal sealed class SimpleDownloader(DownloaderConfig config) : IDownloader
    {
        private readonly DownloaderConfig DownloaderConfig = config;

        public async Task<DownloadResult?> DownloadSegmentAsync(MediaSegment segment, string savePath, SpeedContainer speedContainer, Dictionary<string, string>? headers = null)
        {
            string url = segment.Url;
            (string des, DownloadResult? dResult) = await DownClipAsync(url, savePath, speedContainer, segment.StartRange, segment.StopRange, headers, DownloaderConfig.MyOptions.DownloadRetryCount);

            if (dResult is { Success: true } && dResult.ActualFilePath != des)
            {
                switch (segment.EncryptInfo.Method)
                {
                    case EncryptMethod.AES128:
                        {
                            byte[]? key = segment.EncryptInfo.Key;
                            byte[]? iv = segment.EncryptInfo.IV;
                            AESUtil.AES128Decrypt(dResult.ActualFilePath, key!, iv!);
                            break;
                        }
                    case EncryptMethod.AES128ECB:
                        {
                            byte[]? key = segment.EncryptInfo.Key;
                            byte[]? iv = segment.EncryptInfo.IV;
                            AESUtil.AES128Decrypt(dResult.ActualFilePath, key!, iv!, CipherMode.ECB);
                            break;
                        }
                    case EncryptMethod.CHACHA20:
                        {
                            byte[]? key = segment.EncryptInfo.Key;
                            byte[]? nonce = segment.EncryptInfo.IV;

                            byte[] fileBytes = File.ReadAllBytes(dResult.ActualFilePath);
                            byte[] decrypted = ChaCha20Util.DecryptInChunks(fileBytes, key!, nonce!);
                            await File.WriteAllBytesAsync(dResult.ActualFilePath, decrypted);
                            break;
                        }
                    case EncryptMethod.SAMPLEAESCTR:
                        // throw new NotSupportedException("SAMPLE-AES-CTR");
                        break;
                    case EncryptMethod.NONE:
                        break;
                    case EncryptMethod.SAMPLEAES:
                        break;
                    case EncryptMethod.CENC:
                        break;
                    case EncryptMethod.UNKNOWN:
                        break;
                    default:
                        break;
                }

                // Image header processing
                if (dResult.ImageHeader)
                {
                    await ImageHeaderUtil.ProcessAsync(dResult.ActualFilePath);
                }
                // Gzip decompression
                if (dResult.GzipHeader)
                {
                    await OtherUtil.DeGzipFileAsync(dResult.ActualFilePath);
                }

                // Rename after processing
                File.Move(dResult.ActualFilePath, des);
                dResult.ActualFilePath = des;
            }
            return dResult;
        }

        private static async Task<(string des, DownloadResult? dResult)> DownClipAsync(string url, string path, SpeedContainer speedContainer, long? fromPosition, long? toPosition, Dictionary<string, string>? headers = null, int retryCount = 3)
        {
            CancellationTokenSource? cancellationTokenSource = null;
        retry:
            try
            {
                cancellationTokenSource = new();
                string des = Path.ChangeExtension(path, null);

                // Skip if downloaded
                if (File.Exists(des))
                {
                    _ = speedContainer.Add(new FileInfo(des).Length);
                    return (des, new DownloadResult() { ActualContentLength = 0, ActualFilePath = des });
                }

                // Skip if decrypted
                string dec = Path.Combine(Path.GetDirectoryName(des)!, Path.GetFileNameWithoutExtension(des) + "_dec" + Path.GetExtension(des));
                if (File.Exists(dec))
                {
                    _ = speedContainer.Add(new FileInfo(dec).Length);
                    return (dec, new DownloadResult() { ActualContentLength = 0, ActualFilePath = dec });
                }

                // Start a new thread to monitor
                CancellationTokenSource cts = cancellationTokenSource;
                using Task<Task> watcher = Task.Factory.StartNew(async () =>
                {
                    while (true)
                    {
                        if (cts.IsCancellationRequested)
                        {
                            break;
                        }

                        if (speedContainer.ShouldStop)
                        {
                            cts.Cancel();
                            Logger.DebugMarkUp("Cancel...");
                            break;
                        }
                        await Task.Delay(500);
                    }
                });

                // Call download
                DownloadResult result = await DownloadUtil.DownloadToFileAsync(url, path, speedContainer, cancellationTokenSource, headers, fromPosition, toPosition);
                return (des, result);

                // throw new Exception("please retry"); // Dead code
            }
            catch (Exception ex)
            {
                Logger.DebugMarkUp($"[grey]{ex.Message.EscapeMarkup()} retryCount: {retryCount}[/]");
                Logger.Debug(url + " " + ex);
                Logger.Extra($"Ah oh!{Environment.NewLine}RetryCount => {retryCount}{Environment.NewLine}Exception  => {ex.Message}{Environment.NewLine}Url        => {url}");
                if (retryCount-- > 0)
                {
                    await Task.Delay(1000);
                    goto retry;
                }
                else
                {
                    Logger.Extra($"The retry attempts have been exhausted and the download of this segment has failed.{Environment.NewLine}Exception  => {ex.Message}{Environment.NewLine}Url        => {url}");
                    Logger.WarnMarkUp($"[grey]{ex.Message.EscapeMarkup()}[/]");
                }
                // throw new Exception("download failed", ex);
                return default;
            }
            finally
            {
                if (cancellationTokenSource != null)
                {
                    // Destroy after calling
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }
            }
        }
    }
}
