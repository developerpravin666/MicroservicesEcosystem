using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.Inventory.Queries.GetStock;

public sealed record GetStockQuery(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, string? SortDirection = null)
    : IRequest<PagedResult<StockGroupSummary>>;

public sealed record StockGroupSummary(
    Guid BrandId,
    Guid ProductModelId,
    Guid VariantId,
    string BrandName,
    string ModelName,
    string VariantName,
    decimal TotalProductCost,
    int TotalQuantity);

public sealed record GetAvailableStockProductsQuery(
    Guid BrandId,
    Guid ProductModelId,
    Guid VariantId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null, string? SortBy = null, string? SortDirection = null) : IRequest<PagedResult<AvailableStockProductSummary>>;

public sealed record AvailableStockProductSummary(
    Guid Id,
    string SerialNumber,
    string? SerialNumber1,
    string ColorName,
    decimal TotalAmount);
