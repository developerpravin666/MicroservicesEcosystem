using MediatR;

namespace AccountingInventory.Application.Sales.Products.Commands.UpdateSalesProduct;

public sealed record UpdateSalesProductCommand(
    Guid Id,
    string ProductId,
    string CustomerName,
    string CustomerMobile,
    string CustomerAddress,
    string? CustomerEmail,
    DateOnly SaleDate,
    decimal ProductPrice,
    decimal SellingPrice,
    decimal Discount,
    Guid? TaxId = null, int PaymentTermsDays = 0) : IRequest<SalesProductSummary>;
