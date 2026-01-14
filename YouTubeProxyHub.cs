using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace YouTubeProxyHub
{
    public class YouTubeProxyHub : Hub
    {
        // 1. Клиент просит найти прямую ссылку на видео
        public async Task RequestStreamUrl(string videoId)
        {
            await Clients.Others.SendAsync("GetStreamUrl", Context.ConnectionId, videoId);
        }

        // 2. Фетчер возвращает найденную прямую ссылку
        public async Task SendStreamUrlToClient(string clientId, string url)
        {
            await Clients.Client(clientId).SendAsync("ReceiveStreamUrl", url);
        }

        // 3. Клиент (через SW) просит конкретный диапазон байтов
        public async Task RequestChunk(string url, long startByte, long endByte)
        {
            await Clients.Others.SendAsync("FetchChunk", Context.ConnectionId, url, startByte, endByte);
        }

        // 4. Фетчер передает скачанные байты в Base64 обратно клиенту
        public async Task DeliverChunk(string clientId, string base64Data, string rangeInfo)
        {
            await Clients.Client(clientId).SendAsync("ReceiveChunk", base64Data, rangeInfo);
        }
    }
}
