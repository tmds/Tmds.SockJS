using Microsoft.Net.Http.Headers;
using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class IFramePageTest : TestWebsiteTest
    {
        [Fact]
        public Task SimpleUrl()
        {
            return Verify(BaseUrl + "/iframe.html");
        }

        private static readonly string s_iFrameBody =
            @"^<!DOCTYPE html>
<html>
<head>
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
  <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
  <script src=""(?<sockjs_url>[^""]*)""></script>
  <script>
    document.domain = document.domain;
    SockJS.bootstrap_iframe\(\);
  </script>
</head>
<body>
  <h2>Don't panic!</h2>
  <p>This is a SockJS hidden iframe.It's used for cross domain magic.</p>
</body>
</html>$".Replace("\r\n", "\n").Trim();

        private async Task Verify(string url)
        {
            var client = CreateClient();
            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.NotNull(response.Content.Headers.ContentType);
            Assert.Equal("text/html", response.Content.Headers.ContentType.MediaType);
            Assert.Equal("UTF-8", response.Content.Headers.ContentType.CharSet);

            Assert.NotNull(response.Headers.CacheControl);
            Assert.True(response.Headers.CacheControl.Public);
            Assert.True(response.Headers.CacheControl.MaxAge >= TimeSpan.FromSeconds(1000000));
            Assert.NotNull(response.Headers.ETag);

            Assert.NotNull(response.Content.Headers.Expires);
            Assert.Null(response.Content.Headers.LastModified);

            string content = await response.Content.ReadAsStringAsync();

            Match match = Regex.Match(content, s_iFrameBody);
            Assert.True(match.Success);
            string sockjsUrl = match.Groups["sockjs_url"].Value;
            Assert.True(sockjsUrl.StartsWith("/") || sockjsUrl.StartsWith("http"));

            AssertNoCookie(response);
        }

        [Fact]
        public async Task VersionedUrl()
        {
            foreach (var suffix in new[] { "/iframe-a.html", "/iframe-.html", "/iframe-0.1.2.html",
                       "/iframe-0.1.2abc-dirty.2144.html"})
            {
                await Verify(BaseUrl + suffix);
            }
        }

        [Fact]
        public async Task QueriedUrl()
        {
            foreach (var suffix in new[] { "/iframe-a.html?t=1234", "/iframe-0.1.2.html?t=123414",
                       "/iframe-0.1.2abc-dirty.2144.html?t=qweqweq123"})
            {
                await Verify(BaseUrl + suffix);
            }
        }

        [Fact]
        public async Task InvalidUrl()
        {
            foreach (var suffix in new[] { "/iframe.htm", "/iframe", "/IFRAME.HTML", "/IFRAME",
                       "/iframe.HTML", "/iframe.xml", "/iframe-/.html"})
            {
                var client = CreateClient();
                var response = await client.GetAsync(BaseUrl + suffix);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }

        [Fact]
        public async Task Cacheability()
        {
            var client = CreateClient();
            var response1 = await client.GetAsync(BaseUrl + "/iframe.html");
            var response2 = await client.GetAsync(BaseUrl + "/iframe.html");
            Assert.Equal(response1.Headers.ETag, response2.Headers.ETag);
            Assert.NotNull(response1.Headers.ETag);

            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/iframe.html");
            request.Headers.Add(HeaderNames.IfNoneMatch, response1.Headers.ETag.Tag);
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
            Assert.Null(response.Content.Headers.ContentType);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("", content);
        }
    }
}
