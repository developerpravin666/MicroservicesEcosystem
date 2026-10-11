using AccountingInventory.Application.Customers;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Application.Inventory;
using AccountingInventory.Application.Purchases.Accounting;
using AccountingInventory.Application.Sales.Invoices;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
internal static class SkuWorkflowChecks
{
 public static async Task RunAsync(AccountingInventoryDbContext db,Customer customer,Vendor vendor,Tax tax,Guid ownerId,DateOnly date)
 {
  var count=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
  var handler=new SkuInventoryHandler(db);
  var skuId=await handler.Handle(new CreateStockSkuCommand("CHARGER","Charger","8504","NOS"),default);
  var first=await handler.Handle(new ReceiveSkuStockCommand(skuId,vendor.Id,"SKU-P1",date,10,10,tax.Id),default);
  var second=await handler.Handle(new ReceiveSkuStockCommand(skuId,vendor.Id,"SKU-P2",date,10,20,tax.Id),default);
  var sku=await db.StockSkus.SingleAsync(s=>s.Id==skuId);
  Check(sku.Quantity==20 && sku.InventoryValue==300,"SKU purchases must add quantities and cost.");
  var bills=await new GetPurchaseBillsQueryHandler(db).Handle(new(Search:"SKU-P"),default);
  Check(bills.Items.Count==2 && bills.Items.Sum(b=>b.TotalAmount)==354,"SKU purchases must appear in supplier bills.");
  var bill=await new GetPurchaseBillDetailsQueryHandler(db).Handle(new(vendor.Id,"SKU-P1"),default);
  Check(bill.Bill.TotalAmount==118,"SKU supplier bill details must include GST.");
  var command=new CreateSalesInvoiceCommand(customer.Name,customer.Mobile,customer.Address,null,date.AddDays(1),0,null,
    [new(InvoiceItemType.StandardProduct,null,"Charger",null,null,4,30,TaxId:tax.Id,HsnSac:"8504",SkuId:skuId)],PlaceOfSupplyStateCode:"27");
  var invoice=await new CreateSalesInvoiceCommandHandler(db).Handle(command,default);
  Check(sku.Quantity==16 && sku.InventoryValue==240,"SKU sale must deduct moving-average cost.");
  Check(invoice.Lines.Single().SkuId==skuId,"Invoice details must preserve SKU identity.");
  var cost=await db.SkuMovements.SingleAsync(m=>m.SourceId==invoice.Invoice.Id && m.Kind=="Sale");
  Check(cost.InventoryValue==-60 && cost.Quantity==-4,"SKU sale movement must preserve exact cost.");
  var corrections=new InvoiceCorrectionHandler(db,new Identity(ownerId));
  await corrections.Handle(new ReturnInvoiceCommand("Sale",invoice.Invoice.Id,date.AddDays(2),"Charger return","Restock"),default);
  Check(sku.Quantity==20 && sku.InventoryValue==300,"SKU customer return must restore original sale cost.");
  await new RecordPurchasePaymentCommandHandler(db).Handle(new(vendor.Id,"SKU-P1",118,"Cash",date.AddDays(2),null,null),default);
  var note=await corrections.Handle(new ReturnInvoiceCommand("Purchase",first,date.AddDays(3),"Supplier return","Supplier"),default);
  Check(sku.Quantity==10 && sku.InventoryValue==150,"Supplier return must deduct current carrying cost.");
  Check(note.TotalAmount==118,"Supplier credit must reverse original invoice amount.");
  await corrections.Handle(new RefundCorrectionCommand(note.Id,date.AddDays(3),118,"Cash",null),default);
  await handler.Handle(new WriteOffSkuStockCommand(skuId,date.AddDays(4),2,"Damaged accessories"),default);
  Check(sku.Quantity==8 && sku.InventoryValue==120,"SKU writeoff must remove quantity and carrying cost.");
  var report=await new GetTaxReportHandler(db).Handle(new(date,date.AddDays(4)),default);
  Check(report.InputTaxCredit==36 && report.InputTaxCreditDifference==0 && report.OutputTaxDifference==0,"SKU taxes must reconcile after returns.");
  var reconcile=await new OpeningReconciliationHandler(db).Handle(new GetControlReconciliationQuery(date.AddDays(4)),default);
  Check(reconcile.StockDifference==0 && reconcile.PayableDifference==0 && reconcile.ReceivableDifference==0,"SKU stock and supplier balances must reconcile with ledger.");
  await new RecordPurchasePaymentCommandHandler(db).Handle(new(vendor.Id,"SKU-P2",236,"Cash",date.AddDays(4),null,null),default);
  async Task Reject(Func<Task> action,string message){try{await action();}catch(Exception e)when(e is ArgumentException or BuildingBlocks.Application.Exceptions.ConflictException){db.ChangeTracker.Clear();count++;return;}throw new Exception(message);}
  await Reject(()=>handler.Handle(new WriteOffSkuStockCommand(skuId,date.AddDays(4),9,"Too much"),default),"SKU negative stock must fail.");
  await Reject(()=>handler.Handle(new ReceiveSkuStockCommand(skuId,vendor.Id,"EARLY",date.AddDays(-1),1,10),default),"Backdated SKU movements must fail.");
  var moves=await new GetStockMovementsHandler(db).Handle(new(Search:"CHARGER"),default);
  Check(moves.TotalCount>=6,"SKU movements must appear in stock ledger.");
  var tx=db.Database.CurrentTransaction!;await tx.CreateSavepointAsync("sku_opening");
  var openingDate=new DateOnly(2025,1,1);
  var openingSku=await handler.Handle(new CreateStockSkuCommand("OPEN-CASE","Opening cases","3926","NOS"),default);
  await handler.Handle(new StageOpeningSkuStockCommand(openingSku,openingDate,5,20),default);
  Check(!await db.StockSkus.Where(s => s.Id == openingSku).Select(s => s.IsActive).SingleAsync(),"Staged SKU stock must be unavailable.");
  await Reject(()=>handler.Handle(new WriteOffSkuStockCommand(openingSku,openingDate,1,"Unavailable opening"),default),"Staged SKU stock must reject writeoff.");
  var accounts=await LedgerPosting.EnsureSystemAccountsAsync(db,default);
  await new ImportOpeningBalancesHandler(db).Handle(new ImportOpeningBalancesCommand(openingDate,[new(accounts["1200"],100,0)]),default);
  Check(await db.StockSkus.Where(s => s.Id == openingSku).Select(s => s.IsActive).SingleAsync(),"Opening import must activate reconciled SKU stock.");
  var openingControls=await new OpeningReconciliationHandler(db).Handle(new GetControlReconciliationQuery(openingDate),default);
  Check(openingControls.StockDifference==0,"Opening SKU value must reconcile with account 1200.");
  await Reject(()=>handler.Handle(new StageOpeningSkuStockCommand(openingSku,openingDate,1,20),default),"Opening staging after cutover must fail.");
  await tx.RollbackToSavepointAsync("sku_opening");db.ChangeTracker.Clear();
  await tx.CreateSavepointAsync("sku_down_guard");
  var blocked=false;
  try {
   foreach(var sql in db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator>().Generate(new AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb.AccessorySkuInventory().DownOperations,db.Model))
    await db.Database.ExecuteSqlRawAsync(sql.CommandText);
  }catch(Npgsql.PostgresException e)when(e.SqlState=="P0001"){blocked=true;await tx.RollbackToSavepointAsync("sku_down_guard");}
  Check(blocked && await db.SkuMovements.AnyAsync(),"Used SKU history must block downgrade.");
  Console.WriteLine($"PASS: {count} accessory purchase, payment, moving-average, sale, return, refund, GST and stock checks.");
 }
 private sealed class Identity(Guid id):AccountingInventory.Application.Abstractions.IRequestIdentity {public Guid? UserId=>id;}
}
