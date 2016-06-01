// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Tmds.SockJS
{
    public static class ApplicationBuilderExtensions
    {
        public static void UseSockJS(this IApplicationBuilder app, PathString prefix)
        {
            UseSockJS(app, prefix, new SockJSOptions());
        }
        public static void UseSockJS(this IApplicationBuilder app, PathString prefix, SockJSOptions options)
        {
            app.Use(next => new SessionManager(prefix, next, options).Invoke);
        }
    }
}