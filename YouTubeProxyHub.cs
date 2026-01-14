using Microsoft.AspNetCore.SignalR;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace YouTubeProxyHub
{
    public class YouTubeProxyHub : Hub
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        // ВАЖНО: Мы будем имитировать мобильный Android-браузер
        private const string UserAgent = "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Mobile Safari/537.36";

        static YouTubeProxyHub()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }

        public async Task RequestStreamUrl(string videoId)
        {
            string cookiePath = Path.Combine(Path.GetTempPath(), $"cookies_{Guid.NewGuid()}.txt");
            string cookiesEnv = Environment.GetEnvironmentVariable("YOUTUBE_COOKIES");

            if (!string.IsNullOrEmpty(cookiesEnv))
            {
                await File.WriteAllTextAsync(cookiePath, cookiesEnv);
            }

            Console.WriteLine($"[LOG] Проверка видео через Android-клиент: {videoId}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    // Используем клиент 'android', он лучше всего работает на серверных IP
                    Arguments = $"--cookies \"{cookiePath}\" " +
                                $"--user-agent \"{UserAgent}\" " +
                                $"--extractor-args \"youtube:player-client=android\" " +
                                $"--no-check-certificate --no-warnings " +
                                $"-g -f 18 \"https://www.youtube.com/watch?v={videoId}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                string url = (await outputTask).Trim();
                string error = await errorTask;

                if (!string.IsNullOrEmpty(url) && url.StartsWith("http"))
                {
                    Console.WriteLine("[LOG] ✅ ПОБЕДА: Ссылка получена через Android-API.");
                    await Clients.Caller.SendAsync("ReceiveStreamUrl", url);
                }
                else
                {
                    Console.WriteLine($"[LOG] ❌ YouTube всё еще блокирует: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] ❌ Ошибка процесса: {ex.Message}");
            }
            finally
            {
                if (File.Exists(cookiePath)) File.Delete(cookiePath);
            }
        }

        public async Task RequestChunk(string url, long start, long end, string requestId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);
                request.Headers.Add("User-Agent", UserAgent);

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var bytes = await response.Content.ReadAsByteArrayAsync();

                string base64Data = Convert.ToBase64String(bytes);
                long total = response.Content.Headers.ContentRange?.Length ?? (start + bytes.Length);
                string rangeInfo = $"bytes {start}-{start + bytes.Length - 1}/{total}";

                await Clients.Caller.SendAsync("ReceiveChunk", base64Data, rangeInfo, requestId);
            }
            catch (Exception ex) { Console.WriteLine($"[LOG] Ошибка чанка: {ex.Message}"); }
        }
    }
}