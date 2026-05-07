namespace McpServer.BackendApi.Models;

/// <summary>
/// Request body for transferring budget between two projects.
/// </summary>
public sealed record TransferRequest(
    string SourceProjectId,
    string TargetProjectId,
    decimal Amount);
