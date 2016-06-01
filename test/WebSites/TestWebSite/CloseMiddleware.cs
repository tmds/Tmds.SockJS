using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Tmds.WebSockets;

namespace TestWebSite
{
    public class CloseMiddleware
    {
        private PathString _path;
        private RequestDelegate _next;

        public CloseMiddleware(RequestDelegate next, PathString path)
        {
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
            await socket.SendCloseAsync((WebSocketCloseStatus)3000, "Go away!");
        }
    }
}
