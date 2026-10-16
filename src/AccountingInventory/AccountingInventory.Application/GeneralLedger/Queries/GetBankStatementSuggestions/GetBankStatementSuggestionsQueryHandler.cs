using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger.Common;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger.Queries.GetBankStatementSuggestions;

public sealed class GetBankStatementSuggestionsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetBankStatementSuggestionsQuery, IReadOnlyList<BankStatementSuggestionDto>>
{
    public async Task<IReadOnlyList<BankStatementSuggestionDto>> Handle(GetBankStatementSuggestionsQuery request, CancellationToken ct)
    {
        var reconciliation = await db.BankReconciliations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == request.ReconciliationId, ct)
            ?? throw new NotFoundException("Bank reconciliation was not found.");

        var unmatchedLines = await db.BankStatementLines.AsNoTracking()
            .Where(line => line.BankReconciliationId == reconciliation.Id && line.JournalLineId == null)
            .OrderBy(line => line.TransactionDate)
            .ToListAsync(ct);

        if (unmatchedLines.Count == 0)
        {
            return Array.Empty<BankStatementSuggestionDto>();
        }

        // Preload open sales invoices with customer details
        var openInvoices = await (from inv in db.SalesInvoices.AsNoTracking()
            join cust in db.Customers.AsNoTracking() on inv.CustomerId equals cust.Id
            where !inv.IsCancelled && inv.Balance > 0
            select new
            {
                inv.Id,
                inv.BillNumber,
                inv.TotalAmount,
                inv.Balance,
                CustomerName = cust.Name
            }).ToListAsync(ct);

        // Preload active vendors
        var vendors = await db.Vendors.AsNoTracking()
            .Where(v => v.IsActive)
            .Select(v => new { v.Id, v.Name })
            .ToListAsync(ct);

        var suggestions = new List<BankStatementSuggestionDto>();

        foreach (var stmt in unmatchedLines)
        {
            var textToSearch = $"{stmt.Description} {stmt.Reference}".Trim();

            if (stmt.Amount > 0)
            {
                // Deposit -> Customer Receipt Suggestions
                // Check 1: Exact balance match
                var exactBalanceMatch = openInvoices.FirstOrDefault(i => i.Balance == stmt.Amount);
                if (exactBalanceMatch != null)
                {
                    suggestions.Add(new BankStatementSuggestionDto(
                        stmt.Id,
                        stmt.TransactionDate,
                        stmt.Amount,
                        stmt.Description,
                        stmt.Reference,
                        "CustomerReceipt",
                        exactBalanceMatch.Id,
                        exactBalanceMatch.CustomerName,
                        exactBalanceMatch.BillNumber,
                        exactBalanceMatch.TotalAmount,
                        exactBalanceMatch.Balance,
                        "High",
                        $"Exact amount match with unpaid invoice balance ({stmt.Amount:N2})"));
                    continue;
                }

                // Check 2: Customer Name match in narration/reference
                var nameMatch = openInvoices.FirstOrDefault(i =>
                    !string.IsNullOrWhiteSpace(i.CustomerName) &&
                    textToSearch.Contains(i.CustomerName, StringComparison.OrdinalIgnoreCase));
                if (nameMatch != null)
                {
                    suggestions.Add(new BankStatementSuggestionDto(
                        stmt.Id,
                        stmt.TransactionDate,
                        stmt.Amount,
                        stmt.Description,
                        stmt.Reference,
                        "CustomerReceipt",
                        nameMatch.Id,
                        nameMatch.CustomerName,
                        nameMatch.BillNumber,
                        nameMatch.TotalAmount,
                        nameMatch.Balance,
                        "Medium",
                        $"Customer '{nameMatch.CustomerName}' detected in statement narration"));
                    continue;
                }

                // Check 3: Exact total invoice amount match
                var exactTotalMatch = openInvoices.FirstOrDefault(i => i.TotalAmount == stmt.Amount);
                if (exactTotalMatch != null)
                {
                    suggestions.Add(new BankStatementSuggestionDto(
                        stmt.Id,
                        stmt.TransactionDate,
                        stmt.Amount,
                        stmt.Description,
                        stmt.Reference,
                        "CustomerReceipt",
                        exactTotalMatch.Id,
                        exactTotalMatch.CustomerName,
                        exactTotalMatch.BillNumber,
                        exactTotalMatch.TotalAmount,
                        exactTotalMatch.Balance,
                        "Medium",
                        $"Exact amount match with original invoice total ({stmt.Amount:N2})"));
                    continue;
                }
            }
            else
            {
                // Withdrawal -> Vendor Payment Suggestions
                var absAmount = Math.Abs(stmt.Amount);

                // Check if vendor name is mentioned in description/reference
                var vendorMatch = vendors.FirstOrDefault(v =>
                    !string.IsNullOrWhiteSpace(v.Name) &&
                    textToSearch.Contains(v.Name, StringComparison.OrdinalIgnoreCase));

                if (vendorMatch != null)
                {
                    suggestions.Add(new BankStatementSuggestionDto(
                        stmt.Id,
                        stmt.TransactionDate,
                        stmt.Amount,
                        stmt.Description,
                        stmt.Reference,
                        "VendorPayment",
                        vendorMatch.Id,
                        vendorMatch.Name,
                        stmt.Reference ?? $"PAY-{stmt.TransactionDate:yyyyMMdd}",
                        absAmount,
                        absAmount,
                        "High",
                        $"Vendor '{vendorMatch.Name}' identified from transaction narration"));
                }
            }
        }

        return suggestions;
    }
}

