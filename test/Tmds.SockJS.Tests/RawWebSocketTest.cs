using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tmds.SockJS.Tests
{
    public class RawWebSocketTest : TestWebsiteTest
    {
        [Fact]
        public async Task Transport()
        {
            var webSocket = await ConnectWebSocket(BaseUrl + "/websocket");
            var array = Encoding.UTF8.GetBytes("Hello world!\uffff");
            await webSocket.SendAsync(new ArraySegment<byte>(array), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
            var receiveArray = new byte[1024];
            var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveArray), CancellationToken.None);

            Assert.Equal(WebSocketMessageType.Text, receiveResult.MessageType);
            Assert.Equal(true, receiveResult.EndOfMessage);
            Assert.Equal(array.Length, receiveResult.Count);
            Assert.True(array.SequenceEqual(receiveArray.Take(array.Length)));

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }

        [Fact]
        public async Task Close()
        {
            var webSocket = await ConnectWebSocket(CloseBaseUrl + "/websocket");
            var array = Encoding.UTF8.GetBytes("Hello world!\uffff");
            var receiveArray = new byte[1024];

            var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveArray), CancellationToken.None);
            Assert.Equal(WebSocketMessageType.Close, receiveResult.MessageType);
            Assert.Equal("Go away!", receiveResult.CloseStatusDescription);
        }
    }
}
