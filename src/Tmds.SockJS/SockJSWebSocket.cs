// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    internal class SockJSWebSocket : WebSocket
    {
        private const int NewLine = 1;
        private static readonly byte[] s_openBuffer;

        static SockJSWebSocket()
        {
            s_openBuffer = Encoding.UTF8.GetBytes("o");
        }

        private WebSocket _webSocket;
        private List<JsonString> _receivedMessages;

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
            return _webSocket.SendAsync(new ArraySegment<byte>(s_openBuffer), WebSocketMessageType.Text, true, cancellationToken);
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
            var buffer = MessageWriter.CreateCloseBuffer(closeStatus, statusDescription);
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
                if (_receivedMessages != null)
                {
                    var head = _receivedMessages[0];
                    int length = head.Decode(buffer);
                    if (head.IsEmpty)
                    {
                        _receivedMessages.RemoveAt(0);
                        if (_receivedMessages.Count == 0)
                        {
                            _receivedMessages = null;
                        }
                    }
                    return new WebSocketReceiveResult(length, WebSocketMessageType.Text, true);
                }

                MemoryStream memoryStream = null;
                bool endOfMessage = false;
                var receiveBuffer = new byte[buffer.Count * 2];
                while (!endOfMessage)
                {
                    var receiveResult = await _webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationToken);
                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        endOfMessage = receiveResult.EndOfMessage;
                        if (endOfMessage && (memoryStream == null))
                        {
                            memoryStream = new MemoryStream(receiveBuffer, 0, receiveResult.Count);
                        }
                        else
                        {
                            memoryStream = memoryStream ?? new MemoryStream();
                            memoryStream.Write(receiveBuffer, 0, receiveResult.Count);
                        }
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
                if (memoryStream.Length == 0)
                {
                    continue;
                }

                memoryStream.Position = 0;
                var reader = new ReceiveMessageReader(memoryStream);
                try
                {
                    _receivedMessages = await reader.ReadMessages();
                }
                catch
                {
                    await CloseAsync((WebSocketCloseStatus)3000, "Broken framing.", cancellationToken);
                    throw;
                }
                if (_receivedMessages.Count == 0)
                {
                    _receivedMessages = null;
                }
            }
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (messageType != WebSocketMessageType.Text || !endOfMessage)
            {
                throw new NotSupportedException("SockJS: Only complete text messages are supported");
            }

            var sendBuffer = MessageWriter.CreateSockJSWebSocketSendMessage(buffer);

            return _webSocket.SendAsync(sendBuffer, messageType, endOfMessage, cancellationToken);
        }
    }
}