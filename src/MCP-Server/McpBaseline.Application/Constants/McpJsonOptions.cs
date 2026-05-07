using System.Text.Json;

namespace McpBaseline.Application.Constants;

/// <summary>
/// Centralized JSON serialization options for MCP tools.
/// </summary>
public static class McpJsonOptions
{
    public static readonly JsonSerializerOptions WriteIndented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
