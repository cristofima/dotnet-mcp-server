---
description: "Write tests for a class or feature following the project's xUnit v3 conventions, layer-specific patterns, and AAA structure."
---

Write tests for the specified class or feature.

Identify which test project to use:
- `tests/McpServer.Application.Tests/` — use case logic, `McpToolResult` serialization (no mocks, no HttpContext).
- `tests/McpServer.Infrastructure.Tests/` — HTTP routing, OBO token exchange (FakeHttpHandler, Moq).
- `tests/McpServer.Presentation.Tests/` — MCP tool/prompt authorization filtering (TestAuthHandler, real Kestrel server).

Follow the rules in:

- [test-patterns.instructions.md](../instructions/test-patterns.instructions.md)
- [dotnet-test-generation.instructions.md](../instructions/dotnet-test-generation.instructions.md)
