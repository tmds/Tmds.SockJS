using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tmds.WebSockets
{
    internal static class WebSocketExtensions
    {
        public static async Task<string> ReceiveTextAsync(this WebSocket webSocket, CancellationToken cancellationToken = default(CancellationToken))
        {
            bool endOfMessage = false;
            MemoryStream ms = null;
            var buffer = new byte[4096];
            var segment = new ArraySegment<byte>(buffer);
            while (!endOfMessage)
            {
                var result = await webSocket.ReceiveAsync(segment, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(result.CloseStatus.Value, null, cancellationToken);
                    return null;
                }
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Text message expected, but received Binary message", cancellationToken);
                    throw new InvalidOperationException("Received message is not of type Text.");
                }
                endOfMessage = result.EndOfMessage;
                if (ms == null && endOfMessage)
                {
                    return Encoding.UTF8.GetString(buffer, 0, result.Count);
                }
                if (ms == null)
                {
                    ms = new MemoryStream();
                }
                ms.Write(buffer, 0, result.Count);
            }
            var outputSegment = new ArraySegment<byte>();
#if DNXCORE50
            ms.TryGetBuffer(out outputSegment);
#else
            outputSegment = new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length);
#endif
            return Encoding.UTF8.GetString(outputSegment.Array, outputSegment.Offset, outputSegment.Count);
        }

        public static async Task<ArraySegment<byte>> ReceiveBinaryAsync(this WebSocket webSocket, CancellationToken cancellationToken = default(CancellationToken))
        {
            bool endOfMessage = false;
            MemoryStream ms = null;
            var buffer = new byte[4096];
            var segment = new ArraySegment<byte>(buffer);
            while (!endOfMessage)
            {
                var result = await webSocket.ReceiveAsync(segment, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(result.CloseStatus.Value, null, cancellationToken);
                    return default(ArraySegment<byte>);
                }
                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Binary message expected, but received Text message", cancellationToken);
                    throw new InvalidOperationException("Received message is not of type Binary.");
                }
                endOfMessage = result.EndOfMessage;
                if (ms == null && endOfMessage)
                {
                    return new ArraySegment<byte>(buffer, 0, result.Count);
                }
                if (ms == null)
                {
                    ms = new MemoryStream();
                }
                ms.Write(buffer, 0, result.Count);
            }
            var outputSegment = new ArraySegment<byte>();
#if DNXCORE50
            ms.TryGetBuffer(out outputSegment);
#else
            outputSegment = new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length);
#endif
            return outputSegment;
        }

        public static async Task ReceiveCloseAsync(this WebSocket webSocket, CancellationToken cancellationToken = default(CancellationToken))
        {
            var buffer = new byte[0];
            var segment = new ArraySegment<byte>(buffer);
            var result = await webSocket.ReceiveAsync(segment, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(result.CloseStatus.Value, null, cancellationToken);
            }
            else
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Binary message expected, but received Close message", cancellationToken);
                throw new InvalidOperationException("Received message is not of type Close.");
            }
        }

        public static Task SendAsync(this WebSocket webSocket, string message, CancellationToken cancellationToken = default(CancellationToken))
        {
            var segment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            return webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
        }

        public static Task[] SendAsync(this IEnumerable<WebSocket> webSockets, string message, CancellationToken cancellationToken = default(CancellationToken))
        {
            var segment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            List<Task> tasks = new List<Task>();
            foreach (var webSocket in webSockets)
            {
                Task task = webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
                tasks.Add(task);
            }
            return tasks.ToArray();
        }

        public static Task SendAsync(this WebSocket webSocket, ArraySegment<byte> message, CancellationToken cancellationToken = default(CancellationToken))
        {
            return webSocket.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken);
        }

        public static Task SendCloseAsync(this WebSocket webSocket, WebSocketCloseStatus closeStatus, CancellationToken cancellationToken)
        {
            return webSocket.CloseAsync(closeStatus, null, cancellationToken);
        }

        public static Task SendCloseAsync(this WebSocket webSocket, WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, string statusDescription = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
        }
    }
}