using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Tmds.WebSockets;

namespace TestWebSite
{
    public class TickerMiddleware
    {
        private PathString _path;
        private RequestDelegate _next;

        public TickerMiddleware(RequestDelegate next, PathString path)
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
            var closeTask = socket.ReceiveCloseAsync();
            while (!closeTask.IsCompleted)
            {
                await Task.Delay(1000);
                await socket.SendAsync("tick!");
            }
        }
    }
}
