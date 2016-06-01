// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    internal delegate Task SessionHttpContextHandler(HttpContext context, string sessionId);
}
