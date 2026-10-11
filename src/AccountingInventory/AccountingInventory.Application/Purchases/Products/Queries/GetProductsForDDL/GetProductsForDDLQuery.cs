using MediatR;

namespace AccountingInventory.Application.Purchases.Products.Queries.GetProductsForDDL;

public sealed record GetProductsForDDLQuery(Guid? CurrentSaleId = null) : IRequest<IEnumerable<GetProductsForDDLSummary>>;

public sealed record GetProductsForDDLSummary(Guid Id, string Brand, string ProductType, string ProductModel, string Variant,
string Color, string SerialNumber, string SerialNumber1, decimal PurchasePrice, decimal TotalAmount, decimal Discount,
decimal Cgst = 0, decimal Sgst = 0);
