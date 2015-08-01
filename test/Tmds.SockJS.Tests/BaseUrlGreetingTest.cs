using System.Threading.Tasks;
using Xunit;
using System.Net;

namespace Tmds.SockJS.Tests
{
    public class BaseUrlGreetingTest : TestWebsiteTest
    {
        [Fact]
        public async Task TestGreeting()
        {
            var client = CreateClient();

            foreach (string url in new[] { BaseUrl, BaseUrl + "/"})
            {
                var response = await client.GetAsync(url);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.NotNull(response.Content.Headers.ContentType);
                Assert.Equal("text/plain", response.Content.Headers.ContentType.MediaType);
                Assert.Equal("UTF-8", response.Content.Headers.ContentType.CharSet);

                var body = await response.Content.ReadAsStringAsync();

                Assert.Equal("Welcome to SockJS!\n", body);

                AssertNoCookie(response);
            }
        }

        [Fact]
        public async Task TestNotFound()
        {
            var client = CreateClient();

            foreach (var suffix in new[] { "/a", "/a.html", "//", "///", "/a/a", "/a/a/", "/a", "/a/" })
            {
                var url = BaseUrl + suffix;
                var response = await client.GetAsync(url);

                var content = response.Content.ReadAsStringAsync();

                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }
    }
}
