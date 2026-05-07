# McpBaseline.Domain — Business Constants and Rules

## Overview

Innermost layer of the Clean Architecture. Contains business constants and validation rules with **zero external dependencies**: no NuGet packages, no project references. Every other layer may depend on Domain; Domain depends on nothing.

- **Target Framework**: .NET 10
- **NuGet packages**: none
- **Project references**: none

## Contents

### Constants (`Constants/`)

#### Permissions (`Constants/Permissions.cs`)

MCP Server App Role constants used in `[Authorize(Roles = ...)]` attributes across all Tool and Prompt classes. Values use the `mcp:` prefix to distinguish them from MockApi's own role set (see `McpBaseline.MockApi/Constants/Permissions.cs`).

| Constant        | Value               | Used by                       |
| --------------- | ------------------- | ----------------------------- |
| `TASK_READ`     | `mcp:task:read`     | TaskTools, TaskPrompts        |
| `TASK_WRITE`    | `mcp:task:write`    | TaskTools                     |
| `BALANCE_READ`  | `mcp:balance:read`  | BalancesTools                 |
| `BALANCE_WRITE` | `mcp:balance:write` | (reserved)                    |
| `PROJECT_READ`  | `mcp:project:read`  | ProjectsTools, ProjectPrompts |
| `PROJECT_WRITE` | `mcp:project:write` | (reserved)                    |
| `ADMIN_ACCESS`  | `mcp:admin:access`  | AdminTools, AdminPrompts      |

These must remain `public const string` because they are used as attribute arguments (compile-time constants). The matching Entra ID App Role definitions are in `azure-config/mcp-server-roles.json`.

### Rules (`Rules/`)

#### TaskRules (`Rules/TaskRules.cs`)

Centralized validation constants and methods for task operations. Replaces hardcoded magic values that were previously scattered across `TaskTools.cs`.

| Member                | Type                    | Value / Purpose                         |
| --------------------- | ----------------------- | --------------------------------------- |
| `TitleMaxLength`      | `const int`             | `200`, used in `[MaxLength]` attributes |
| `ValidPriorities`     | `IReadOnlyList<string>` | `Low`, `Medium`, `High`                 |
| `ValidStatuses`       | `IReadOnlyList<string>` | `Pending`, `In Progress`, `Completed`   |
| `IsValidPriority()`   | `bool`                  | Case-insensitive priority check         |
| `IsValidStatus()`     | `bool`                  | Case-insensitive status check           |
| `ValidPrioritiesList` | `string`                | Formatted string for error messages     |
| `ValidStatusesList`   | `string`                | Formatted string for error messages     |

Usage in `TaskTools.cs`:

```csharp
[MaxLength(TaskRules.TitleMaxLength)] string title,
// ...
if (!TaskRules.IsValidPriority(priority))
{
    return McpToolResult.Fail(400, $"Priority must be one of: {TaskRules.ValidPrioritiesList}", "priority").ToJson();
}
```

## Design Decisions

1. **No entities**: The MCP Server is stateless: it receives `JsonElement` from the downstream API and passes it through to MCP clients via `McpToolResult.Ok(result)`. There is no deserialization, mapping, or domain object hydration, so entity types would violate YAGNI.

2. **`const` for attributes, `static` for everything else**: `TitleMaxLength` is `const` because `[MaxLength]` requires a compile-time constant. Lists and formatted strings are `static readonly` / `static { get; }` per SonarQube S3008.

3. **Rules, not validators**: `TaskRules` exposes pure functions (`IsValidPriority`, `IsValidStatus`) rather than a validator pattern. Tools call these directly and map failures to `McpToolResult.Fail()` with the appropriate field name and metric recording.

## Dependency Graph

```
McpBaseline.Domain  (this project)
    ↑
McpBaseline.Application
    ↑
McpBaseline.Infrastructure
    ↑
McpBaseline.Presentation
```

Domain is at the center: all arrows point inward. No outward dependencies exist or should be added.
