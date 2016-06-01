using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TestWebSite;
using Microsoft.AspNetCore.TestHost;

namespace Tmds.SockJS.Tests
{
    public class WebSocketHixie76Test : TestWebsiteTest
    {
        [Fact]
        public async Task Transport()
        {
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString() + "/websocket";
            var webSocket = await ConnectWebSocket(url);
            var buffer = new byte[10];

            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("o", Encoding.UTF8.GetString(buffer, 0, result.Count));

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("[\"a\"]")), WebSocketMessageType.Text, true, CancellationToken.None);

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("a[\"a\"]", Encoding.UTF8.GetString(buffer, 0, result.Count));

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }

        [Fact]
        public async Task Close()
        {
            string url = CloseBaseUrl + "/000/" + Guid.NewGuid().ToString() + "/websocket";
            var webSocket = await ConnectWebSocket(url);
            var buffer = new byte[50];

            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("o", Encoding.UTF8.GetString(buffer, 0, result.Count));

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("c[3000,\"Go away!\"]", Encoding.UTF8.GetString(buffer, 0, result.Count));

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.MessageType, WebSocketMessageType.Close);
        }

        [Fact]
        public async Task EmptyFrame()
        {
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString() + "/websocket";
            var webSocket = await ConnectWebSocket(url);
            var buffer = new byte[10];

            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("o", Encoding.UTF8.GetString(buffer, 0, result.Count));

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("")), WebSocketMessageType.Text, true, CancellationToken.None);
            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("[]")), WebSocketMessageType.Text, true, CancellationToken.None);
            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("[\"a\"]")), WebSocketMessageType.Text, true, CancellationToken.None);

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("a[\"a\"]", Encoding.UTF8.GetString(buffer, 0, result.Count));

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }

        [Fact]
        public async Task ReuseSessionId()
        {
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString() + "/websocket";
            var server = TestWebsiteTest.GetServer();
            var client = server.CreateWebSocketClient();
            var webSocket1 = await client.ConnectAsync(new Uri(url), CancellationToken.None);
            var webSocket2 = await client.ConnectAsync(new Uri(url), CancellationToken.None);
            var buffer = new byte[10];

            var result = await webSocket1.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("o", Encoding.UTF8.GetString(buffer, 0, result.Count));

            result = await webSocket2.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("o", Encoding.UTF8.GetString(buffer, 0, result.Count));

            await webSocket1.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("[\"a\"]")), WebSocketMessageType.Text, true, CancellationToken.None);
            result = await webSocket1.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("a[\"a\"]", Encoding.UTF8.GetString(buffer, 0, result.Count));

            await webSocket2.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("[\"b\"]")), WebSocketMessageType.Text, true, CancellationToken.None);
            result = await webSocket2.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("a[\"b\"]", Encoding.UTF8.GetString(buffer, 0, result.Count));

            await webSocket1.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            await webSocket2.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);


            webSocket1 = await client.ConnectAsync(new Uri(url), CancellationToken.None);
            result = await webSocket1.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("o", Encoding.UTF8.GetString(buffer, 0, result.Count));
            await webSocket1.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("[\"a\"]")), WebSocketMessageType.Text, true, CancellationToken.None);
            result = await webSocket1.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("a[\"a\"]", Encoding.UTF8.GetString(buffer, 0, result.Count));
            await webSocket1.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }

        [Fact]
        public async Task BrokenJson()
        {
            string url = BaseUrl + "/000/" + Guid.NewGuid().ToString() + "/websocket";
            var webSocket = await ConnectWebSocket(url);
            var buffer = new byte[10];

            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.Equal(result.EndOfMessage, true);
            Assert.Equal(result.MessageType, WebSocketMessageType.Text);
            Assert.Equal("o", Encoding.UTF8.GetString(buffer, 0, result.Count));

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("[\"a")), WebSocketMessageType.Text, true, CancellationToken.None);

            await Assert.ThrowsAsync<IOException>(async () => await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None));
        }
    }
}
