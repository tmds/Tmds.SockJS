using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNet.WebSockets.Protocol;

namespace Tmds.SockJS.Tests
{
    public class WebSocketHttpErrorsTest : TestWebsiteTest
    {
        [Fact]
        public async Task Method()
        {
            var client = CreateClient();
            var response = await client.GetAsync(BaseUrl + "/0/0/websocket");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task InvalidConnectionHeader()
        {
            var client = CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/0/0/websocket");
            request.Headers.Add(HeaderNames.Upgrade, Constants.Headers.UpgradeWebSocket);
            request.Headers.Add(HeaderNames.Connection, "close");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("Not a valid websocket request", content);
        }

        [Fact]
        public async Task InvalidMethod()
        {
            var client = CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/0/0/websocket");
            request.Headers.Add(HeaderNames.Upgrade, Constants.Headers.UpgradeWebSocket);
            request.Headers.Add(HeaderNames.Connection, Constants.Headers.ConnectionUpgrade);
            var response = await client.SendAsync(request);
            await AssertNotAllowed(response);

            request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/0/0/websocket");
            response = await client.SendAsync(request);
            await AssertNotAllowed(response);
        }

        private async Task AssertNotAllowed(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            Assert.Null(response.Content.Headers.ContentType);
            Assert.NotNull(response.Content.Headers.Allow);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal(string.Empty, content);
        }
    }
}
