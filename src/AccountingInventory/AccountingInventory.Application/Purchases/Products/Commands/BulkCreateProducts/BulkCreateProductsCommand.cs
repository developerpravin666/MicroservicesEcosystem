using AccountingInventory.Application.Purchases.Products.Commands.CreateProduct;
using MediatR;

namespace AccountingInventory.Application.Purchases.Products.Commands.BulkCreateProducts;

public sealed record DuplicateProductDto(
    string SerialNumber,
    string? SerialNumber1,
    string? BillNumber,
    string Reason
);

public sealed record BulkCreateProductsResult(
    IReadOnlyList<CreateProductResult> Products,
    IReadOnlyList<DuplicateProductDto> Duplicates,
    int TotalInserted,
    int TotalDuplicates
);

public sealed record BulkCreateProductsCommand(IReadOnlyList<CreateProductCommand> Products)
    : IRequest<BulkCreateProductsResult>;
