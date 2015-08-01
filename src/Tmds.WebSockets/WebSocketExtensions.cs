// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tmds.WebSockets
{
    public static class WebSocketExtensions
    {
        public static Task SendAsync(this WebSocket websocket, string text, CancellationToken cancellationToken = default(CancellationToken))
        {
            var segment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(text));
            return websocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
        }

        public async static Task<string> ReceiveTextAsync(this WebSocket websocket, CancellationToken cancellationToken = default(CancellationToken))
        {
            bool endOfMessage = false;
            MemoryStream ms = null;
            var buffer = new byte[4096];
            var segment = new ArraySegment<byte>(buffer);
            while (!endOfMessage)
            {
                var result = await websocket.ReceiveAsync(segment, cancellationToken);
                if (result.MessageType != WebSocketMessageType.Text)
                {
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

        public static Task SendAsync(this WebSocket websocket, byte[] buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            return websocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, cancellationToken);
        }

        public static Task SendAsync(this WebSocket websocket, ArraySegment<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            return websocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
        }

        public async static Task<ArraySegment<byte>> ReceiveBinaryAsync(this WebSocket websocket, CancellationToken cancellationToken = default(CancellationToken))
        {
            bool endOfMessage = false;
            MemoryStream ms = null;
            var buffer = new byte[4096];
            var segment = new ArraySegment<byte>(buffer);
            while (!endOfMessage)
            {
                var result = await websocket.ReceiveAsync(segment, cancellationToken);
                if (result.MessageType != WebSocketMessageType.Binary)
                {
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
    }
}
