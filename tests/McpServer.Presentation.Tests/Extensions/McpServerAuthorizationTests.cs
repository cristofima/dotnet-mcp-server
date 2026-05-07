using System.Net;
using System.Text;
using System.Text.Json;
using McpServer.Presentation.Tests.Helpers;
using Xunit;

namespace McpServer.Presentation.Tests.Extensions;

/// <summary>
/// Integration tests that verify MCP tool registration, authentication enforcement,
/// and prompt visibility. Authorization is authentication-only — any authenticated user
/// can see and invoke all tools and prompts.
/// Starts a real Kestrel server per test with fake authentication
/// that injects a ClaimsPrincipal via middleware (following the MCP SDK's own test pattern).
/// </summary>
public sealed class McpServerAuthorizationTests
{
    private const int ExpectedToolCount = 8;
    private const int ExpectedPromptCount = 4;

    // --- Tool Registration ---

    [Fact]
    public async Task ListTools_Authenticated_Returns_AllTools()
    {
        await using var env = await StartServerAsync("user", cancellationToken: TestContext.Current.CancellationToken);

        var tools = await env.Client!.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedToolCount, tools.Count);
    }

    [Fact]
    public async Task ListTools_Authenticated_Includes_TransferBudget()
    {
        await using var env = await StartServerAsync("user", cancellationToken: TestContext.Current.CancellationToken);

        var tools = await env.Client!.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(tools, t => t.Name == "transfer_budget");
    }

    [Fact]
    public async Task ListTools_Authenticated_Includes_AllExpectedToolNames()
    {
        await using var env = await StartServerAsync("user", cancellationToken: TestContext.Current.CancellationToken);

        var tools = await env.Client!.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var names = tools.Select(t => t.Name).Order().ToArray();

        Assert.Equal(
            new[]
            {
                "create_task", "delete_task", "get_project_balance",
                "get_project_details", "get_projects", "get_tasks", "transfer_budget", "update_task_status"
            },
            names);
    }

    // --- Tool Invocation ---

    [Fact]
    public async Task CallTool_Authenticated_Returns_SuccessResponse()
    {
        await using var env = await StartServerAsync("user", cancellationToken: TestContext.Current.CancellationToken);

        var result = await env.Client!.CallToolAsync("get_tasks", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
    }

    // --- Prompt Visibility ---

    [Fact]
    public async Task ListPrompts_Authenticated_Returns_AllPrompts()
    {
        await using var env = await StartServerAsync("user", cancellationToken: TestContext.Current.CancellationToken);

        var prompts = await env.Client!.ListPromptsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedPromptCount, prompts.Count);
    }

    // --- Unauthenticated Access ---

    [Fact]
    public async Task Unauthenticated_PostToMcp_Returns_Unauthorized()
    {
        await using var env = await StartServerAsync(userName: null, cancellationToken: TestContext.Current.CancellationToken);

        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(
            $"{env.Address}/mcp",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/sse")]
    [InlineData("/message")]
    [InlineData("/mcp/sse")]
    [InlineData("/mcp/message")]
    public async Task LegacySseEndpoints_AreNotExposed_Returns_NotFound(string path)
    {
        await using var env = await StartServerAsync(userName: null, cancellationToken: TestContext.Current.CancellationToken);

        var method = path.EndsWith("sse", StringComparison.OrdinalIgnoreCase)
            ? HttpMethod.Get
            : HttpMethod.Post;

        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(method, $"{env.Address}{path}");
        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOnlyFlow_InitializeAndListTools_ReturnsAllTools()
    {
        await using var env = await StartServerAsync("copilot-user", cancellationToken: TestContext.Current.CancellationToken);

        using var httpClient = new HttpClient();

        var initializeResponse = await SendJsonRpcRequestAsync(
            httpClient,
            env.Address,
            "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"post-only-test\",\"version\":\"1.0\"}}}",
            "application/json, text/event-stream",
            mcpSessionId: null,
            TestContext.Current.CancellationToken);

        await AssertSuccessAsync(initializeResponse, TestContext.Current.CancellationToken);
        var initializeResult = await ReadJsonRpcResultAsync(initializeResponse, TestContext.Current.CancellationToken);
        Assert.True(initializeResult.TryGetProperty("protocolVersion", out _));

        initializeResponse.Headers.TryGetValues("Mcp-Session-Id", out var sessionHeaderValues);
        var mcpSessionId = sessionHeaderValues?.FirstOrDefault();

        var listToolsResponse = await SendJsonRpcRequestAsync(
            httpClient,
            env.Address,
            "{\"jsonrpc\":\"2.0\",\"id\":\"2\",\"method\":\"tools/list\",\"params\":{}}",
            "application/json, text/event-stream",
            mcpSessionId,
            TestContext.Current.CancellationToken);

        await AssertSuccessAsync(listToolsResponse, TestContext.Current.CancellationToken);
        var listToolsResult = await ReadJsonRpcResultAsync(listToolsResponse, TestContext.Current.CancellationToken);
        Assert.True(listToolsResult.TryGetProperty("tools", out var tools));
        Assert.Equal(JsonValueKind.Array, tools.ValueKind);
        Assert.Equal(ExpectedToolCount, tools.EnumerateArray().Count());
    }

    [Fact]
    public async Task GetPlusPostFlow_InitializeAndListTools_ReturnsAllTools()
    {
        await using var env = await StartServerAsync("vscode-user", cancellationToken: TestContext.Current.CancellationToken);

        using var httpClient = new HttpClient();

        var initializeResponse = await SendJsonRpcRequestAsync(
            httpClient,
            env.Address,
            "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"get-post-test\",\"version\":\"1.0\"}}}",
            "application/json, text/event-stream",
            mcpSessionId: null,
            TestContext.Current.CancellationToken);

        await AssertSuccessAsync(initializeResponse, TestContext.Current.CancellationToken);
        var initializeResult = await ReadJsonRpcResultAsync(initializeResponse, TestContext.Current.CancellationToken);
        Assert.True(initializeResult.TryGetProperty("serverInfo", out _));

        initializeResponse.Headers.TryGetValues("Mcp-Session-Id", out var sessionHeaderValues);
        var mcpSessionId = sessionHeaderValues?.FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(mcpSessionId));

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"{env.Address}/mcp");
        getRequest.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
        getRequest.Headers.TryAddWithoutValidation("Mcp-Session-Id", mcpSessionId);

        using var getResponse = await httpClient.SendAsync(
            getRequest,
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken);

        await AssertSuccessAsync(getResponse, TestContext.Current.CancellationToken);

        var listToolsResponse = await SendJsonRpcRequestAsync(
            httpClient,
            env.Address,
            "{\"jsonrpc\":\"2.0\",\"id\":\"2\",\"method\":\"tools/list\",\"params\":{}}",
            "application/json, text/event-stream",
            mcpSessionId,
            TestContext.Current.CancellationToken);

        await AssertSuccessAsync(listToolsResponse, TestContext.Current.CancellationToken);
        var listToolsResult = await ReadJsonRpcResultAsync(listToolsResponse, TestContext.Current.CancellationToken);
        Assert.True(listToolsResult.TryGetProperty("tools", out var tools));
        Assert.Equal(JsonValueKind.Array, tools.ValueKind);
        Assert.Equal(ExpectedToolCount, tools.EnumerateArray().Count());
    }

    // --- Server Setup ---

    private static async Task<HttpResponseMessage> SendJsonRpcRequestAsync(
        HttpClient httpClient,
        string address,
        string payload,
        string accept,
        string? mcpSessionId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{address}/mcp");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        request.Headers.TryAddWithoutValidation("Accept", accept);

        if (!string.IsNullOrWhiteSpace(mcpSessionId))
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", mcpSessionId);
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task<JsonElement> ReadJsonRpcResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonPayload = ExtractJsonPayload(payload);

        using var document = JsonDocument.Parse(jsonPayload);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("result", out var result));
        return result.Clone();
    }

    private static string ExtractJsonPayload(string payload)
    {
        var trimmed = payload.Trim();
        if (trimmed.StartsWith('{'))
        {
            return trimmed;
        }

        var sseDataLines = trimmed
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
            .Select(line => line["data:".Length..].Trim())
            .Where(line => line.StartsWith('{'))
            .ToList();

        Assert.NotEmpty(sseDataLines);
        return sseDataLines[0];
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected successful response but received {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
    }

    private static Task<TestServerEnvironment> StartServerAsync(
        string? userName, string[]? roles = null,
        CancellationToken cancellationToken = default)
        => TestServerBuilder.StartAsync(userName, roles, cancellationToken: cancellationToken);
}


