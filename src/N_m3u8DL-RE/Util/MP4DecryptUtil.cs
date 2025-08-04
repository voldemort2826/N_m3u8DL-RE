using System.Diagnostics;
using System.Text.RegularExpressions;

using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;
using N_m3u8DL_RE.Enumerations;
using N_m3u8DL_RE.StreamParser.Mp4;

namespace N_m3u8DL_RE.Util
{
    internal static partial class MP4DecryptUtil
    {
        private static readonly string ZeroKid = "00000000000000000000000000000000";

        /// <summary>
        /// Generate a random 16-character alphanumeric string (a-z0-9, no spaces)
        /// Thread-safe and collision-resistant for rapid successive calls
        /// </summary>
        private static string GenerateRandomFileName()
        {
            return Guid.NewGuid().ToString("N")[..16];
        }

        public static async Task<bool> DecryptAsync(DecryptEngine decryptEngine, string bin, string[]? keys, string source, string dest, string? kid, string init = "", bool isMultiDRM = false)
        {
            if (keys == null || keys.Length == 0)
            {
                return false;
            }

            List<string> keyPairs = [.. keys];
            string? keyPair = null;
            string? trackId = null;
            string? tmpSrcFile = null;
            string? tmpDestFile = null;
            string? tmpFile = null; // For merged init+source file

            if (isMultiDRM)
            {
                trackId = "1";
            }

            if (!string.IsNullOrEmpty(kid))
            {
                List<string> test = [.. keyPairs.Where(k => k.StartsWith(kid, StringComparison.OrdinalIgnoreCase))];
                if (test.Count != 0)
                {
                    keyPair = test.First();
                }
            }

            // Apple
            if (kid == ZeroKid)
            {
                keyPair = keyPairs.First();
                trackId = "1";
            }

            // user only input key, append kid
            if (keyPair == null && keyPairs.Count == 1 && !keyPairs.First().Contains(':'))
            {
                keyPairs = [.. keyPairs.Select(x => $"{kid}:{x}")];
                keyPair = keyPairs.First();
            }

            if (keyPair == null)
            {
                return false;
            }

            // shakaPackager/ffmpeg cannot decrypt init files individually
            if (source.EndsWith("_init.mp4", StringComparison.OrdinalIgnoreCase) && decryptEngine != DecryptEngine.MP4DECRYPT)
            {
                return false;
            }

            // Generate safe temporary filenames
            string sourceDir = Path.GetDirectoryName(source) ?? "";
            string destDir = Path.GetDirectoryName(dest) ?? "";
            string sourceExt = Path.GetExtension(source);
            string destExt = Path.GetExtension(dest);

            tmpSrcFile = Path.Combine(sourceDir, GenerateRandomFileName() + sourceExt);
            tmpDestFile = Path.Combine(destDir, GenerateRandomFileName() + destExt);

            string cmd;

            try
            {
                // Step 1: Prepare source file with safe name
                if (init != "")
                {
                    // Merge init+source first
                    tmpFile = Path.ChangeExtension(source, ".itmp");
                    MergeUtil.CombineMultipleFilesIntoSingleFile([init, source], tmpFile);
                    File.Copy(tmpFile, tmpSrcFile);
                }
                else
                {
                    File.Copy(source, tmpSrcFile);
                }

                // Step 2: Build command with safe filenames
                if (decryptEngine == DecryptEngine.SHAKA_PACKAGER)
                {
                    cmd = $"--quiet --enable_raw_key_decryption input=\"{tmpSrcFile}\",stream=0,output=\"{tmpDestFile}\" " +
                          $"--keys {(trackId != null ? $"label={trackId}:" : "")}key_id={(trackId != null ? ZeroKid : kid)}:key={keyPair.Split(':')[1]}";
                }
                else if (decryptEngine == DecryptEngine.MP4DECRYPT)
                {
                    cmd = trackId == null
                        ? string.Join(" ", keyPairs.Select(k => $"--key {k}"))
                        : string.Join(" ", keyPairs.Select(k => $"--key {trackId}:{k.Split(':')[1]}"));

                    string workDir = Path.GetDirectoryName(tmpSrcFile)!;
                    if (init != "")
                    {
                        string infoFile = Path.GetDirectoryName(init) == workDir ? Path.GetFileName(init) : init;
                        cmd += $" --fragments-info \"{infoFile}\" ";
                    }
                    cmd += $" \"{Path.GetFileName(tmpSrcFile)}\" \"{Path.GetFileName(tmpDestFile)}\"";

                    // Set working directory for mp4decrypt
                    Process? process = Process.Start(new ProcessStartInfo()
                    {
                        FileName = bin,
                        Arguments = cmd,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WorkingDirectory = workDir
                    });
                    await process!.WaitForExitAsync();
                    bool isSuccess = process.ExitCode == 0;

                    // Handle the result
                    if (isSuccess && File.Exists(tmpDestFile))
                    {
                        File.Move(tmpDestFile, dest);
                        return true;
                    }
                    else
                    {
                        Logger.Error(ResString.DecryptionFailed);
                        return false;
                    }
                }
                else // FFMPEG
                {
                    cmd = $"-loglevel error -nostdin -decryption_key {keyPair.Split(':')[1]} -i \"{tmpSrcFile}\" -c copy \"{tmpDestFile}\"";
                }

                // Run command (for Shaka and FFmpeg)
                if (decryptEngine != DecryptEngine.MP4DECRYPT)
                {
                    bool isSuccess = await RunCommandAsync(bin, cmd);

                    // Handle the result
                    if (isSuccess && File.Exists(tmpDestFile))
                    {
                        File.Move(tmpDestFile, dest);
                        return true;
                    }
                    else
                    {
                        Logger.Error(ResString.DecryptionFailed);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorMarkUp($"Decryption failed: {ex.Message}");
                return false;
            }
            finally
            {
                // Cleanup temporary files
                try
                {
                    if (tmpSrcFile != null && File.Exists(tmpSrcFile))
                    {
                        File.Delete(tmpSrcFile);
                    }

                    if (tmpDestFile != null && File.Exists(tmpDestFile))
                    {
                        File.Delete(tmpDestFile);
                    }

                    if (tmpFile != null && File.Exists(tmpFile))
                    {
                        File.Delete(tmpFile);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WarnMarkUp($"Failed to cleanup temp files: {ex.Message}");
                }
            }

            return false;
        }

        private static async Task<bool> RunCommandAsync(string name, string arg, string? workDir = null)
        {
            Logger.DebugMarkUp($"FileName: {name}");
            Logger.DebugMarkUp($"Arguments: {arg}");
            Process? process = Process.Start(new ProcessStartInfo()
            {
                FileName = name,
                Arguments = arg,
                // RedirectStandardOutput = true,
                // RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = workDir
            });
            await process!.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        /// <summary>
        /// Search for the KEY of the KID in the text file
        /// </summary>
        /// <param name="file">Text file</param>
        /// <param name="kid">Target KID</param>
        /// <returns></returns>
        public static async Task<string?> SearchKeyFromFileAsync(string? file, string? kid)
        {
            try
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file) || string.IsNullOrEmpty(kid))
                {
                    return null;
                }

                Logger.InfoMarkUp(ResString.SearchKey);
                using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                using StreamReader reader = new(stream);
                while (await reader.ReadLineAsync() is { } line)
                {
                    if (!line.Trim().StartsWith(kid, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Logger.InfoMarkUp($"[green]OK[/] [grey]{line.Trim()}[/]");
                    return line.Trim();
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorMarkUp(ex.Message);
            }
            return null;
        }

        public static ParsedMP4Info GetMP4Info(byte[] data)
        {
            ParsedMP4Info info = MP4InitUtil.ReadInit(data);
            if (info.Scheme != null)
            {
                Logger.WarnMarkUp($"[grey]Type: {info.Scheme}[/]");
            }

            if (info.PSSH != null)
            {
                Logger.WarnMarkUp($"[grey]PSSH(WV): {info.PSSH}[/]");
            }

            if (info.KID != null)
            {
                Logger.WarnMarkUp($"[grey]KID: {info.KID}[/]");
            }

            return info;
        }

        public static ParsedMP4Info GetMP4Info(string output)
        {
            using FileStream fs = File.OpenRead(output);
            byte[] header = new byte[1 * 1024 * 1024]; // 1MB
            _ = fs.Read(header);
            return GetMP4Info(header);
        }

        public static string? ReadInitShaka(string output, string bin)
        {
            Regex shakaKeyIdRegex = KidOutputRegex();

            // TODO: handle the case that shaka packager actually decrypted (key ID == ZeroKid)
            //       - stop process
            //       - remove {output}.tmp.webm
            string cmd = $"--quiet --enable_raw_key_decryption input=\"{output}\",stream=0,output=\"{output}.tmp.webm\" " +
                      $"--keys key_id={ZeroKid}:key={ZeroKid}";

            using Process p = new();
            p.StartInfo = new ProcessStartInfo()
            {
                FileName = bin,
                Arguments = cmd,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            _ = p.Start();
            string errorOutput = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return shakaKeyIdRegex.Match(errorOutput).Groups[1].Value;
        }

        [GeneratedRegex("Key for key_id=([0-9a-f]+) was not found")]
        private static partial Regex KidOutputRegex();
    }
}
