// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System.Net.WebSockets;

namespace Tmds.SockJS
{
    class PendingReceive
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
