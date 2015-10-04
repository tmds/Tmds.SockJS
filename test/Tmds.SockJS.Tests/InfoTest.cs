using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class InfoTest : TestWebsiteTest
    {
        [Fact]
        public async Task Basic()
        {
            var client = CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/info");
            var origin = "test";
            request.Headers.Add("Origin", origin);
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
            Assert.Equal("UTF-8", response.Content.Headers.ContentType.CharSet);
            AssertNoCookie(response);
            AssertNotCached(response);
            AssertCors(response, origin);

            var content = await response.Content.ReadAsStringAsync();
            var info = JsonConvert.DeserializeObject<Info>(content);
            Assert.Equal(true, info.websocket);
            Assert.Equal(false, info.cookie_needed);
            Assert.Equal(1, info.origins.Count);
            Assert.Equal("*:*", info.origins[0]);
        }
        [Fact]
        public async Task Entropy()
        {
            var client = CreateClient();
            var response1 = await client.GetAsync(BaseUrl + "/info");
            var response2 = await client.GetAsync(BaseUrl + "/info");
            var content1 = await response1.Content.ReadAsStringAsync();
            var content2 = await response2.Content.ReadAsStringAsync();
            var info1 = JsonConvert.DeserializeObject<Info>(content1);
            var info2 = JsonConvert.DeserializeObject<Info>(content2);
            Assert.NotEqual(info1.entropy, info2.entropy);
        }

        [Fact]
        public async Task Options()
        {
            await AssertOptions(BaseUrl + "/info", new[] { "OPTIONS", "GET" });
        }

        [Fact]
        public async Task DisabledWebSocket()
        {
            var client = CreateClient();
            var response = await client.GetAsync(NoWebSocketBaseUrl + "/info");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();

            var info = JsonConvert.DeserializeObject<Info>(content);
            Assert.Equal(false, info.websocket);
        }
    }
}
