using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class HtmlFileTest : TestWebsiteTest
    {
        private static readonly string s_head;
        private static readonly string s_open;
        static HtmlFileTest()
        {
            s_head =
@"<!DOCTYPE html>
<html>
<head>
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
  <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
</head><body><h2>Don't panic!</h2>
  <script>
    document.domain = document.domain;
    var c = parent.{0};
    c.start();
    function p(d) {{c.message(d);}};
    window.onload = function() {{c.stop();}};
  </script>
".Replace("\r\n", "\n").Trim();

            s_open = "<script>\np(\"o\");\n</script>\r\n";
        }

        private async Task AssertHeaderAndOpen(Stream stream)
        {
            var buffer = new byte[2000];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            var head = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.True(head.StartsWith(string.Format(s_head, "callback")));
            Assert.True(bytesRead > 1024);

            if (head.EndsWith(s_open))
            {
                Assert.True(head.EndsWith(s_open));
            }
            else
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                var open = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Assert.Equal(s_open, open);
            }
        }

        [Fact]
        public async Task Transport()
        {
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString();
            var client = CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url + "/htmlfile?c=%63allback");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            AssertNotCached(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("UTF-8", response.Content.Headers.ContentType.CharSet);
            Assert.Equal("text/html", response.Content.Headers.ContentType.MediaType);
            var stream = await response.Content.ReadAsStreamAsync();
            await AssertHeaderAndOpen(stream);

            var sendResponse = await client.PostAsync(url + "/xhr_send", new StringContent("[\"x\"]", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NoContent, sendResponse.StatusCode);

            var buffer = new byte[2000];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.Equal("<script>\np(\"a[\\\"x\\\"]\");\n</script>\r\n", message);
        }

        [Fact]
        public async Task NoCallback()
        {
            var client = CreateClient();
            var response = await client.GetAsync(BaseUrl + "/a/a/htmlfile");
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(content.Contains("\"callback\" parameter required"));
        }

        [Fact]
        public async Task InvalidCallback()
        {
            var client = CreateClient();
            foreach (string cb in new[] { "%20", "*", "abc(", "abc%28" })
            {
                var response = await client.GetAsync(BaseUrl + "/a/a/htmlfile?c=" + cb);
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
                var content = await response.Content.ReadAsStringAsync();
                Assert.True(content.Contains("invalid \"callback\" parameter"));
            }
        }

        [Fact]
        public async Task ResponseLimit()
        {
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString();
            var client = CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url + "/htmlfile?c=callback");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var stream = await response.Content.ReadAsStreamAsync();
            await AssertHeaderAndOpen(stream);

            string msg = new string('x', 4096);
            var sendResponse = await client.PostAsync(url + "/xhr_send", new StringContent("[\"" + msg + "\"]", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NoContent, sendResponse.StatusCode);

            var reader = new StreamReader(stream);
            var remainder = reader.ReadToEnd();
            Assert.Equal("<script>\np(\"a[\\\"" + msg + "\\\"]\");\n</script>\r\n", remainder);
        }
    }
}
