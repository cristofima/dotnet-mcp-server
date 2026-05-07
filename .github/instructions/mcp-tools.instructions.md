---
name: "MCP Tool and Prompt Implementation"
description: "Implementation rules for MCP tools and prompts in McpServer.Presentation. Covers tool class pattern, mandatory rules, prompt pattern, use case delegation, and telemetry boundaries."
applyTo: "src/MCP-Server/McpServer.Presentation/{Tools,Prompts}/**/*.cs"
---

# MCP Tool and Prompt Implementation

## 1. MCP Server Tool Implementation

### Tool Class Pattern

Tools live in `McpServer.Presentation/Tools/`. Each is a sealed class per domain. Follow `TaskTools.cs` as the canonical example:

```csharp
[McpServerToolType]
[Authorize]
public sealed class TaskTools(
    GetTasksUseCase getTasksUseCase,
    CreateTaskUseCase createTaskUseCase)
{
    [McpServerTool(Name = "get_tasks", Title = "Get Tasks",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get all tasks for the authenticated user.")]
    public async Task<string> GetTasksAsync(CancellationToken cancellationToken)
    {
        var result = await getTasksUseCase.ExecuteAsync(cancellationToken);
        return result.ToJson();
    }
}
```

### Mandatory Rules for Every Tool

1. **`[Authorize]` at class level** — always required.
2. **Inject use cases** (from `Application/UseCases/`), not `IDownstreamApiService`. Each tool method delegates to exactly one use case via `ExecuteAsync()` and returns `result.ToJson()`.
3. **Never call `McpToolResult.Ok/Fail` directly in tools** — use cases return `McpToolResult`, tools just serialize it.
4. **Permission constants from `Permissions` class** (e.g., `Permissions.TASK_READ`, `Permissions.ADMIN_ACCESS`). Compile-time constants from `TaskRules` (e.g., `TaskRules.TitleMaxLength`) are still used in `[MaxLength]` attributes.
5. **Do NOT add** `Stopwatch`, `McpActivitySource`, `McpMetrics.RecordToolInvocation()`, or `McpMetrics.RecordResponseSize()` — all handled by `McpTelemetryFilter`.
6. **Do NOT add** try/catch for general error handling — the `McpTelemetryFilter` handles exception recording, metrics, and logging. Only catch exceptions for tool-specific business logic.
7. **Do NOT add** `McpMetrics.RecordValidationError()` in tools — validation is in use cases; the `McpTelemetryFilter` records tool failures.
8. **Use `[Description]` on parameters** with `[Required]` and `[MaxLength]` for input validation via data annotations.
9. **Use `snake_case` for tool names** in `[McpServerTool(Name = "...")]`.
10. **Set `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld`** accurately on every tool.
11. **Use primary constructors** for dependency injection in tool classes.
12. **All `if`/`else`/`for`/`foreach`/`while` must have curly braces** — even single-line guard clauses (SonarQube S121).
13. **`CancellationToken` without `= default`** in tool methods — the MCP SDK injects it automatically. Exception: when preceded by other optional parameters (e.g., `string priority = "Medium"`), `CancellationToken cancellationToken = default` is required by C#.

### Tool Registration

Register tools in `McpServerExtensions.cs` by chaining `.WithTools<NewTools>()`:

```csharp
services
    .AddMcpServer()
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithRequestFilters(filters => filters.AddCallToolFilter(McpTelemetryFilter.Create()))
    .WithTools<TaskTools>()
    .WithTools<ProjectsTools>()
    // chain new tools here
```

### Input Validation in Use Cases

Validation logic lives in Application layer use cases, not in tools. Use cases validate all parameters and return structured errors via `McpToolResult.Fail()`:

```csharp
public sealed class CreateTaskUseCase(IDownstreamApiService downstreamApiService)
{
    public async Task<McpToolResult> ExecuteAsync(
        string title,
        string description,
        string priority,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return McpToolResult.Fail(400, "Title is required.", "title");
        }

        if (!TaskRules.IsValidPriority(priority))
        {
            return McpToolResult.Fail(400,
                $"Priority must be one of: {TaskRules.ValidPrioritiesList}", "priority");
        }

        var result = await downstreamApiService.CreateTaskAsync(title, description, priority, cancellationToken);
        return McpToolResult.Ok(result);
    }
}
```

Tools delegate to use cases and return the serialized result:

```csharp
public async Task<string> CreateTaskAsync(
    [Description("Task title"), Required, MaxLength(TaskRules.TitleMaxLength)] string title,
    [Description("Task description"), Required, MinLength(1)] string description,
    [Description("Priority level")] string priority = "Medium",
    CancellationToken cancellationToken = default)
{
    var result = await createTaskUseCase.ExecuteAsync(title, description, priority, cancellationToken);
    return result.ToJson();
}
```

## 2. MCP Server Prompt Implementation

### Prompt Class Pattern

Prompts live in `McpServer.Presentation/Prompts/`. Each returns `ChatMessage` (from `Microsoft.Extensions.AI`):

```csharp
[McpServerPromptType]
[Authorize]
public sealed class TaskPrompts
{
    [McpServerPrompt(Name = "summarize_tasks")]
    [Description("Generate a summary of all user tasks.")]
    [Authorize(Roles = Permissions.TASK_READ)]
    public ChatMessage SummarizeTasks(
        [Description("Optional status filter")] string? status = null)
    {
        var prompt = status is not null
            ? $"Summarize all tasks with status '{status}'."
            : "Summarize all tasks.";

        return new ChatMessage(ChatRole.User, prompt);
    }
}
```

### Mandatory Rules for Every Prompt

1. **`[McpServerPromptType]`** at class level.
2. **`[Authorize]` at class level AND `[Authorize(Roles = Permissions.XXX)]` at method level.**
3. **Return `ChatMessage(ChatRole.User, text)`** — never raw strings.
4. **`[Description]` on class-level `[McpServerPrompt]`** and on each parameter.
5. **Register** in `McpServerExtensions.cs` via `.WithPrompts<NewPrompts>()`.
6. Prompt classes are stateless — no constructor injection needed.

## 3. Telemetry Boundaries

`McpTelemetryFilter` (registered via `AddCallToolFilter`) handles all cross-cutting concerns. Tools must NOT duplicate telemetry.

| Do in tools                          | Do NOT do in tools                        |
| ------------------------------------ | ----------------------------------------- |
| Delegate to use cases                | Input validation (belongs in use cases)   |
| Return `result.ToJson()`             | `McpToolResult.Ok/Fail` directly in tools |
| `[Description]`, `[Required]`, etc.  | `Stopwatch` / duration timing             |
|                                      | `McpActivitySource.StartActivity()`       |
|                                      | `McpMetrics.RecordToolInvocation()`       |
|                                      | `McpMetrics.RecordResponseSize()`         |
|                                      | `McpMetrics.RecordValidationError()`      |
|                                      | try/catch for general errors              |
|                                      | Inject `ILogger` (telemetry filter logs)  |
