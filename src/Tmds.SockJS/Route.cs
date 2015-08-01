// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Tmds.SockJS
{
    class Route
    {
        public Route(string method, string path, SessionHttpContextHandler handler)
        {
            Method = method;
            Path = new Regex(path);
            Handler = handler;
        }
        public string Method { get; set; }
        public Regex Path { get; set; }
        public SessionHttpContextHandler Handler { get; set; }
    }
}
