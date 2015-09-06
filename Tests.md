## Tests

This table maps the [sockjs-protocol tests](https://github.com/sockjs/sockjs-protocol) to the tests in Tmds.SockJSTests.

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
