// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    class SockJSWebSocket : WebSocket
    {
        private const int NewLine = 1;
        private static readonly byte[] OpenBuffer;

        static SockJSWebSocket()
        {
            OpenBuffer = Encoding.UTF8.GetBytes("o");
        }

        private WebSocket _webSocket;
        List<JsonString> _receivedMessages;

        public SockJSWebSocket(WebSocket webSocket)
        {
            _webSocket = webSocket;
        }

        public override WebSocketCloseStatus? CloseStatus
        {
            get
            {
                return _webSocket.CloseStatus;
            }
        }

        public override string CloseStatusDescription
        {
            get
            {
                return _webSocket.CloseStatusDescription;
            }
        }

        public override WebSocketState State
        {
            get
            {
                return _webSocket.State;
            }
        }

        public override string SubProtocol
        {
            get
            {
                return _webSocket.SubProtocol;
            }
        }

        public override void Abort()
        {
            _webSocket.Abort();
        }

        internal Task Open(CancellationToken cancellationToken)
        {
            return _webSocket.SendAsync(new ArraySegment<byte>(OpenBuffer), WebSocketMessageType.Text, true, cancellationToken);
        }

        public async override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            await SendCloseBuffer(closeStatus, statusDescription, cancellationToken);
            await _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
        }

        public async override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            await SendCloseBuffer(closeStatus, statusDescription, cancellationToken);
            await _webSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }

        private async Task SendCloseBuffer(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            var buffer = Receiver.CloseBuffer(closeStatus, statusDescription);
            await _webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length - NewLine), WebSocketMessageType.Text, true, cancellationToken);
        }

        public override void Dispose()
        {
            _webSocket.Dispose();
        }

        public async override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            while (true)
            {
                if (_receivedMessages != null && _receivedMessages.Count > 0)
                {
                    var first = _receivedMessages[0];
                    if (first.IsEmpty)
                    {
                        _receivedMessages.RemoveAt(0);
                        continue;
                    }
                    int count = first.Decode(buffer);
                    return new WebSocketReceiveResult(count, WebSocketMessageType.Text, true);
                }

                var memoryStream = new MemoryStream();
                bool endOfMessage = false;
                var receiveBuffer = new byte[buffer.Count * 2];
                while (!endOfMessage)
                {
                    var receiveResult = await _webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationToken);
                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        memoryStream.Write(receiveBuffer, 0, receiveResult.Count);
                        endOfMessage = receiveResult.EndOfMessage;
                    }
                    else if (receiveResult.MessageType == WebSocketMessageType.Binary)
                    {
                        throw new NotSupportedException("SockJS: Binary messages are not supported");
                    }
                    else if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        return receiveResult;
                    }
                }

                memoryStream.Position = 0;
                var reader = new ReceiveMessageReader(memoryStream);
                _receivedMessages = await reader.ReadMessages();
            }
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (messageType != WebSocketMessageType.Text || !endOfMessage)
            {
                throw new NotSupportedException("SockJS: Only complete text messages are supported");
            }

            var writer = new PendingSendsWriter();
            writer.WriteMessages(new[] { new PendingSend(null, messageType, buffer, cancellationToken) });
            var sendBuffer = new ArraySegment<byte>(writer.Buffer.Array, writer.Buffer.Offset, writer.Buffer.Count - NewLine);
            return _webSocket.SendAsync(sendBuffer, messageType, endOfMessage, cancellationToken);
        }
    }
}