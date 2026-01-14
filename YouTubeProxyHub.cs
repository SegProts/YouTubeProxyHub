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
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
        }

        // 1. Поиск ссылки прямо на сервере (Render)
        public async Task RequestStreamUrl(string videoId)
        {
            Console.WriteLine($"[Render] Поиск видео: {videoId}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"-g --no-playlist --no-check-certificate -f \"best[ext=mp4]/best\" https://www.youtube.com/watch?v={videoId}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                string url = (await process.StandardOutput.ReadToEndAsync()).Trim();

                await Clients.Caller.SendAsync("ReceiveStreamUrl", url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка yt-dlp: {ex.Message}");
            }
        }

        // 2. Скачивание чанка прямо на сервере (Render)
        public async Task RequestChunk(string url, long startByte, long endByte, string requestId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startByte, endByte);

                var response = await _httpClient.SendAsync(request);
                var bytes = await response.Content.ReadAsByteArrayAsync();

                long totalSize = response.Content.Headers.ContentRange?.Length ?? (startByte + bytes.Length);
                string base64Data = Convert.ToBase64String(bytes);
                string rangeInfo = $"bytes {startByte}-{startByte + bytes.Length - 1}/{totalSize}";

                await Clients.Caller.SendAsync("ReceiveChunk", base64Data, rangeInfo, requestId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка скачивания: {ex.Message}");
            }
        }
    }
}