using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Cors.Infrastructure;
using Microsoft.AspNet.TestHost;
using Microsoft.Net.Http.Headers;
using TestWebSite;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class TestWebsiteTest
    {
        protected const string SiteName = nameof(TestWebSite);
        protected readonly string BaseUrl = "http://localhost/echo";
        protected readonly string CloseBaseUrl = "http://localhost/close";
        protected readonly string NoWebSocketBaseUrl = "http://localhost/disabled_websocket_echo";
        protected readonly string CookieBaseUrl = "http://localhost/cookie_needed_echo";

        protected HttpClient CreateClient()
        {
            var server = new TestServer(TestServer.CreateBuilder().UseStartup<Startup>());
            return server.CreateClient();
        }

        protected Task<WebSocket> ConnectWebSocket(string url)
        {
            var server = new TestServer(TestServer.CreateBuilder().UseStartup<Startup>());
            var client = server.CreateWebSocketClient();
            return client.ConnectAsync(new Uri(url), CancellationToken.None);
        }

        protected void AssertNoCookie(HttpResponseMessage response)
        {
            Assert.True(!response.Headers.Contains(HeaderNames.SetCookie));
        }

        protected void AssertNotCached(HttpResponseMessage response)
        {
            Assert.True(response.Headers.CacheControl.NoCache);
            Assert.True(response.Headers.CacheControl.NoStore);
            Assert.True(response.Headers.CacheControl.MustRevalidate);
            Assert.Equal(TimeSpan.Zero, response.Headers.CacheControl.MaxAge);
            Assert.Null(response.Content.Headers.Expires);
            Assert.Null(response.Content.Headers.LastModified);
        }
        protected void AssertCors(HttpResponseMessage response, string origin)
        {
            if (origin != null)
            {
                Assert.Equal(origin, response.Headers.GetValues(CorsConstants.AccessControlAllowOrigin).FirstOrDefault());
                Assert.Equal("true", response.Headers.GetValues(CorsConstants.AccessControlAllowCredentials).FirstOrDefault());
            }
            else
            {
                Assert.Equal("*", response.Headers.GetValues(CorsConstants.AccessControlAllowOrigin).FirstOrDefault());
                Assert.True(!response.Headers.Contains(CorsConstants.AccessControlAllowCredentials));
            }
        }
        protected async Task AssertOptions(string url, IList<string> allowedMethods)
        {
            foreach (var origin in new[] { "test", null })
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Options, url);
                request.Headers.Add(CorsConstants.AccessControlRequestMethod, string.Join(", ", allowedMethods));
                if (origin != null)
                {
                    request.Headers.Add(CorsConstants.Origin, origin);
                }
                var client = CreateClient();
                var response = await client.SendAsync(request);
                Assert.True((response.StatusCode == HttpStatusCode.OK) ||
                    (response.StatusCode == HttpStatusCode.NoContent));
                Assert.Equal(true, response.Headers.CacheControl.Public);
                Assert.True(response.Headers.CacheControl.MaxAge >= TimeSpan.FromSeconds(1000000));
                Assert.NotNull(response.Content.Headers.Expires);
                int maxAge = int.Parse(response.Headers.GetValues(CorsConstants.AccessControlMaxAge).Single());
                Assert.True(maxAge > 1000000);
                var responseAllowedMethods = response.Headers.GetValues(CorsConstants.AccessControlAllowMethods).Single();
                foreach (var method in allowedMethods)
                {
                    Assert.True(responseAllowedMethods.Contains(method));
                }
                var content = await response.Content.ReadAsStringAsync();
                Assert.Equal(0, content.Length);
                AssertCors(response, origin);
            }
        }
    }
}
