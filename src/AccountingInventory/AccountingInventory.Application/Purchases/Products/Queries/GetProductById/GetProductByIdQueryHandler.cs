using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Purchases.Products.Queries.GetProducts;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Purchases.Products.Queries.GetProductById;

public sealed class GetProductByIdQueryHandler(IAccountingInventoryDbContext dbContext)
    : IRequestHandler<GetProductByIdQuery, ProductSummary>
{
    public async Task<ProductSummary> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new ProductSummary(x.Id, x.VendorId, x.BrandId, x.ProductTypeId, x.ProductModelId,
                x.VariantId, x.ColorId, x.SerialNumber, x.SerialNumber1, x.PurchasePrice,
                x.TotalAmount, x.Discount, x.Cgst, x.Sgst, x.Tax, x.IsActive, x.IsSold, x.BillNumber, x.PurchaseDate, x.PaymentTermsDays, x.DueDate,
                dbContext.Vendors.Where(v => v.Id == x.VendorId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.Brands.Where(v => v.Id == x.BrandId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.ProductTypes.Where(v => v.Id == x.ProductTypeId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.ProductModels.Where(v => v.Id == x.ProductModelId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.Variants.Where(v => v.Id == x.VariantId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.Colors.Where(v => v.Id == x.ColorId).Select(v => v.Name).FirstOrDefault() ?? string.Empty))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"A Product with ID '{request.Id}' was not found.");
        return product;
    }
}
