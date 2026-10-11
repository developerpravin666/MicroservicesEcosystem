using System.Text.Json;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Brands.Queries.GetBrands;
using AccountingInventory.Application.Colors.Queries.GetColors;
using AccountingInventory.Application.Variants.Queries.GetVariants;
using AccountingInventory.Application.Vendors.Queries.GetVendors;
using AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendors;
using AccountingInventory.Application.ProductTypes.Queries.GetProductTypes;
using AccountingInventory.Application.ProductModels.Queries.GetProductModels;
using AccountingInventory.Application.Purchases.Products.Queries.GetProducts;
using AccountingInventory.Application.Sales.Products.Queries.GetSalesProducts;
using AccountingInventory.Application.Inventory.Queries.GetStock;
using AccountingInventory.Application.Taxes.Queries.GetTaxes;
using AccountingInventory.Application.Customers;
using AccountingInventory.Application.Purchases.Accounting;
using AccountingInventory.Application.Sales.Accounting;
using AccountingInventory.Application.AuditLogs;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("GRID_CHECK_CONNECTION_STRING");
if (args.Contains("--local"))
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,
        "src/AccountingInventory/AccountingInventory.Api/appsettings.json")));
    connectionString = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("AccountingInventoryDb").GetString();
    if (new NpgsqlConnectionStringBuilder(connectionString).Host is not ("localhost" or "127.0.0.1" or "::1"))
        throw new Exception("--local requires a local PostgreSQL host.");
}
if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception("Set GRID_CHECK_CONNECTION_STRING or pass --local.");
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();
var schema = "grid_check_" + Guid.NewGuid().ToString("N");
await using (var command = new NpgsqlCommand($"CREATE SCHEMA {schema}; SET LOCAL search_path TO {schema};", connection, transaction))
    await command.ExecuteNonQueryAsync();
var options = new DbContextOptionsBuilder<AccountingInventoryDbContext>()
    .UseNpgsql(connection).UseSnakeCaseNamingConvention().Options;
