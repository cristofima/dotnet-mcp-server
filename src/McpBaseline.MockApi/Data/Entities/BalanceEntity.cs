namespace McpBaseline.MockApi.Data.Entities;

/// <summary>
/// Balance entity for EF Core persistence.
/// Stores budget and spending information per project.
/// Schema constraints are defined in <c>BalanceEntityConfiguration</c> via Fluent API.
/// </summary>
public sealed class BalanceEntity
{
    public string ProjectNumber { get; set; } = string.Empty;

    public decimal Allocated { get; set; }

    public decimal Spent { get; set; }

    public decimal Remaining { get; set; }

    public decimal Committed { get; set; }

    public decimal Available { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTimeOffset LastUpdated { get; set; }
}
