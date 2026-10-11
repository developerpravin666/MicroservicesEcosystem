using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record StatementTransactionDto(
    DateOnly Date,
    string VoucherType,
    string VoucherNumber,
    string Particulars,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string BalanceSide,
    Guid? SourceId = null);

public sealed record PartyAgingSummaryDto(
    decimal Current0To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal DaysOver90,
    decimal TotalOutstanding);

public sealed record PartyStatementOfAccountDto(
    Guid PartyId,
    string PartyType, // "Customer" or "Vendor"
    string PartyName,
    string PartyMobile,
    string? PartyEmail,
    string PartyAddress,
    string? PartyGstin,
    string? PartyStateCode,
    string? PartyStateName,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningBalance,
    string OpeningBalanceSide,
    IReadOnlyList<StatementTransactionDto> Transactions,
    decimal TotalPeriodDebit,
    decimal TotalPeriodCredit,
    decimal ClosingBalance,
    string ClosingBalanceSide,
    PartyAgingSummaryDto Aging);

public sealed record GetPartyStatementQuery(
    string PartyType, // "Customer" or "Vendor"
    Guid PartyId,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IRequest<PartyStatementOfAccountDto>;

public sealed class GetPartyStatementHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetPartyStatementQuery, PartyStatementOfAccountDto>
{
    public async Task<PartyStatementOfAccountDto> Handle(GetPartyStatementQuery request, CancellationToken ct)
    {
        if (request.PartyId == Guid.Empty) throw new ArgumentException("Party ID is required.");
        var partyType = request.PartyType.Trim();
        var isCustomer = string.Equals(partyType, "Customer", StringComparison.OrdinalIgnoreCase);
        var isVendor = string.Equals(partyType, "Vendor", StringComparison.OrdinalIgnoreCase);

        if (!isCustomer && !isVendor)
            throw new ArgumentException("PartyType must be either 'Customer' or 'Vendor'.");

        var toDate = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = request.FromDate ?? new DateOnly(toDate.Year, 1, 1);
        if (fromDate > toDate) throw new ConflictException("From date must be on or before To date.");

        if (isCustomer)
        {
            return await BuildCustomerStatementAsync(request.PartyId, fromDate, toDate, ct);
        }
        else
        {
            return await BuildVendorStatementAsync(request.PartyId, fromDate, toDate, ct);
        }
    }

    private async Task<PartyStatementOfAccountDto> BuildCustomerStatementAsync(
        Guid customerId, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new NotFoundException($"Customer '{customerId}' was not found.");

        // Internal transaction line accumulator
        var rawTransactions = new List<(DateOnly Date, string Type, string Number, string Particulars, decimal Debit, decimal Credit, Guid? SourceId, DateTimeOffset CreatedAt)>();

        // 1. Opening balance item
        var openingBalances = await db.OpeningSubledgerBalances.AsNoTracking()
            .Where(o => o.Kind == "Customer" && o.PartyId == customerId)
            .ToListAsync(ct);

        foreach (var op in openingBalances)
        {
            rawTransactions.Add((
                op.CutoverDate,
                "Opening Balance",
                op.Reference,
                "Opening AR balance carry-forward",
                op.Amount,
                0m,
                op.Id,
                new DateTimeOffset(op.CutoverDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)));

            var settlements = await db.OpeningSettlements.AsNoTracking()
                .Where(s => s.OpeningBalanceId == op.Id)
                .ToListAsync(ct);

            foreach (var set in settlements)
            {
                rawTransactions.Add((
                    set.PaymentDate,
                    "Opening Settlement",
                    op.Reference,
                    $"Settlement via {set.PaymentMode}",
                    0m,
                    set.Amount,
                    set.Id,
                    new DateTimeOffset(set.PaymentDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)));
            }
        }

        // 2. Multi-line Sales Invoices
        var salesInvoices = await db.SalesInvoices.AsNoTracking()
            .Include(i => i.Lines)
            .Where(i => i.CustomerId == customerId )
            .ToListAsync(ct);

        var invoiceIds = salesInvoices.Select(i => i.Id).ToList();

        foreach (var inv in salesInvoices)
        {
            var itemDesc = inv.Lines.Count == 1
                ? inv.Lines[0].ItemDescription
                : $"{inv.Lines[0].ItemDescription} (+{inv.Lines.Count - 1} more items)";

            rawTransactions.Add((
                inv.InvoiceDate,
                "Sales Invoice",
                inv.BillNumber,
                $"Invoice #{inv.BillNumber}: {itemDesc}",
                inv.TotalAmount,
                0m,
                inv.Id,
                inv.CreatedAt));
        }

        // 3. Sales Receipts for these invoices
        var receipts = await db.SalesInvoiceReceipts.AsNoTracking()
            .Where(r => invoiceIds.Contains(r.InvoiceId) && r.AdvanceId == null)
            .ToListAsync(ct);

        var invoiceBillMap = salesInvoices.ToDictionary(i => i.Id, i => i.BillNumber);

        foreach (var rc in receipts)
        {
            var billNum = invoiceBillMap.TryGetValue(rc.InvoiceId, out var b) ? b : "";
            var refText = !string.IsNullOrWhiteSpace(rc.ReferenceNumber) ? $" (Ref: {rc.ReferenceNumber})" : "";
            var noteText = !string.IsNullOrWhiteSpace(rc.Note) ? $" - {rc.Note}" : "";

            rawTransactions.Add((
                rc.PaymentDate,
                "Payment Receipt",
                billNum,
                $"Receipt via {rc.PaymentMode}{refText}{noteText}",
                0m,
                rc.Amount,
                rc.Id,
                rc.CreatedAt));
        }

        // 4. Legacy single sales products
        var legacySales = await db.SalesProducts.AsNoTracking()
            .Where(s => s.CustomerId == customerId && !s.IsDeleted)
            .ToListAsync(ct);

        var legacySaleIds = legacySales.Select(s => s.Id).ToList();

        foreach (var ls in legacySales)
        {
            rawTransactions.Add((
                ls.SaleDate,
                "Sales Invoice",
                ls.BillNumber,
                $"Bill #{ls.BillNumber}",
                ls.TotalAmount,
                0m,
                ls.Id,
                ls.CreatedAt));
        }

        var legacyReceipts = await db.SalesReceipts.AsNoTracking()
            .Where(r => legacySaleIds.Contains(r.SalesProductId))
            .ToListAsync(ct);

        var legacyBillMap = legacySales.ToDictionary(s => s.Id, s => s.BillNumber);

        foreach (var lr in legacyReceipts)
        {
            var billNum = legacyBillMap.TryGetValue(lr.SalesProductId, out var b) ? b : "";
            var refText = !string.IsNullOrWhiteSpace(lr.ReferenceNumber) ? $" (Ref: {lr.ReferenceNumber})" : "";
            var noteText = !string.IsNullOrWhiteSpace(lr.Note) ? $" - {lr.Note}" : "";

            rawTransactions.Add((
                lr.PaymentDate,
                "Payment Receipt",
                billNum,
                $"Receipt via {lr.PaymentMode}{refText}{noteText}",
                0m,
                lr.Amount,
                lr.Id,
                lr.CreatedAt));
        }

        // 5. Credit Notes / Sales Returns
        var creditNotes = await db.InvoiceCorrections.AsNoTracking()
            .Where(c => c.Kind == "Sale" && c.PartyId == customerId)
            .ToListAsync(ct);

        foreach (var cn in creditNotes)
        {
            rawTransactions.Add((
                cn.NoteDate,
                "Credit Note",
                cn.NoteNumber,
                $"Credit Note for Bill #{cn.BillNumber}: {cn.Reason}",
                0m,
                cn.TotalAmount,
                cn.Id,
                cn.CreatedAt));
        }

        var noteIds = creditNotes.Select(n => n.Id).ToArray();
        foreach (var refund in await db.CorrectionRefunds.Where(r => noteIds.Contains(r.CorrectionId)).ToListAsync(ct))
            rawTransactions.Add((refund.PaymentDate, "Customer Refund", refund.Reference, "Credit note refund", refund.Amount, 0m, refund.Id, refund.CreatedAt));
        var advances = await db.CustomerAdvances.Where(a => a.CustomerId == customerId).ToListAsync(ct);
        foreach (var advance in advances)
            rawTransactions.Add((advance.PaymentDate, "Customer Advance", advance.ReferenceNumber ?? "", "On-account receipt", 0m, advance.Amount, advance.Id, advance.CreatedAt));
        var advanceIds = advances.Select(a => a.Id).ToArray();
        foreach (var refund in await db.CustomerAdvanceRefunds.Where(r => advanceIds.Contains(r.AdvanceId)).ToListAsync(ct))
            rawTransactions.Add((refund.PaymentDate, "Advance Refund", refund.ReferenceNumber ?? "", "On-account refund", refund.Amount, 0m, refund.Id, refund.CreatedAt));
        // Sort all historical transactions chronologically
        var sorted = rawTransactions
            .OrderBy(t => t.Date)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        // Calculate opening balance before fromDate
        decimal priorDebits = 0m;
        decimal priorCredits = 0m;

        foreach (var tx in sorted.Where(t => t.Date < fromDate))
        {
            priorDebits += tx.Debit;
            priorCredits += tx.Credit;
        }

        // For customer AR: Debit balance is Receivable, Credit balance is Advance
        var openingNet = decimal.Round(priorDebits - priorCredits, 2, MidpointRounding.AwayFromZero);
        var openingSide = openingNet >= 0 ? "Dr" : "Cr";
        var absOpening = Math.Abs(openingNet);

        // Compute running balance across in-period transactions
        var periodTransactions = sorted.Where(t => t.Date >= fromDate && t.Date <= toDate).ToList();
        var statementRows = new List<StatementTransactionDto>();

        var currentRunning = openingNet;
        decimal totalPeriodDebit = 0m;
        decimal totalPeriodCredit = 0m;

        foreach (var pt in periodTransactions)
        {
            currentRunning += (pt.Debit - pt.Credit);
            totalPeriodDebit += pt.Debit;
            totalPeriodCredit += pt.Credit;

            var roundedRunning = decimal.Round(currentRunning, 2, MidpointRounding.AwayFromZero);
            var side = roundedRunning >= 0 ? "Dr" : "Cr";

            statementRows.Add(new StatementTransactionDto(
                pt.Date,
                pt.Type,
                pt.Number,
                pt.Particulars,
                pt.Debit,
                pt.Credit,
                Math.Abs(roundedRunning),
                side,
                pt.SourceId));
        }

        var closingNet = decimal.Round(currentRunning, 2, MidpointRounding.AwayFromZero);
        var closingSide = closingNet >= 0 ? "Dr" : "Cr";
        var absClosing = Math.Abs(closingNet);

        var aging = await BuildAging(customerId, true, toDate, ct);

        return new PartyStatementOfAccountDto(
            customer.Id,
            "Customer",
            customer.Name,
            customer.Mobile,
            customer.Email,
            customer.Address,
            customer.Gstin,
            customer.StateCode,
            customer.StateName,
            fromDate,
            toDate,
            absOpening,
            openingSide,
            statementRows,
            decimal.Round(totalPeriodDebit, 2),
            decimal.Round(totalPeriodCredit, 2),
            absClosing,
            closingSide,
            aging);
    }

    private async Task<PartyStatementOfAccountDto> BuildVendorStatementAsync(
        Guid vendorId, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var vendor = await db.Vendors.AsNoTracking().SingleOrDefaultAsync(v => v.Id == vendorId, ct)
            ?? throw new NotFoundException($"Vendor '{vendorId}' was not found.");

        var rawTransactions = new List<(DateOnly Date, string Type, string Number, string Particulars, decimal Debit, decimal Credit, Guid? SourceId, DateTimeOffset CreatedAt)>();

        // 1. Opening balance item
        var openingBalances = await db.OpeningSubledgerBalances.AsNoTracking()
            .Where(o => o.Kind == "Vendor" && o.PartyId == vendorId)
            .ToListAsync(ct);

        foreach (var op in openingBalances)
        {
            // For vendor AP: Credit is Payable increase
            rawTransactions.Add((
                op.CutoverDate,
                "Opening Balance",
                op.Reference,
                "Opening AP payable carry-forward",
                0m,
                op.Amount,
                op.Id,
                new DateTimeOffset(op.CutoverDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)));

            var settlements = await db.OpeningSettlements.AsNoTracking()
                .Where(s => s.OpeningBalanceId == op.Id)
                .ToListAsync(ct);

            foreach (var set in settlements)
            {
                rawTransactions.Add((
                    set.PaymentDate,
                    "Opening Settlement",
                    op.Reference,
                    $"Payment via {set.PaymentMode}",
                    set.Amount,
                    0m,
                    set.Id,
                    new DateTimeOffset(set.PaymentDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)));
            }
        }

        // 2. Vendor purchases (grouping by PurchaseDate & BillNumber)
        var products = await AccountingInventory.Application.Purchases.Accounting.PurchaseStockRows.Query(db)
            .Where(p => p.VendorId == vendorId && !p.IsOpeningStock && !p.IsDeleted)
            .ToListAsync(ct);

        var productBrandIds = products.Select(p => p.BrandId).Distinct().ToList();
        var productModelIds = products.Select(p => p.ProductModelId).Distinct().ToList();

        var brandMap = await db.Brands.AsNoTracking()
            .Where(b => productBrandIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var modelMap = await db.ProductModels.AsNoTracking()
            .Where(m => productModelIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        var purchaseBills = products
            .GroupBy(p => new { p.PurchaseDate, Bill = p.BillNumber ?? "PURCHASE" })
            .Select(g => new
            {
                g.Key.PurchaseDate,
                g.Key.Bill,
                Count = g.Count(),
                Total = g.Sum(p => p.TotalAmount),
                EarliestCreated = g.Min(p => p.CreatedAt),
                FirstProduct = g.First()
            })
            .ToList();

        foreach (var pb in purchaseBills)
        {
            var brandName = brandMap.TryGetValue(pb.FirstProduct.BrandId, out var bn) ? bn : "Product";
            var modelName = modelMap.TryGetValue(pb.FirstProduct.ProductModelId, out var mn) ? mn : "";

            var summary = pb.Count == 1
                ? (string.IsNullOrWhiteSpace(modelName) ? brandName : $"{brandName} ({modelName})")
                : $"{brandName} (+{pb.Count - 1} items)";

            rawTransactions.Add((
                pb.PurchaseDate,
                "Purchase Invoice",
                pb.Bill,
                $"Bill #{pb.Bill}: {summary}",
                0m,
                pb.Total,
                pb.FirstProduct.Id,
                pb.EarliestCreated));
        }

        // 3. Purchase Payments made to Vendor
        var payments = await db.PurchasePayments.AsNoTracking()
            .Where(p => p.VendorId == vendorId)
            .ToListAsync(ct);

        foreach (var p in payments)
        {
            var refText = !string.IsNullOrWhiteSpace(p.ReferenceNumber) ? $" (Ref: {p.ReferenceNumber})" : "";
            var noteText = !string.IsNullOrWhiteSpace(p.Note) ? $" - {p.Note}" : "";

            rawTransactions.Add((
                p.PaymentDate,
                "Vendor Payment",
                p.ReferenceNumber ?? "",
                $"Payment via {p.PaymentMode}{refText}{noteText}",
                p.Amount,
                0m,
                p.Id,
                p.CreatedAt));
        }

        // 4. Debit Notes / Purchase Returns
        var debitNotes = await db.InvoiceCorrections.AsNoTracking()
            .Where(c => c.Kind == "Purchase" && c.PartyId == vendorId)
            .ToListAsync(ct);

        foreach (var dn in debitNotes)
        {
            rawTransactions.Add((
                dn.NoteDate,
                "Debit Note",
                dn.NoteNumber,
                $"Debit Note for Bill #{dn.BillNumber}: {dn.Reason}",
                dn.TotalAmount,
                0m,
                dn.Id,
                dn.CreatedAt));
        }

        var refundNoteIds = debitNotes.Select(n => n.Id).ToArray();
        foreach (var refund in await db.CorrectionRefunds.Where(r => refundNoteIds.Contains(r.CorrectionId)).ToListAsync(ct))
            rawTransactions.Add((refund.PaymentDate, "Supplier Refund", refund.Reference, "Debit note refund", 0m, refund.Amount, refund.Id, refund.CreatedAt));
        var sorted = rawTransactions
            .OrderBy(t => t.Date)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        // Calculate opening balance before fromDate
        // For Vendor AP: Credit is Payable, Debit is Payment. Net = Credits - Debits
        decimal priorDebits = 0m;
        decimal priorCredits = 0m;

        foreach (var tx in sorted.Where(t => t.Date < fromDate))
        {
            priorDebits += tx.Debit;
            priorCredits += tx.Credit;
        }

        var openingNet = decimal.Round(priorCredits - priorDebits, 2, MidpointRounding.AwayFromZero);
        var openingSide = openingNet >= 0 ? "Cr" : "Dr";
        var absOpening = Math.Abs(openingNet);

        var periodTransactions = sorted.Where(t => t.Date >= fromDate && t.Date <= toDate).ToList();
        var statementRows = new List<StatementTransactionDto>();

        var currentRunning = openingNet;
        decimal totalPeriodDebit = 0m;
        decimal totalPeriodCredit = 0m;

        foreach (var pt in periodTransactions)
        {
            // For vendor AP: Credit increases payable, Debit decreases payable
            currentRunning += (pt.Credit - pt.Debit);
            totalPeriodDebit += pt.Debit;
            totalPeriodCredit += pt.Credit;

            var roundedRunning = decimal.Round(currentRunning, 2, MidpointRounding.AwayFromZero);
            var side = roundedRunning >= 0 ? "Cr" : "Dr";

            statementRows.Add(new StatementTransactionDto(
                pt.Date,
                pt.Type,
                pt.Number,
                pt.Particulars,
                pt.Debit,
                pt.Credit,
                Math.Abs(roundedRunning),
                side,
                pt.SourceId));
        }

        var closingNet = decimal.Round(currentRunning, 2, MidpointRounding.AwayFromZero);
        var closingSide = closingNet >= 0 ? "Cr" : "Dr";
        var absClosing = Math.Abs(closingNet);

        var aging = await BuildAging(vendorId, false, toDate, ct);

        return new PartyStatementOfAccountDto(
            vendor.Id,
            "Vendor",
            vendor.Name,
            vendor.Mobile,
            null,
            vendor.Address ?? "",
            null,
            null,
            null,
            fromDate,
            toDate,
            absOpening,
            openingSide,
            statementRows,
            decimal.Round(totalPeriodDebit, 2),
            decimal.Round(totalPeriodCredit, 2),
            absClosing,
            closingSide,
            aging);
    }
    private async Task<PartyAgingSummaryDto> BuildAging(Guid partyId, bool customer, DateOnly asOf, CancellationToken ct)
    {
        var report = await new GetAgingReportHandler(db).Handle(new GetAgingReportQuery(asOf, 1, 100, PartyId: partyId), ct);
        var rows = new List<AgingInvoiceSummary>();
        var pageCount = customer ? report.Receivables.TotalPages : report.Payables.TotalPages;
        rows.AddRange((customer ? report.Receivables : report.Payables).Items.Where(r => r.CounterpartyId == partyId));
        for (var page = 2; page <= pageCount; page++)
        {
            var next = await new GetAgingReportHandler(db).Handle(new GetAgingReportQuery(asOf, page, 100, PartyId: partyId), ct);
            rows.AddRange((customer ? next.Receivables : next.Payables).Items.Where(r => r.CounterpartyId == partyId));
        }
        return new(rows.Where(r => r.AgeDays <= 30).Sum(r => r.Balance), rows.Where(r => r.AgeDays > 30 && r.AgeDays <= 60).Sum(r => r.Balance),
            rows.Where(r => r.AgeDays > 60 && r.AgeDays <= 90).Sum(r => r.Balance), rows.Where(r => r.AgeDays > 90).Sum(r => r.Balance), rows.Sum(r => r.Balance));
    }
}

