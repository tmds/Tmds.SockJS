// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNet.Cors.Core;
using System.Collections.Generic;
using System.Net.WebSockets;
using Microsoft.AspNet.Http.Features;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tmds.SockJS
{
    internal class SessionManager
    {
        private static readonly string s_iFrameTemplate;
        private static readonly byte[] s_otherReceiverCloseMessage;
        private static readonly string[] s_allowedXhrSendMediaTypes;

        private static readonly string[] s_noCacheCacheControlValue = new[] { "no-store, no-cache, must-revalidate, max-age=0" };
        private static readonly string[] s_corsVaryValue = new[] { CorsConstants.Origin };
        private static readonly string[] s_trueValue = new[] { "true" };
        private static readonly string[] s_oneYearCacheCacheControlValue = new[] { "public, max-age=31536000" };
        private static readonly string[] s_oneYearAccessControlMaxAge = new[] { "31536000" };
        private static readonly string[] s_optionsPostAllowedMethods = new[] { "OPTIONS, POST" };
        private static readonly string[] s_optionsGetAllowedMethods = new[] { "OPTIONS, GET" };
        private static readonly Random s_random = new Random();
        private static readonly char[] s_contentTypeSplitter = new[] { ';' };
        private static readonly Regex s_htmlFileCallbackRegex;
        private static readonly Regex[] s_pathRegex;
        private const int TypeEmpty = 0;
        private const int TypeTop = 1;
        private const int TypeSession = 2;

        static SessionManager()
        {
            s_iFrameTemplate =
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
            s_otherReceiverCloseMessage = MessageWriter.CreateCloseBuffer((WebSocketCloseStatus)2010, "Another connection still open");
            s_allowedXhrSendMediaTypes = new[] { "text/plain", "T", "application/json", "application/xml", "", "text/xml" };
            s_htmlFileCallbackRegex = new Regex("[^a-zA-Z0-9-_.]");
            s_pathRegex = new[]
            {
                new Regex("^[/]?$"),
                new Regex("^/([0-9-.a-z_]+)[/]?$"),
                new Regex("^/([^/.]+)/([^/.]+)/([0-9-.a-z_]+)[/]?$")
            };
        }
        private readonly RequestDelegate _next;
        private readonly SockJSOptions _options;
        private readonly string _iframeContent;
        private readonly string _iframeETag;
        private readonly List<Route> _routes;
        private readonly PathString _prefix;
        private readonly ConcurrentDictionary<string, Session> _sessions = new ConcurrentDictionary<string, Session>();
        private readonly PathString _rewritePath;

        public SessionManager(PathString prefix, RequestDelegate next, SockJSOptions options)
        {
            _prefix = prefix;
            _next = next;
            _options = options;
            _rewritePath = options.RewritePath.HasValue ? options.RewritePath : _prefix;

            _iframeContent = string.Format(s_iFrameTemplate, options.JSClientLibraryUrl);
            _iframeETag = CalculateETag(_iframeContent);

            _routes = new List<Route>(new[]
            {
                new Route("GET", TypeEmpty, false, "", HandleGreetingAsync),
                new Route("GET", TypeTop, true, "iframe[0-9-.a-z_]*.html", HandleIFrameAsync),
                new Route("GET", TypeTop, false, "info", HandleInfoAsync),
                new Route("OPTIONS", TypeTop, false, "info", HandleOptionsGetResourceAsync),
                new Route("GET", TypeSession, false, "jsonp", HandleJsonpAsync),
                new Route("POST", TypeSession, false, "jsonp_send", HandleJsonpSendAsync),
                new Route("POST", TypeSession, false, "xhr", HandleXhrAsync),
                new Route("OPTIONS", TypeSession, false, "xhr", HandleOptionsPostResourceAsync),
                new Route("POST", TypeSession, false, "xhr_send", HandleXhrSendAsync),
                new Route("OPTIONS", TypeSession, false, "xhr_send", HandleOptionsPostResourceAsync),
                new Route("POST", TypeSession, false, "xhr_streaming", HandleXhrStreamingAsync),
                new Route("OPTIONS", TypeSession, false, "xhr_streaming", HandleOptionsPostResourceAsync),
                new Route("GET", TypeSession, false, "eventsource", HandleEventSourceAsync),
                new Route("GET", TypeSession, false, "htmlfile", HandleHtmlFileAsync),
            });
            if (_options.UseWebSocket)
            {
                _routes.AddRange(new[] {
                    new Route("GET", TypeSession, false, "websocket", HandleSockJSWebSocketAsync),
                    new Route("GET", TypeTop, false, "websocket", HandleWebSocketAsync),
                });
            }
            else
            {
                _routes.AddRange(new[] {
                    new Route("GET", TypeSession, false, "websocket", HandleNoWebSocketAsync),
                    new Route("GET", TypeTop, false, "websocket", HandleNoWebSocketAsync),
                });
            }
        }

        private Task HandleNoWebSocketAsync(HttpContext context, string sessionId)
        {
            AddCachingHeader(context);
            return HandleNotFoundAsync(context);
        }

        private Task HandleSockJSWebSocketAsync(HttpContext context, string sessionId)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return ExposeTextAsync(context, "Not a valid websocket request");
            }

            var feature = new SockJSWebSocketFeature(context.GetFeature<IHttpWebSocketFeature>());
            context.SetFeature<IHttpWebSocketFeature>(feature);
            context.Request.Path = _rewritePath;

            return _next(context);
        }

        private Task HandleWebSocketAsync(HttpContext context, string sessionId)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return ExposeTextAsync(context, "Not a valid websocket request");
            }
            context.Request.Path = _rewritePath;
            return _next(context);
        }

        private Task HandleJsonpSendAsync(HttpContext context, string sessionId)
        {
            throw new NotImplementedException();
        }

        private Task HandleJsonpAsync(HttpContext context, string sessionId)
        {
            throw new NotImplementedException();
        }

        private Task HandleEventSourceAsync(HttpContext context, string sessionId)
        {
            throw new NotImplementedException();
        }

        private Task HandleHtmlFileAsync(HttpContext context, string sessionId)
        {
            AddSessionCookie(context);
            AddNoCacheHeader(context);
            AddCorsHeader(context);

            var query = context.Request.Query;

            var htmlFileCallback = query.Get("c") ?? query.Get("callback");

            if (htmlFileCallback == null)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return context.Response.WriteAsync("\"callback\" parameter required");
            }
            if (s_htmlFileCallbackRegex.IsMatch(htmlFileCallback))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return context.Response.WriteAsync("invalid \"callback\" parameter");
            }

            var receiver = new Receiver(context, ReceiverType.HtmlFile, _options.MaxResponseLength, htmlFileCallback);
            return HandleReceiverAsync(sessionId, receiver);
        }

        private async Task HandleXhrSendAsync(HttpContext context, string sessionId)
        {
            AddSessionCookie(context);
            AddNoCacheHeader(context);
            AddCorsHeader(context);

            List<JsonString> messages;
            try
            {
                string mediaType = context.Request.ContentType?.Split(s_contentTypeSplitter)[0];
                if (!s_allowedXhrSendMediaTypes.Contains(mediaType))
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
            if (session == null || !session.IsAccepted)
            {
                await HandleNotFoundAsync(context);
                return;
            }

            session.ClientSend(messages);
            session.ExitSharedLock();

            context.Response.ContentType = "text/plain; charset=UTF-8";
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            await ExposeNothingAsync();
        }

        private Task HandleXhrStreamingAsync(HttpContext context, string sessionId)
        {
            AddSessionCookie(context);
            AddNoCacheHeader(context);
            AddCorsHeader(context);

            var receiver = new Receiver(context, ReceiverType.XhrStreaming, _options.MaxResponseLength, null);
            return HandleReceiverAsync(sessionId, receiver);
        }

        private Task HandleXhrAsync(HttpContext context, string sessionId)
        {
            AddSessionCookie(context);
            AddNoCacheHeader(context);
            AddCorsHeader(context);

            var receiver = new Receiver(context, ReceiverType.XhrPolling, _options.MaxResponseLength, null);
            return HandleReceiverAsync(sessionId, receiver);
        }

        private Session GetSession(string sessionId)
        {
            Session session;
            _sessions.TryGetValue(sessionId, out session);
            if (session != null)
            {
                session.EnterSharedLock();
                Session check;
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
                Session check = _sessions.GetOrAdd(sessionId, newSession);
                if (check == newSession)
                {
                    return Tuple.Create(newSession, true);
                }
            }
        }

        private async Task HandleReceiverAsync(string sessionId, Receiver receiver)
        {
            var getOrCreate = GetOrCreateSession(sessionId, receiver);
            Session session = getOrCreate.Item1;
            bool newSession = getOrCreate.Item2;
            if (newSession)
            {
                TaskCompletionSource<bool> acceptCompletionSource = new TaskCompletionSource<bool>();
                var feature = new SessionWebSocketFeature(acceptCompletionSource, session);
                receiver.Context.SetFeature<IHttpWebSocketFeature>(feature);
                receiver.Context.Request.Path = _rewritePath;

                var wsHandler = Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        await _next(receiver.Context);
                        acceptCompletionSource.TrySetResult(false);
                    }
                    catch
                    {
                        acceptCompletionSource.TrySetResult(false);
                    }
                });

                bool accepted = await acceptCompletionSource.Task;
                if (!accepted)
                {
                    _sessions.TryRemove(sessionId, out session);
                    return;
                }
            }
            else
            {
                bool receiverSet = session.SetReceiver(receiver);
                if (receiverSet)
                {
                    session.CancelSessionTimeout();
                }
                session.ExitSharedLock();
                if (!receiverSet)
                {
                    await receiver.OpenAsync();
                    await receiver.SendCloseAsync(s_otherReceiverCloseMessage, CancellationToken.None);
                    return;
                }
            }
            try
            {
                await session.ClientReceiveAsync();
            }
            finally
            {
                // schedule before clear to ensure cancel is possible when receiverSet
                session.ScheduleSessionTimeout(session, OnSessionTimeout, _options.DisconnectTimeout);
                session.ClearReceiver();
            }
        }

        private void OnSessionTimeout(object state, CancellableTimer timer)
        {
            Session session = (Session)state;
            try
            {
                session.EnterExclusiveLock();
                if (timer.IsCancelled)
                {
                    return;
                }
                _sessions.TryRemove(session.SessionId, out session);
                session.HandleClientTimeOut();
            }
            finally
            {
                session.ExitExclusiveLock();
            }
        }

        private Task HandleOptionsGetResourceAsync(HttpContext context, string session)
        {
            AddSessionCookie(context);
            AddCorsHeader(context);
            AddCachingHeader(context);

            context.Response.Headers.Add(CorsConstants.AccessControlAllowMethods, s_optionsGetAllowedMethods);
            context.Response.Headers.Add(CorsConstants.AccessControlMaxAge, s_oneYearAccessControlMaxAge);

            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return ExposeNothingAsync();
        }

        private Task HandleOptionsPostResourceAsync(HttpContext context, string session)
        {
            AddSessionCookie(context);
            AddCorsHeader(context);
            AddCachingHeader(context);

            context.Response.Headers.Add(CorsConstants.AccessControlAllowMethods, s_optionsPostAllowedMethods);
            context.Response.Headers.Add(CorsConstants.AccessControlMaxAge, s_oneYearAccessControlMaxAge);

            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return ExposeNothingAsync();
        }

        private Task ExposeNothingAsync()
        {
            return Task.FromResult(true);
        }

        private void AddSessionCookie(HttpContext context)
        {
            if (_options.SetJSessionIDCookie)
            {
                var jsid = context.Request.Cookies["JSESSIONID"] ?? "dummy";
                context.Response.Headers.Add(HeaderNames.SetCookie, new[] { "JSESSIONID=" + jsid + "; path=/" });
            }
        }

        private Task HandleInfoAsync(HttpContext context, string session)
        {
            AddNoCacheHeader(context);
            AddCorsHeader(context);
            int entropy;
            lock (s_random)
            {
                entropy = s_random.Next();
            }
            string info =
                string.Format(@"{{""websocket"":{0},""origins"":[""*:*""],""cookie_needed"":{1},""entropy"":{2}}}",
                        (_options.UseWebSocket ? "true" : "false"),
                        (_options.SetJSessionIDCookie ? "true" : "false"),
                        entropy
                        );
            return ExposeJsonAsync(context, info);
        }

        private Task HandleIFrameAsync(HttpContext context, string session)
        {
            if (_iframeETag.Equals(context.Request.Headers.Get(HeaderNames.IfNoneMatch)))
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                return Task.FromResult(true);
            }
            AddCachingHeader(context);
            context.Response.Headers.Add(HeaderNames.ETag, new[] { _iframeETag });
            return ExposeHtmlAsync(context, _iframeContent);
        }

        private void AddCachingHeader(HttpContext context)
        {
            context.Response.Headers.Add(HeaderNames.CacheControl, s_oneYearCacheCacheControlValue);
            context.Response.Headers.Add(HeaderNames.Expires, new[] { DateTime.UtcNow.AddYears(1).ToString("R") });
        }

        private Task ExposeHtmlAsync(HttpContext context, string content)
        {
            return ExposeAsync(context, "text/html", content);
        }

        private Task ExposeTextAsync(HttpContext context, string content)
        {
            return ExposeAsync(context, "text/plain", content);
        }

        private Task ExposeJsonAsync(HttpContext context, string content)
        {
            return ExposeAsync(context, "application/json", content);
        }

        private Task ExposeAsync(HttpContext context, string contentType, string content)
        {
            context.Response.ContentType = contentType + ";charset=UTF-8";

            byte[] data = Encoding.UTF8.GetBytes(content);
            context.Response.ContentLength = data.Length;

            return context.Response.Body.WriteAsync(data, 0, data.Length);
        }

        private Task HandleGreetingAsync(HttpContext context, string session)
        {
            return ExposeTextAsync(context, "Welcome to SockJS!\n");
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

        private bool Matches(Match match, Route route)
        {
            if ((match.Groups.Count == (route.Type + 1)) ||
                ((match.Groups.Count == 4) && (route.Type == TypeSession)))
            {
                string last = match.Groups.Count > 1 ? match.Groups[match.Groups.Count - 1].Value : string.Empty;
                if (((route.Raw != null) && (route.Raw.Equals(last, StringComparison.Ordinal))) ||
                    ((route.RegEx != null) && route.RegEx.IsMatch(last)))
                {
                    return true;
                }
            }
            return false;
        }

        public Task Invoke(HttpContext context)
        {
            PathString path = context.Request.Path;
            if (!path.StartsWithSegments(_prefix, out path))
            {
                return _next(context);
            }
            Match match = null;
            for (int i = 0; ((i < s_pathRegex.Length) && (match == null)); i++)
            {
                match = s_pathRegex[i].Match(path.Value);
                if (!match.Success)
                {
                    match = null;
                }
            }
            bool pathMatch = false;
            if (match != null)
            {
                foreach (var route in _routes)
                {
                    if (Matches(match, route))
                    {
                        pathMatch = true;
                        if (context.Request.Method.Equals(route.Method, StringComparison.OrdinalIgnoreCase))
                        {
                            string session = null;
                            if (route.Type == TypeSession)
                            {
                                session = match.Groups[2].Value;
                            }
                            return route.Handler(context, session);
                        }
                    }
                }
            }
            if (pathMatch)
            {
                string methods = string.Join(", ", _routes.Where(route => Matches(match, route)).Select(route => route.Method));
                return HandleNotAllowedAsync(context, methods);
            }
            return HandleNotFoundAsync(context);
        }

        private Task HandleNotAllowedAsync(HttpContext context, string methods)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers.Add(HeaderNames.Allow, new[] { methods });
            return ExposeNothingAsync();
        }

        private Task HandleNotFoundAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return ExposeNothingAsync();
        }

        private void AddNoCacheHeader(HttpContext context)
        {
            context.Response.Headers.Add(HeaderNames.CacheControl, s_noCacheCacheControlValue);
        }

        private void AddCorsHeader(HttpContext context)
        {
            var request = context.Request;
            var response = context.Response;
            var requestOrigin = request.Headers[CorsConstants.Origin];
            if (requestOrigin != null)
            {
                response.Headers.Add(CorsConstants.AccessControlAllowCredentials, s_trueValue);
            }
            var origin = requestOrigin ?? CorsConstants.AnyOrigin;
            response.Headers.Add(CorsConstants.AccessControlAllowOrigin, new[] { origin });
            response.Headers.Add(HeaderNames.Vary, s_corsVaryValue);
            var requestHeaders = request.Headers[CorsConstants.AccessControlRequestHeaders];
            if (!string.IsNullOrEmpty(requestHeaders))
            {
                response.Headers.Add(CorsConstants.AccessControlAllowHeaders, new[] { requestHeaders });
            }
        }
    }
}
