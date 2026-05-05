# McpServer.Application.Tests

Unit tests for the **Application layer**: serialization contracts that MCP clients depend on.

## What This Layer Tests

The Application layer owns the `McpToolResult` model, the standard envelope every MCP tool returns. These tests guarantee the JSON contract stays stable: field names, casing, null handling, and error shapes. If any test here breaks, every MCP client consuming tools will misinterpret responses.

## Test Classes

### `Models/McpToolResultSerializationTests` (9 tests)

Validates `McpToolResult` JSON serialization using `System.Text.Json`.

#### Success Envelope (2 tests)

| Test                                        | What it verifies                                                                     |
| ------------------------------------------- | ------------------------------------------------------------------------------------ |
| `Ok_Serializes_WithSuccessTrue_AndData`     | Success envelope contains `success: true` and `data` with the provided `JsonElement` |
| `Ok_WithMetadata_Serializes_MetadataFields` | Optional `metadata` block includes `correlationId` and `executionTimeMs`             |

#### Error Factories (5 tests)

| Test                                                  | What it verifies                                                                        |
| ----------------------------------------------------- | --------------------------------------------------------------------------------------- |
| `ValidationError_Serializes_With400_AndFieldName`     | Validation errors produce `statusCode: 400`, `message`, `field`, and `retryable: false` |
| `NotFoundError_Serializes_With404`                    | Not-found errors produce `statusCode: 404` with field reference                         |
| `GatewayError_Serializes_With502_AndRetryableTrue`    | Gateway errors default to `statusCode: 502` and `retryable: true`                       |
| `GatewayError_NonRetryable_Serializes_RetryableFalse` | Gateway errors can override `retryable` to `false`                                      |
| `Fail_WithAllParameters_Serializes_AllErrorFields`    | Generic `Fail()` factory serializes all four error properties                           |

#### JSON Serialization Conventions (2 tests)

| Test                                  | What it verifies                                           |
| ------------------------------------- | ---------------------------------------------------------- |
| `ToJson_Uses_CamelCasePropertyNaming` | Output uses `camelCase` (e.g., `success`, not `Success`)   |
| `ToJson_OmitsNullFields`              | Null fields (`error`, `metadata`) are excluded from output |

## Running

```bash
dotnet test tests/McpServer.Application.Tests/
```

## Dependencies

- **xUnit v3**
- References `McpServer.Application` (the layer under test)
