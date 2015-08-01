// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;

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