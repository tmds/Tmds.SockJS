using System;
using System.IO;
using System.Linq;
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
            // this is a small buffer to test overflow
            byte[] buffer = new byte[10];
            for (int i = 0; i < expected.Length; i++)
            {
                var decodedStream = new MemoryStream();
                do
                {
                    int length = messages[i].Decode(new ArraySegment<byte>(buffer, 0, buffer.Length));
                    decodedStream.Write(buffer, 0, length);
                } while (!messages[i].IsEmpty);
#if DNXCORE50
                ArraySegment<byte> streamBuffer;
                decodedStream.TryGetBuffer(out streamBuffer);
#else
                ArraySegment<byte> streamBuffer = new ArraySegment<byte>(decodedStream.GetBuffer(), 0, (int)decodedStream.Length);
#endif
                var s = Encoding.UTF8.GetString(streamBuffer.Array, streamBuffer.Offset, streamBuffer.Count);
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

        [Fact]
        public async Task SingleByteOverflow()
        {
            // string is longer than the read buffer
            string longString = new string('a', 100);
            await TestReader(@"[""" + longString + @"""]", new[] { longString });
        }

        [Fact]
        public async Task MultiByteOverflow()
        {
            // When this string is decoded, a 4 byte unicode character needs to be written
            // When there is no more space for writing the complete character, Decode must return !IsEmpty
            // and the unicode character must be completed with the next call to Decode
            string longUnicodeStringDecoded = string.Join("", Enumerable.Repeat("\U0001D11E", 50));
            string longUnicodeStringEncoded = string.Join("", Enumerable.Repeat("\\uD834\\uDD1E", 50));
            await TestReader(@"[""" + longUnicodeStringEncoded + @"""]", new[] { longUnicodeStringDecoded });
        }
    }
}
