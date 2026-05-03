using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using McpServer.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace McpServer.Presentation.Prompts;

/// <summary>
/// MCP Prompts for project analysis and reporting.
/// Provides structured prompt templates for LLM interactions related to projects.
/// </summary>
[McpServerPromptType]
[Authorize]
public sealed class ProjectPrompts
{
    /// <summary>
    /// Creates a prompt to analyze a specific project's details and financial status.
    /// </summary>
    [McpServerPrompt(Name = "analyze_project")]
    [Description("Creates a prompt instructing the LLM to analyze a project's details, status, and financial health.")]
    [Authorize(Roles = Permissions.PROJECT_READ)]
    public ChatMessage AnalyzeProject(
        [Description("The unique identifier of the project to analyze.")]
        [Required]
        string projectId)
    {
        return new ChatMessage(ChatRole.User,
            $"""
            Please perform a comprehensive analysis of project '{projectId}'.
            
            Use the following tools to gather data:
            1. get_project_details - to retrieve the project's name, description, status, and dates
            2. get_project_balance - to check the project's financial status (budget vs spent)
            
            In your analysis, provide:
            - Project overview and current status
            - Timeline assessment (start date, due date, days remaining)
            - Budget utilization (percentage spent, remaining balance)
            - Risk indicators (over-budget, behind schedule, etc.)
            - Recommendations for project health improvement
            
            Format the response as a structured report.
            """);
    }

    /// <summary>
    /// Creates a prompt to compare and rank all projects by health metrics.
    /// </summary>
    [McpServerPrompt(Name = "compare_projects")]
    [Description("Creates a prompt for the LLM to compare all projects and rank them by health indicators.")]
    [Authorize(Roles = Permissions.PROJECT_READ)]
    public ChatMessage CompareProjects()
    {
        return new ChatMessage(ChatRole.User,
            """
            Please compare all available projects and provide a health ranking.
            
            Use the get_projects tool to retrieve the list of all projects.
            For each project, evaluate:
            1. Status (Active, On Hold, Completed)
            2. Timeline progress (based on start and due dates)
            3. Budget utilization if available
            
            Create a ranking from healthiest to most at-risk projects.
            Highlight any projects requiring immediate attention.
            Suggest specific actions for underperforming projects.
            """);
    }
}
