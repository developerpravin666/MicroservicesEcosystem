namespace AccountingInventory.Application.Common.Models;

/// <summary>A page of list results and the paging metadata needed to navigate the full result set.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
