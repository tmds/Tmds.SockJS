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

## Alternatives

### SignalR

ASP.NET SignalR (http://signalr.net/) includes a mechanism for websocket emulation just like SockJS. SignalR builds on top of that to provide a bi-directional remote procedure call (RPC) channel between the client and the server. Both the cliend and server use the SignalR API. If you don't need control over the WebSocket subprotocol: use SignalR instead of SockJS.
