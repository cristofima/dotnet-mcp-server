using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;

namespace McpBaseline.ServiceDefaults.Telemetry;

/// <summary>
/// Suppresses OTLP export for health-probe and root-path activities by setting
/// <see cref="Activity.IsAllDataRequested"/> to <c>false</c> in <see cref="BaseProcessor{T}.OnStart"/>.
/// </summary>
/// <remarks>
/// See the ServiceDefaults README (§ Health Check Trace Filtering) for rationale and
/// a comparison with alternative approaches that were evaluated and discarded.
/// </remarks>
/// <seealso href="https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-filter#filter-telemetry-using-span-processors"/>
internal sealed class HealthCheckActivityFilter : BaseProcessor<Activity>
{
    private static readonly PathString HealthPath = new("/health");
    private static readonly PathString AlivePath = new("/alive");

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HealthCheckActivityFilter(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public override void OnStart(Activity activity)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return; // Non-HTTP activity (background task, startup, etc.)
        }

        var path = httpContext.Request.Path;

        if (path.StartsWithSegments(HealthPath)
            || path.StartsWithSegments(AlivePath)
            || path == new PathString("/")
            || !path.HasValue) // empty path = root probe (e.g. Azure App Service hitting host:port without trailing /)
        {
            // Prevent the SDK from collecting data and from exporting this activity.
            activity.IsAllDataRequested = false;
        }
    }
}
