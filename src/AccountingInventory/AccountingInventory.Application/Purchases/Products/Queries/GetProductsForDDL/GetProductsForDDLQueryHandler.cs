using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Purchases.Products.Queries.GetProductsForDDL;

public sealed class GetProductsForDDLQueryHandler(IAccountingInventoryDbContext dbContext)
    : IRequestHandler<GetProductsForDDLQuery, IEnumerable<GetProductsForDDLSummary>>
{
    public async Task<IEnumerable<GetProductsForDDLSummary>> Handle(
        GetProductsForDDLQuery request, CancellationToken cancellationToken)
    {
        Guid? currentProductId = null;
        if (request.CurrentSaleId is { } currentSaleId)
        {
            var currentProduct = await dbContext.SalesProducts.AsNoTracking()
                .Where(sale => sale.Id == currentSaleId)
                .Select(sale => sale.ProductId)
                .FirstOrDefaultAsync(cancellationToken);
            if (Guid.TryParse(currentProduct, out var parsedProductId))
                currentProductId = parsedProductId;
        }

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive
                && (!product.IsSold || product.Id == currentProductId))
            .OrderBy(product => product.SerialNumber)
            .Select(product => new GetProductsForDDLSummary(
                product.Id,
                dbContext.Brands
                    .Where(brand => brand.Id == product.BrandId)
                    .Select(brand => brand.Name)
                    .FirstOrDefault() ?? string.Empty,
                dbContext.ProductTypes
                    .Where(type => type.Id == product.ProductTypeId)
                    .Select(type => type.Name)
                    .FirstOrDefault() ?? string.Empty,
                dbContext.ProductModels
                    .Where(model => model.Id == product.ProductModelId)
                    .Select(model => model.Name)
                    .FirstOrDefault() ?? string.Empty,
                dbContext.Variants
                    .Where(variant => variant.Id == product.VariantId)
                    .Select(variant => variant.Name)
                    .FirstOrDefault() ?? string.Empty,
                dbContext.Colors
                    .Where(color => color.Id == product.ColorId)
                    .Select(color => color.Name)
                    .FirstOrDefault() ?? string.Empty,
                product.SerialNumber,
                product.SerialNumber1!,
                product.PurchasePrice,
                product.TotalAmount,
                product.Discount,
                product.Cgst,
                product.Sgst))
            .ToListAsync(cancellationToken);

        return products;
    }
}
