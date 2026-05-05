using Microsoft.AspNetCore.Builder;
using ModelContextProtocol.Client;

namespace McpServer.Presentation.Tests.Helpers;

/// <summary>
/// Disposable wrapper around a test WebApplication and optional MCP client.
/// Ensures both are cleaned up after each test.
/// </summary>
internal sealed class TestServerEnvironment(
    WebApplication app,
    McpClient? client,
    string address) : IAsyncDisposable
{
    public McpClient? Client { get; } = client;
    public string Address { get; } = address;

    public async ValueTask DisposeAsync()
    {
        if (Client is not null)
        {
            await Client.DisposeAsync();
        }

        await app.DisposeAsync();
    }
}
