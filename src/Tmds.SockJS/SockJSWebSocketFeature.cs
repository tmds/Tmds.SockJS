// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNet.Http.Features;
using System.Threading;

namespace Tmds.SockJS
{
    class SockJSWebSocketFeature : IHttpWebSocketFeature
    {
        private IHttpWebSocketFeature _feature;

        public SockJSWebSocketFeature(IHttpWebSocketFeature httpWebSocketFeature)
        {
            _feature = httpWebSocketFeature;
        }

        public bool IsWebSocketRequest
        {
            get
            {
                return _feature.IsWebSocketRequest;
            }
        }

        public async Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            var socket = new SockJSWebSocket(await _feature.AcceptAsync(context));
            await socket.Open(CancellationToken.None);
            return socket;
        }
    }
}