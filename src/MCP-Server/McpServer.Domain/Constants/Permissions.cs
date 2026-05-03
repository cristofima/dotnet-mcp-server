namespace McpServer.Domain.Constants;

/// <summary>
/// MCP Server App Role permission constants used for authorization.
/// These values must match App Roles configured in the MCP Server Entra ID app registration.
/// </summary>
public static class Permissions
{
    // Task permissions

    /// <summary>
    /// Permission to read tasks.
    /// </summary>
    public const string TASK_READ = "mcp:task:read";

    /// <summary>
    /// Permission to create, update, and delete tasks.
    /// </summary>
    public const string TASK_WRITE = "mcp:task:write";

    // Balance permissions

    /// <summary>
    /// Permission to view account balances.
    /// </summary>
    public const string BALANCE_READ = "mcp:balance:read";

    /// <summary>
    /// Permission to modify account balances.
    /// </summary>
    public const string BALANCE_WRITE = "mcp:balance:write";

    // Project permissions

    /// <summary>
    /// Permission to view projects.
    /// </summary>
    public const string PROJECT_READ = "mcp:project:read";

    /// <summary>
    /// Permission to create, update, and delete projects.
    /// </summary>
    public const string PROJECT_WRITE = "mcp:project:write";

    // Admin permissions

    /// <summary>
    /// Permission to access admin tools.
    /// </summary>
    public const string ADMIN_ACCESS = "mcp:admin:access";
}
