// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.WebSockets;

namespace Tmds.SockJS
{
    internal class PendingReceive
    {
        public PendingReceive(JsonString textMessage)
        {
            Type = WebSocketMessageType.Text;
            TextMessage = textMessage;
        }
        public PendingReceive(WebSocketCloseStatus closeStatus, string closeDescription)
        {
            Type = WebSocketMessageType.Close;
            CloseStatusDescription = closeDescription;
            CloseStatus = closeStatus;
        }
        public JsonString TextMessage { get; private set; }
        public WebSocketMessageType Type { get; private set; }
        public WebSocketCloseStatus? CloseStatus { get; private set; }
        public string CloseStatusDescription { get; private set; }
    }
}
