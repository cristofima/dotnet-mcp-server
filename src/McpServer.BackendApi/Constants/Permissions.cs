namespace McpServer.BackendApi.Constants;

/// <summary>
/// MockApi App Role permission constants used for authorization.
/// These values must match App Roles configured in the MockApi Entra ID app registration.
/// </summary>
public static class Permissions
{
    // Task permissions

    /// <summary>
    /// Permission to read tasks.
    /// </summary>
    public const string TASK_READ = "task:read";

    /// <summary>
    /// Permission to create, update, and delete tasks.
    /// </summary>
    public const string TASK_WRITE = "task:write";

    // Balance permissions

    /// <summary>
    /// Permission to view account balances.
    /// </summary>
    public const string BALANCE_READ = "balance:read";

    /// <summary>
    /// Permission to modify account balances.
    /// </summary>
    public const string BALANCE_WRITE = "balance:write";

    // Project permissions

    /// <summary>
    /// Permission to view projects.
    /// </summary>
    public const string PROJECT_READ = "project:read";

    /// <summary>
    /// Permission to create, update, and delete projects.
    /// </summary>
    public const string PROJECT_WRITE = "project:write";

    // Admin permissions

    /// <summary>
    /// Permission to access admin tools.
    /// </summary>
    public const string ADMIN_ACCESS = "admin:access";
}
