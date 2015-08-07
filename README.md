# Tmds.SockJS
Tmds.SockJS is an ASP.NET 5 implementation of the SockJS protocol. The library maintains the standard ASP.NET WebSocket interface even when websockets are emulated.

AppVeyor: [![AppVeyor](https://ci.appveyor.com/api/projects/status/kpmtd98p5p4x1bd0?svg=true)](https://ci.appveyor.com/project/tmds/tmds-sockjs/branch/master)

## Example

This example implements an 'echo' websocket service. The SockJS endpoint for the service is at the '/echo' path.

In *project.json* add Tmds.SockJS and Tmds.WebSockets to the dependencies:

	"dependencies": {
		...
		"Tmds.SockJS": "0.3.0",
		"Tmds.WebSockets": "1.0.0",
		...
	},

Inside the *Startup.cs* Configure-method we setup the SockJS end-point and implement the echo service:

	public void Configure(IApplicationBuilder app)
	{
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

**note** The Tmds.WebSockets library provides the WebSocket ReceiveTextAsync and SendAsync extension methods used in this example. This library can be used independent of Tmds.SockJS.

## Test Coverage

sockjs-protocol test | Tmds.SockJS Test | comments
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
WebsocketHttpErrors.test_httpMethod | | 
WebsocketHixie76.* | notimplemented | Not supported by ASP.NET stack
WebsocketHybi10.* | notimplemented |  Not supported by ASP.NET stack
XhrPolling.test_options | XhrPollingTest.Options | 
XhrPolling.test_transport | XhrPollingTest.Transport | 
XhrPolling.test_invalid_session | XhrPollingTest.InvalidSession | 
XhrPolling.test_invalid_json | XhrPollingTest.InvalidJson | 
XhrPolling.test_content_types | XhrPollingTest.ContentTypes | Content Types "", "T", and explicit charset not tested
XhrPolling.test_request_headers_cors | XhrPollingTest.RequestHeadersCors | 
XhrPolling.test_sending_empty_frame | | 
XhrStreaming.test_options | XhrStreamingTest.Options | 
XhrStreaming.test_transport | XhrStreamingTest.Transport | 
XhrStreaming.test_response_limit | XhrStreamingTest.ResponseLimit | 
EventSource.* | notimplemented | 
HtmlFile.test_transport | HtmlFileTest.Transport | 
HtmlFile.test_no_callback | HtmlFileTest.NoCallback | 
HtmlFile.test_invalid_callback | HtmlFileTest.InvalidCallback | 
HtmlFile.test_response_limit | HtmlFileTest.Transport | 
JsonPolling.* | notimplemented | 
JsessionidCookie.test_basic | | 
JsessionidCookie.test_xhr | | 
JsessionidCookie.test_xhr_streaming | | 
JsessionidCookie.test_eventsource | notimplemented | 
JsessionidCookie.test_htmlfile | | 
JsessionidCookie.test_jsonp | notimplemented | 
RawWebsocket.test_transport | | 
RawWebsocket.test_close | | 
JSONEncoding.test_xhr_server_encodes | | 
JSONEncoding.test_xhr_server_decodes | | 
HandlingClose.test_close_frame | | 
HandlingClose.test_close_request | | 
HandlingClose.test_abort_xhr_streaming | | 
HandlingClose.test_abort_xhr_polling | | 
Http10.test_synchronous | wonttest | 
Http10.test_streaming | wonttest | 
Http11.test_synchronous | wonttest | 
Http11.test_streaming | wonttest | 
 | ReaderWriterTest.Reader | Test more