using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class JsonEncodingTest : TestWebsiteTest
    {
        public static readonly string _clientKillerStringEscEncoded;
        public static readonly string _clientKillerStringEscDecoded;
        public static readonly string _serverKillerStringEscEncoded;
        public static readonly string _serverKillerStringEscDecoded;

        private static string NewtonsoftEncode(string input)
        {
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            JsonWriter writer = new JsonTextWriter(sw);
            writer.WriteValue(input);
            writer.Close();
            return sb.ToString();
        }

        private static string NewtonsoftDecode(string input)
        {
            var sr = new StringReader(input);
            var reader = new JsonTextReader(sr);
            var output = reader.ReadAsString();
            reader.Close();
            return output;
        }

        private static string CreateKillerString(Regex regex)
        {
            var builder = new StringBuilder();
            builder.Append('"');
            foreach (char c in Enumerable.Range(char.MinValue, char.MaxValue))
            {
                if (regex.IsMatch(c.ToString()))
                {
                    builder.AppendFormat("\\u{0:x4}", (int)c);
                }
            }
            builder.Append('"');
            return builder.ToString();
        }
        static JsonEncodingTest()
        {
            var escapableByClient = new Regex("[\\\"\u0000-\u001f\u007f-\u009f\u00ad\u0600-\u0604\u070f\u17b4\u17b5\u2000-\u20ff\ufeff\ufff0-\uffff\x00-\x1f\ufffe\uffff\u0300-\u0333\u033d-\u0346\u034a-\u034c\u0350-\u0352\u0357-\u0358\u035c-\u0362\u0374\u037e\u0387\u0591-\u05af\u05c4\u0610-\u0617\u0653-\u0654\u0657-\u065b\u065d-\u065e\u06df-\u06e2\u06eb-\u06ec\u0730\u0732-\u0733\u0735-\u0736\u073a\u073d\u073f-\u0741\u0743\u0745\u0747\u07eb-\u07f1\u0951\u0958-\u095f\u09dc-\u09dd\u09df\u0a33\u0a36\u0a59-\u0a5b\u0a5e\u0b5c-\u0b5d\u0e38-\u0e39\u0f43\u0f4d\u0f52\u0f57\u0f5c\u0f69\u0f72-\u0f76\u0f78\u0f80-\u0f83\u0f93\u0f9d\u0fa2\u0fa7\u0fac\u0fb9\u1939-\u193a\u1a17\u1b6b\u1cda-\u1cdb\u1dc0-\u1dcf\u1dfc\u1dfe\u1f71\u1f73\u1f75\u1f77\u1f79\u1f7b\u1f7d\u1fbb\u1fbe\u1fc9\u1fcb\u1fd3\u1fdb\u1fe3\u1feb\u1fee-\u1fef\u1ff9\u1ffb\u1ffd\u2000-\u2001\u20d0-\u20d1\u20d4-\u20d7\u20e7-\u20e9\u2126\u212a-\u212b\u2329-\u232a\u2adc\u302b-\u302c\uaab2-\uaab3\uf900-\ufa0d\ufa10\ufa12\ufa15-\ufa1e\ufa20\ufa22\ufa25-\ufa26\ufa2a-\ufa2d\ufa30-\ufa6d\ufa70-\ufad9\ufb1d\ufb1f\ufb2a-\ufb36\ufb38-\ufb3c\ufb3e\ufb40-\ufb41\ufb43-\ufb44\ufb46-\ufb4e]");
            _clientKillerStringEscDecoded = NewtonsoftDecode(CreateKillerString(escapableByClient));
            _clientKillerStringEscEncoded = NewtonsoftEncode(_clientKillerStringEscDecoded);
            var escapableByServer = new Regex("[\u0000-\u001f\u200c-\u200f\u2028-\u202f\u2060-\u206f\ufff0-\uffff]");
            _serverKillerStringEscDecoded = NewtonsoftDecode(CreateKillerString(escapableByServer));
            _serverKillerStringEscEncoded = NewtonsoftEncode(_serverKillerStringEscDecoded);
        }
        [Fact]
        public async Task ServerEncodes()
        {
            var client = CreateClient();
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString();
            var response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("o\n", content);

            var requestContent = new StringContent("[" + _serverKillerStringEscEncoded + "]", Encoding.UTF8, "application/json");
            response = await client.PostAsync(url + "/xhr_send", requestContent);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            var decoded = NewtonsoftDecode(content.Substring(2, content.Length - 4));
            Assert.Equal(_serverKillerStringEscDecoded, decoded);
        }

        [Fact]
        public async Task ServerDecodes()
        {
            var client = CreateClient();
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString();
            var response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("o\n", content);

            var requestContent = new StringContent("[" + _clientKillerStringEscEncoded + "]", Encoding.UTF8, "application/json");
            response = await client.PostAsync(url + "/xhr_send", requestContent);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            response = await client.PostAsync(url + "/xhr", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            content = await response.Content.ReadAsStringAsync();
            var decoded = NewtonsoftDecode(content.Substring(2, content.Length - 4));
            Assert.Equal(_clientKillerStringEscDecoded, decoded);
        }
    }
}
