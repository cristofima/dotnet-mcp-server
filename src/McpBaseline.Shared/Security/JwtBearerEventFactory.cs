using McpBaseline.Shared.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;

namespace McpBaseline.Shared.Security;

/// <summary>
/// Factory for creating standardized JwtBearerEvents with consistent logging.
/// </summary>
public static class JwtBearerEventFactory
{
    /// <summary>
    /// Creates JWT Bearer events with provider-specific structured logging.
    /// </summary>
    /// <param name="providerName">Identity provider name for log messages (e.g., "EntraId").</param>
    /// <returns>Configured JwtBearerEvents instance.</returns>
    public static JwtBearerEvents Create(string providerName)
    {
        return new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var userName = context.Principal?.GetUserName();
                Log.Information("[{Provider}] Token validated for user: {UserName}", providerName, userName);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Log.Warning("{Provider}: Authentication failed: {Error}", providerName, context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Log.Warning(
                    "[{Provider}] Authentication challenge triggered. Path: {Path}, Error: {Error}",
                    providerName, context.Request.Path, context.Error ?? "none");
                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                var userName = context.Principal?.GetUserName();
                Log.Warning(
                    "[{Provider}] Authorization forbidden for user: {UserName}, Path: {Path}",
                    providerName, userName, context.Request.Path);
                return Task.CompletedTask;
            }
        };
    }
}
