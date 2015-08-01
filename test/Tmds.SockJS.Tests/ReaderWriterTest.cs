using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class ReaderWriterTest
    {
        private async Task TestReader(string input, string[] expected)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            var reader = new ReceiveMessageReader(stream);
            var messages = await reader.ReadMessages();
            Assert.Equal(expected.Length, messages.Count);
            byte[] buffer = new byte[512];
            for (int i = 0; i < expected.Length; i++)
            {
                int length = messages[i].Decode(new ArraySegment<byte>(buffer, 0, buffer.Length));
                var s = Encoding.UTF8.GetString(buffer, 0, length);
                Assert.Equal(expected[i], s);
            }
        }
        [Fact]
        public async Task Reader()
        {
            await TestReader(@"""""", new[] { "" });
            await TestReader(@"""a""", new[] { "a" });
            await TestReader(@"""aa""", new[] { "aa" });
            await TestReader(@"[""a"", ""b""]", new[] { "a", "b" });
            await TestReader(@"[""a"", """"]", new[] { "a", "" });
            await TestReader(@"["""", ""b""]", new[] { "", "b" });
            await TestReader(@"[""aa"", ""b""]", new[] { "aa", "b" });
            await TestReader(@"[""aa"", """"]", new[] { "aa", "" });
            await TestReader(@"["""", ""b""]", new[] { "", "b" });
            await TestReader(@"[""aa"", ""bb""]", new[] { "aa", "bb" });
            await TestReader(@"[""aa"", """"]", new[] { "aa", "" });
            await TestReader(@"["""", ""bb""]", new[] { "", "bb" });
            await TestReader(@"[""\b\f\n\r\t""]", new[] { "\b\f\n\r\t" });
            await TestReader(@"[""\u005C""]", new[] { "\\" });
            await TestReader(@"[""\u005c""]", new[] { "\\" });
            await TestReader(@"[""\uD834\uDD1E""]", new[] { "\U0001D11E" });
        }
    }
}
