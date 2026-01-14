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
        // Используем специфический User-Agent для TV-клиента
        private const string TvUserAgent = "Mozilla/5.0 (PlayStation; PlayStation 5/8.20) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.4 Safari/605.1.15";

        static YouTubeProxyHub()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", TvUserAgent);
        }

        public async Task RequestStreamUrl(string videoId)
        {
            Console.WriteLine($"[LOG] Stealth-запрос (TV Client) для ID: {videoId}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    // МЫ НЕ ИСПОЛЬЗУЕМ КУКИ (они палят смену IP)
                    // Используем связку клиентов tv_embedded и web_embedded
                    Arguments = $"--user-agent \"{TvUserAgent}\" " +
                                $"--extractor-args \"youtube:player-client=tv_embedded,web_embedded\" " +
                                $"--no-check-certificate " +
                                $"--no-warnings " +
                                $"-g -f \"best[ext=mp4]/best\" " +
                                $"\"https://www.youtube.com/watch?v={videoId}\"",
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
                    Console.WriteLine("[LOG] ✅ ПОБЕДА: Ссылка получена через Stealth-клиент.");
                    await Clients.Caller.SendAsync("ReceiveStreamUrl", url);
                }
                else
                {
                    Console.WriteLine($"[LOG] ❌ YouTube заблокировал даже TV-клиент: {error}");
                    // Если это не сработает, Render как хостинг для этого проекта бесполезен из-за IP-бана
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] ❌ Ошибка Hub: {ex.Message}");
            }
        }

        public async Task RequestChunk(string url, long start, long end, string requestId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);
                request.Headers.Add("User-Agent", TvUserAgent);

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var bytes = await response.Content.ReadAsByteArrayAsync();

                string base64Data = Convert.ToBase64String(bytes);
                long total = response.Content.Headers.ContentRange?.Length ?? (start + bytes.Length);
                string rangeInfo = $"bytes {start}-{start + bytes.Length - 1}/{total}";

                await Clients.Caller.SendAsync("ReceiveChunk", base64Data, rangeInfo, requestId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] Ошибка чанка: {ex.Message}");
            }
        }
    }
}