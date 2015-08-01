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
        public async Task ResponseLimit()
        {
            var client = CreateClient();
            var url = BaseUrl + "/000/" + Guid.NewGuid().ToString();

            var responseTask = client.PostAsync(url + "/xhr_streaming", new ByteArrayContent(new byte[] { }));
            await Task.Delay(1000);

            string msg = "\"" + new string('x', 128) + "\"";
            for (int i = 0; i < 32; i++)
            {
                var sendResponse = await client.PostAsync(url + "/xhr_send", new StringContent("[" + msg + "]", Encoding.UTF8, "application/json"));
                Assert.Equal(HttpStatusCode.NoContent, sendResponse.StatusCode);
            }

            var response = await responseTask;
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
