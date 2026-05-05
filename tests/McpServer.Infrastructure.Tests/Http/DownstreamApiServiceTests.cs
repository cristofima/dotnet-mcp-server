using System.Net;
using System.Text.Json;
using McpServer.Application.Abstractions;
using McpServer.Application.Configuration;
using McpServer.Infrastructure.Http;
using McpServer.Infrastructure.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Infrastructure.Tests.Http;

/// <summary>
/// Tests that DownstreamApiService routes requests correctly, performs OBO token exchange,
/// and attaches the exchanged token to downstream calls.
/// Uses a fake HttpMessageHandler to intercept all outbound HTTP.
/// </summary>
public sealed class DownstreamApiServiceTests : IDisposable
{
    private readonly Mock<ITokenExchangeService> _tokenExchange = new();
    private readonly FakeHttpHandler _httpHandler = new();
    private readonly HttpClient _httpClient;

    private const string ExchangedToken = "exchanged-obo-token";
    private const string UserBearerToken = "user-jwt-token";

    public DownstreamApiServiceTests()
    {
        _httpClient = new HttpClient(_httpHandler)
        {
            BaseAddress = new Uri("https://mock-api.local"),
        };

        _tokenExchange
            .Setup(x => x.ExchangeTokenAsync(UserBearerToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExchangedToken);
    }

    public void Dispose() => _httpClient.Dispose();

    private DownstreamApiService CreateService(string? bearerToken = UserBearerToken)
    {
        var httpContextAccessor = CreateHttpContextAccessor(bearerToken);
        var options = Options.Create(new DownstreamApiOptions
        {
            BaseUrl = "https://mock-api.local",
            Audience = "api://mock-api",
            Scopes = ["api://mock-api/.default"],
        });

        var tokenProvider = new ApiTokenProvider(
            httpContextAccessor,
            _tokenExchange.Object,
            options,
            NullLogger<ApiTokenProvider>.Instance);

        return new DownstreamApiService(
            _httpClient,
            tokenProvider,
            NullLogger<DownstreamApiService>.Instance);
    }

    // --- OBO Token Exchange Tests ---

    [Fact]
    public async Task GetProjectsAsync_Attaches_ExchangedOboToken()
    {
        _httpHandler.SetResponse("""[{"id": "P1", "name": "Alpha"}]""");
        var service = CreateService();

        await service.GetProjectsAsync(CancellationToken.None);

        var request = _httpHandler.LastRequest!;
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(ExchangedToken, request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetProjectsAsync_Calls_TokenExchange_WithUserToken()
    {
        _httpHandler.SetResponse("[]");
        var service = CreateService();

        await service.GetProjectsAsync(CancellationToken.None);

        _tokenExchange.Verify(
            x => x.ExchangeTokenAsync(UserBearerToken, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTasksAsync_WithMissingBearerToken_StillSendsRequest()
    {
        _httpHandler.SetResponse("[]");
        var service = CreateService(bearerToken: null);

        // Should not throw — the client logs a warning but still sends the request
        var result = await service.GetTasksAsync(CancellationToken.None);
        Assert.NotNull(_httpHandler.LastRequest);
    }

    // --- Route Correctness Tests ---

    [Fact]
    public async Task GetProjectsAsync_Sends_GET_ToProjectsRoute()
    {
        _httpHandler.SetResponse("[]");
        var service = CreateService();

        await service.GetProjectsAsync(CancellationToken.None);

        Assert.Equal(HttpMethod.Get, _httpHandler.LastRequest!.Method);
        Assert.Equal("/api/projects", _httpHandler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetProjectByIdAsync_Sends_GET_ToProjectIdRoute()
    {
        _httpHandler.SetResponse("""{"id": "P1"}""");
        var service = CreateService();

        await service.GetProjectByIdAsync("P1", CancellationToken.None);

        Assert.Equal(HttpMethod.Get, _httpHandler.LastRequest!.Method);
        Assert.Equal("/api/projects/P1", _httpHandler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetBalanceAsync_Sends_GET_ToBalancesRoute()
    {
        _httpHandler.SetResponse("""{"allocated": 1000}""");
        var service = CreateService();

        await service.GetBalanceAsync("P1", CancellationToken.None);

        Assert.Equal(HttpMethod.Get, _httpHandler.LastRequest!.Method);
        Assert.Equal("/api/balances/P1", _httpHandler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetTasksAsync_Sends_GET_ToTasksRoute()
    {
        _httpHandler.SetResponse("[]");
        var service = CreateService();

        await service.GetTasksAsync(CancellationToken.None);

        Assert.Equal(HttpMethod.Get, _httpHandler.LastRequest!.Method);
        Assert.Equal("/api/tasks", _httpHandler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetUsersAsync_Sends_GET_ToAdminUsersRoute()
    {
        _httpHandler.SetResponse("[]");
        var service = CreateService();

        await service.GetUsersAsync(CancellationToken.None);

        Assert.Equal(HttpMethod.Get, _httpHandler.LastRequest!.Method);
        Assert.Equal("/api/admin/users", _httpHandler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreateTaskAsync_Sends_POST_WithJsonBody()
    {
        _httpHandler.SetResponse("""{"id": "T1", "title": "New Task"}""");
        var service = CreateService();

        await service.CreateTaskAsync("New Task", "Description", "High", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, _httpHandler.LastRequest!.Method);
        Assert.Equal("/api/tasks", _httpHandler.LastRequest.RequestUri!.AbsolutePath);

        using var doc = JsonDocument.Parse(_httpHandler.LastRequestBody!);
        Assert.Equal("New Task", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("Description", doc.RootElement.GetProperty("description").GetString());
        Assert.Equal("High", doc.RootElement.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_Sends_PATCH_WithStatusBody()
    {
        _httpHandler.SetResponse("""{"id": "T1", "status": "Completed"}""");
        var service = CreateService();

        await service.UpdateTaskStatusAsync("T1", "Completed", CancellationToken.None);

        Assert.Equal(HttpMethod.Patch, _httpHandler.LastRequest!.Method);
        Assert.Equal("/api/tasks/T1/status", _httpHandler.LastRequest.RequestUri!.AbsolutePath);

        using var doc = JsonDocument.Parse(_httpHandler.LastRequestBody!);
        Assert.Equal("Completed", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DeleteTaskAsync_Sends_DELETE_ToTaskIdRoute()
    {
        _httpHandler.SetResponse("""{"deleted": true}""");
        var service = CreateService();

        await service.DeleteTaskAsync("T1", CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, _httpHandler.LastRequest!.Method);
        Assert.Equal("/api/tasks/T1", _httpHandler.LastRequest.RequestUri!.AbsolutePath);
    }

    // --- Error Handling Tests ---

    [Fact]
    public async Task GetProjectsAsync_DownstreamReturns500_ReturnsErrorJsonElement()
    {
        _httpHandler.SetResponse("Internal Server Error", HttpStatusCode.InternalServerError);
        var service = CreateService();

        var result = await service.GetProjectsAsync(CancellationToken.None);

        // AuthenticatedApiClient wraps non-2xx into an error JsonElement
        Assert.True(result.TryGetProperty("error", out var errorProp));
        Assert.True(errorProp.GetBoolean());
        Assert.True(result.TryGetProperty("statusCode", out var statusCode));
        Assert.Equal(500, statusCode.GetInt32());
    }

    // --- Helpers ---

    private static IHttpContextAccessor CreateHttpContextAccessor(string? bearerToken)
    {
        var context = new DefaultHttpContext();
        if (bearerToken is not null)
        {
            context.Request.Headers.Authorization = $"Bearer {bearerToken}";
        }

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(context);
        return accessor.Object;
    }
}
