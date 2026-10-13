using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Application.Inventory;
using AccountingInventory.Application.Purchases.Products.Commands.CreateProduct;
using AccountingInventory.Application.Sales.Invoices;
using AccountingInventory.Application.Taxes;
using AccountingInventory.Application.Taxes.Commands.AddTax;
using AccountingInventory.Application.Taxes.Commands.UpdateTax;
using AccountingInventory.Application.Taxes.Commands.DeleteTax;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class DatedTaxWorkflowChecks
{
    private sealed class Identity(Guid id) : AccountingInventory.Application.Abstractions.IRequestIdentity { public Guid? UserId => id; }
    public static async Task RunAsync(AccountingInventoryDbContext db)
    {
        var tx = db.Database.CurrentTransaction!;
        await tx.CreateSavepointAsync("dated_tax_checks");
        var assertions = 0;
        void Check(bool success, string message) { if (!success) throw new Exception(message); assertions++; }
        async Task Reject(Func<Task> action, string message)
        {
            try { await action(); }
            catch (Exception e) when (e is ArgumentException or BuildingBlocks.Application.Exceptions.ConflictException) { db.ChangeTracker.Clear(); assertions++; return; }
            throw new Exception(message);
        }
        var before = new DateOnly(2026, 2, 5);
        var change = before.AddDays(5);
        var add = new AddTaxCommandHandler(db);
        var oldRate = await add.Handle(new(9, 9, "Dated fixture GST", before), default);
        var nextRate = await add.Handle(new(5, 5, "Dated fixture GST", change, oldRate.ProfileId), default);
        var resolver = await TaxRateResolver.LoadAsync(db, default);
        Check(resolver.Resolve(oldRate.Id, change.AddDays(-1))!.TotalTax == 18, "The day before the change must use the old rate.");
        Check(resolver.Resolve(oldRate.Id, change)!.Id == nextRate.Id, "The effective date must use the new version even when an old ID is selected.");
        Check(resolver.Resolve(nextRate.Id, before)!.Id == oldRate.Id, "A backdated transaction must resolve the historical version.");
        await Reject(() => Task.FromResult(resolver.Resolve(oldRate.Id, before.AddDays(-1))), "Missing dated rate must be rejected.");
        await Reject(() => add.Handle(new(6, 6, "Dated fixture GST", change, oldRate.ProfileId), default), "Duplicate effective dates must be rejected.");
        await Reject(() => new UpdateTaxCommandHandler(db).Handle(new(oldRate.Id, 6, 6, true), default), "Existing rate versions must not be overwritten.");
        var vendor = Vendor.Create("Dated vendor", "DATED-V", "7234567890", "dated@example.com", null, "Address");
        var brand = Brand.Create("Dated brand", null);
        var type = ProductType.Create("Dated phones", null, "85171300", oldRate.ProfileId);
        var model = ProductModel.Create(brand.Id, type.Id, "DATED-M", "Dated model");
        var variant = Variant.Create("Dated variant", null); var color = Color.Create("Dated color", null);
        db.AddRange(vendor, brand, type, model, variant, color); await db.SaveChangesAsync();
        var purchaseHandler = new CreateProductCommandHandler(db, NullLogger<CreateProductCommandHandler>.Instance);
        var purchase = new CreateProductCommand(vendor.Id, brand.Id, type.Id, model.Id, variant.Id, color.Id, "DATED-1", "DATED-A", "DATED-BILL1", 100, 0, 0, 0, 0, before);
        var stock1 = await purchaseHandler.Handle(purchase, default);
        var stock2 = await purchaseHandler.Handle(purchase with { SerialNumber = "DATED-2", SerialNumber1 = "DATED-B", BillNumber = "DATED-BILL2" }, default);
        Check(stock1.TaxId == oldRate.Id && stock1.TotalAmount == 118, "Purchase must snapshot the type default on the purchase date.");
        var saleHandler = new CreateSalesInvoiceCommandHandler(db);
        var saleCommand = new CreateSalesInvoiceCommand("Dated customer", "8234567890", "Address", null, before, 0, null,
            [new(InvoiceItemType.SerializedProduct, stock1.Id, "Dated phone", null, null, 1, 100)], PlaceOfSupplyStateCode: "27");
        var soldOld = await saleHandler.Handle(saleCommand, default);
        var soldNew = await saleHandler.Handle(saleCommand with { InvoiceDate = change, Lines = [saleCommand.Lines[0] with { ProductId = stock2.Id, TaxId = oldRate.Id }] }, default);
        Check(soldOld.Invoice.TotalAmount == 118 && soldNew.Invoice.TotalAmount == 110, "Old stock sold after a slab change must use the new sale GST.");
        Check(soldOld.Lines.Single().TaxId == oldRate.Id && soldNew.Lines.Single().TaxId == nextRate.Id, "Invoices must snapshot the actual resolved rate version.");
        Check((await db.Products.SingleAsync(p => p.Id == stock2.Id)).TotalAmount == 118, "New sale GST must not revalue the original purchase or input GST.");
        var later = await add.Handle(new(2.5m, 2.5m, "Dated fixture GST", change.AddDays(5), oldRate.ProfileId), default);
        var reread = await new GetSalesInvoiceDetailsQueryHandler(db).Handle(new(soldOld.Invoice.Id), default);
        Check(reread.Invoice.TotalAmount == 118 && reread.Lines.Single().CgstRate == 9, "Scheduling another slab must preserve issued invoice history.");
        var snapshot = await db.InvoiceSnapshots.SingleAsync(s => s.SourceId == soldOld.Invoice.Id);
        Check(snapshot.DetailsJson.Contains("\"cgstRate\":9"), "Printed snapshots must preserve the original rate.");
        var overrideSale = await saleHandler.Handle(saleCommand with { InvoiceDate = change, TaxPointDate = before, TaxDateReason = "Fixture supply occurred before change", Lines = [new(InvoiceItemType.Service, null, "Dated service", null, null, 1, 100, TaxId: oldRate.ProfileId, HsnSac: "998599")] }, default);
        Check(overrideSale.Invoice.TotalAmount == 118 && overrideSale.TaxPointDate == before, "An explicit audited tax-effective date must resolve its own version.");
        var skuHandler = new SkuInventoryHandler(db);
        var skuId = await skuHandler.Handle(new CreateStockSkuCommand("DATED-SKU", "Dated accessory", "8504", "NOS", oldRate.ProfileId), default);
        var receipt = await skuHandler.Handle(new ReceiveSkuStockCommand(skuId, vendor.Id, "DATED-SKU-BILL", change, 2, 100), default);
        var movement = await db.SkuMovements.SingleAsync(m => m.Id == receipt);
        Check(movement.TaxId == nextRate.Id && movement.TotalAmount == 220, "SKU receipts must use the dated profile default.");
        var zero = await add.Handle(new(0, 0, "Exempt fixture", before, Treatment: "Exempt"), default);
        var exempt = await saleHandler.Handle(saleCommand with { Lines = [new(InvoiceItemType.Service, null, "Exempt service", null, null, 1, 100, TaxId: zero.Id, HsnSac: "998599")] }, default);
        Check(exempt.Lines.Single().TaxTreatment == "Exempt" && exempt.Invoice.TotalAmount == 100, "Explicit zero-tax treatment must be preserved separately from the rate.");
        var ownerId = await db.Users.Where(u => u.IsOwner).Select(u => u.Id).FirstAsync();
        var returned = await new InvoiceCorrectionHandler(db, new Identity(ownerId)).Handle(new ReturnInvoiceCommand("Sale", soldOld.Invoice.Id, change.AddDays(6), "Dated fixture return", "Restock"), default);
        Check(returned.TotalAmount == 118 && returned.CgstAmount == 9 && returned.SgstAmount == 9, "A return after later slab changes must reverse the original sale GST.");
        await Reject(() => new DeleteTaxCommandHandler(db).Handle(new(later.Id), default), "Used profile history must not be deleted.");
        var editable = Product.Create(vendor.Id, brand.Id, type.Id, model.Id, variant.Id, color.Id, "DATED-EDIT", "DATED-EDIT-A", "DATED-UNPOSTED", 100, 0, 9, 9, 18, before);
        editable.SetTaxDetails(oldRate.Id, "Taxable"); db.Products.Add(editable); await db.SaveChangesAsync();
        var editCommand = new AccountingInventory.Application.Purchases.Products.Commands.UpdateProduct.UpdateProductCommand(editable.Id, vendor.Id, brand.Id, type.Id, model.Id, variant.Id, color.Id, editable.SerialNumber, editable.SerialNumber1, editable.BillNumber, 100, 0, 9, 9, 18, before, TaxId: zero.ProfileId);
        var edited = await new AccountingInventory.Application.Purchases.Products.Commands.BulkUpdateProducts.BulkUpdateProductsCommandHandler(db).Handle(new([editCommand]), default);
        Check(edited.Single().TaxTreatment == "Exempt" && edited.Single().TotalAmount == 100, "Unposted purchase bulk overrides must apply the new rate before validating its treatment.");
        var report = await new GetTaxReportHandler(db).Handle(new(before, change.AddDays(6)), default);
        Check(report.Sales.Any(s => s.TaxTreatment == "Exempt") && report.OutputTaxDifference == 0 && report.InputTaxCreditDifference == 0, "Dated and exempt GST must reconcile with saved ledger postings.");
        await tx.RollbackToSavepointAsync("dated_tax_checks"); db.ChangeTracker.Clear();
        Console.WriteLine($"PASS: {assertions} dated GST, purchase/sale separation, backdated rates, immutable snapshots, SKU defaults and treatment checks.");
    }
}
