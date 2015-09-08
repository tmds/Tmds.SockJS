// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using Microsoft.AspNet.Http.Features;
using System.Threading.Tasks;
using System.Net.WebSockets;

namespace Tmds.SockJS
{
    class SessionWebSocketFeature : IHttpWebSocketFeature
    {
        Session _session;
        TaskCompletionSource<bool> _acceptedTcs;
        public SessionWebSocketFeature(Session session)
        {
            _session = session;
            _acceptedTcs = new TaskCompletionSource<bool>();
        }

        public Task IsAcceptedPromise { get { return _acceptedTcs.Task; } }

        public bool IsWebSocketRequest
        {
            get
            {
                return true;
            }
        }

        public async Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            WebSocket ws = await _session.AcceptWebSocket();
            _acceptedTcs.SetResult(true);
            await Task.Yield(); // ensure _next returns
            return ws;
        }
    }
}
