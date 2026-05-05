using System.Net;

namespace McpServer.Infrastructure.Tests.Helpers;

/// <summary>
/// Minimal fake HttpMessageHandler that captures the last outbound HTTP request
/// and returns a configurable response.
/// Used across Infrastructure tests to intercept calls from DownstreamApiService
/// without making real network connections.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private string _responseBody = "{}";
    private HttpStatusCode _statusCode = HttpStatusCode.OK;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public void SetResponse(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseBody = body;
        _statusCode = statusCode;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;

        // Read body eagerly — caller may dispose Content via using statement
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
