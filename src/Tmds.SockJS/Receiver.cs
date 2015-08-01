// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using System.Threading;
using System.Text;
using System.Net.WebSockets;
using System.IO;

namespace Tmds.SockJS
{
    class Receiver
    {
        private static readonly byte[] HeartBeatBuffer;
        private static readonly byte[] StreamingHeaderBuffer;
        private static readonly byte[] OpenBuffer;

        static Receiver()
        {
            HeartBeatBuffer = Encoding.UTF8.GetBytes("h");
            OpenBuffer = Encoding.UTF8.GetBytes("o\n");
            StreamingHeaderBuffer = Encoding.UTF8.GetBytes(new string('h', 2048) + "\n");
        }
        enum State
        {
            NotOpen,
            Open,
            Closed
        }

        private HttpContext _context;
        private bool _polling;
        private int _bytesSent;
        private State _state;
        private int _maxResponseLength;

        public static byte[] CloseBuffer(WebSocketCloseStatus status, string description)
        {
            description = description.Replace("\\", "\\\\");
            description = description.Replace("\"", "\\\"");
            return Encoding.UTF8.GetBytes("c[" + ((int)status).ToString() + ",\"" + description + "\"]\n");
        }

        public Receiver(HttpContext context, bool polling, int maxResponseLength)
        {
            _context = context;
            _polling = polling;
            _state = State.NotOpen;
            _maxResponseLength = maxResponseLength;
        }

        public bool IsClosed { get { return _state == State.Closed; } }
        public bool IsNotOpen { get { return _state == State.NotOpen; } }
        public HttpContext Context { get { return _context; } }
        public int BytesSent { get { return _bytesSent; } }
        public CancellationToken Aborted { get { return _context.RequestAborted; } }
        
        public async Task Open(bool sessionOpen = false)
        {
            _state = State.Open;

            _context.Response.ContentType = "application/javascript; charset=UTF-8";

            if (!_polling)
            {
                await Send(false, StreamingHeaderBuffer, CancellationToken.None);
                _bytesSent = 0;
            }

            if (sessionOpen)
            {
                await Send(false, OpenBuffer, CancellationToken.None);
            }
        }
        public Task Send(bool isCloseMessage, byte[] buffer, CancellationToken cancellationToken)
        {
            return Send(isCloseMessage, new ArraySegment<byte>(buffer), cancellationToken);
        }
        public async Task Send(bool isCloseMessage, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            ThrowIfNotOpen();

            try
            {
                if (_polling)
                {
                    _context.Response.ContentLength = buffer.Count;
                }
                await _context.Response.Body.WriteAsync(buffer.Array, buffer.Offset, buffer.Count, cancellationToken);
                await _context.Response.Body.FlushAsync(cancellationToken);
                _bytesSent += buffer.Count;
                if (_polling || isCloseMessage || (_bytesSent >= _maxResponseLength))
                {
                    _state = State.Closed;
                }
            }
            catch
            {
                _state = State.Closed;
                throw;
            }
        }

        public Task SendHeartBeat()
        {
            return Send(false, HeartBeatBuffer, CancellationToken.None);
        }

        internal async Task SendMessages(List<PendingSend> sends)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(sends.Select(send => send.CancellationToken).ToArray());

            try
            {
                var writer = new PendingSendsWriter();
                writer.WriteMessages(sends);
                await Send(false, writer.Buffer, cts.Token);

                foreach (var send in sends)
                {
                    send.CompleteSuccess();
                }
            }
            catch (Exception e)
            {
                _state = State.Closed;
                bool isCancellationException = e is OperationCanceledException;
                Exception ioException = null;
                foreach (var send in sends)
                {
                    if (!isCancellationException)
                    {
                        send.CompleteException(e);

                    }
                    else if (send.CancellationToken.IsCancellationRequested)
                    {
                        send.CompleteException(e);
                    }
                    else
                    {
                        if (ioException == null)
                        {
                             ioException = new IOException("Operation failed because another operation was cancelled", e);
                        }
                        send.CompleteException(ioException);
                    }
                }
                throw;
            }
        }

        public async Task SendCloseMessage(PendingSend send)
        {
            try
            {
                await Send(true, send.Buffer, send.CancellationToken);
                send.CompleteSuccess();
            }
            catch (Exception e)
            {
                send.CompleteException(e);
                throw;
            }
        }
        
        private void ThrowIfNotOpen()
        {
            if (_state != State.Open)
            {
                throw new InvalidOperationException("Cannot send on closed receiver");
            }
        }
    }
}
