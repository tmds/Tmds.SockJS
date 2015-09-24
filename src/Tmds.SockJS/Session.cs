// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

namespace Tmds.SockJS
{
    internal class Session
    {
        private const int SendOpen = 0;
        private const int SendDisposed = -1;
        private const int SendCloseSent = -2;
        private const int SendClientTimeout = -3;

        private const int ReceiveNone = 0;
        private const int ReceiveOne = 1;
        private const int ReceiveDisposed = -1;
        private const int ReceiveCloseReceived = -2;
        
        public static readonly byte[] DisposeCloseBuffer;
        public static readonly byte[] SendErrorCloseBuffer;
        public static readonly PendingReceive CloseSentPendingReceive;
        public static readonly PendingReceive CloseNotSentPendingReceive;
        private const string ReceiverIsClosed = "The receiver is closed";
        private const string SimultaneousReceivesNotSupported = "Simultaneous receives are not supported";

        static Session()
        {
            DisposeCloseBuffer = MessageWriter.CreateCloseBuffer(WebSocketCloseStatus.EndpointUnavailable, "Going Away");
            SendErrorCloseBuffer = MessageWriter.CreateCloseBuffer(WebSocketCloseStatus.ProtocolError, "Connection interrupted");
            CloseNotSentPendingReceive = new PendingReceive(WebSocketCloseStatus.EndpointUnavailable, "Going Away");
            CloseSentPendingReceive = new PendingReceive(WebSocketCloseStatus.NormalClosure, "Normal Closure");
        }

        private SessionWebSocket _socket;
        private string _sessionId;
        private SessionManager _sessionManager;
        private Receiver _receiver;
        private SockJSOptions _options;
        private ReaderWriterLockSlim _clientLock;
        private CancellableTimer _timeoutTimer;
        private ConcurrentQueue<PendingReceive> _receives;
        private SemaphoreSlim _receivesSem;
        private SemaphoreSlim _sendDequeueSem;
        private SemaphoreSlim _sendsSem;
        private SemaphoreSlim _sendEnqueueSem;
        private ConcurrentQueue<PendingSend> _sends;
        private volatile byte[] _closeMessage;
        private int _sendState; // use _sendEnqueueSem to synchronize
        private volatile int _receiveState;
        private bool _isAccepted;

        public string SessionId { get { return _sessionId; } }

        public bool IsAccepted { get { return _isAccepted; } }

        public Session(SessionManager sessionContainer, string sessionId, Receiver receiver, SockJSOptions options)
        {
            _clientLock = new ReaderWriterLockSlim();
            _sessionManager = sessionContainer;
            _sessionId = sessionId;
            _options = options;
            _receiver = receiver;
            _sendState = SendOpen;
            _receiveState = ReceiveNone;
            _receives = new ConcurrentQueue<PendingReceive>();
            _receivesSem = new SemaphoreSlim(0);
            _sendDequeueSem = new SemaphoreSlim(1);
            _sendsSem = new SemaphoreSlim(0);
            _sendEnqueueSem = new SemaphoreSlim(1);
            _sends = new ConcurrentQueue<PendingSend>();
        }

