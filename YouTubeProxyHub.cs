using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace YouTubeProxyHub
{
    public class YouTubeProxyHub : Hub
    {
        // Клиент (браузер) вызывает этот метод, чтобы попросить Фетчер скачать видео
        public async Task RequestChunk(string url, long startByte, long endByte)
        {
            await Clients.Others.SendAsync("FetchChunk", Context.ConnectionId, url, startByte, endByte);
        }

        // Фетчер вызывает этот метод, чтобы передать скачанные байты обратно конкретному клиенту
        public async Task DeliverChunk(string clientId, string base64Data)
        {
            await Clients.Client(clientId).SendAsync("ReceiveChunk", base64Data);
        }
    }
}