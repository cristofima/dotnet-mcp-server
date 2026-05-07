using System.Net;
using System.Text.Json;
using McpServer.Infrastructure.Configuration;
using McpServer.Presentation.Extensions;
using McpServer.Presentation.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace McpServer.Presentation.Tests.Extensions;

/// <summary>
/// Tests that the OAuth2 well-known discovery endpoints (RFC 9728 and RFC 8414)
/// are accessible anonymously and return valid JSON with the expected structure.
/// Does NOT connect to Entra ID — the authorization server metadata endpoint
/// uses a fake HttpClient that returns a canned OpenID Configuration response.
/// </summary>
public sealed class WellKnownEndpointTests
{
    private const string TestTenantId = "test-tenant-id";
    private const string TestClientId = "test-client-id";
    private const string TestInstance = "https://login.microsoftonline.com/";
    private const string TestScope = "api://test-client-id/mcp.access";
    private const string TestResourceDocumentation = "https://github.com/org/mcp-docs";

    // --- RFC 9728: Protected Resource Metadata ---

    [Fact]
    public async Task ProtectedResourceMetadata_Returns_200_WithValidJson()
    {
        await using var env = await StartServerWithWellKnownEndpointsAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var httpClient = new HttpClient();

        var response = await httpClient.GetAsync($"{env.Address}/.well-known/oauth-protected-resource", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProtectedResourceMetadata_Contains_ResourceField()
    {
        await using var env = await StartServerWithWellKnownEndpointsAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var httpClient = new HttpClient();

        var json = await httpClient.GetStringAsync($"{env.Address}/.well-known/oauth-protected-resource", TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("resource", out var resource));
        Assert.StartsWith("http://127.0.0.1:", resource.GetString());
    }

    [Fact]
    public async Task ProtectedResourceMetadata_Contains_AuthorizationServers()
    {
        await using var env = await StartServerWithWellKnownEndpointsAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var httpClient = new HttpClient();

        var json = await httpClient.GetStringAsync($"{env.Address}/.well-known/oauth-protected-resource", TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("authorization_servers", out var servers));
        Assert.Equal(JsonValueKind.Array, servers.ValueKind);
        Assert.Single(servers.EnumerateArray().ToList());

        var expectedAuthority = $"{TestInstance.TrimEnd('/')}/{TestTenantId}";
        Assert.Equal(expectedAuthority, servers[0].GetString());
    }

    [Fact]
    public async Task ProtectedResourceMetadata_Contains_BearerMethodsSupported()
    {
        await using var env = await StartServerWithWellKnownEndpointsAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var httpClient = new HttpClient();

        var json = await httpClient.GetStringAsync($"{env.Address}/.well-known/oauth-protected-resource", TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("bearer_methods_supported", out var methods));
        Assert.Contains("header", methods.EnumerateArray().Select(e => e.GetString()!));
    }

    [Fact]
    public async Task ProtectedResourceMetadata_Contains_ScopesSupported()
    {
        await using var env = await StartServerWithWellKnownEndpointsAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var httpClient = new HttpClient();

        var json = await httpClient.GetStringAsync($"{env.Address}/.well-known/oauth-protected-resource", TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("scopes_supported", out var scopes));
        Assert.Contains(TestScope, scopes.EnumerateArray().Select(e => e.GetString()!));
    }

    [Fact]
    public async Task ProtectedResourceMetadata_Contains_ResourceDocumentation()
    {
        await using var env = await StartServerWithWellKnownEndpointsAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var httpClient = new HttpClient();

        var json = await httpClient.GetStringAsync($"{env.Address}/.well-known/oauth-protected-resource", TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("resource_documentation", out var docUrl));
        Assert.Equal(TestResourceDocumentation, docUrl.GetString());
    }

    [Fact]
    public async Task ProtectedResourceMetadata_IsAccessibleAnonymously()
    {
        // Start server without any user identity — endpoint must still succeed
        await using var env = await StartServerWithWellKnownEndpointsAsync(authenticated: false, cancellationToken: TestContext.Current.CancellationToken);
        using var httpClient = new HttpClient();

        var response = await httpClient.GetAsync($"{env.Address}/.well-known/oauth-protected-resource", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- RFC 8414: Authorization Server Metadata ---

    [Fact]
    public async Task AuthorizationServerMetadata_Returns_200_WithValidJson()
    {
        await using var env = await StartServerWithWellKnownEndpointsAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var httpClient = new HttpClient();

        var response = await httpClient.GetAsync($"{env.Address}/.well-known/oauth-authorization-server", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AuthorizationServerMetadata_IsAccessibleAnonymously()
    {
        await using var env = await StartServerWithWellKnownEndpointsAsync(authenticated: false, cancellationToken: TestContext.Current.CancellationToken);
        using var httpClient = new HttpClient();

        var response = await httpClient.GetAsync($"{env.Address}/.well-known/oauth-authorization-server", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Server Setup ---

    private static Task<TestServerEnvironment> StartServerWithWellKnownEndpointsAsync(
        bool authenticated = true, CancellationToken cancellationToken = default)
    {
        var userName = authenticated ? "test-user" : null;
        string[]? roles = authenticated ? [] : null;

        return TestServerBuilder.StartAsync(
            userName,
            roles,
            configureServices: services =>
            {
                // Register EntraIdServerOptions needed by WellKnownEndpointExtensions
                var entraIdOptions = new EntraIdServerOptions
                {
                    Instance = TestInstance,
                    TenantId = TestTenantId,
                    ClientId = TestClientId,
                    ClientSecret = "test-secret",
                    Scopes = [TestScope],
                    ResourceDocumentation = TestResourceDocumentation,
                };
                services.AddSingleton(Options.Create(entraIdOptions));

                // Register a fake IHttpClientFactory that returns a handler
                // serving canned OpenID Configuration (avoids calling real Entra ID)
                services.AddHttpClient(string.Empty)
                    .ConfigurePrimaryHttpMessageHandler(() => new FakeOpenIdConfigHandler(TestTenantId));
            },
            configureApp: app =>
            {
                app.MapWellKnownEndpoints();
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Fake HTTP handler that intercepts OpenID Connect metadata requests
    /// and returns a canned response instead of calling Entra ID.
    /// </summary>
    private sealed class FakeOpenIdConfigHandler(string tenantId) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
            var responseJson = $$"""
                {
                    "issuer": "{{authority}}",
                    "authorization_endpoint": "{{authority}}/authorize",
                    "token_endpoint": "{{authority}}/token",
                    "jwks_uri": "{{authority}}/keys",
                    "response_types_supported": ["code", "id_token", "token"],
                    "scopes_supported": ["openid", "profile", "email"]
                }
                """;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
            };

            return Task.FromResult(response);
        }
    }
}
