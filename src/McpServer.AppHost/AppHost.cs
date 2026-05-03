var builder = DistributedApplication.CreateBuilder(args);

// MockApi — JWT-protected backend API
// Ports defined in MockApi/Properties/launchSettings.json
var mockApi = builder.AddProject<Projects.McpServer_BackendApi>("mock-api")
    .WithEnvironment("OTEL_SEMCONV_STABILITY_OPT_IN", "database");

// MCP Server — secured with Microsoft Entra ID, calls MockApi via OBO
// Ports defined in Server/Properties/launchSettings.json (5230/7043)
builder.AddProject<Projects.McpServer_Presentation>("mcp-server")
    .WithEnvironment("DownstreamApi__BaseUrl", mockApi.GetEndpoint("http"))
    .WithReference(mockApi)
    .WaitFor(mockApi);

await builder.Build().RunAsync();
