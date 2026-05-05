using System.Text.Json;
using McpServer.Application.Models;
using Xunit;

namespace McpServer.Application.Tests.Models;

/// <summary>
/// Tests that McpToolResult serialization produces the expected JSON contract.
/// If this breaks, every MCP client consuming tools will misinterpret responses.
/// </summary>
public sealed class McpToolResultSerializationTests
{
    [Fact]
    public void Ok_Serializes_WithSuccessTrue_AndData()
    {
        const string title = "Test Task";
        var data = JsonDocument.Parse($$"""{"id": 1, "title": "{{title}}"}""").RootElement;

        var result = McpToolResult.Ok(data);
        var json = result.ToJson();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(1, root.GetProperty("data").GetProperty("id").GetInt32());
        Assert.Equal(title, root.GetProperty("data").GetProperty("title").GetString());
        Assert.False(root.TryGetProperty("error", out _));
    }

    [Fact]
    public void Ok_WithMetadata_Serializes_MetadataFields()
    {
        const string correlationId = "abc-123";
        var data = JsonDocument.Parse("""{"id": 1}""").RootElement;
        var metadata = new McpToolMetadata
        {
            CorrelationId = correlationId,
            ExecutionTimeMs = 42,
        };

        var result = McpToolResult.Ok(data, metadata);
        var json = result.ToJson();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(correlationId, root.GetProperty("metadata").GetProperty("correlationId").GetString());
        Assert.Equal(42, root.GetProperty("metadata").GetProperty("executionTimeMs").GetInt64());
    }

    [Fact]
    public void ValidationError_Serializes_With400_AndFieldName()
    {
        const string message = "Title is required";
        const string field = "title";
        var result = McpToolResult.ValidationError(message, field);
        var json = result.ToJson();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());

        var error = root.GetProperty("error");
        Assert.Equal(400, error.GetProperty("statusCode").GetInt32());
        Assert.Equal(message, error.GetProperty("message").GetString());
        Assert.Equal(field, error.GetProperty("field").GetString());
        Assert.False(error.GetProperty("retryable").GetBoolean());
        Assert.False(root.TryGetProperty("data", out _));
    }

    [Fact]
    public void NotFoundError_Serializes_With404()
    {
        const string message = "Project not found";
        const string field = "projectId";
        var result = McpToolResult.NotFoundError(message, field);
        var json = result.ToJson();

        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error");

        Assert.Equal(404, error.GetProperty("statusCode").GetInt32());
        Assert.Equal(message, error.GetProperty("message").GetString());
        Assert.Equal(field, error.GetProperty("field").GetString());
    }

    [Fact]
    public void GatewayError_Serializes_With502_AndRetryableTrue()
    {
        const string message = "Downstream API unavailable";
        var result = McpToolResult.GatewayError(message);
        var json = result.ToJson();

        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error");

        Assert.Equal(502, error.GetProperty("statusCode").GetInt32());
        Assert.Equal(message, error.GetProperty("message").GetString());
        Assert.True(error.GetProperty("retryable").GetBoolean());
    }

    [Fact]
    public void GatewayError_NonRetryable_Serializes_RetryableFalse()
    {
        var result = McpToolResult.GatewayError("Permanent failure", retryable: false);
        var json = result.ToJson();

        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error");

        Assert.False(error.GetProperty("retryable").GetBoolean());
    }

    [Fact]
    public void ToJson_Uses_CamelCasePropertyNaming()
    {
        var data = JsonDocument.Parse("""{"id": 1}""").RootElement;
        var result = McpToolResult.Ok(data);
        var json = result.ToJson();

        // Verify camelCase: "success" not "Success", "data" not "Data"
        Assert.Contains("\"success\"", json);
        Assert.Contains("\"data\"", json);
        Assert.DoesNotContain("\"Success\"", json);
        Assert.DoesNotContain("\"Data\"", json);
    }

    [Fact]
    public void ToJson_OmitsNullFields()
    {
        var data = JsonDocument.Parse("""{"id": 1}""").RootElement;
        var result = McpToolResult.Ok(data);
        var json = result.ToJson();

        // Null fields should be omitted (JsonIgnoreCondition.WhenWritingNull)
        Assert.DoesNotContain("\"error\"", json);
        Assert.DoesNotContain("\"metadata\"", json);
    }

    [Fact]
    public void Fail_WithAllParameters_Serializes_AllErrorFields()
    {
        const string message = "Service unavailable";
        const string field = "endpoint";
        var result = McpToolResult.Fail(503, message, field, true);
        var json = result.ToJson();

        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error");

        Assert.Equal(503, error.GetProperty("statusCode").GetInt32());
        Assert.Equal(message, error.GetProperty("message").GetString());
        Assert.Equal(field, error.GetProperty("field").GetString());
        Assert.True(error.GetProperty("retryable").GetBoolean());
    }
}
