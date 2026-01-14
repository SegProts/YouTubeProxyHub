using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace YouTubeProxyHub
{
    public class YouTubeProxyHub : Hub
    {
        public async Task RequestStreamUrl(string videoId)
        {
            await Clients.Others.SendAsync("GetStreamUrl", Context.ConnectionId, videoId);
        }

        public async Task SendStreamUrlToClient(string clientId, string url)
        {
            await Clients.Client(clientId).SendAsync("ReceiveStreamUrl", url);
        }

        public async Task RequestChunk(string url, long startByte, long endByte)
        {
            await Clients.Others.SendAsync("FetchChunk", Context.ConnectionId, url, startByte, endByte);
        }

        // Добавили параметр totalSize
        public async Task DeliverChunk(string clientId, string base64Data, string rangeInfo, long totalSize)
        {
            await Clients.Client(clientId).SendAsync("ReceiveChunk", base64Data, rangeInfo, totalSize);
        }
    }
}