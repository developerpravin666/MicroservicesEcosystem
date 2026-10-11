using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

/// <summary>Creates immutable, balanced source journals in the same SaveChanges unit as the source transaction.</summary>
public static class LedgerPosting
{
    public static async Task EnsurePeriodsOpenAsync(IAccountingInventoryDbContext db,
        IEnumerable<DateOnly> journalDates, CancellationToken ct)
    {
        var dates = journalDates.Distinct().ToArray();
        if (dates.Length == 0) return;
        var earliest = dates.Min();
        var latest = dates.Max();
        var cutover = await db.JournalEntries.AsNoTracking().Where(x => x.SourceType == "OpeningBalances")
            .Select(x => (DateOnly?)x.JournalDate).SingleOrDefaultAsync(ct);
        if (cutover.HasValue && dates.Any(x => x <= cutover.Value))
            throw new ConflictException("Operational postings must be dated after the opening-balance cutover date.");
        var closedPeriods = await db.AccountingPeriods.AsNoTracking()
            .Where(period => period.IsClosed && period.StartDate <= latest && period.EndDate >= earliest)
            .Select(period => new { period.Name, period.StartDate, period.EndDate }).ToListAsync(ct);
        var blocked = closedPeriods.FirstOrDefault(period => dates.Any(date => date >= period.StartDate && date <= period.EndDate));
        if (blocked is not null)
            throw new ConflictException($"Posting is blocked because accounting period '{blocked.Name}' ({blocked.StartDate} to {blocked.EndDate}) is closed.");
    }

    public static Task EnsurePeriodOpenAsync(IAccountingInventoryDbContext db, DateOnly journalDate, CancellationToken ct)
        => EnsurePeriodsOpenAsync(db, [journalDate], ct);

    public static async Task<IReadOnlyDictionary<string, Guid>> EnsureSystemAccountsAsync(
        IAccountingInventoryDbContext db, CancellationToken ct)
    {
        var accounts = await db.ChartAccounts.ToDictionaryAsync(account => account.Code, ct);
        var created = new List<ChartAccount>();
        foreach (var template in LedgerAccountDefaults.All)
        {
            if (accounts.TryGetValue(template.Code, out var existing))
            {
                if (!existing.IsActive || existing.Type != template.Type || existing.NormalBalance != template.Side)
                    throw new ConflictException($"Required ledger account {template.Code} is inactive or incorrectly configured.");
                continue;
            }
            var account = ChartAccount.Create(template.Code, template.Name, template.Type, template.Side, isSystem: true);
            accounts.Add(template.Code, account);
            created.Add(account);
        }
        if (created.Count > 0) db.ChartAccounts.AddRange(created);
        return accounts.ToDictionary(pair => pair.Key, pair => pair.Value.Id, StringComparer.OrdinalIgnoreCase);
    }

    public static JournalEntry? ForPurchase(Product product, IReadOnlyDictionary<string, Guid> accounts)
    {
        var inventoryCost = decimal.Round(product.PurchasePrice - product.Discount, 2, MidpointRounding.AwayFromZero);
        var inputTax = decimal.Round(product.TotalAmount - inventoryCost, 2, MidpointRounding.AwayFromZero);
        var billTotal = inventoryCost + inputTax;
        if (billTotal <= 0) return null;
        var lines = new List<(Guid AccountId, decimal Debit, decimal Credit, string? Memo)>();
        if (inventoryCost > 0) lines.Add((accounts["1200"], inventoryCost, 0, "Inventory received"));
        if (inputTax > 0) lines.Add((accounts["2200"], inputTax, 0, "Purchase input tax"));
        lines.Add((accounts["2000"], 0, billTotal, "Supplier payable"));
        return JournalEntry.Post(product.PurchaseDate, "Inventory purchase",
            "PurchaseProduct", product.Id.ToString(), lines);
    }

    public static JournalEntry? ForSale(SalesProduct sale, Product product, IReadOnlyDictionary<string, Guid> accounts)
    {
        var revenue = decimal.Round(sale.SellingPrice, 2, MidpointRounding.AwayFromZero);
        var discount = decimal.Round(sale.Discount, 2, MidpointRounding.AwayFromZero);
        var receivable = sale.TotalAmount;
        var cost = decimal.Round(product.PurchasePrice - product.Discount, 2, MidpointRounding.AwayFromZero);
        if (receivable < 0 || cost < 0) throw new ConflictException("Sale amount and inventory cost cannot be negative.");
        var lines = new List<(Guid AccountId, decimal Debit, decimal Credit, string? Memo)>();
        if (revenue > 0)
        {
            if (receivable > 0) lines.Add((accounts["1100"], receivable, 0, "Customer receivable"));
            if (discount > 0) lines.Add((accounts["4100"], discount, 0, "Sales discount"));
            lines.Add((accounts["4000"], 0, revenue, "Sales revenue"));
            var salesTax = sale.CgstAmount + sale.SgstAmount;
            if (salesTax > 0) lines.Add((accounts["2100"], 0, salesTax, "GST collected"));
        }
        if (cost > 0)
        {
            lines.Add((accounts["5000"], cost, 0, "Cost of goods sold"));
            lines.Add((accounts["1200"], 0, cost, "Inventory relieved"));
        }
        return lines.Count == 0 ? null : JournalEntry.Post(sale.SaleDate, "Product sale", "Sale", sale.Id.ToString(), lines);
    }

