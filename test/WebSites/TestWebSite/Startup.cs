using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tmds.SockJS;
using Microsoft.AspNetCore.Builder;

namespace TestWebSite
{
    public class Startup
    {
        public static readonly TimeSpan CloseDisconnectTimeout = TimeSpan.FromSeconds(2);

        public static object WebApplication { get; private set; }

        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            app.UseWebSockets();
            app.UseBroadcast("/broadcast");
            app.UseEcho("/echo", new SockJSOptions() { MaxResponseLength = 4096 });
            app.UseEcho("/disabled_websocket_echo", new SockJSOptions() { UseWebSocket = false });
            app.UseEcho("/cookie_needed_echo", new SockJSOptions() { SetJSessionIDCookie = true });
            app.UseClose("/close", new SockJSOptions() { HeartbeatInterval = TimeSpan.FromSeconds(10), DisconnectTimeout = CloseDisconnectTimeout });
            app.UseTicker("/ticker");
            app.UseAmplify("/amplify");
        }
    }
}
