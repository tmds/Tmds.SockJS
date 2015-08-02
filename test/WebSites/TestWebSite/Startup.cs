using System;
using Microsoft.AspNet.Builder;
using Microsoft.Framework.DependencyInjection;
using Tmds.SockJS;
using System.Text;
using System.Threading;
using Tmds.WebSockets;
using System.Net.WebSockets;

namespace TestWebSite
{
    public class Startup
    {
        public static readonly TimeSpan CloseDisconnectTimeout = TimeSpan.FromSeconds(2);

        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseSockJS("/echo", new SockJSOptions() { MaxResponseLength = 4200 });
            app.UseSockJS("/disabled_websocket_echo", new SockJSOptions() { UseWebSocket = false });
            app.UseSockJS("/close", new SockJSOptions() { HeartbeatInterval = TimeSpan.FromSeconds(10), DisconnectTimeout = CloseDisconnectTimeout });

            app.Use(async (context, next) =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var path = context.Request.Path;

                    if (path == "/close")
                    {
                        var socket = await context.WebSockets.AcceptWebSocketAsync();
                        await socket.CloseAsync((WebSocketCloseStatus)3000, "Go away!", CancellationToken.None);
                    }
                    else
                    {
                        var socket = await context.WebSockets.AcceptWebSocketAsync();
                        while (true)
                        {
                            string received = await socket.ReceiveTextAsync();
                            await socket.SendAsync(received);
                        }
                    }
                }
            });
        }
    }
}
