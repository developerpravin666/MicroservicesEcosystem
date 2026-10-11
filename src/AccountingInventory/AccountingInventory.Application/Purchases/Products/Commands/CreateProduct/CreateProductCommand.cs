using MediatR;

namespace AccountingInventory.Application.Purchases.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    Guid VendorId, Guid BrandId, Guid ProductTypeId, Guid ProductModelId, Guid VariantId, Guid ColorId,
    string SerialNumber, string? SerialNumber1, string? BillNumber, decimal PurchasePrice, decimal Discount,
    decimal Cgst, decimal Sgst, decimal Tax, DateOnly? PurchaseDate = null, int PaymentTermsDays = 0) : IRequest<CreateProductResult>;

public sealed record CreateProductResult(
    Guid Id, Guid VendorId, Guid BrandId, Guid ProductTypeId, Guid ProductModelId, Guid VariantId,
    Guid ColorId, string SerialNumber, string? SerialNumber1, decimal PurchasePrice,
    decimal TotalAmount, decimal Discount, decimal Cgst, decimal Sgst, decimal Tax, bool IsActive,
    bool IsSold, string? BillNumber);
