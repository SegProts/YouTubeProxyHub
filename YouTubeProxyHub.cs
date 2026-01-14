using Microsoft.AspNetCore.SignalR;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace YouTubeProxyHub
{
    public class YouTubeProxyHub : Hub
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        static YouTubeProxyHub()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        }

        public async Task RequestStreamUrl(string videoId)
        {
            Console.WriteLine($"[LOG] Запрос ссылки для ID: {videoId}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    // Выбираем формат 18 (mp4, 360p) - он самый стабильный для проксирования
                    Arguments = $"-g --no-playlist --format 18 https://www.youtube.com/watch?v={videoId}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                string url = (await process.StandardOutput.ReadToEndAsync()).Trim();
                string error = await process.StandardError.ReadToEndAsync();

                if (!string.IsNullOrEmpty(url) && url.StartsWith("http"))
                {
                    Console.WriteLine($"[LOG] Ссылка найдена: {url.Substring(0, 30)}...");
                    await Clients.Caller.SendAsync("ReceiveStreamUrl", url);
                }
                else
                {
                    Console.WriteLine($"[LOG] Ошибка yt-dlp: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] Ошибка Hub: {ex.Message}");
            }
        }

        public async Task RequestChunk(string url, long startByte, long endByte, string requestId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startByte, endByte);

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var bytes = await response.Content.ReadAsByteArrayAsync();

                long totalSize = response.Content.Headers.ContentRange?.Length ?? (startByte + bytes.Length);
                string base64Data = Convert.ToBase64String(bytes);
                string rangeInfo = $"bytes {startByte}-{startByte + bytes.Length - 1}/{totalSize}";

                await Clients.Caller.SendAsync("ReceiveChunk", base64Data, rangeInfo, requestId);
                // Console.WriteLine($"[LOG] Чанк отправлен: {rangeInfo}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] Ошибка чанка: {ex.Message}");
            }
        }
    }
}