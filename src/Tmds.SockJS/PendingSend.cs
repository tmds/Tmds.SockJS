// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    public class PendingSend
    {
        public WebSocketMessageType Type { get; private set; }
        public ArraySegment<byte> Buffer { get; private set; }
        private TaskCompletionSource<bool> TaskCompletionSource { get; set; }
        public Task CompleteTask { get { return TaskCompletionSource.Task; } }
        public CancellationToken CancellationToken { get; private set; }

        public PendingSend(TaskCompletionSource<bool> tcs, WebSocketMessageType type, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            Type = type;
            TaskCompletionSource = tcs;
            Buffer = buffer;
            CancellationToken = cancellationToken;
        }

        public void CompleteCloseSent()
        {
            if (TaskCompletionSource != null)
            {
                TaskCompletionSource.SetException(new InvalidOperationException("Session is not open"));
            }
        }

        public void CompleteDisposed()
        {
            if (TaskCompletionSource != null)
            {
                TaskCompletionSource.SetException(SessionWebSocket.NewDisposedException());
            }
        }

        public void CompleteClientTimeout()
        {
            if (TaskCompletionSource != null)
            {
                TaskCompletionSource.SetException(new IOException("Connection timed out"));
            }
        }

        public void CompleteSuccess()
        {
            if (TaskCompletionSource != null)
            {
                TaskCompletionSource.SetResult(true);
            }
        }

        public void CompleteException(Exception e)
        {
            if (TaskCompletionSource != null)
            {
                TaskCompletionSource.SetException(e);
            }
        }
    }


}
