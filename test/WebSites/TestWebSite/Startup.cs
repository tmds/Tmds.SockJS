using System;
using Microsoft.AspNet.Builder;
using Microsoft.Framework.DependencyInjection;
using Tmds.SockJS;
using System.Text;
using System.Threading;
using Tmds.WebSockets;
using System.Net.WebSockets;
using System.IO;

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
            app.UseSockJS("/echo", new SockJSOptions() { MaxResponseLength = 4096 });
            app.UseSockJS("/disabled_websocket_echo", new SockJSOptions() { UseWebSocket = false });
            app.UseSockJS("/close", new SockJSOptions() { HeartbeatInterval = TimeSpan.FromSeconds(10), DisconnectTimeout = CloseDisconnectTimeout });
            app.UseSockJS("/cookie_needed_echo", new SockJSOptions() { SetJSessionIDCookie = true });

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
                        var memoryStream = new MemoryStream();
                        while (true)
                        {
                            var array = new byte[1024];
                            var receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(array), CancellationToken.None);
                            memoryStream.Write(array, 0, receiveResult.Count);
                            if (receiveResult.MessageType == WebSocketMessageType.Close)
                            {
                                await socket.CloseAsync(receiveResult.CloseStatus.Value, string.Empty, CancellationToken.None);
                                return;
                            }
                            else if (receiveResult.EndOfMessage)
                            {
                                var buffer = new ArraySegment<byte>();
#if DNXCORE50
                                memoryStream.TryGetBuffer(out buffer);
#else
                                buffer = new ArraySegment<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
#endif
                                await socket.SendAsync(buffer, receiveResult.MessageType, receiveResult.EndOfMessage, CancellationToken.None);
                                memoryStream.SetLength(0);
                            }
                        }
                    }
                }
            });
        }
    }
}
