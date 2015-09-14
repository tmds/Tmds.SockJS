// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Tmds.SockJS
{
    internal class Route
    {
        public Route(string method, int type, bool regex, string match, SessionHttpContextHandler handler)
        {
            Method = method;
            Type = type;
            if (regex)
            {
                RegEx = new Regex(match);
            }
            else
            {
                Raw = match;
            }
            Handler = handler;
        }
        public string Method { get; set; }
        public int Type { get; set; }
        public Regex RegEx { get; set; }
        public string Raw { get; set; }
        public SessionHttpContextHandler Handler { get; set; }
    }
}
