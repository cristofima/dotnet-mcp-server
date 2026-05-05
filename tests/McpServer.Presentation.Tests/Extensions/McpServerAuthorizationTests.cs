using System.Net;
using System.Text;
using System.Text.Json;
using McpServer.Domain.Constants;
using McpServer.Presentation.Tests.Helpers;
using Xunit;

namespace McpServer.Presentation.Tests.Extensions;

/// <summary>
/// Integration tests that verify MCP tool registration, role-based authorization filtering,
/// and prompt visibility. Starts a real Kestrel server per test with fake authentication
/// that injects a ClaimsPrincipal via middleware (following the MCP SDK's own test pattern).
/// </summary>
public sealed class McpServerAuthorizationTests
{
    private static readonly string[] AllRoles =
    [
        Permissions.TASK_READ,
        Permissions.TASK_WRITE,
        Permissions.BALANCE_READ,
        Permissions.BALANCE_WRITE,
        Permissions.PROJECT_READ,
        Permissions.PROJECT_WRITE,
        Permissions.ADMIN_ACCESS,
    ];

    // --- Tool Registration ---

    [Fact]
    public async Task ListTools_WithAllRoles_Returns_AllEightTools()
    {
        await using var env = await StartServerAsync("allroles-user", AllRoles, TestContext.Current.CancellationToken);

        var tools = await env.Client!.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(8, tools.Count);
    }

    // --- Tool Authorization Filtering ---

    [Fact]
    public async Task ListTools_WithTaskReadOnly_Returns_OnlyGetTasks()
    {
        await using var env = await StartServerAsync("reader", [Permissions.TASK_READ], TestContext.Current.CancellationToken);

        var tools = await env.Client!.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(tools);
        Assert.Equal("get_tasks", tools[0].Name);
    }

    [Fact]
    public async Task ListTools_WithNoRoles_Returns_NoTools()
    {
        await using var env = await StartServerAsync("noroles-user", [], TestContext.Current.CancellationToken);

        var tools = await env.Client!.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(tools);
    }

    public static TheoryData<string, int, string[]> RoleToolMappings => new()
    {
        { Permissions.TASK_WRITE, 3, ["create_task", "delete_task", "update_task_status"] },
        { Permissions.PROJECT_READ, 2, ["get_project_details", "get_projects"] },
        { Permissions.BALANCE_READ, 1, ["get_project_balance"] },
        { Permissions.ADMIN_ACCESS, 1, ["get_backend_users"] },
    };

    [Theory]
    [MemberData(nameof(RoleToolMappings))]
    public async Task ListTools_PerRole_Returns_CorrectToolSubset(
        string role, int expectedCount, string[] expectedToolNames)
    {
        await using var env = await StartServerAsync("user", [role], TestContext.Current.CancellationToken);

        var tools = await env.Client!.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expectedCount, tools.Count);
        var actualNames = tools.Select(t => t.Name).Order().ToArray();
        Assert.Equal(expectedToolNames, actualNames);
    }

    // --- Tool Invocation Authorization ---

    [Fact]
    public async Task CallTool_WithCorrectRole_Returns_SuccessResponse()
    {
        await using var env = await StartServerAsync("reader", [Permissions.TASK_READ], TestContext.Current.CancellationToken);

        var result = await env.Client!.CallToolAsync("get_tasks", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task CallTool_WithWrongRole_IsRejected()
    {
        await using var env = await StartServerAsync("reader", [Permissions.TASK_READ], TestContext.Current.CancellationToken);

        // get_backend_users requires ADMIN_ACCESS, not TASK_READ
        await Assert.ThrowsAnyAsync<Exception>(
            () => env.Client!.CallToolAsync("get_backend_users", cancellationToken: TestContext.Current.CancellationToken).AsTask());
    }

    // --- Prompt Authorization Filtering ---

    [Fact]
    public async Task ListPrompts_WithAllRoles_Returns_AllSixPrompts()
    {
        await using var env = await StartServerAsync("allroles-user", AllRoles, TestContext.Current.CancellationToken);

        var prompts = await env.Client!.ListPromptsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(6, prompts.Count);
    }

    [Fact]
    public async Task ListPrompts_WithTaskReadOnly_Returns_OnlyTaskPrompts()
    {
        await using var env = await StartServerAsync("reader", [Permissions.TASK_READ], TestContext.Current.CancellationToken);

        var prompts = await env.Client!.ListPromptsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, prompts.Count);
        var names = prompts.Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "analyze_task_priorities", "summarize_tasks" }, names);
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
    public async Task PostOnlyFlow_InitializeAndListTools_ReturnsExpectedToolSubset()
    {
        await using var env = await StartServerAsync("copilot-user", [Permissions.TASK_READ], TestContext.Current.CancellationToken);

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
        Assert.Single(tools.EnumerateArray().ToList());
        Assert.Equal("get_tasks", tools[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetPlusPostFlow_InitializeAndListTools_ReturnsExpectedToolSubset()
    {
        await using var env = await StartServerAsync("vscode-user", [Permissions.TASK_READ], TestContext.Current.CancellationToken);

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
        Assert.Single(tools.EnumerateArray().ToList());
        Assert.Equal("get_tasks", tools[0].GetProperty("name").GetString());
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
