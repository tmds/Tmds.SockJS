using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using System.Text;
using System.Net.Http.Headers;

namespace Tmds.SockJS.Tests
{
    public class SessionUrlsTest : TestWebsiteTest
    {
        [Fact]
        public async Task AnyValue()
        {
            foreach (var sessionPart in new[] { "/a/a", "/_/_", "/1/1", "/abcdefgh_i-j%20/abcdefg_i-j%20" })
            {
                await Verify(sessionPart);
            }
        }

        private async Task Verify(string sessionPart)
        {
            var client = CreateClient();
            var response = await client.PostAsync(BaseUrl + sessionPart + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("o\n", content);
        }

        [Fact]
        public async Task InvalidPaths()
        {
            foreach (var suffix in new[] { "//", "/a./a", "/a/a.", "/./.", "/", "///" })
            {
                var client = CreateClient();

                var response = await client.GetAsync(BaseUrl + suffix + "/xhr");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                response = await client.PostAsync(BaseUrl + suffix + "/xhr", null);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }

        [Fact]
        public async Task IgnoringServerId()
        {
            string sessionId = Guid.NewGuid().ToString();
            var client = CreateClient();

            var response = await client.PostAsync(BaseUrl + "/000/" + sessionId + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("o\n", content);

            var requestContent = new ByteArrayContent(Encoding.UTF8.GetBytes("[\"a\"]"));
            requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response = await client.PostAsync(BaseUrl + "/000/" + sessionId + "/xhr_send", requestContent);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.Equal(string.Empty, content);

            response = await client.PostAsync(BaseUrl + "/999/" + sessionId + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.Equal("a[\"a\"]\n", content);
        }
    }
}
