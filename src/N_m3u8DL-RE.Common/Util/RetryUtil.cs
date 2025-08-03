using System.Net;
using System.Net.Sockets;

using N_m3u8DL_RE.Common.Log;

using Spectre.Console;

namespace N_m3u8DL_RE.Common.Util
{
    public static class RetryUtil
    {
        public static async Task<T?> WebRequestRetryAsync<T>(Func<Task<T>> funcAsync, int maxRetries = 10, int retryDelayMilliseconds = 1500, int retryDelayIncrementMilliseconds = 0)
        {
            int retryCount = 0;
            T? result = default;
            Exception? currentException = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    result = await funcAsync();
                    break;
                }
                catch (Exception ex) when (IsRetriableException(ex))
                {
                    currentException = ex;
                    retryCount++;
                    Logger.WarnMarkUp($"[grey]{ex.Message.EscapeMarkup()} ({retryCount}/{maxRetries})[/]");
                    await Task.Delay(retryDelayMilliseconds + (retryDelayIncrementMilliseconds * (retryCount - 1)));
                }
            }

            return retryCount == maxRetries
                ? throw new InvalidOperationException($"Failed to execute action after {maxRetries} retries.", currentException)
                : result;
        }

        private static bool IsRetriableException(Exception ex)
        {
            return ex is WebException or IOException or HttpRequestException or
                TaskCanceledException or SocketException or TimeoutException;
        }
    }
}