using System.Net;
using System.Text;

namespace Gondwana.Tests.GondwanaMcp;

internal sealed class TestHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responder)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(responder(request));

    internal static HttpResponseMessage Json(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };

    internal static HttpResponseMessage Text(
        string text,
        string mediaType = "text/plain",
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(
                text,
                Encoding.UTF8,
                mediaType)
        };
}
