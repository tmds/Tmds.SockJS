// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    internal class Receiver
    {
        private static readonly byte[] s_heartBeatBuffer;
        private static readonly byte[] s_htmlFileHeartBeatBuffer;
        private static readonly byte[] s_streamingHeaderBuffer;
        private static readonly byte[] s_openBuffer;
        private static readonly byte[] s_htmlFileOpenBuffer;
        private static readonly string s_htmlFileTemplate;

        static Receiver()
        {
            s_heartBeatBuffer = Encoding.UTF8.GetBytes("h");
            s_htmlFileHeartBeatBuffer = Encoding.UTF8.GetBytes("<script>\np(\"h\");\n</script>\r\n");
            s_openBuffer = Encoding.UTF8.GetBytes("o\n");
            s_streamingHeaderBuffer = Encoding.UTF8.GetBytes(new string('h', 2048) + "\n");
            s_htmlFileOpenBuffer = Encoding.UTF8.GetBytes("<script>\np(\"o\");\n</script>\r\n");
            s_htmlFileTemplate =
@"<!DOCTYPE html>
<html>
<head>
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
  <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
</head><body><h2>Don't panic!</h2>
  <script>
    document.domain = document.domain;
    var c = parent.{0};
    c.start();
    function p(d) {{c.message(d);}};
    window.onload = function() {{c.stop();}};
  </script>
".Replace("\r\n", "\n");
            s_htmlFileTemplate += new string(' ', 1024 - s_htmlFileTemplate.Length + 14 - 1);
            s_htmlFileTemplate += "\r\n\r\n";
        }
        private enum State
        {
            NotOpen,
            Open,
            Closed
        }

        private HttpContext _context;
        private ReceiverType _type;
        private int _bytesSent;
        private State _state;
        private int _maxResponseLength;
        private string _htmlFileCallback;

        public static byte[] CloseBuffer(WebSocketCloseStatus status, string description)
        {
            return MessageWriter.CreateCloseBuffer(status, description);
        }

        public Receiver(HttpContext context, ReceiverType type, int maxResponseLength, string htmlFileCallback)
        {
            _context = context;
            _type = type;
            _state = State.NotOpen;
            _maxResponseLength = maxResponseLength;
            _htmlFileCallback = htmlFileCallback;
        }

        public bool IsClosed { get { return _state == State.Closed; } }
        public bool IsNotOpen { get { return _state == State.NotOpen; } }
        public HttpContext Context { get { return _context; } }
        public int BytesSent { get { return _bytesSent; } }
        public CancellationToken Aborted { get { return _context.RequestAborted; } }

        public async Task Open(bool openSession = false)
        {
            _state = State.Open;

            if (_type == ReceiverType.HtmlFile)
            {
                _context.Response.ContentType = "text/html; charset=UTF-8";
            }
            else
            {
                _context.Response.ContentType = "application/javascript; charset=UTF-8";
            }

            await SendHeaderAsync();

            if (openSession)
            {
                await SendOpenAsync();
            }
        }

        private async Task SendHeaderAsync()
        {
            if (_type == ReceiverType.XhrStreaming)
            {
                await SendRawAsync(RawSendType.Header, s_streamingHeaderBuffer, CancellationToken.None);
            }
            else if (_type == ReceiverType.HtmlFile)
            {
                var buffer = Encoding.UTF8.GetBytes(string.Format(s_htmlFileTemplate, _htmlFileCallback));
                await SendRawAsync(RawSendType.Header, buffer, CancellationToken.None);
            }
        }

        private async Task SendOpenAsync()
        {
            if (_type == ReceiverType.HtmlFile)
            {
                await SendRawAsync(RawSendType.Other, s_htmlFileOpenBuffer, CancellationToken.None);
            }
            else
            {
                await SendRawAsync(RawSendType.Other, s_openBuffer, CancellationToken.None);
            }
        }
        public Task SendCloseAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return SendRawAsync(RawSendType.Close, MessageWriter.CreateCloseMessage(_type, buffer), cancellationToken);
        }

        private Task SendRawAsync(RawSendType type, byte[] buffer, CancellationToken cancellationToken)
        {
            return SendRawAsync(type, new ArraySegment<byte>(buffer), cancellationToken);
        }
        private async Task SendRawAsync(RawSendType type, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            ThrowIfNotOpen();

            if (buffer.Count == 0)
            {
                return;
            }

            try
            {
                if (_type == ReceiverType.XhrPolling)
                {
                    _context.Response.ContentLength = buffer.Count;
                }
                await _context.Response.Body.WriteAsync(buffer.Array, buffer.Offset, buffer.Count, cancellationToken);
                await _context.Response.Body.FlushAsync(cancellationToken);
                if (type != RawSendType.Header)
                {
                    _bytesSent += buffer.Count;
                }
                if ((_type == ReceiverType.XhrPolling) || (type == RawSendType.Close) || (_bytesSent >= _maxResponseLength))
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
            if (_type == ReceiverType.HtmlFile)
            {
                return SendRawAsync(RawSendType.Other, s_htmlFileHeartBeatBuffer, CancellationToken.None);
            }
            else
            {
                return SendRawAsync(RawSendType.Other, s_heartBeatBuffer, CancellationToken.None);
            }
        }

        internal async Task SendMessages(List<PendingSend> sends)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(sends.Select(send => send.CancellationToken).ToArray());

            try
            {
                await SendRawAsync(RawSendType.Other, MessageWriter.CreateSendsMessage(_type, sends), cts.Token);

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

        private void ThrowIfNotOpen()
        {
            if (_state != State.Open)
            {
                throw new InvalidOperationException("Cannot send on closed receiver");
            }
        }
    }
}
