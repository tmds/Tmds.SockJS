# Tmds.SockJS
Tmds.SockJS is a ASP.NET 5 server implementation of the SockJS Protocol. The library maintains the standard ASP.NET Websocket interface even when websockets are emulated.

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
