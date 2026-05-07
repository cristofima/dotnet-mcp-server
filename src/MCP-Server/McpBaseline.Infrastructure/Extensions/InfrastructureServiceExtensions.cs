using System.Net;
using McpBaseline.Application.Abstractions;
using McpBaseline.Application.Configuration;
using McpBaseline.Infrastructure.Health;
using McpBaseline.Infrastructure.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace McpBaseline.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Infrastructure layer services.
/// </summary>
public static class InfrastructureServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers all Infrastructure layer services: identity provider (OBO token exchange),
        /// downstream API client, and health checks.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddInfrastructure(IConfiguration configuration)
        {
            // Register Identity Provider services (MSAL client, token exchange, options)
            services.AddIdentityProvider(configuration);

            // Register scoped token provider — extracts and OBO-exchanges the caller's token per request
            services.AddScoped<ApiTokenProvider>();

            // Register downstream API service with HttpClient — base URL from DownstreamApiOptions.
            // RemoveAllResilienceHandlers removes the standard Polly pipeline (30 s / 3 retries / circuit breaker)
            // that ServiceDefaults registers globally via ConfigureHttpClientDefaults. Without this call the custom
            // "downstream-api" pipeline below would stack INSIDE the standard one, allowing the outer pipeline to
            // retry a timed-out request up to 3 more times — incompatible with the M365 10–20 s budget.
            // The custom pipeline caps total execution at 10 s with 1 retry and 300 ms jitter.
            // EXTEXP0001: RemoveAllResilienceHandlers is experimental — suppressed intentionally.
#pragma warning disable EXTEXP0001
            services.AddHttpClient<IDownstreamApiService, DownstreamApiService>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<DownstreamApiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            })
            .RemoveAllResilienceHandlers()
#pragma warning restore EXTEXP0001
            .AddResilienceHandler("downstream-api", pipeline =>
            {
                pipeline.AddTimeout(TimeSpan.FromSeconds(10));

                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.FromMilliseconds(300),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(r => r.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout),
                });
            });

            // Register named HttpClient for health checks (short-lived per invocation, avoids handler staleness)
            services.AddHttpClient(EntraIdHealthCheck.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            // Add Identity Provider health check
            services.AddHealthChecks()
                .AddCheck<EntraIdHealthCheck>("identity-provider", tags: ["ready"]);

            return services;
        }
    }
}
