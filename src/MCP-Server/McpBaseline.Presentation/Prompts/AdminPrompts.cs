using System.ComponentModel;
using McpBaseline.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace McpBaseline.Presentation.Prompts;

/// <summary>
/// MCP Prompts for administrative and compliance operations.
/// Provides structured prompt templates for LLM interactions related to admin tasks.
/// </summary>
[McpServerPromptType]
[Authorize]
public sealed class AdminPrompts
{
    /// <summary>
    /// Creates a prompt to generate a security and compliance checklist.
    /// </summary>
    [McpServerPrompt(Name = "compliance_checklist")]
    [Description("Creates a prompt instructing the LLM to generate a security and compliance checklist for the MCP server deployment.")]
    [Authorize(Roles = Permissions.ADMIN_ACCESS)]
    public ChatMessage ComplianceChecklist()
    {
        return new ChatMessage(ChatRole.User,
            """
            Please generate a security and compliance checklist for this MCP OAuth2 baseline deployment.
            
            The checklist should cover:
            
            ## Authentication & Authorization
            - [ ] Microsoft Entra ID (Azure AD) integration configured
            - [ ] OAuth 2.0 On-Behalf-Of (OBO) flow functioning
            - [ ] JWT token validation enabled
            - [ ] Role-based access control (RBAC) enforced on all tools
            
            ## Data Protection
            - [ ] HTTPS/TLS encryption in transit
            - [ ] Sensitive data not logged
            - [ ] Token exchange secrets secured
            
            ## Operational Security
            - [ ] Rate limiting configured
            - [ ] Health checks enabled
            - [ ] OpenTelemetry tracing active
            - [ ] Audit logging in place
            
            ## MCP-Specific
            - [ ] Tool authorization filters active (AddAuthorizationFilters)
            - [ ] RFC 9728 protected resource metadata exposed
            - [ ] SSE and Streamable HTTP transports secured
            
            Provide status recommendations based on the deployment configuration.
            Use the whoami tool to verify the current user's authentication status.
            Use the get_backend_users tool (if authorized) to verify user provisioning.
            """);
    }

    /// <summary>
    /// Creates a prompt to analyze system users and their permissions.
    /// </summary>
    [McpServerPrompt(Name = "user_audit")]
    [Description("Creates a prompt for the LLM to audit system users, their roles, and access patterns.")]
    [Authorize(Roles = Permissions.ADMIN_ACCESS)]
    public ChatMessage UserAudit()
    {
        return new ChatMessage(ChatRole.User,
            """
            Please perform a user access audit for the system.
            
            Use the get_backend_users tool to retrieve the list of registered users.
            
            For your audit report, analyze:
            1. Total number of users in the system
            2. Distribution of users by role (Teller, Supervisor, Manager)
            3. Identify any users with elevated permissions (Admin access)
            4. Look for potential security concerns:
               - Users with multiple high-privilege roles
               - Service accounts that may need review
               - Any anomalies in user provisioning
            
            Provide recommendations for:
            - Role consolidation if applicable
            - Principle of least privilege compliance
            - Users who may need permission reviews
            
            Format as a security audit report suitable for compliance documentation.
            """);
    }
}
