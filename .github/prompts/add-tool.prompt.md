---
description: "Add a new MCP tool: use case in Application layer + tool class in Presentation layer with all mandatory attributes and authorization."
---

Add a new MCP tool following the project conventions.

Steps:
1. Create the use case class in `src/MCP-Server/McpServer.Application/UseCases/{Domain}/`.
2. Register the use case in `src/MCP-Server/McpServer.Application/ApplicationServiceExtensions.cs`.
3. Add or update the tool class in `src/MCP-Server/McpServer.Presentation/Tools/`.
4. Register the tool in `src/MCP-Server/McpServer.Presentation/Extensions/McpServerExtensions.cs`.
5. Add the permission constant to `src/MCP-Server/McpServer.Domain/Constants/Permissions.cs` if needed.

Follow the rules in:

- [mcp-tools.instructions.md](../instructions/mcp-tools.instructions.md)
- [dotnet-code-generation.instructions.md](../instructions/dotnet-code-generation.instructions.md)
