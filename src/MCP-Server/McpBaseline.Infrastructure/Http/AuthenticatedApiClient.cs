using System.Net.Http.Headers;
using System.Text.Json;

namespace McpBaseline.Infrastructure.Http;

/// <summary>
/// Abstract base class for authenticated downstream API clients.
/// Provides request creation, HTTP execution, and JSON response parsing.
/// Token acquisition is delegated to <see cref="ApiTokenProvider"/>.
/// Concrete services inherit from this class and add domain-specific routes and operations.
/// </summary>
/// <remarks>
/// OBO-mandatory, no token passthrough. See McpBaseline.Presentation/README.md § OBO Security Posture.
/// Base URL set via <see cref="HttpClient.BaseAddress"/> from <see cref="McpBaseline.Application.Configuration.DownstreamApiOptions.BaseUrl"/>.
/// Endpoints should be relative paths (e.g., "/api/projects").
/// </remarks>
public class AuthenticatedApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ApiTokenProvider _tokenProvider;
    private readonly ILogger _logger;

    protected AuthenticatedApiClient(
        HttpClient httpClient,
        ApiTokenProvider tokenProvider,
        ILogger logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <summary>
    /// Creates an HTTP request with the proper bearer token.
    /// Endpoints should be relative paths (e.g., "/api/projects").
    /// </summary>
    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, new Uri(endpoint, UriKind.RelativeOrAbsolute));

        var token = await _tokenProvider.GetTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _logger.LogWarning("No bearer token available for downstream API call");
        }

        return request;
    }

    /// <summary>
    /// Creates an HTTP request with a JSON body and bearer token.
    /// </summary>
    private async Task<HttpRequestMessage> CreateJsonRequestAsync<T>(
        HttpMethod method,
        string endpoint,
        T body,
        CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(method, endpoint, cancellationToken);
        request.Content = JsonContent.Create(body);
        return request;
    }

    /// <summary>
    /// Executes an HTTP request and returns the parsed JSON response.
    /// </summary>
    private async Task<JsonElement> ExecuteAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Calling downstream API: {Method} {Uri}", request.Method, request.RequestUri);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Downstream API returned {StatusCode}: {Content}",
                    response.StatusCode, content);

                return JsonSerializer.SerializeToElement(new
                {
                    error = true,
                    statusCode = (int)response.StatusCode,
                    message = response.ReasonPhrase,
                    details = content
                });
            }

            _logger.LogInformation("Downstream API call successful: {StatusCode}", response.StatusCode);

            try
            {
                return JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch (JsonException)
            {
                return JsonSerializer.SerializeToElement(new { data = content });
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            _logger.LogError(ex, "Error calling downstream API");
            return JsonSerializer.SerializeToElement(new
            {
                error = true,
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Sends a GET request to the specified endpoint.
    /// </summary>
    /// <param name="endpoint">Relative path of the downstream API endpoint (e.g., "api/tasks").</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Parsed JSON response from the downstream API.</returns>
    protected async Task<JsonElement> GetAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, endpoint, cancellationToken);
        return await ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a POST request with a JSON body to the specified endpoint.
    /// </summary>
    /// <param name="endpoint">Relative path of the downstream API endpoint (e.g., "api/tasks").</param>
    /// <param name="body">Object to serialize as the JSON request body.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <typeparam name="T">Type of the request body.</typeparam>
    /// <returns>Parsed JSON response from the downstream API.</returns>
    protected async Task<JsonElement> PostAsync<T>(string endpoint, T body, CancellationToken cancellationToken)
    {
        using var request = await CreateJsonRequestAsync(HttpMethod.Post, endpoint, body, cancellationToken);
        return await ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a PATCH request with a JSON body to the specified endpoint.
    /// </summary>
    /// <param name="endpoint">Relative path of the downstream API endpoint (e.g., "api/tasks/1").</param>
    /// <param name="body">Object to serialize as the JSON request body.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <typeparam name="T">Type of the request body.</typeparam>
    /// <returns>Parsed JSON response from the downstream API.</returns>
    protected async Task<JsonElement> PatchAsync<T>(string endpoint, T body, CancellationToken cancellationToken)
    {
        using var request = await CreateJsonRequestAsync(HttpMethod.Patch, endpoint, body, cancellationToken);
        return await ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a DELETE request to the specified endpoint.
    /// </summary>
    /// <param name="endpoint">Relative path of the downstream API endpoint (e.g., "api/tasks/1").</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Parsed JSON response from the downstream API.</returns>
    protected async Task<JsonElement> DeleteAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Delete, endpoint, cancellationToken);
        return await ExecuteAsync(request, cancellationToken);
    }
}
