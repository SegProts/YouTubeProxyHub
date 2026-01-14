using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace YouTubeProxyHub
{
    public class YouTubeProxyHub : Hub
    {
        public async Task SendChunk(string chunkData)
        {
            // Пересылка данных всем остальным подключенным клиентам
            await Clients.Others.SendAsync("ReceiveChunk", chunkData);
        }
    }
}