// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
