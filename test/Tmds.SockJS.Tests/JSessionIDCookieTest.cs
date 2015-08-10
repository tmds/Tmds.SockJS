using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class JSessionIDCookieTest : TestWebsiteTest
    {
        [Fact]
        public async Task Basic()
        {
            var client = CreateClient();
            var response = await client.GetAsync(CookieBaseUrl + "/info");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertNoCookie(response);
            var content = await response.Content.ReadAsStringAsync();
            var info = JsonConvert.DeserializeObject<Info>(content);
            Assert.Equal(info.cookie_needed, true);
        }

        [Fact]
        public async Task Xhr()
        {
            string url = CookieBaseUrl + "/000/" + Guid.NewGuid().ToString();
            var client = CreateClient();
            var response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal(content, "o\n");
            AssertCookie(response);

            var requestContent = new StreamContent(Stream.Null);
            requestContent.Headers.Add(HeaderNames.Cookie, "JSESSIONID=abcdef");
            url = CookieBaseUrl + "/000/" + Guid.NewGuid().ToString();
            response = await client.PostAsync(url + "/xhr", requestContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.Equal(content, "o\n");
            var setCookie = response.Headers.GetValues(HeaderNames.SetCookie).First().Split(';');
            Assert.Equal("JSESSIONID=abcdef", setCookie[0].Trim());
            Assert.Equal("path=/", setCookie[1].ToLowerInvariant().Trim());
        }

        [Fact]
        public async Task XhrStreaming()
        {
            string url = CookieBaseUrl + "/000/" + Guid.NewGuid().ToString();
            var client = CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, url + "/xhr_streaming");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertCookie(response);
        }

        [Fact]
        public async Task HtmlFile()
        {
            string url = CookieBaseUrl + "/000/" + Guid.NewGuid().ToString();
            var client = CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url + "/htmlfile?c=%63allback");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertCookie(response);
        }

        private void AssertCookie(HttpResponseMessage response)
        {
            var setCookie = response.Headers.GetValues(HeaderNames.SetCookie).First().Split(';');
            Assert.Equal("JSESSIONID=dummy", setCookie[0].Trim());
            Assert.Equal("path=/", setCookie[1].ToLowerInvariant().Trim());
        }
    }
}
