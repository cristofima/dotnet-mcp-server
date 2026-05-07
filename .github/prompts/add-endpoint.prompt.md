---
description: "Add a new endpoint to McpServer.BackendApi and wire it through IDownstreamApiService + DownstreamApiService."
---

Add a new endpoint following the project's backend API conventions.

Steps:
1. Add the controller action in `src/McpServer.BackendApi/Controllers/`.
2. Add the service interface method to `src/MCP-Server/McpServer.Application/Abstractions/IDownstreamApiService.cs`.
3. Implement the method in `src/MCP-Server/McpServer.Infrastructure/Http/DownstreamApiService.cs`.
4. Register any new scoped service in `src/McpServer.BackendApi/Program.cs`.

Follow the rules in:

- [mcp-infrastructure.instructions.md](../instructions/mcp-infrastructure.instructions.md)
- [dotnet-code-generation.instructions.md](../instructions/dotnet-code-generation.instructions.md)
