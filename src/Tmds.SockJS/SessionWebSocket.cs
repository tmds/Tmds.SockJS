// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    class SessionWebSocket : WebSocket
    {
        private Session _session;
        private WebSocketState _state;
        private WebSocketCloseStatus? _closeStatus;
        private string _closeStatusDescription;

        public SessionWebSocket(Session session)
        {
            this._session = session;
            _state = WebSocketState.Open;
        }

        public override WebSocketCloseStatus? CloseStatus
        {
            get
            {
                return _closeStatus;
            }
        }

        public override string CloseStatusDescription
        {
            get
            {
                return _closeStatusDescription ?? string.Empty;
            }
        }

        public override WebSocketState State
        {
            get
            {
                return _state;
            }
        }

        public override string SubProtocol
        {
            get
            {
                return string.Empty;
            }
        }

        public override void Abort()
        {
            if (_state >= WebSocketState.Closed) // or Aborted
            {
                return;
            }

            _state = WebSocketState.Aborted;
            _session.WebSocketDispose();
        }

        public override void Dispose()
        {
            if (_state >= WebSocketState.Closed) // or Aborted
            {
                return;
            }

            _state = WebSocketState.Closed;
            _session.WebSocketDispose();
        }

        internal static Exception NewDisposedException()
        {
            throw new ObjectDisposedException(typeof(SessionWebSocket).FullName);
        }

        public async override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (State == WebSocketState.Open || State == WebSocketState.CloseReceived)
            {
                // Send a close message.
                await CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
            }

            if (State == WebSocketState.CloseSent)
            {
                // Do a receiving drain
                byte[] data = new byte[1024];
                WebSocketReceiveResult result;
                do
                {
                    result = await ReceiveAsync(new ArraySegment<byte>(data), cancellationToken);
                }
                while (result.MessageType != WebSocketMessageType.Close);
            }
        }

        public async override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfOutputClosed();

            await _session.SendCloseToClientAsync(closeStatus, statusDescription, cancellationToken);

            if (State == WebSocketState.Open)
            {
                _state = WebSocketState.CloseSent;
            }
            else if (State == WebSocketState.CloseReceived)
            {
                _state = WebSocketState.Closed;
                _session.WebSocketDispose();
            }
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfInputClosed();
            ValidateSegment(buffer);
            
            var result = await _session.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _closeStatusDescription = result.CloseStatusDescription;
                _closeStatus = result.CloseStatus;

                if (State == WebSocketState.Open)
                {
                    _state = WebSocketState.CloseReceived;
                }
                else if (State == WebSocketState.CloseSent)
                {
                    _state = WebSocketState.Closed;
                    _session.WebSocketDispose();
                }
            }
            return result;
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            ValidateSegment(buffer);
            if (messageType != WebSocketMessageType.Text || !endOfMessage)
            {
                throw new NotSupportedException("SockJS: Only complete text messages are supported");
            }

            ThrowIfDisposed();
            ThrowIfOutputClosed();
            return _session.ServerSendTextAsync(buffer, cancellationToken);
        }
        

        private void ValidateSegment(ArraySegment<byte> buffer)
        {
            if (buffer.Array == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (buffer.Offset < 0 || buffer.Offset > buffer.Array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(buffer.Offset), buffer.Offset, string.Empty);
            }
            if (buffer.Count < 0 || buffer.Count > buffer.Array.Length - buffer.Offset)
            {
                throw new ArgumentOutOfRangeException(nameof(buffer.Count), buffer.Count, string.Empty);
            }
        }

        private void ThrowIfOutputClosed()
        {
            if (State == WebSocketState.CloseSent)
            {
                throw new InvalidOperationException("Close already sent.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_state >= WebSocketState.Closed) // or Aborted
            {
                throw NewDisposedException();
            }
        }

        private void ThrowIfInputClosed()
        {
            if (State == WebSocketState.CloseReceived)
            {
                throw new InvalidOperationException("Close already received.");
            }
        }
    }
}
