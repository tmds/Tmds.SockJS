// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNet.WebUtilities;
using Microsoft.AspNet.Cors.Core;
using System.Collections.Generic;
using System.Net.WebSockets;
using Microsoft.AspNet.Http.Features;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;

namespace Tmds.SockJS
{
    class SessionManager
    {
        private static readonly string IFrameTemplate;
        private static readonly byte[] OtherReceiverCloseMessage;
        private static readonly string[] AllowedXhrSendMediaTypes;

        private static readonly string[] NoCacheCacheControlValue = new[] { "no-store, no-cache, must-revalidate, max-age=0" };
        private static readonly string[] CorsVaryValue = new[] { CorsConstants.Origin };
        private static readonly string[] TrueValue = new[] { "true" };
        private static readonly string[] OneYearCacheCacheControlValue = new[] { "public, max-age=31536000" };
        private static readonly string[] OneYearAccessControlMaxAge = new[] { "31536000" };
        private static readonly string[] OptionsPostAllowedMethods = new[] { "OPTIONS, POST" };
        private static readonly string[] OptionsGetAllowedMethods = new[] { "OPTIONS, GET" };
        private static readonly Random _random = new Random();
        private static readonly char[] ContentTypeSplitter = new[] { ';' };

        static SessionManager()
        {
            IFrameTemplate =
@"<!DOCTYPE html>
<html>
<head>
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
  <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
  <script src=""{0}""></script>
  <script>
    document.domain = document.domain;
    SockJS.bootstrap_iframe();
  </script>
</head>
<body>
  <h2>Don't panic!</h2>
  <p>This is a SockJS hidden iframe.It's used for cross domain magic.</p>
</body>
</html>".Replace("\r\n", "\n").Trim();
            OtherReceiverCloseMessage = Receiver.CloseBuffer((WebSocketCloseStatus)2010, "Another connection still open");
            AllowedXhrSendMediaTypes = new[] { "text/plain", "T", "application/json", "application/xml", "", "text/xml" };
        }
        private readonly RequestDelegate _next;
        private readonly SockJSOptions _options;
        private readonly string _iframeContent;
        private readonly string _iframeETag;
        private readonly List<Route> _routes;
        private readonly PathString _prefix;
        private readonly ConcurrentDictionary<string, Session> _sessions = new ConcurrentDictionary<string, Session>();

        public SessionManager(PathString prefix, RequestDelegate next, SockJSOptions options)
        {
            _prefix = prefix;
            _next = next;
            _options = options;

            _iframeContent = string.Format(IFrameTemplate, options.JSClientLibraryUrl);
            _iframeETag = CalculateETag(_iframeContent);

            _routes = new List<Route>(new[]
            {
                new Route("GET", "^[/]?$", HandleGreeting),
                new Route("GET", "^/iframe[0-9-.a-z_]*.html[/]?$", HandleIFrame),
                new Route("GET", "^/info[/]?$", HandleInfo),
                new Route("OPTIONS", "^/info[/]?$", HandleOptionsGetResource),
                new Route("GET", "^/([^/.]+)/([^/.]+)/jsonp[/]?$", HandleJsonp),
                new Route("POST", "^/([^/.]+)/([^/.]+)/jsonp_send[/]?$", HandleJsonpSend),
                new Route("POST", "^/([^/.]+)/([^/.]+)/xhr[/]?$", HandleXhr),
                new Route("OPTIONS", "^/([^/.]+)/([^/.]+)/xhr[/]?$", HandleOptionsPostResource),
                new Route("POST", "^/([^/.]+)/([^/.]+)/xhr_send[/]?$", HandleXhrSend),
                new Route("OPTIONS", "^/([^/.]+)/([^/.]+)/xhr_send[/]?$", HandleOptionsPostResource),
                new Route("POST", "^/([^/.]+)/([^/.]+)/xhr_streaming[/]?$", HandleXhrStreaming),
                new Route("OPTIONS", "^/([^/.]+)/([^/.]+)/xhr_streaming[/]?$", HandleOptionsPostResource),
                new Route("GET", "^/([^/.]+)/([^/.]+)/eventsource[/]?$", HandleEventSource),
                new Route("GET", "^/([^/.]+)/([^/.]+)/htmlfile[/]?$", HandleHtmlFile),
            });
            if (_options.UseWebSocket)
            {
                _routes.AddRange(new[] {
                    new Route("GET", "^/([^/.]+)/([^/.]+)/websocket[/]?$", HandleSockJSWebSocket),
                    new Route("GET", "^/websocket[/]?$", HandleWebSocket),
                });
            }
            else
            {
                _routes.AddRange(new[] {
                    new Route("GET", "^/([^/.]+)/([^/.]+)/websocket[/]?$", HandleNoWebSocket),
                    new Route("GET", "^/websocket[/]?$", HandleNoWebSocket),
                });
            }
        }

