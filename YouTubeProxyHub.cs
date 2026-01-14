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
                await File.WriteAllTextAsync(cookiePath, cookiesEnv);

            Console.WriteLine($"[LOG] Проверка видео: {videoId}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--cookies \"{cookiePath}\" --user-agent \"{UserAgent}\" " +
                                $"--extractor-args \"youtube:player-client=web\" " +
                                $"-g --no-playlist --format 18 " +
                                $"https://www.youtube.com/watch?v={videoId}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                string url = (await process.StandardOutput.ReadToEndAsync()).Trim();
                string err = await process.StandardError.ReadToEndAsync();

                if (!string.IsNullOrEmpty(url) && url.StartsWith("http"))
                {
                    Console.WriteLine("[LOG] ✅ Ссылка успешно получена!");
                    await Clients.Caller.SendAsync("ReceiveStreamUrl", url);
                }
                else
                {
                    Console.WriteLine($"[LOG] ❌ Ошибка yt-dlp: {err}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"[LOG] ❌ Hub Error: {ex.Message}"); }
            finally { if (File.Exists(cookiePath)) File.Delete(cookiePath); }
        }

        public async Task RequestChunk(string url, long start, long end, string requestId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var bytes = await response.Content.ReadAsByteArrayAsync();

                string base64 = Convert.ToBase64String(bytes);
                long total = response.Content.Headers.ContentRange?.Length ?? (start + bytes.Length);
                string rangeHeader = $"bytes {start}-{start + bytes.Length - 1}/{total}";

                // Передаем requestId обратно, чтобы клиент знал, какому запросу этот чанк принадлежит
                await Clients.Caller.SendAsync("ReceiveChunk", base64, rangeHeader, requestId);
            }
            catch (Exception ex) { Console.WriteLine($"[LOG] ❌ Chunk Error: {ex.Message}"); }
        }
    }
}