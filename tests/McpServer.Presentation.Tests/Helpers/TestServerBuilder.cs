using System.Security.Claims;
using System.Text.Json;
using McpServer.Application;
using McpServer.Application.Abstractions;
using McpServer.Presentation.Prompts;
using McpServer.Presentation.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;

namespace McpServer.Presentation.Tests.Helpers;

/// <summary>
/// Builds a lightweight Kestrel test server with fake authentication,
/// mocked downstream services, and real MCP tool/prompt registrations.
/// Shared by MCP authorization tests and well-known endpoint tests.
/// </summary>
internal static class TestServerBuilder
{
    /// <summary>
    /// Starts a test server with MCP endpoints and optional user identity.
    /// </summary>
    /// <param name="userName">Username for the fake identity. Null for unauthenticated tests.</param>
    /// <param name="roles">Roles to assign to the fake identity.</param>
    /// <param name="configureServices">Optional callback to register additional services (e.g., EntraIdServerOptions).</param>
    /// <param name="configureApp">Optional callback to map additional endpoints (e.g., well-known).</param>
    public static async Task<TestServerEnvironment> StartAsync(
        string? userName,
        string[]? roles = null,
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? configureApp = null,
        CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Test authentication (fake scheme that trusts the injected ClaimsPrincipal)
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();

        // Mock downstream API service (tools delegate to use cases which call this)
        builder.Services.AddSingleton(CreateMockDownstreamService().Object);

        // Register real use cases from Application layer
        builder.Services.AddApplication();

        // Register MCP server with the actual tools and prompts
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "test-server",
                    Version = "1.0.0",
                };
            })
            .WithHttpTransport()
            .AddAuthorizationFilters()
            .WithTools<TaskTools>()
            .WithTools<ProjectsTools>()
            .WithTools<BalancesTools>()
            .WithPrompts<TaskPrompts>()
            .WithPrompts<ProjectPrompts>();

        configureServices?.Invoke(builder.Services);

        var app = builder.Build();

        // Inject fake user identity via middleware (before auth middleware,
        // following the MCP SDK's own AuthorizeAttributeTests pattern)
        if (userName is not null)
        {
            app.Use(next => context =>
            {
                var claims = new List<Claim> { new(ClaimTypes.Name, userName) };
                if (roles is not null)
                {
                    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
                }

                context.User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "Test"));
                return next(context);
            });
        }

        app.UseAuthentication();
        app.UseAuthorization();

        configureApp?.Invoke(app);

        app.MapMcp("/mcp").RequireAuthorization();

        await app.StartAsync(cancellationToken);
        var address = app.Urls.First();

        // Create MCP client only for authenticated tests
        McpClient? client = null;
        if (userName is null)
        {
            return new TestServerEnvironment(app, client, address);
        }

        try
        {
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri($"{address}/mcp"),
                });
            client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return new TestServerEnvironment(app, client, address);
    }

    private static Mock<IDownstreamApiService> CreateMockDownstreamService()
    {
        var mock = new Mock<IDownstreamApiService>();
        var emptyArray = JsonSerializer.SerializeToElement(Array.Empty<object>());
        var emptyObject = JsonSerializer.SerializeToElement(new { });

        mock.Setup(x => x.GetTasksAsync(It.IsAny<CancellationToken>())).ReturnsAsync(emptyArray);
        mock.Setup(x => x.GetProjectsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(emptyArray);
        mock.Setup(x => x.GetProjectByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyObject);
        mock.Setup(x => x.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyObject);
        mock.Setup(x => x.GetTaskByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyObject);
        mock.Setup(x => x.CreateTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyObject);
        mock.Setup(x => x.UpdateTaskStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyObject);
        mock.Setup(x => x.DeleteTaskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyObject);

        return mock;
    }
}
