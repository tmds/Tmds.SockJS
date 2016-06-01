using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Tmds.WebSockets;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace TestWebSite
{
    public class BroadcastMiddleware
    {
        private PathString _path;
        private RequestDelegate _next;
        private ConcurrentDictionary<WebSocket, byte> _clients;

        public BroadcastMiddleware(RequestDelegate next, PathString path)
        {
            _clients = new ConcurrentDictionary<WebSocket, byte>();
            _next = next;
            _path = path;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path != _path)
            {
                await _next(context);
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            _clients.TryAdd(socket, 0);
            try
            {
                while (true)
                {
                    var msg = await socket.ReceiveTextAsync();
                    if (msg == null)
                    {
                        break;
                    }
                    var webSockets = _clients.Select(_ => _.Key);
                    webSockets.SendAsync(msg);
                }
            }
            finally
            {
                byte b;
                _clients.TryRemove(socket, out b);
            }
        }
    }
}
