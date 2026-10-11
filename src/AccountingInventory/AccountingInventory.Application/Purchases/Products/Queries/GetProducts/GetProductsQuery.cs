using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.Purchases.Products.Queries.GetProducts;

public sealed record GetProductsQuery(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, string? SortDirection = null)
    : IRequest<PagedResult<ProductSummary>>;

public sealed record ProductSummary(Guid Id, Guid VendorId, Guid BrandId, Guid ProductTypeId,
    Guid ProductModelId, Guid VariantId, Guid ColorId, string SerialNumber, string? SerialNumber1,
    decimal PurchasePrice, decimal TotalAmount, decimal Discount, decimal Cgst, decimal Sgst, decimal Tax,
    bool IsActive, bool IsSold, string? BillNumber, DateOnly PurchaseDate, int PaymentTermsDays, DateOnly DueDate, string VendorName, string BrandName, string ProductTypeName,
    string ProductModelName, string VariantName, string ColorName);
