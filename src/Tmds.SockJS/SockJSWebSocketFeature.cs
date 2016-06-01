// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Tmds.SockJS
{
    internal class SockJSWebSocketFeature : IHttpWebSocketFeature
    {
        private readonly IHttpWebSocketFeature _feature;

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
            await socket.OpenAsync(CancellationToken.None);
            return socket;
        }
    }
}