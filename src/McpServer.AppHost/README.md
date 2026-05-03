# McpServer.AppHost

.NET Aspire orchestrator for local development. Starts `McpServer.Presentation` and `McpServer.BackendApi` with service discovery, health checks, and the Aspire Dashboard.

> **This project is never deployed.** Production services are deployed independently to Azure App Service and configured via App Service Application Settings.

## Running

```powershell
cd McpServer.AppHost
dotnet run
```

After startup, the console shows the **Aspire Dashboard URL** for live logs, traces, and metrics of both services.