        public bool SetReceiver(Receiver receiver)
        {
            Receiver original = Interlocked.CompareExchange(ref _receiver, receiver, null);
            if (original != null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public void ClearReceiver()
        {
            Volatile.Write(ref _receiver, null);
        }

        public async Task ClientReceiveAsync()
        {
            try
            {
                if (_receiver.IsNotOpen)
                {
                    await _receiver.Open();
                }
                while (!_receiver.IsClosed)
                {
                    if (_closeMessage != null)
                    {
                        await _receiver.SendCloseAsync(_closeMessage, CancellationToken.None);
                        return;
                    }

                    await _sendDequeueSem.WaitAsync();
                    bool release = true;
                    List<PendingSend> messages = null;
                    try
                    {
                        PendingSend firstSend;
                        bool timeout = !(await _sendsSem.WaitAsync(_options.HeartbeatInterval, _receiver.Aborted));
                        if (_sendState == SendDisposed)
                        {
                            throw SessionWebSocket.NewDisposedException();
                        }
                        if (timeout)
                        {
                            // heartbeat
                            await _receiver.SendHeartBeat();
                            continue;
                        }
                        _sends.TryDequeue(out firstSend);

                        if (firstSend.Type == WebSocketMessageType.Close)
                        {
                            _sendDequeueSem.Release();
                            release = false;
                            if (_closeMessage == null)
                            {
                                var closeMessage = new byte[firstSend.Buffer.Count];
                                Array.Copy(firstSend.Buffer.Array, firstSend.Buffer.Offset, closeMessage, 0, firstSend.Buffer.Count);
                                Interlocked.CompareExchange(ref _closeMessage, closeMessage, null);
                            }
                            await _receiver.SendCloseAsync(_closeMessage, CancellationToken.None);
                            return;
                        }
                        else // WebSocketMessageType.Text
                        {
                            messages = new List<PendingSend>();
                            messages.Add(firstSend);
                            PendingSend nextSend;
                            int length = firstSend.Buffer.Count + _receiver.BytesSent;
                            while (_sends.TryPeek(out nextSend) && (nextSend.Type == WebSocketMessageType.Text))
                            {
                                await _sendsSem.WaitAsync(TimeSpan.Zero);
                                _sends.TryDequeue(out nextSend);

                                messages.Add(nextSend);
                                length += nextSend.Buffer.Count;
                                if (length >= _options.MaxResponseLength)
                                {
                                    break;
                                }
                            }
                            _sendDequeueSem.Release();
                            release = false;
                            await _receiver.SendMessages(messages);
                        }
                    }
                    catch (ObjectDisposedException) // SendDisposed
                    {
                        if (messages != null)
                        {
                            foreach (var message in messages)
                            {
                                message.CompleteDisposed();
                            }
                        }
                        PendingSend send;
                        while (_sends.TryDequeue(out send))
                        {
                            send.CompleteDisposed();
                        }
                        continue; // _closeMessage was set when SendDisposed
                    }
                    finally
                    {
                        if (release)
                        {
                            _sendDequeueSem.Release();
                        }
                    }
                }
            }
            catch
            {
                await HandleClientSendErrorAsync();
                throw;
            }
        }

        public void WebSocketDispose()
        {
            // no new _sends
            _sendEnqueueSem.Wait();
            // only dispose once
            if (_sendState == SendDisposed)
            {
                _sendEnqueueSem.Release();
                return;
            }
            _sendState = SendDisposed;
            _sendEnqueueSem.Release();

            // set close message
            Interlocked.CompareExchange(ref _closeMessage, DisposeCloseBuffer, null);

            // dispose sends
            _sendsSem.Release();
            _sendDequeueSem.Wait();
            PendingSend send;
            while (_sends.TryDequeue(out send))
            {
                send.CompleteDisposed();
            }
            _sendDequeueSem.Release();

            // stop receive
            _receiveState = ReceiveDisposed;
            _receivesSem.Release();
        }

        private async Task HandleClientSendErrorAsync()
        {
            // no new _sends
            await _sendEnqueueSem.WaitAsync();
            if (_sendState == SendOpen)
            {
                _sendState = SendClientTimeout;
            }
            _sendEnqueueSem.Release();

            // set close message
            Interlocked.CompareExchange(ref _closeMessage, SendErrorCloseBuffer, null);

            // dispose sends
            await _sendDequeueSem.WaitAsync();
            PendingSend send;
            while (_sends.TryDequeue(out send))
            {
                send.CompleteClientTimeout();
            }
            _sendDequeueSem.Release();

            // stop receive
            _receives.Enqueue(CloseNotSentPendingReceive);
            _receivesSem.Release();
        }

        public void ClientSend(List<JsonString> messages)
        {
            if (_receiveState < ReceiveNone)
            {
                return;
            }
            foreach (var message in messages)
            {
                _receives.Enqueue(new PendingReceive(message));
                _receivesSem.Release();
            }
        }

        public void CancelSessionTimeout()
        {
            _timeoutTimer.Cancel();
            _timeoutTimer = null;
        }

        public void ScheduleSessionTimeout(Session session, CancellableTimerCallback callback, TimeSpan dueTime)
        {
            _timeoutTimer = CancellableTimer.Schedule(callback, session, dueTime);
        }

        public async void HandleClientTimeOut()
        {
            await _sendEnqueueSem.WaitAsync();
            if (_sendState == SendOpen)
            {
                _sendState = SendClientTimeout;
            }
            _sendEnqueueSem.Release();

            await _sendDequeueSem.WaitAsync();
            PendingSend send;
            while (_sends.TryDequeue(out send))
            {
                send.CompleteClientTimeout();
            }
            _sendDequeueSem.Release();

            if (_closeMessage == null)
            {
                _receives.Enqueue(CloseNotSentPendingReceive);
            }
            else
            {
                _receives.Enqueue(CloseSentPendingReceive);
            }
            _receivesSem.Release();
        }

        public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            int oldState = Interlocked.CompareExchange(ref _receiveState, ReceiveOne, ReceiveNone);
            if (oldState == ReceiveDisposed)
            {
                throw SessionWebSocket.NewDisposedException();
            }
            if (oldState == ReceiveCloseReceived)
            {
                throw new InvalidOperationException(ReceiverIsClosed);
            }
            if (oldState == ReceiveOne)
            {
                throw new InvalidOperationException(SimultaneousReceivesNotSupported);
            }

            try
            {
                await _receivesSem.WaitAsync(cancellationToken);
                if (_receiveState == ReceiveDisposed)
                {
                    throw SessionWebSocket.NewDisposedException();
                }
                PendingReceive receive;
                _receives.TryPeek(out receive);

                if (receive.Type == WebSocketMessageType.Text)
                {
                    try
                    {
                        int length = receive.TextMessage.Decode(buffer);
                        bool endOfMessage = receive.TextMessage.IsEmpty;
                        var result = new WebSocketReceiveResult(length, WebSocketMessageType.Text, endOfMessage);

                        if (endOfMessage)
                        {
                            _receives.TryDequeue(out receive);
                        }
                        else
                        {
                            // undo Wait
                            _receivesSem.Release();
                        }
                        return result;
                    }
                    catch // Decode exception
                    {
                        _receives.TryDequeue(out receive);
                        throw;
                    }
                }
                else // (receive.Type == WebSocketMessageType.Close)
                {
                    var result = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, receive.CloseStatus, receive.CloseStatusDescription);
                    Interlocked.CompareExchange(ref _receiveState, ReceiveCloseReceived, ReceiveOne);
                    _receives.TryDequeue(out receive);
                    return result;
                }
            }
            finally
            {
                Interlocked.CompareExchange(ref _receiveState, ReceiveNone, ReceiveOne);
            }
        }

