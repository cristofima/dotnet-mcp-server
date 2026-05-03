namespace McpServer.Domain.Rules;

/// <summary>
/// Centralized validation constants for <see cref="Entities.TaskItem"/>.
/// Replaces hardcoded magic values previously scattered across tool classes.
/// </summary>
public static class TaskRules
{
    /// <summary>Maximum allowed title length. Usable in <c>[MaxLength]</c> attributes (compile-time constant).</summary>
    public const int TitleMaxLength = 200;

    private static readonly string[] _validPriorities = ["Low", "Medium", "High"];
    private static readonly string[] _validStatuses = ["Pending", "In Progress", "Completed"];

    /// <summary>Allowed priority values (case-insensitive comparison recommended).</summary>
    public static IReadOnlyList<string> ValidPriorities => _validPriorities;

    /// <summary>Allowed status values (case-insensitive comparison recommended).</summary>
    public static IReadOnlyList<string> ValidStatuses => _validStatuses;

    public static bool IsValidPriority(string value) =>
        Array.Exists(_validPriorities, p => string.Equals(p, value, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidStatus(string value) =>
        Array.Exists(_validStatuses, s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>Formatted string listing valid priorities, suitable for error messages.</summary>
    public static string ValidPrioritiesList { get; } = string.Join(", ", _validPriorities);

    /// <summary>Formatted string listing valid statuses, suitable for error messages.</summary>
    public static string ValidStatusesList { get; } = string.Join(", ", _validStatuses);
}