await using var db = new AccountingInventoryDbContext(options, new TestTenantProvider(schema));
await db.Database.UseTransactionAsync(transaction);
try
{
    await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
    var names = new[] { "Zulu", "Alpha", "Mike" };
    var brands = new List<Brand>();
    var models = new List<ProductModel>();
    var variants = new List<Variant>();
    for (var index = 0; index < names.Length; index++)
    {
        var name = names[index];
        var vendor = Vendor.Create(name, "V" + index, "900000000" + index, name + "@example.com", name, name);
        var brand = Brand.Create(name, name);
        var type = ProductType.Create(name, name);
        var model = ProductModel.Create(brand.Id, type.Id, "M" + index, name, name);
        var variant = Variant.Create(name, name);
        var color = Color.Create(name, name);
        var customer = Customer.Create(name, "800000000" + index, name, name + "@example.com");
        db.AddRange(vendor, brand, type, model, variant, color, customer,
            FinanceVendor.Create(name, "F" + index, "700000000" + index, name + "@example.com", name, "600000000" + index, name),
            Tax.Create(index + 1, index + 1));
        brands.Add(brand); models.Add(model); variants.Add(variant);
        for (var unit = 0; unit < index + 4; unit++)
        {
            var product = Product.Create(vendor.Id, brand.Id, type.Id, model.Id, variant.Id, color.Id,
                name + unit, name + "-alt-" + unit, "B" + index,
                100 * (index + 1) + unit, index, index + 1, index + 1, 2 * (index + 1));
            db.Products.Add(product);
            if (unit != 0) continue;
            product.MarkSold();
            var sale = SalesProduct.Create("S" + index, product.Id.ToString(), customer.Id,
                new DateOnly(2026, 1, index + 1), product.TotalAmount, 100 * (index + 1), 0, null, index + 1, index + 1);
            db.SalesProducts.Add(sale);
            if (index > 0)
            {
                var amount = index == 1 ? 200m : 10m;
                db.SalesReceipts.Add(SalesReceipt.Create(sale.Id, amount, "Cash", sale.SaleDate, null, null));
                db.PurchasePayments.Add(PurchasePayment.Create(vendor.Id, "B" + index, amount, "Cash", sale.SaleDate, null, null));
            }
        }
    }
    await db.SaveChangesAsync();

    if (!args.Contains("--business-only"))
    {
    var checks = 0;
    await CheckPages((field, direction, page) => new GetBrandsQueryHandler(db).Handle(new( page, 1, field, direction), default), ["name", "description", "isActive"]);
    await CheckPages((field, direction, page) => new GetColorsQueryHandler(db).Handle(new(page, 1, field, direction), default), ["name", "description", "isActive"]);
    await CheckPages((field, direction, page) => new GetVariantsQueryHandler(db).Handle(new(page, 1, field, direction), default), ["name", "description", "isActive"]);
    await CheckPages((field, direction, page) => new GetProductTypesQueryHandler(db).Handle(new(page, 1, field, direction), default), ["name", "description", "isActive"]);
    await CheckPages((field, direction, page) => new GetVendorsQueryHandler(db).Handle(new(page, 1, field, direction), default), ["name", "code", "mobile", "email", "address", "description", "isActive"]);
    await CheckPages((field, direction, page) => new GetFinanceVendorsQueryHandler(db).Handle(new(page, 1, field, direction), default), ["name", "code", "mobile", "email", "contactName", "contactMobile", "isActive"]);
    await CheckPages((field, direction, page) => new GetProductModelsQueryHandler(db).Handle(new(page, 1, field, direction), default), ["name", "code", "brandName", "productTypeName", "isActive"]);
    await CheckPages((field, direction, page) => new GetProductsQueryHandler(db).Handle(new(page, 1, SortBy: field, SortDirection: direction), default), ["serialNumber", "serialNumber1", "vendorName", "brandName", "productTypeName", "productModelName", "variantName", "colorName", "purchasePrice", "totalAmount", "discount", "tax", "isSold"]);
    await CheckPages((field, direction, page) => new GetSalesProductsQueryHandler(db).Handle(new(page, 1, SortBy: field, SortDirection: direction), default), ["productName", "serialNumber", "billNumber", "customerName", "customerMobile", "customerEmail", "saleDate", "productPrice", "sellingPrice", "discount", "paymentMode"]);
    await CheckPages((field, direction, page) => new GetStockQueryHandler(db).Handle(new(page, 1, SortBy: field, SortDirection: direction), default), ["brandName", "modelName", "variantName", "totalQuantity", "totalProductCost"]);
    await CheckPages((field, direction, page) => new GetAvailableStockProductsQueryHandler(db).Handle(new(brands[0].Id, models[0].Id, variants[0].Id, page, 1, SortBy: field, SortDirection: direction), default), ["serialNumber", "serialNumber1", "colorName", "totalAmount"]);
    await CheckPages((field, direction, page) => new GetTaxesQueryHandler(db).Handle(new(page, 1, field, direction), default), ["cgst", "sgst", "totalTax", "isActive"]);
    await CheckPages((field, direction, page) => new GetCustomersQueryHandler(db).Handle(new(page, 1, SortBy: field, SortDirection: direction), default), ["name", "mobile", "email", "address", "salesCount", "isActive"]);
    await CheckPages((field, direction, page) => new GetPurchaseBillsQueryHandler(db).Handle(new(page, 1, SortBy: field, SortDirection: direction), default), ["vendorName", "billNumber", "billDate", "dueDate", "paymentTermsDays", "totalAmount", "amountPaid", "balance", "paymentStatus"]);
    await CheckPages((field, direction, page) => new GetSalesBillsQueryHandler(db).Handle(new(page, 1, SortBy: field, SortDirection: direction), default), ["productName", "customerName", "customerMobile", "billNumber", "billDate", "dueDate", "paymentTermsDays", "totalAmount", "amountPaid", "balance", "paymentStatus"]);
    await CheckPages((field, direction, page) => new GetAuditLogsQueryHandler(db).Handle(new(page, 1, SortBy: field, SortDirection: direction), default), ["tableName", "action", "createdDate", "createdByName"]);

    var multi = await GridSorting.Apply(db.Products, "isSold,purchasePrice", "asc,desc", "SerialNumber").ToListAsync();
    for (var index = 1; index < multi.Count; index++)
        Assert(multi[index - 1].IsSold.CompareTo(multi[index].IsSold) < 0
            || multi[index - 1].IsSold == multi[index].IsSold && multi[index - 1].PurchasePrice >= multi[index].PurchasePrice,
            "Multi-column sort preserves priority and mixed directions");
    var fallback = await new GetBrandsQueryHandler(db).Handle(new(SortBy: "unknown", SortDirection: "desc"), default);
    Assert(fallback.Items[0].Name == "Alpha", "Unknown sort field uses default order");
    var defaultBills = await new GetPurchaseBillsQueryHandler(db).Handle(new(), default);
    Assert(defaultBills.Items[0].BillNumber == "B0", "Default purchase bill order retains ascending bill number for equal dates");
    Console.WriteLine($"PASS: {checks} ascending/descending checks across 16 grid endpoints, pagination, multi-sort and unknown-field fallback.");

    async Task CheckPages<T>(Func<string, string, int, Task<PagedResult<T>>> load, string[] fields)
    {
        foreach (var field in fields)
        foreach (var direction in new[] { "asc", "desc" })
        {
            var first = await load(field, direction, 1);
            Assert(first.TotalCount > 1, "Fixture must cover multiple pages");
            var property = typeof(T).GetProperties().Single(item => string.Equals(item.Name, field, StringComparison.OrdinalIgnoreCase));
            var pages = new List<T>();
            for (var page = 1; page <= first.TotalCount; page++)
                pages.Add((await load(field, direction, page)).Items.Single());
            for (var index = 1; index < pages.Count; index++)
            {
                var left = property.GetValue(pages[index - 1]);
                var right = property.GetValue(pages[index]);
                var compared = left is null ? right is null ? 0 : 1 : right is null ? -1 : ((IComparable)left).CompareTo(right);
                Assert(direction == "asc" ? compared <= 0 : compared >= 0, $"{typeof(T).Name}.{field} {direction} must sort before pagination");
            }
            checks++;
        }
        Console.WriteLine($"PASS: {typeof(T).Name} sorting across pages");
    }
    }
    await BusinessIntegrityChecks.RunAsync(db);
    var p0Schema = "p0_check_" + Guid.NewGuid().ToString("N");
    await using (var command = new NpgsqlCommand($"CREATE SCHEMA {p0Schema}; SET LOCAL search_path TO {p0Schema};", connection, transaction))
        await command.ExecuteNonQueryAsync();
    await using var p0Db = new AccountingInventoryDbContext(options, new TestTenantProvider(p0Schema));
    await p0Db.Database.UseTransactionAsync(transaction);
    await p0Db.Database.ExecuteSqlRawAsync(p0Db.Database.GenerateCreateScript());
    await P0WorkflowChecks.RunAsync(p0Db);
    await P0MigrationChecks.RunAsync(p0Db);
    var invoiceSchema = "invoice_check_" + Guid.NewGuid().ToString("N");
    await using (var command = new NpgsqlCommand($"CREATE SCHEMA {invoiceSchema}; SET LOCAL search_path TO {invoiceSchema};", connection, transaction))
        await command.ExecuteNonQueryAsync();
    await using var invoiceDb = new AccountingInventoryDbContext(options, new TestTenantProvider(invoiceSchema));
    await invoiceDb.Database.UseTransactionAsync(transaction);
    await invoiceDb.Database.ExecuteSqlRawAsync(invoiceDb.Database.GenerateCreateScript());
    await InvoiceWorkflowChecks.RunAsync(invoiceDb);
}
finally
{
    await transaction.RollbackAsync(); // Temporary schema and fixture data are discarded.
}
await TransactionChecks.RunAsync(connectionString!);
await P0ApiChecks.RunAsync(connectionString!);
static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
sealed class TestTenantProvider(string schema) : ITenantProvider
{
    public string SchemaName => schema;
}
