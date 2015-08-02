using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class ProtocolTest : TestWebsiteTest
    {
        [Fact]
        public async Task SimpleSession()
        {
            string sessionUrl = BaseUrl + "/000/" + Guid.NewGuid().ToString();

            var client = CreateClient();

            var response = await client.PostAsync(sessionUrl + "/xhr", null);
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("o\n", responseContent);

            var requestContent = new StringContent("[\"a\"]", Encoding.UTF8, "application/json");
            response = await client.PostAsync(sessionUrl + "/xhr_send", requestContent);
            responseContent = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(string.Empty, responseContent);

            response = await client.PostAsync(sessionUrl + "/xhr", null);
            responseContent = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("a[\"a\"]\n", responseContent);

            requestContent = new StringContent("[\"a\"]", Encoding.UTF8, "application/json");
            response = await client.PostAsync(BaseUrl + "/000/bad_session/xhr_send", requestContent);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            Task t = client.PostAsync(sessionUrl + "/xhr", null);
            response = await client.PostAsync(sessionUrl + "/xhr", null);
            responseContent = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("c[2010,\"Another connection still open\"]\n", responseContent);
        }

        [Fact]
        public async Task CloseSession()
        {
            var client = CreateClient();
            string sessionUrl = CloseBaseUrl + "/000/" + Guid.NewGuid().ToString();

            var response = await client.PostAsync(sessionUrl + "/xhr", null);
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("o\n", responseContent);

            response = await client.PostAsync(sessionUrl + "/xhr", null);
            responseContent = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("c[3000,\"Go away!\"]\n", responseContent);

            response = await client.PostAsync(sessionUrl + "/xhr", null);
            responseContent = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("c[3000,\"Go away!\"]\n", responseContent);

            // wait for the session to time out
            await Task.Delay(TestWebSite.Startup.CloseDisconnectTimeout + TimeSpan.FromSeconds(2));
            response = await client.PostAsync(sessionUrl + "/xhr", null);
            responseContent = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("o\n", responseContent);
        }
    }
}
