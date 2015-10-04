using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class HandlingCloseTest : TestWebsiteTest
    {
        private string TrimHeader(string s)
        {
            return s.TrimStart(new char[] { 'h', '\n' });
        }
        [Fact]
        public async Task CloseFrame()
        {
            var client = CreateClient();
            var url = CloseBaseUrl + "/000/" + Guid.NewGuid().ToString();

            var response = await client.PostAsync(url + "/xhr_streaming", null);
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent = TrimHeader(responseContent);
            Assert.Equal("o\nc[3000,\"Go away!\"]\n", responseContent);

            response = await client.PostAsync(url + "/xhr_streaming", null);
            responseContent = await response.Content.ReadAsStringAsync();
            responseContent = TrimHeader(responseContent);
            Assert.Equal("c[3000,\"Go away!\"]\n", responseContent);
        }

        [Fact]
        public async Task CloseRequest()
        {
            var client = CreateClient();
            string sessionUrl = BaseUrl + "/000/" + Guid.NewGuid().ToString();

            var request = new HttpRequestMessage(HttpMethod.Post, sessionUrl + "/xhr_streaming");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            var response2 = await client.PostAsync(sessionUrl + "/xhr_streaming", null);
            var responseContent2 = await response2.Content.ReadAsStringAsync();
            responseContent2 = TrimHeader(responseContent2);
            Assert.Equal("c[2010,\"Another connection still open\"]\n", responseContent2);

            client.Dispose();
        }
    }
}