        public void ExitExclusiveLock()
        {
            _clientLock.ExitWriteLock();
        }

        public void EnterExclusiveLock()
        {
            _clientLock.EnterWriteLock();
        }

        public void ExitSharedLock()
        {
            _clientLock.ExitReadLock();
        }

        public void EnterSharedLock()
        {
            _clientLock.EnterReadLock();
        }

        public Task SendCloseToClientAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            return ServerSendMessageAsync(WebSocketMessageType.Close, new ArraySegment<byte>(MessageWriter.CreateCloseBuffer(closeStatus, statusDescription)), cancellationToken);
        }

        private async Task ServerSendMessageAsync(WebSocketMessageType type, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await _sendEnqueueSem.WaitAsync(cancellationToken);

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            var send = new PendingSend(tcs, type, buffer, cancellationToken);

            try
            {
                if (_sendState == SendOpen)
                {
                    if (type == WebSocketMessageType.Close)
                    {
                        _sendState = SendCloseSent;
                    }
                    _sends.Enqueue(send);
                    _sendsSem.Release();
                }
                else
                {
                    if (_sendState == SendCloseSent)
                    {
                        send.CompleteCloseSent();
                    }
                    else if (_sendState == SendDisposed)
                    {
                        send.CompleteDisposed();
                    }
                    else if (_sendState == SendClientTimeout)
                    {
                        send.CompleteClientTimeout();
                    }
                }
            }
            finally
            {
                _sendEnqueueSem.Release();
            }

            await send.CompleteTask;
        }

        public Task ServerSendTextAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            return ServerSendMessageAsync(WebSocketMessageType.Text, buffer, cancellationToken);
        }

        public async Task<WebSocket> AcceptWebSocket()
        {
            try
            {
                _isAccepted = true;
                await _receiver.OpenSession();
                _socket = new SessionWebSocket(this);
                return _socket;
            }
            catch
            {
                await HandleClientSendErrorAsync();
                throw;
            }
        }
    }
}
