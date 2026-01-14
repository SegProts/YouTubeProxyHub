using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace YouTubeProxyHub
{
    public class YouTubeProxyHub : Hub
    {
        // 1. Клиент (браузер) просит найти прямую ссылку на видео
        public async Task RequestStreamUrl(string videoId)
        {
            await Clients.Others.SendAsync("GetStreamUrl", Context.ConnectionId, videoId);
        }

        // 2. Фетчер (ПК) возвращает найденную прямую ссылку хабу
        public async Task SendStreamUrlToClient(string clientId, string url)
        {
            await Clients.Client(clientId).SendAsync("ReceiveStreamUrl", url);
        }

        // 3. Клиент просит конкретный кусок байтов (на будущее для SW)
        public async Task RequestChunk(string url, long startByte, long endByte)
        {
            await Clients.Others.SendAsync("FetchChunk", Context.ConnectionId, url, startByte, endByte);
        }

        // 4. Фетчер передает байты в Base64 обратно клиенту
        public async Task DeliverChunk(string clientId, string base64Data)
        {
            await Clients.Client(clientId).SendAsync("ReceiveChunk", base64Data);
        }
    }
}