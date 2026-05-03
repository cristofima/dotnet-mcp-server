using System.ComponentModel.DataAnnotations;

namespace McpServer.Presentation.Configuration;

/// <summary>
/// Configuration options for the MCP server in-process fixed-window rate limiter.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>Configuration section name in appsettings.</summary>
    public static string SectionName { get; } = "RateLimit";

    /// <summary>Maximum requests allowed per user or IP within <see cref="WindowSeconds"/>.</summary>
    [Range(1, 10000)]
    public int PermitLimit { get; init; } = 100;

    /// <summary>Fixed window duration in seconds.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;

    /// <summary>Maximum requests queued when the permit limit is reached before returning 429.</summary>
    [Range(0, 100)]
    public int QueueLimit { get; init; } = 10;
}
