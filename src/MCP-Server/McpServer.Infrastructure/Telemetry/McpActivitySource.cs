using System.Diagnostics;
using System.Security.Claims;
using McpServer.Shared.Constants;

namespace McpServer.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry ActivitySource for MCP tool-level tracing with MCP semantic conventions.
/// Correlation is handled automatically by OpenTelemetry W3C Trace Context (traceparent/tracestate).
/// Downstream HTTP calls are auto-instrumented by OpenTelemetry.Instrumentation.Http.
/// </summary>
public static class McpActivitySource
{
    public static string Name { get; } = "McpServer.Presentation";
    public static string Version { get; } = "1.0.0";

    public static readonly ActivitySource Instance = new(Name, Version);

    /// <summary>
    /// Starts a new activity for tool execution with MCP semantic convention attributes.
    /// Respects parent context from W3C Trace Context (traceparent header) for proper correlation.
    /// When using Streamable HTTP (/mcp), each POST is independent but can be grouped by client-provided traceparent.
    /// </summary>
    public static Activity? StartToolActivity(string toolName)
    {
        // Respect Activity.Current (HTTP POST parent when using /mcp transport)
        // This allows agent frameworks to group multiple tool calls under a single parent span
        var activity = Instance.StartActivity($"mcp.tool.{toolName}", ActivityKind.Server);

        if (activity is null)
        {
            return activity;
        }

        // MCP semantic convention attributes
        activity.SetTag("mcp.tool.name", toolName);
        activity.SetTag("mcp.method.name", $"tools/call/{toolName}");
        activity.SetTag("rpc.system", "jsonrpc");
        activity.SetTag("rpc.method", "tools/call");
        activity.SetTag("jsonrpc.protocol.version", "2.0");

        return activity;
    }

    /// <summary>
    /// Enriches the activity with authenticated user context extracted from JWT claims.
    /// Adds identity, client, role, tenant, and IP address attributes for audit compliance.
    /// </summary>
    public static void EnrichWithUserContext(Activity? activity, HttpContext? httpContext)
    {
        if (activity is null || httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var user = httpContext.User;

        EnrichWithIdentityClaims(activity, user);
        EnrichWithClientAddress(activity, httpContext);
    }

    private static void EnrichWithIdentityClaims(Activity activity, ClaimsPrincipal user)
    {
        // enduser.id: always oid (stable tenant-wide GUID), never sub (pairwise-per-app).
        // See McpServer.Presentation/README.md § Span Enrichment for rationale.
        SetFirstClaimTag(activity, user, "enduser.id", "oid", EntraClaimTypes.ObjectId);
        SetFirstClaimTag(activity, user, "oauth.client.id", "azp", "client_id");
        SetRolesTag(activity, user);
        SetFirstClaimTag(activity, user, "tenant.id", "tid", EntraClaimTypes.TenantId);
        SetFirstClaimTag(activity, user, "enduser.scope", "scp", EntraClaimTypes.Scope, "scope");
    }

    private static void SetFirstClaimTag(Activity activity, ClaimsPrincipal user, string tagName, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrEmpty(value))
            {
                activity.SetTag(tagName, value);
                return;
            }
        }
    }

    private static void SetRolesTag(Activity activity, ClaimsPrincipal user)
    {
        var roleClaimType = (user.Identity as ClaimsIdentity)?.RoleClaimType ?? ClaimTypes.Role;
        var roles = user.FindAll(roleClaimType).Select(c => c.Value).ToList();
        if (roles.Count > 0)
        {
            activity.SetTag("enduser.roles", string.Join(",", roles));
        }
    }

    private static void EnrichWithClientAddress(Activity activity, HttpContext httpContext)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(ipAddress))
        {
            activity.SetTag("client.address", ipAddress);
        }
    }

    /// <summary>
    /// Records an error on the current activity with OpenTelemetry semantic conventions.
    /// </summary>
    public static void RecordError(Activity? activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error.type", exception.GetType().FullName);
        activity.SetTag("exception.type", exception.GetType().Name);
        activity.SetTag("exception.message", exception.Message);
    }
}
