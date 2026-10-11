using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Dashboard;

public sealed record DashboardMetricCardDto(
    decimal MonthlyRevenue,
    decimal TotalReceivables,
    decimal TotalPayables,
    decimal NetGstLiability,
    decimal InventoryValuation,
    int InStockUnits,
    int UnpaidInvoicesCount);

public sealed record DashboardRecentInvoiceDto(
    Guid Id,
    string BillNumber,
    string CustomerName,
    DateOnly InvoiceDate,
    decimal TotalAmount,
    decimal Balance,
    string PaymentStatus);

public sealed record DashboardLowStockDto(
    string Brand,
    string ProductModel,
    int AvailableCount);

public sealed record DashboardSummaryDto(
    DashboardMetricCardDto Metrics,
    IReadOnlyList<DashboardRecentInvoiceDto> RecentInvoices,
    IReadOnlyList<DashboardLowStockDto> LowStockAlerts);

public sealed record GetDashboardSummaryQuery() : IRequest<DashboardSummaryDto>;

public sealed class GetDashboardSummaryQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);

        // 1. Monthly Revenue
        var monthlyRevenue = await (from line in db.JournalLines join entry in db.JournalEntries on line.JournalEntryId equals entry.Id
            join account in db.ChartAccounts on line.AccountId equals account.Id
            where entry.JournalDate >= firstOfMonth && entry.JournalDate <= today && entry.SourceType != "YearEndClose"
                && (account.Code == "4000" || account.Code == "4100")
            select (decimal?)(line.Credit - line.Debit)).SumAsync(ct) ?? 0m;

        // 2. Total Receivables (Customer balance due)
        var aging = await new GetAgingReportHandler(db).Handle(new GetAgingReportQuery(today), ct);
        var totalInvoiceReceivables = aging.TotalReceivables;
        var unpaidInvoicesCount = aging.Receivables.TotalCount;
        // 3. Inventory Units & Valuation
        var inStockProducts = await db.Products.AsNoTracking()
            .Where(p => !p.IsDeleted && !p.IsSold && p.IsActive)
            .Select(p => new { p.BrandId, p.ProductModelId, Cost = p.PurchasePrice - p.Discount })
            .ToListAsync(ct);

        var inStockUnits = inStockProducts.Count;
        var inventoryValuation = inStockProducts.Sum(p => p.Cost) + (await db.StockSkus.Where(s => s.IsActive).SumAsync(s => (decimal?)s.InventoryValue, ct) ?? 0m);

        // 4. Low stock alerts (brands/models with <= 2 units)
        var lowStockGrouped = inStockProducts
            .GroupBy(p => new { p.BrandId, p.ProductModelId })
            .Select(g => new { g.Key.BrandId, g.Key.ProductModelId, Count = g.Count() })
            .Where(x => x.Count <= 2)
            .OrderBy(x => x.Count)
            .Take(5)
            .ToList();

        var brandIds = lowStockGrouped.Select(x => x.BrandId).Distinct().ToList();
        var modelIds = lowStockGrouped.Select(x => x.ProductModelId).Distinct().ToList();

        var brandMap = await db.Brands.AsNoTracking()
            .Where(b => brandIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var modelMap = await db.ProductModels.AsNoTracking()
            .Where(m => modelIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        var lowStockAlerts = lowStockGrouped
            .Select(x => new DashboardLowStockDto(
                brandMap.TryGetValue(x.BrandId, out var bName) ? bName : "Brand",
                modelMap.TryGetValue(x.ProductModelId, out var mName) ? mName : "Model",
                x.Count))
            .ToList();

        // 5. Total Payables (Vendor balances) & GST liability
        var gstOutput = await (from line in db.JournalLines.AsNoTracking()
                               join account in db.ChartAccounts.AsNoTracking() on line.AccountId equals account.Id
                               where account.Code == "2100"
                               select (decimal?)(line.Credit - line.Debit)).SumAsync(ct) ?? 0m;

        var gstInput = await (from line in db.JournalLines.AsNoTracking()
                              join account in db.ChartAccounts.AsNoTracking() on line.AccountId equals account.Id
                              where account.Code == "2200"
                              select (decimal?)(line.Debit - line.Credit)).SumAsync(ct) ?? 0m;

        var netGstLiability = Math.Max(0m, gstOutput - gstInput);

        var accountsPayable = await (from line in db.JournalLines.AsNoTracking()
                                     join account in db.ChartAccounts.AsNoTracking() on line.AccountId equals account.Id
                                     where account.Code == "2000"
                                     select (decimal?)(line.Credit - line.Debit)).SumAsync(ct) ?? 0m;

        // 6. Recent Sales Invoices
        var recentInvoices = await db.SalesInvoices.AsNoTracking()
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.CreatedAt)
            .Take(6)
            .ToListAsync(ct);

        var customerIds = recentInvoices.Select(i => i.CustomerId).Distinct().ToList();
        var customerMap = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var recentList = recentInvoices.Select(i => new DashboardRecentInvoiceDto(
            i.Id,
            i.BillNumber,
            customerMap.TryGetValue(i.CustomerId, out var name) ? name : "Customer",
            i.InvoiceDate,
            i.TotalAmount,
            i.Balance,
            i.PaymentStatus
        )).ToList();

        var metrics = new DashboardMetricCardDto(
            decimal.Round(monthlyRevenue, 2),
            decimal.Round(totalInvoiceReceivables, 2),
            decimal.Round(Math.Max(0m, accountsPayable), 2),
            decimal.Round(netGstLiability, 2),
            decimal.Round(inventoryValuation, 2),
            inStockUnits,
            unpaidInvoicesCount);

        return new DashboardSummaryDto(metrics, recentList, lowStockAlerts);
    }
}

