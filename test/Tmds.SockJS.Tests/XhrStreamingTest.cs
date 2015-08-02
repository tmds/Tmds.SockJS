using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class XhrStreamingTest : TestWebsiteTest
    {
        [Fact]
        public async Task Options()
        {
            await AssertOptions(BaseUrl + "/abc/abc/xhr_streaming", new[] { "OPTIONS", "POST" });
        }

        [Fact]
        public async Task Transport()
        {
            var client = CreateClient();
            var url = BaseUrl + "/000/" + Guid.NewGuid().ToString();

            var request = new HttpRequestMessage(HttpMethod.Post, url + "/xhr_streaming");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("UTF-8", response.Content.Headers.ContentType.CharSet);
            Assert.Equal("application/javascript", response.Content.Headers.ContentType.MediaType);
            AssertCors(response, null);

            var contentStream = await response.Content.ReadAsStreamAsync();
            var reader = new StreamReader(contentStream);
            var prelude = await reader.ReadLineAsync();
            var open = await reader.ReadLineAsync();
            Assert.Equal(new string('h', 2048), prelude);
            Assert.Equal("o", open);

            var sendResponse = await client.PostAsync(url + "/xhr_send", new StringContent("[\"x\"]", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NoContent, sendResponse.StatusCode);
            
            var message = await reader.ReadLineAsync();
            Assert.Equal("a[\"x\"]", message);

            client.Dispose();}

        [Fact]
        public async Task ResponseLimit()
        {
            var client = CreateClient();
            var url = BaseUrl + "/000/" + Guid.NewGuid().ToString();

            var request = new HttpRequestMessage(HttpMethod.Post, url + "/xhr_streaming");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            string msg = "\"" + new string('x', 128) + "\"";
            for (int i = 0; i < 32; i++)
            {
                var sendResponse = await client.PostAsync(url + "/xhr_send", new StringContent("[" + msg + "]", Encoding.UTF8, "application/json"));
                Assert.Equal(HttpStatusCode.NoContent, sendResponse.StatusCode);
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var contentStream = await response.Content.ReadAsStreamAsync();
            var reader = new StreamReader(contentStream);
            var prelude = await reader.ReadLineAsync();
            var open = await reader.ReadLineAsync();
            Assert.Equal(new string('h', 2048), prelude);
            Assert.Equal("o", open);
            var remaining = await reader.ReadToEndAsync();
            Assert.Equal(string.Concat(Enumerable.Repeat("a[" + msg + "]\n", 32)), remaining);
        }
    }
}
