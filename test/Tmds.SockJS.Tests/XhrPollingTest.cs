using Microsoft.AspNet.Cors.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class XhrPollingTest : TestWebsiteTest
    {
        [Fact]
        public async Task Options()
        {
            foreach (string suffix in new[] { "/xhr", "/xhr_send" })
            {
                await AssertOptions(BaseUrl + "/abc/abc" + suffix, new[] { "OPTIONS", "POST" });
            }
        }

        [Fact]
        public async Task Transport()
        {
            var client = CreateClient();
            var url = BaseUrl + "/000/" + Guid.NewGuid().ToString();

            var response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("o\n", content);
            Assert.Equal("UTF-8", response.Content.Headers.ContentType.CharSet);
            Assert.Equal("application/javascript", response.Content.Headers.ContentType.MediaType);
            AssertCors(response, null);

            response = await client.PostAsync(url + "/xhr_send", new StringContent("[\"x\"]", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.Equal(string.Empty, content);
            Assert.Equal("UTF-8", response.Content.Headers.ContentType.CharSet);
            Assert.Equal("text/plain", response.Content.Headers.ContentType.MediaType);
            AssertCors(response, null);

            response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.Equal("a[\"x\"]\n", content);
        }

        [Fact]
        public async Task InvalidSession()
        {
            var client = CreateClient();
            var url = BaseUrl + "/000/" + Guid.NewGuid().ToString();

            var response = await client.PostAsync(url + "/xhr_send", new StringContent("[\"x\"]", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task InvalidJson()
        {
            var client = CreateClient();
            var url = BaseUrl + "/000/" + Guid.NewGuid().ToString();

            var response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("o\n", content);

            response = await client.PostAsync(url + "/xhr_send", new StringContent("[\"x", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.True(content.Contains("Broken JSON encoding."));

            response = await client.PostAsync(url + "/xhr_send", new StringContent("", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.True(content.Contains("Payload expected."));

            response = await client.PostAsync(url + "/xhr_send", new StringContent("[\"a\"]", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.Equal(string.Empty, content);

            response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.Equal("a[\"a\"]\n", content);
        }

        [Fact]
        public async Task ContentTypes()
        {
            var client = CreateClient();
            var url = BaseUrl + "/000/" + Guid.NewGuid().ToString();

            var response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("o\n", content);

            // Test should be extended to also test these types: "T", "", "application/json; charset=utf-8", "text/xml; charset=utf-8"
            var contentTypes = new[] { "text/plain", "application/json", "application/xml", "text/xml" };

            foreach (var contentType in contentTypes)
            {
                response = await client.PostAsync(url + "/xhr_send", new StringContent("[\"a\"]", Encoding.UTF8, contentType));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                content = await response.Content.ReadAsStringAsync();
                Assert.Equal(string.Empty, content);
            }
        }

        [Fact]
        public async Task RequestHeadersCors()
        {
            var client = CreateClient();

            var url = BaseUrl + "/000/" + Guid.NewGuid().ToString();
            var requestContent = new StreamContent(Stream.Null);
            requestContent.Headers.Add(CorsConstants.AccessControlRequestHeaders, "a, b, c");
            var response = await client.PostAsync(url + "/xhr", requestContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertCors(response, null);
            Assert.True(response.Headers.GetValues(CorsConstants.AccessControlAllowHeaders).Contains("a, b, c"));

            url = BaseUrl + "/000/" + Guid.NewGuid().ToString();
            requestContent = new StreamContent(Stream.Null); ;
            requestContent.Headers.Add(CorsConstants.AccessControlRequestHeaders, "");
            response = await client.PostAsync(url + "/xhr", requestContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertCors(response, null);
            IEnumerable<string> values;
            Assert.False(response.Headers.TryGetValues(CorsConstants.AccessControlAllowHeaders, out values));

            url = BaseUrl + "/000/" + Guid.NewGuid().ToString();
            requestContent = new StreamContent(Stream.Null); ;
            response = await client.PostAsync(url + "/xhr", requestContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertCors(response, null);
            Assert.False(response.Headers.TryGetValues(CorsConstants.AccessControlAllowHeaders, out values));
        }

        [Fact]
        public async Task SendingEmptyFrame()
        {
            var client = CreateClient();
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString();
            var response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("o\n", content);

            var requestContent = new StringContent("[]", Encoding.UTF8, "application/json");
            response = await client.PostAsync(url + "/xhr_send", requestContent);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            requestContent = new StringContent("[\"a\"]", Encoding.UTF8, "application/json");
            response = await client.PostAsync(url + "/xhr_send", requestContent);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.Equal("a[\"a\"]\n", content);
        }

        [Fact]
        public async Task SendingEmptyText()
        {
            var client = CreateClient();
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString();
            var response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("o\n", content);

            var requestContent = new StringContent("[\"\"]", Encoding.UTF8, "application/json");
            response = await client.PostAsync(url + "/xhr_send", requestContent);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            Assert.Equal("a[\"\"]\n", content);
        }
    }
}
