using System.Diagnostics;
using McpBaseline.MockApi.Telemetry;
using McpBaseline.Shared.Constants;
using Microsoft.AspNetCore.Mvc.Filters;

namespace McpBaseline.MockApi.Filters;

/// <summary>
/// Action filter that automatically instruments all API controller actions with OpenTelemetry
/// traces and metrics. Applied globally — no per-controller changes needed.
/// Creates child spans under the ASP.NET Core HTTP span for granular API-level visibility.
/// </summary>
public sealed class ApiTelemetryFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "unknown";
        var actionName = context.RouteData.Values["action"]?.ToString() ?? "unknown";
        var operationName = $"{controllerName}.{actionName}";

        var stopwatch = Stopwatch.StartNew();
        using var activity = ApiActivitySource.StartApiActivity(operationName);

        EnrichWithUserId(context.HttpContext, activity);

        var executedContext = await next();
        stopwatch.Stop();

        RecordResult(controllerName, actionName, context.HttpContext.Response.StatusCode,
            stopwatch.Elapsed.TotalMilliseconds, activity, executedContext);
    }

    /// <summary>
    /// Enriches the activity with the authenticated user's Entra ID object identifier.
    /// </summary>
    private static void EnrichWithUserId(HttpContext httpContext, Activity? activity)
    {
        // enduser.id: always oid (stable tenant-wide GUID), never sub (pairwise-per-app).
        // See McpBaseline.Presentation/README.md § Span Enrichment for rationale.
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var oid = httpContext.User.FindFirst("oid")?.Value
            ?? httpContext.User.FindFirst(EntraClaimTypes.ObjectId)?.Value;

        if (!string.IsNullOrEmpty(oid))
        {
            activity?.SetTag("enduser.id", oid);
        }
    }

    /// <summary>
    /// Records metrics and enriches the activity with response information.
    /// </summary>
    private static void RecordResult(string controllerName, string actionName, int statusCode,
        double durationMs, Activity? activity, ActionExecutedContext executedContext)
    {
        ApiMetrics.RecordApiInvocation(controllerName, actionName, statusCode, durationMs);

        activity?.SetTag("http.response.status_code", statusCode);

        if (executedContext.Exception is not null && !executedContext.ExceptionHandled
            && executedContext.Exception is not OperationCanceledException)
        {
            ApiActivitySource.RecordError(activity, executedContext.Exception);
        }
    }
}
