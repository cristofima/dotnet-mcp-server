namespace McpServer.BackendApi.Models.Responses;

/// <summary>
/// API response wrapper for single items with typed metadata.
/// Follows envelope pattern: { metadata, data }
/// </summary>
/// <typeparam name="TData">The type of data payload.</typeparam>
/// <typeparam name="TMetadata">The type of metadata.</typeparam>
public record ApiResponse<TData, TMetadata>(
    TMetadata Metadata,
    TData Data
);

/// <summary>
/// API response wrapper for lists with typed metadata.
/// Follows envelope pattern: { metadata, data[] }
/// </summary>
/// <typeparam name="TItem">The type of items in the list.</typeparam>
/// <typeparam name="TMetadata">The type of metadata.</typeparam>
public record ApiListResponse<TItem, TMetadata>(
    TMetadata Metadata,
    IEnumerable<TItem> Data
);

/// <summary>
/// Standard metadata for list responses containing item count.
/// </summary>
public record ListMetadata(int Count);

/// <summary>
/// Metadata for admin list responses with admin context.
/// </summary>
public record AdminListMetadata(int Count, bool IsAdmin);

/// <summary>
/// Empty metadata for responses that don't need context.
/// Used as a type marker in <see cref="ApiResponse{TData, TMetadata}"/> when no metadata is needed.
/// </summary>
public record EmptyMetadata
{
    /// <summary>Singleton instance to avoid repeated allocations.</summary>
    public static readonly EmptyMetadata Instance = new();
}
