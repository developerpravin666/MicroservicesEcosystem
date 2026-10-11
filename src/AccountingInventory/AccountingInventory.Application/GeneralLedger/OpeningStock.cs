using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Purchases.Products;
using AccountingInventory.Application.Purchases.Products.Commands.CreateProduct;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record ImportOpeningStockCommand(CreateProductCommand Product) : IRequest<Guid>;
public sealed class ImportOpeningStockHandler(IAccountingInventoryDbContext db) : IRequestHandler<ImportOpeningStockCommand, Guid>
{
    public async Task<Guid> Handle(ImportOpeningStockCommand q, CancellationToken ct)
    {
        var validation = await new CreateProductCommandValidator().ValidateAsync(q.Product, ct);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);
        if (await db.JournalEntries.AnyAsync(x => x.SourceType == "OpeningBalances", ct))
            throw new ConflictException("Opening stock must be imported before the opening journal.");
        var p = q.Product;
        await PurchaseIntegrity.ValidateAsync(db, [new(p.VendorId, p.BrandId, p.ProductTypeId, p.ProductModelId,
            p.VariantId, p.ColorId, p.BillNumber, p.PurchaseDate, p.PaymentTermsDays)], ct);
        var serials = new[] { p.SerialNumber.Trim().ToLowerInvariant(), p.SerialNumber1?.Trim().ToLowerInvariant() ?? "" };
        if (serials[0] == serials[1] || await db.Products.AnyAsync(x => serials.Contains(x.SerialNumber.ToLower())
            || (x.SerialNumber1 != null && serials.Contains(x.SerialNumber1.ToLower())), ct))
            throw new ConflictException("Opening stock serial numbers must be unique.");
        var product = Product.Create(p.VendorId, p.BrandId, p.ProductTypeId, p.ProductModelId, p.VariantId, p.ColorId,
            p.SerialNumber, p.SerialNumber1, p.BillNumber, p.PurchasePrice, p.Discount, p.Cgst, p.Sgst, p.Tax, p.PurchaseDate, p.PaymentTermsDays);
        product.StageOpeningStock();
        db.Products.Add(product); await db.SaveChangesAsync(ct); return product.Id;
    }
}
