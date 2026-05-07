using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace McpServer.Presentation.Prompts;

/// <summary>
/// MCP Prompts for task management operations.
/// Provides structured prompt templates for LLM interactions related to tasks.
/// </summary>
[McpServerPromptType]
[Authorize]
public sealed class TaskPrompts
{
    /// <summary>
    /// Creates a prompt to summarize tasks by their current status.
    /// </summary>
    [McpServerPrompt(Name = "summarize_tasks")]
    [Description("Creates a prompt instructing the LLM to summarize tasks. Optionally filter by status (Pending, In Progress, Completed).")]
    public ChatMessage SummarizeTasks(
        [Description("Optional status filter: Pending, In Progress, or Completed. Leave empty to summarize all tasks.")] string? status = null)
    {
        var statusFilter = string.IsNullOrWhiteSpace(status)
            ? "all tasks regardless of status"
            : $"tasks with status '{status}'";

        return new ChatMessage(ChatRole.User,
            $"""
            Please analyze and summarize {statusFilter} from the task list.
            
            Provide a concise overview including:
            1. Total count of tasks matching the criteria
            2. Distribution by priority (High/Medium/Low)
            3. Key patterns or trends observed
            4. Any tasks that appear to be blocked or delayed
            
            Format the response in a clear, bulleted structure.
            Use the get_tasks tool to retrieve the current task data first.
            """);
    }

    /// <summary>
    /// Creates a prompt to analyze task priorities and suggest optimizations.
    /// </summary>
    [McpServerPrompt(Name = "analyze_task_priorities")]
    [Description("Creates a prompt for the LLM to analyze task priorities and provide recommendations for workload optimization.")]
    public ChatMessage AnalyzeTaskPriorities()
    {
        return new ChatMessage(ChatRole.User,
            """
            Please analyze the current task list and provide priority optimization recommendations.
            
            Consider the following in your analysis:
            1. Are there too many high-priority tasks? (suggest re-prioritization if so)
            2. Are there any pending tasks that should be escalated?
            3. What is the balance between task creation and completion?
            4. Recommend which tasks should be addressed first based on priority and status
            
            Use the get_tasks tool to retrieve the current task data.
            Present your findings with specific, actionable recommendations.
            """);
    }
}
