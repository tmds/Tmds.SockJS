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
        private static readonly byte[] HtmlFileHeartBeatBuffer;
        private static readonly byte[] StreamingHeaderBuffer;
        private static readonly byte[] OpenBuffer;
        private static readonly byte[] HtmlFileOpenBuffer;
        private static readonly string HtmlFileTemplate;

        static Receiver()
        {
            HeartBeatBuffer = Encoding.UTF8.GetBytes("h");
            HtmlFileHeartBeatBuffer = Encoding.UTF8.GetBytes("<script>\np(\"h\");\n</script>\r\n");
            OpenBuffer = Encoding.UTF8.GetBytes("o\n");
            StreamingHeaderBuffer = Encoding.UTF8.GetBytes(new string('h', 2048) + "\n");
            HtmlFileOpenBuffer = Encoding.UTF8.GetBytes("<script>\np(\"o\");\n</script>\r\n");
            HtmlFileTemplate =
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
            HtmlFileTemplate += new string(' ', 1024 - HtmlFileTemplate.Length + 14 - 1);
            HtmlFileTemplate += "\r\n\r\n";
        }
        enum State
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
                await SendRawAsync(RawSendType.Header, StreamingHeaderBuffer, CancellationToken.None);
            }
            else if (_type == ReceiverType.HtmlFile)
            {
                var buffer = Encoding.UTF8.GetBytes(string.Format(HtmlFileTemplate, _htmlFileCallback));
                await SendRawAsync(RawSendType.Header, buffer, CancellationToken.None);
            }
        }

        private async Task SendOpenAsync()
        {
            if (_type == ReceiverType.HtmlFile)
            {
                await SendRawAsync(RawSendType.Other, HtmlFileOpenBuffer, CancellationToken.None);
            }
            else
            {
                await SendRawAsync(RawSendType.Other, OpenBuffer, CancellationToken.None);
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
                return SendRawAsync(RawSendType.Other, HtmlFileHeartBeatBuffer, CancellationToken.None);
            }
            else
            {
                return SendRawAsync(RawSendType.Other, HeartBeatBuffer, CancellationToken.None);
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
