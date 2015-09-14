// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using Microsoft.AspNet.Http;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    internal delegate Task SessionHttpContextHandler(HttpContext context, string sessionId);
}