        private Task HandleNoWebSocket(HttpContext context, string sessionId)
        {
            AddCachingHeader(context);
            return HandleNotFound(context);
        }

        private Task HandleSockJSWebSocket(HttpContext context, string sessionId)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return ExposeText(context, "Not a valid websocket request");
            }
            var feature = new SockJSWebSocketFeature(context.GetFeature<IHttpWebSocketFeature>());
            context.Request.Path = _prefix;

            throw new NotImplementedException();
        }

        private Task HandleWebSocket(HttpContext context, string sessionId)
        {
            context.Request.Path = _prefix;
            return _next(context);
        }

        private Task HandleJsonpSend(HttpContext context, string sessionId)
        {
            throw new NotImplementedException();
        }

        private Task HandleJsonp(HttpContext context, string sessionId)
        {
            throw new NotImplementedException();
        }

        private Task HandleEventSource(HttpContext context, string sessionId)
        {
            throw new NotImplementedException();
        }

        private Task HandleHtmlFile(HttpContext context, string sessionId)
        {
            throw new NotImplementedException();
        }

        private async Task HandleXhrSend(HttpContext context, string sessionId)
        {
            AddSessionCookie(context);
            AddNoCacheHeader(context);
            AddCorsHeader(context);

            List<JsonString> messages;
            try
            {
                string mediaType = context.Request.ContentType?.Split(ContentTypeSplitter)[0];
                if (!AllowedXhrSendMediaTypes.Contains(mediaType))
                {
                    throw new Exception("Payload expected.");
                }

                var reader = new ReceiveMessageReader(context.Request.Body);
                messages = await reader.ReadMessages();
            }
            catch (Exception e)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(e.Message);
                return;
            }

            Session session = GetSession(sessionId);
            if (session == null)
            {
                await HandleNotFound(context);
                return;
            }

            session.ClientSend(messages);
            session.ExitSharedLock();
            
            context.Response.ContentType = "text/plain; charset=UTF-8";
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            await ExposeNothing(context);
        }

        private Task HandleXhrStreaming(HttpContext context, string sessionId)
        {
            AddSessionCookie(context);
            AddNoCacheHeader(context);
            AddCorsHeader(context);

            var receiver = new Receiver(context, false, _options.MaxResponseLength);
            return HandleReceiver(sessionId, receiver);
        }
        
        private Task HandleXhr(HttpContext context, string sessionId)
        {
            AddSessionCookie(context);
            AddNoCacheHeader(context);
            AddCorsHeader(context);

            var receiver = new Receiver(context, true, _options.MaxResponseLength);
            return HandleReceiver(sessionId, receiver);
        }

        private Session GetSession(string sessionId)
        {
            Session session = null;
            _sessions.TryGetValue(sessionId, out session);
            if (session != null)
            {
                session.EnterSharedLock();
                Session check = null;
                _sessions.TryGetValue(sessionId, out check);
                if (session == check)
                {
                    return session;
                }
                session.ExitSharedLock();
            }
            return null;
        }

        private Tuple<Session, bool> GetOrCreateSession(string sessionId, Receiver receiver)
        {
            while (true)
            {
                Session session = GetSession(sessionId);
                if (session != null)
                {
                    return Tuple.Create(session, false);
                }
                Session newSession = new Session(this, sessionId, receiver, _options);
                newSession.EnterExclusiveLock();
                Session check = _sessions.GetOrAdd(sessionId, newSession);
                if (check == newSession)
                {
                    return Tuple.Create(newSession, true);
                }
                newSession.ExitExclusiveLock();
            }
        }

        internal bool TryRemoveSession(Session session, CancellationToken ct)
        {
            try
            {
                session.EnterExclusiveLock();
                if (ct.IsCancellationRequested)
                {
                    return false;
                }
                return _sessions.TryRemove(session.SessionId, out session);
            }
            finally
            {
                session.ExitExclusiveLock();
            }
        }

        private async Task HandleReceiver(string sessionId, Receiver receiver)
        {
            var getOrCreate = GetOrCreateSession(sessionId, receiver);
            Session session = getOrCreate.Item1;
            bool newSession = getOrCreate.Item2;
            if (newSession)
            {
                try
                {
                    var feature = new SessionWebSocketFeature(session);
                    receiver.Context.SetFeature<IHttpWebSocketFeature>(feature);
                    receiver.Context.Request.Path = _prefix;

                    var pipeline = _next(receiver.Context);

                    Task.WaitAny(new[] { pipeline, feature.IsAcceptedPromise });

                    if (feature.IsAcceptedPromise.Status == TaskStatus.Created)
                    {
                        _sessions.TryRemove(sessionId, out session);
                        await pipeline;
                        return;
                    }
                }
                finally
                {
                    session.ExitExclusiveLock();
                }
            }
            else
            {
                bool receiverSet = session.SetReceiver(receiver);
                session.ExitSharedLock();
                if (!receiverSet)
                {
                    await receiver.Open();
                    await receiver.Send(true, OtherReceiverCloseMessage, CancellationToken.None);
                    return;
                }
            }
            await session.ClientReceiveAsync();
        }

        private Task HandleOptionsGetResource(HttpContext context, string session)
        {
            AddSessionCookie(context);
            AddCorsHeader(context);
            AddCachingHeader(context);

            context.Response.Headers.Add(CorsConstants.AccessControlAllowMethods, OptionsGetAllowedMethods);
            context.Response.Headers.Add(CorsConstants.AccessControlMaxAge, OneYearAccessControlMaxAge);

            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return ExposeNothing(context);
        }

        private Task HandleOptionsPostResource(HttpContext context, string session)
        {
            AddSessionCookie(context);
            AddCorsHeader(context);
            AddCachingHeader(context);

            context.Response.Headers.Add(CorsConstants.AccessControlAllowMethods, OptionsPostAllowedMethods);
            context.Response.Headers.Add(CorsConstants.AccessControlMaxAge, OneYearAccessControlMaxAge);

            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return ExposeNothing(context);
        }

        private Task ExposeNothing(HttpContext context)
        {
            return Task.FromResult<bool>(true);
        }

        private void AddSessionCookie(HttpContext context)
        {
            if (_options.SetJSessionIDCookie)
            {
                var jsid = context.Request.Cookies["JSESSIONID"] ?? "dummy";
                context.Response.Headers.Add(HeaderNames.SetCookie, new[] { "JSESSIONID=" + jsid + "; path=/" });
            }
        }

        private Task HandleInfo(HttpContext context, string session)
        {
            AddNoCacheHeader(context);
            AddCorsHeader(context);
            int entropy = 0;
            lock (_random)
            {
                entropy = _random.Next();
            }
            string info =
                string.Format(@"{{""websocket"":{0},""origins"":[""*:*""],""cookie_needed"":{1},""entropy"":{2}}}",
                        (_options.UseWebSocket ? "true" : "false"),
                        (_options.SetJSessionIDCookie ? "true" : "false"),
                        entropy
                        );
            return ExposeJson(context, info);
        }

        private Task HandleIFrame(HttpContext context, string session)
        {
            if (_iframeETag.Equals(context.Request.Headers.Get(HeaderNames.IfNoneMatch)))
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                return Task.FromResult(true);
            }
            AddCachingHeader(context);
            context.Response.Headers.Add(HeaderNames.ETag, new[] { _iframeETag });
            return ExposeHtml(context, _iframeContent);
        }

        private void AddCachingHeader(HttpContext context)
        {
            context.Response.Headers.Add(HeaderNames.CacheControl, OneYearCacheCacheControlValue);
            context.Response.Headers.Add(HeaderNames.Expires, new[] { DateTime.UtcNow.AddYears(1).ToString("R") });
        }

        private Task ExposeHtml(HttpContext context, string content)
        {
            return Expose(context, "text/html", content);   
        }

        private Task ExposeText(HttpContext context, string content)
        {
            return Expose(context, "text/plain", content);
        }

        private Task ExposeJson(HttpContext context, string content)
        {
            return Expose(context, "application/json", content);
        }

        private Task Expose(HttpContext context, string contentType, string content)
        {
            context.Response.ContentType = contentType + ";charset=UTF-8";

            byte[] data = Encoding.UTF8.GetBytes(content);
            context.Response.ContentLength = data.Length;

            return context.Response.Body.WriteAsync(data, 0, data.Length);
        }

        private Task HandleGreeting(HttpContext context, string session)
        {
            return ExposeText(context, "Welcome to SockJS!\n");
        }

        public string CalculateETag(string input)
        {
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            StringBuilder sb = new StringBuilder();
            sb.Append("\"");
            for (int i = 0; i < hash.Length; i++)
            {
                sb.AppendFormat("{0:x2}", hash[i]);
            }
            sb.Append("\"");
            return sb.ToString();
        }
        
        public Task Invoke(HttpContext context)
        {
            PathString path = context.Request.Path;
            if (!path.StartsWithSegments(_prefix, out path))
            {
                return _next(context);
            }
            foreach (var route in _routes)
            {
                if (context.Request.Method.Equals(route.Method, StringComparison.Ordinal))
                {
                    var match = route.Path.Match(path.Value);
                    if (match.Success)
                    {
                        string session = null;
                        if (match.Groups.Count == 3)
                        {
                            session = match.Groups[2].Value;
                        }
                        return route.Handler(context, session);
                    }
                }
            }
            return HandleNotFound(context);
        }

        private Task HandleNotFound(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return ExposeNothing(context);
        }

        private void AddNoCacheHeader(HttpContext context)
        {
            context.Response.Headers.Add(HeaderNames.CacheControl, NoCacheCacheControlValue);
        }

        private void AddCorsHeader(HttpContext context)
        {
            var request = context.Request;
            var response = context.Response;
            var requestOrigin = request.Headers[CorsConstants.Origin];
            if (requestOrigin != null)
            {
                response.Headers.Add(CorsConstants.AccessControlAllowCredentials, TrueValue);
            }
            var origin = requestOrigin ?? CorsConstants.AnyOrigin;
            response.Headers.Add(CorsConstants.AccessControlAllowOrigin, new[] { origin });
            response.Headers.Add(HeaderNames.Vary, CorsVaryValue);
            var requestHeaders = request.Headers[CorsConstants.AccessControlRequestHeaders];
            if (!string.IsNullOrEmpty(requestHeaders))
            {
                response.Headers.Add(CorsConstants.AccessControlAllowHeaders, new[] { requestHeaders });
            }
        }
    }
}