    public static JournalEntry? ForSalesInvoice(SalesInvoice invoice, IReadOnlyList<Product> products, IReadOnlyDictionary<string, Guid> accounts, decimal skuCost = 0m)
    {
        var grossRevenue = invoice.SubTotal;
        var discount = invoice.Discount;
        var receivable = invoice.TotalAmount;
        var totalCost = skuCost + products.Sum(p => decimal.Round(p.PurchasePrice - p.Discount, 2, MidpointRounding.AwayFromZero));
        if (receivable < 0 || totalCost < 0) throw new ConflictException("Sale amount and inventory cost cannot be negative.");

        var lines = new List<(Guid AccountId, decimal Debit, decimal Credit, string? Memo)>();
        if (receivable > 0)
        {
            lines.Add((accounts["1100"], receivable, 0, "Customer receivable"));
            if (discount > 0) lines.Add((accounts["4100"], discount, 0, "Sales discount"));
            if (grossRevenue > 0) lines.Add((accounts["4000"], 0, grossRevenue, "Sales revenue"));
            var totalTax = invoice.CgstAmount + invoice.SgstAmount + invoice.IgstAmount;
            if (totalTax > 0) lines.Add((accounts["2100"], 0, totalTax, "GST collected"));
        }
        if (totalCost > 0)
        {
            lines.Add((accounts["5000"], totalCost, 0, "Cost of goods sold"));
            lines.Add((accounts["1200"], 0, totalCost, "Inventory relieved"));
        }
        return lines.Count == 0 ? null : JournalEntry.Post(invoice.InvoiceDate, $"Sales invoice {invoice.BillNumber}", "SalesInvoice", invoice.Id.ToString(), lines);
    }

    public static JournalEntry ForSalesReceipt(SalesReceipt receipt, IReadOnlyDictionary<string, Guid> accounts)
    {
        var cashAccount = receipt.PaymentMode == "Cash" ? "1000" : "1010";
        return JournalEntry.Post(receipt.PaymentDate, "Customer receipt", "SalesReceipt", receipt.Id.ToString(),
        [
            (accounts[cashAccount], receipt.Amount, 0, receipt.PaymentMode),
            (accounts["1100"], 0, receipt.Amount, "Receivable settled")
        ]);
    }

    public static JournalEntry ForSalesReceipt(SalesInvoiceReceipt receipt, IReadOnlyDictionary<string, Guid> accounts)
    {
        var cash = receipt.PaymentMode == "Cash" ? "1000" : "1010";
        return JournalEntry.Post(receipt.PaymentDate, "Customer invoice receipt", "SalesInvoiceReceipt", receipt.Id.ToString(),
            [(accounts[cash], receipt.Amount, 0m, (string?)receipt.PaymentMode), (accounts["1100"], 0m, receipt.Amount, "Receivable settled")]);
    }

    public static JournalEntry ForPurchasePayment(PurchasePayment payment, IReadOnlyDictionary<string, Guid> accounts)
    {
        var cashAccount = payment.PaymentMode == "Cash" ? "1000" : "1010";
        return JournalEntry.Post(payment.PaymentDate, "Supplier payment", "PurchasePayment", payment.Id.ToString(),
        [
            (accounts["2000"], payment.Amount, 0, "Supplier payable settled"),
            (accounts[cashAccount], 0, payment.Amount, payment.PaymentMode)
        ]);
    }

    public static JournalEntry ForInventoryWriteOff(InventoryAdjustment adjustment,
        IReadOnlyDictionary<string, Guid> accounts)
        => JournalEntry.Post(adjustment.AdjustmentDate, "Inventory write-off", "InventoryAdjustment", adjustment.Id.ToString(),
        [
            (accounts["5100"], adjustment.Cost, 0, adjustment.Reason),
            (accounts["1200"], 0, adjustment.Cost, "Inventory written off")
        ]);

    public static void Add(IAccountingInventoryDbContext db, JournalEntry? journal)
    {
        if (journal is null) return;
        db.JournalEntries.Add(journal);
        db.JournalLines.AddRange(journal.Lines);
    }
}
