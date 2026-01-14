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
        private const string UserAgent = "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Mobile Safari/537.36";

        static YouTubeProxyHub()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }

        public async Task RequestStreamUrl(string videoId)
        {
            Console.WriteLine($"[LOG] Попытка через Android TestSuite: {videoId}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    // android_testsuite - самый мощный клиент для обхода 429 на серверах
                    Arguments = $"--no-check-certificate " +
                                $"--no-warnings " +
                                $"--extractor-args \"youtube:player-client=android_testsuite\" " +
                                $"-g -f 18 " +
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
                    Console.WriteLine("[LOG] ✅ УСПЕХ: Ссылка получена через TestSuite!");
                    await Clients.Caller.SendAsync("ReceiveStreamUrl", url);
                }
                else
                {
                    Console.WriteLine($"[LOG] ❌ Ошибка TestSuite: {error}");
                    // Если это не сработает, значит IP Render во Франкфурте забанен тотально.
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] ❌ Критическая ошибка: {ex.Message}");
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
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] Ошибка чанка: {ex.Message}");
            }
        }
    }
}