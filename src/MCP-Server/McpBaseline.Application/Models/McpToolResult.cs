using System.Text.Json;
using System.Text.Json.Serialization;
using McpBaseline.Application.Constants;

namespace McpBaseline.Application.Models;

/// <summary>
/// Standardized result wrapper for MCP tool responses.
/// Provides consistent structure for success and error cases.
/// </summary>
public record McpToolResult
{
    private const int HttpBadRequest = 400;
    private const int HttpNotFound = 404;
    private const int HttpBadGateway = 502;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Data { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpToolError? Error { get; init; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpToolMetadata? Metadata { get; init; }

    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    public static McpToolResult Ok(JsonElement data) => new()
    {
        Success = true,
        Data = data,
    };

    /// <summary>
    /// Creates a successful result with data and metadata.
    /// </summary>
    public static McpToolResult Ok(JsonElement data, McpToolMetadata metadata) => new()
    {
        Success = true,
        Data = data,
        Metadata = metadata,
    };

    /// <summary>
    /// Creates a failed result using HTTP status codes for error categorization.
    /// </summary>
    /// <param name="statusCode">HTTP status code (400=Bad Request, 502=Bad Gateway, etc.)</param>
    /// <param name="message">Human-readable error message</param>
    public static McpToolResult Fail(int statusCode, string message) => Fail(statusCode, message, field: null, retryable: false);

    /// <summary>
    /// Creates a failed result with a field name for validation errors.
    /// </summary>
    /// <param name="statusCode">HTTP status code (400=Bad Request, 502=Bad Gateway, etc.)</param>
    /// <param name="message">Human-readable error message</param>
    /// <param name="field">Field name for validation errors</param>
    public static McpToolResult Fail(int statusCode, string message, string? field) => Fail(statusCode, message, field, retryable: false);

    /// <summary>
    /// Creates a failed result with full error details.
    /// </summary>
    /// <param name="statusCode">HTTP status code (400=Bad Request, 502=Bad Gateway, etc.)</param>
    /// <param name="message">Human-readable error message</param>
    /// <param name="field">Optional field name for validation errors</param>
    /// <param name="retryable">Whether the operation can be retried</param>
    public static McpToolResult Fail(int statusCode, string message, string? field, bool retryable) => new()
    {
        Success = false,
        Error = new McpToolError
        {
            StatusCode = statusCode,
            Message = message,
            Field = field,
            Retryable = retryable,
        },
    };

    /// <summary>
    /// Creates a validation error result (HTTP 400 Bad Request).
    /// </summary>
    /// <param name="message">Human-readable validation error message.</param>
    /// <param name="field">Field name that failed validation.</param>
    public static McpToolResult ValidationError(string message, string field) =>
        Fail(HttpBadRequest, message, field);

    /// <summary>
    /// Creates a not-found error result (HTTP 404 Not Found).
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="field">Optional field name related to the missing resource.</param>
    public static McpToolResult NotFoundError(string message, string? field = null) =>
        Fail(HttpNotFound, message, field);

    /// <summary>
    /// Creates a gateway error result (HTTP 502 Bad Gateway).
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="retryable">Whether the operation can be retried.</param>
    public static McpToolResult GatewayError(string message, bool retryable = true) =>
        Fail(HttpBadGateway, message, field: null, retryable);

    public string ToJson() => JsonSerializer.Serialize(this, McpJsonOptions.WriteIndented);
}

/// <summary>
/// Error details for failed tool operations using standard HTTP status codes.
/// </summary>
public record McpToolError
{
    /// <summary>
    /// HTTP status code: 400 (Bad Request), 502 (Bad Gateway), etc.
    /// </summary>
    [JsonPropertyName("statusCode")]
    public required int StatusCode { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Field name for validation errors (when StatusCode is 400).
    /// </summary>
    [JsonPropertyName("field")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; init; }

    [JsonPropertyName("retryable")]
    public bool Retryable { get; init; }
}

/// <summary>
/// Metadata about the tool operation (pagination, timing, etc.)
/// </summary>
public record McpToolMetadata
{
    [JsonPropertyName("correlationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("executionTimeMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long ExecutionTimeMs { get; init; }

    [JsonPropertyName("pagination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationInfo? Pagination { get; init; }
}

/// <summary>
/// Pagination information for list operations.
/// </summary>
public record PaginationInfo
{
    [JsonPropertyName("limit")]
    public int Limit { get; init; }

    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; init; }
}
