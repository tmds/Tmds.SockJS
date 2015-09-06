# Tmds.SockJS
Tmds.SockJS is an ASP.NET 5 implementation of the SockJS protocol. The library maintains the standard ASP.NET WebSocket interface even when websockets are emulated.

AppVeyor: [![AppVeyor](https://ci.appveyor.com/api/projects/status/kpmtd98p5p4x1bd0?svg=true)](https://ci.appveyor.com/project/tmds/tmds-sockjs/branch/master)

## Description

### Tmds.SockJS

From: https://github.com/sockjs/sockjs-client 
> SockJS is a browser JavaScript library that provides a WebSocket-like object. SockJS gives you a coherent, cross-browser, Javascript API which creates a low latency, full duplex, cross-domain communication channel between the browser and the web server.

Tmds.SockJS enables SockJS on ASP.NET5. It can be installed added as a middleware and requires no change to the WebSocket implementation. For example, if the server provides an standard websocket endpoint at '/websocket', a SockJS endpoint can be added at '/sockjs' with this one-liner:
```C#
app.UseSockJS("/sockjs", new SockJSOptions() { RewritePath = "/websocket" });
```

Two features of RFC6455 are not supported by SockJS (and thus by SockJS.Tmds):
- Splitting a message into several sends (WebSocket.SendAsync: endOfMessage)
- Sending binary messages (WebSocket.SendAsync: messageType)

In practice this means: send operations are done using strings. This is okay for a lot of use-cases.

### Tmds.WebSockets.Sources

This source package contains a number of WebSocket extension methods. It can be used independent of Tmds.SockJS.

These are the most interesting methods:
```C#
Task SendAsync(string)
Task<string> ReceiveTextAsync() // returns 'null' when peer closed the WebSocket

Task SendCloseAsync()
Task ReceiveCloseAsync()
```

## Example

This example implements an 'echo' websocket service. The SockJS endpoint for the service is at the '/echo' path.

In *project.json* add Tmds.SockJS and Tmds.WebSockets.Sources to the dependencies:
```JSON
	"dependencies": {
		"Tmds.SockJS": "",
		"Tmds.WebSockets.Sources": "",
	},
```

Inside the *Startup.cs* Configure-method we setup the SockJS end-point and implement the echo service:
```C#
public void Configure(IApplicationBuilder app)
{
	// from Microsoft.AspNet.WebSockets.Server
	app.UseWebSockets();
	
	// add a SockJS end point to the website
	app.UseSockJS("/echo");

	// routing logic
	app.Use(async (context, next) =>
	{
		if (context.Request.Path == "/echo")
		{
			// use the standard WebSocket API to handle websocket connections
			var ws = await context.WebSockets.AcceptWebSocketAsync();
			while (true)
			{
				string received = await ws.ReceiveTextAsync();
				if (received == null)
				{
					break;
				}
				await ws.SendAsync(received);
			}
		}
		else
		{
			// not for us to handle
			await next();
		}
	});

	// other application logic
	app.Run(async (context) =>
	{
		await context.Response.WriteAsync("Hello World!");
	});
}
```

## Supported Browsers

The tables below show what browsers are supported using sockjs-client and Tmds.SockJS.
~~Strikethrough~~ is used to indicate the technique is not supported, alltough sockjs-client supports it.
Techniques in the Streaming and Polling column are implemented in Tmds.SockJS.
Techniques in the Websockets column are implemented in Microsoft.AspNet.WebSockets.

### http/https

_Browser_       | _Websockets_     | _Streaming_ | _Polling_
----------------|------------------|-------------|-------------------
~~IE 6, 7~~        | no               | no          | ~~jsonp-polling~~
~~IE 8, 9 (cookies=no)~~ |    no       |  ~~xdr-streaming &dagger;~~ |  ~~xdr-polling~~
IE 8, 9 (cookies=yes)|    no       | iframe-htmlfile | iframe-xhr-polling
IE 10           | rfc6455          | xhr-streaming   | xhr-polling
Chrome 6-13     | ~~hixie-76~~         | xhr-streaming   | xhr-polling
Chrome 14+      | rfc6455 / ~~hybi-10~~ | xhr-streaming   | xhr-polling
Firefox <10     | no               | xhr-streaming   | xhr-polling
Firefox 10+     | rfc6455 / ~~hybi-10~~ | xhr-streaming   | xhr-polling
Safari 5        | ~~hixie-76~~         | xhr-streaming   | xhr-polling
Opera 10.70+    | no               | ~~iframe-eventsource~~ | iframe-xhr-polling
~~Konqueror~~       | no               | no          | ~~jsonp-polling~~

## Test Coverage

_sockjs-protocol_ | _Tmds.SockJS_ | _Comments_
-------------------- | ---------------- | --------
BaseUrlGreeting.test_greeting | BaseUrlGreetingTest.TestGreeting | 
BaseUrlGreeting.test_notFound | BaseUrlGreetingTest.TestNotFound | 
IframePage.test_simpleUrl | IFramePageTest.SimpleUrl | 
IframePage.test_versionedUrl | IFramePageTest.VersionedUrl | 
IframePage.test_queriedUrl | IFramePageTest.QueriedUrl | 
IframePage.test_invalidUrl | IFramePageTest.InvalidUrl | 
IframePage.test_cacheability | IFramePageTest.Cacheability | 
InfoTest.test_basic | InfoTest.Basic | 
InfoTest.test_entropy | InfoTest.Entropy | 
InfoTest.test_options | InfoTest.Options | 
InfoTest.test_options_null_origin |  | https://github.com/sockjs/sockjs-node/issues/177
InfoTest.test_disabled_websocket | InfoTest.DisabledNullOrigin | 
SessionURLs.test_anyValue | SessionUrlsTest.AnyValue | 
SessionURLs.test_invalidPaths | SessionUrlsTest.InvalidPaths | 
SessionURLs.test_ignoringServerId | SessionUrlTest.IgnoringServerId | 
Protocol.test_simpleSession | ProtocolTest.SimpleSession | 
Protocol.test_closeSession | ProtocolTest.CloseSession | 
WebsocketHttpErrors.test_httpMethod | WebSocketHttpErrorsTest.Method | 
WebsocketHttpErrors.test_invalidConnectionHeader | WebSocketHttpErrorsTest.InvalidConnectionHeader | 
WebsocketHttpErrors.test_invalidMethod | WebSocketHttpErrorsTest.InvalidMethod | 
WebsocketHixie76.* | notimplemented | Not supported by ASP.NET stack
WebsocketHybi10.* | notimplemented |  Not supported by ASP.NET stack
XhrPolling.test_options | XhrPollingTest.Options | 
XhrPolling.test_transport | XhrPollingTest.Transport | 
XhrPolling.test_invalid_session | XhrPollingTest.InvalidSession | 
XhrPolling.test_invalid_json | XhrPollingTest.InvalidJson | 
XhrPolling.test_content_types | XhrPollingTest.ContentTypes | Content Types "", "T", and explicit charset not tested
XhrPolling.test_request_headers_cors | XhrPollingTest.RequestHeadersCors | 
XhrPolling.test_sending_empty_frame | XhrPollingTest.SendingEmptyFrame | 
 | XhrPollingTest.SendingEmptyText | 
XhrStreaming.test_options | XhrStreamingTest.Options | 
XhrStreaming.test_transport | XhrStreamingTest.Transport | 
XhrStreaming.test_response_limit | XhrStreamingTest.ResponseLimit | 
EventSource.* | notimplemented | 
HtmlFile.test_transport | HtmlFileTest.Transport | 
HtmlFile.test_no_callback | HtmlFileTest.NoCallback | 
HtmlFile.test_invalid_callback | HtmlFileTest.InvalidCallback | 
HtmlFile.test_response_limit | HtmlFileTest.Transport | 
JsonPolling.* | notimplemented | 
JsessionidCookie.test_basic | JSessionIDCookieTest.Basic | 
JsessionidCookie.test_xhr | JSessionIDCookieTest.Xhr | 
JsessionidCookie.test_xhr_streaming | JSessionIDCookieTest.XhrStreaming | 
JsessionidCookie.test_eventsource | notimplemented | 
JsessionidCookie.test_htmlfile | JSessionIDCookieTest.HtmlFile | 
JsessionidCookie.test_jsonp | notimplemented | 
RawWebsocket.test_transport | | 
RawWebsocket.test_close | | 
JSONEncoding.test_xhr_server_encodes | JsonEncodingTest.ServerEncodes | 
JSONEncoding.test_xhr_server_decodes | JsonEncodingTest.ServerDecodes | 
HandlingClose.test_close_frame | HandlingCloseTest.CloseFrame | 
HandlingClose.test_close_request | HandlingCloseTest.CloseRequest | 
HandlingClose.test_abort_xhr_streaming | | 
HandlingClose.test_abort_xhr_polling | | 
Http10.test_synchronous | wonttest | 
Http10.test_streaming | wonttest | 
Http11.test_synchronous | wonttest | 
Http11.test_streaming | wonttest | 
 | ReaderWriterTest.Reader | 
 | ReaderWriterTest.SingleByteOverflow | 
 | ReaderWriterTest.MultiByteOverflow | 


## Alternatives

### SignalR

ASP.NET SignalR (http://signalr.net/) includes a mechanism for websocket emulation just like SockJS. SignalR builds on top of that to provide a bi-directional remote procedure call (RPC) channel between the client and the server. Both the cliend and server use the SignalR API. If you don't need control over the WebSocket subprotocol: use SignalR instead of SockJS.
