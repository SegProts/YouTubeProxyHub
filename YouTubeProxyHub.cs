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
        // Используем User-Agent, максимально похожий на тот, под которым были получены куки
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";

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

            Console.WriteLine($"[LOG] Запрос ссылки через Cookies для: {videoId}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    // КРИТИЧНО: используем клиент 'web' (он поддерживает куки) и 'tv' (он дает стабильные ссылки)
                    Arguments = $"--cookies \"{cookiePath}\" " +
                                $"--user-agent \"{UserAgent}\" " +
                                $"--extractor-args \"youtube:player-client=web,tv\" " +
                                $"-g --no-playlist --format \"best[ext=mp4]/best\" " +
                                $"https://www.youtube.com/watch?v={videoId}",
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
                    Console.WriteLine("[LOG] ✅ УСПЕХ: Прямая ссылка получена.");
                    await Clients.Caller.SendAsync("ReceiveStreamUrl", url);
                }
                else
                {
                    Console.WriteLine($"[LOG] ❌ ОШИБКА YouTube: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] ❌ КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
            }
            finally
            {
                if (File.Exists(cookiePath)) File.Delete(cookiePath);
            }
        }

        public async Task RequestChunk(string url, long startByte, long endByte, string requestId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startByte, endByte);
                request.Headers.Add("User-Agent", UserAgent);

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var bytes = await response.Content.ReadAsByteArrayAsync();

                string base64Data = Convert.ToBase64String(bytes);
                long total = response.Content.Headers.ContentRange?.Length ?? (startByte + bytes.Length);
                string rangeInfo = $"bytes {startByte}-{startByte + bytes.Length - 1}/{total}";

                await Clients.Caller.SendAsync("ReceiveChunk", base64Data, rangeInfo, requestId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] Ошибка чанка: {ex.Message}");
            }
        }
    }
}
