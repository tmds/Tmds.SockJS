using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Tmds.SockJS;

namespace TestWebSite
{
    public static class ApplicationBuilderExtensions
    {
        public static void UseBroadcast(this IApplicationBuilder app, PathString path, SockJSOptions options = null)
        {
            app.UseSockJS(path, options ?? new SockJSOptions());
            app.UseMiddleware<BroadcastMiddleware>(path);
        }

        public static void UseEcho(this IApplicationBuilder app, PathString path, SockJSOptions options = null)
        {
            app.UseSockJS(path, options ?? new SockJSOptions());
            app.UseMiddleware<EchoMiddleware>(path);
        }

        public static void UseClose(this IApplicationBuilder app, PathString path, SockJSOptions options = null)
        {
            app.UseSockJS(path, options ?? new SockJSOptions());
            app.UseMiddleware<CloseMiddleware>(path);
        }

        public static void UseTicker(this IApplicationBuilder app, PathString path, SockJSOptions options = null)
        {
            app.UseSockJS(path, options ?? new SockJSOptions());
            app.UseMiddleware<TickerMiddleware>(path);
        }

        public static void UseAmplify(this IApplicationBuilder app, PathString path, SockJSOptions options = null)
        {
            app.UseSockJS(path, options ?? new SockJSOptions());
            app.UseMiddleware<AmplifyMiddleware>(path);
        }
    }
}
