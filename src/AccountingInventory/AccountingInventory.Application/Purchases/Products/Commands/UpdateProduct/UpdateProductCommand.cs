using MediatR;

namespace AccountingInventory.Application.Purchases.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id, Guid VendorId, Guid BrandId, Guid ProductTypeId, Guid ProductModelId, Guid VariantId, Guid ColorId,
    string SerialNumber, string? SerialNumber1, string? BillNumber, decimal PurchasePrice, decimal Discount,
    decimal Cgst, decimal Sgst, decimal Tax, DateOnly? PurchaseDate = null, int PaymentTermsDays = 0,
    bool? IsActive = null, string? HsnSac = null, Guid? TaxId = null, string TaxTreatment = "Taxable") : IRequest<UpdateProductResult>;

public sealed record UpdateProductResult(
    Guid Id, Guid VendorId, Guid BrandId, Guid ProductTypeId, Guid ProductModelId, Guid VariantId,
    Guid ColorId, string SerialNumber, string? SerialNumber1, decimal PurchasePrice,
    decimal TotalAmount, decimal Discount, decimal Cgst, decimal Sgst, decimal Tax, bool IsActive,
    bool IsSold, string? BillNumber, string? HsnSac = null, Guid? TaxId = null, string TaxTreatment = "Taxable");
