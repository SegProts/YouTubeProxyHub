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

        static YouTubeProxyHub()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        }

        public async Task RequestStreamUrl(string videoId)
        {
            string cookiePath = Path.Combine(Path.GetTempPath(), "cookies.txt");
            string cookiesEnv = Environment.GetEnvironmentVariable("YOUTUBE_COOKIES");

            if (!string.IsNullOrEmpty(cookiesEnv))
            {
                await File.WriteAllTextAsync(cookiePath, cookiesEnv);
            }

            Console.WriteLine($"[LOG] Запрос ссылки для ID: {videoId}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    // Используем куки и принудительно эмулируем клиент iOS (он стабильнее)
                    Arguments = $"--cookies \"{cookiePath}\" --extractor-args \"youtube:player-client=ios\" -g --no-playlist --format 18 https://www.youtube.com/watch?v={videoId}",
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
                    Console.WriteLine("[LOG] УСПЕХ: Ссылка получена с использованием Cookies.");
                    await Clients.Caller.SendAsync("ReceiveStreamUrl", url);
                }
                else
                {
                    Console.WriteLine($"[LOG] ОШИБКА YouTube: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] Ошибка Hub: {ex.Message}");
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
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var bytes = await response.Content.ReadAsByteArrayAsync();

                string base64Data = Convert.ToBase64String(bytes);
                string rangeInfo = $"bytes {startByte}-{startByte + bytes.Length - 1}/{response.Content.Headers.ContentRange?.Length ?? (startByte + bytes.Length)}";
                await Clients.Caller.SendAsync("ReceiveChunk", base64Data, rangeInfo, requestId);
            }
            catch (Exception ex) { Console.WriteLine($"[LOG] Ошибка чанка: {ex.Message}"); }
        }
    }
}