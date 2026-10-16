using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.Rcm.Commands.CreateRcmSelfInvoice;

public sealed class CreateRcmSelfInvoiceCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateRcmSelfInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(CreateRcmSelfInvoiceCommand request, CancellationToken ct)
    {
        var vendor = await db.Vendors.SingleOrDefaultAsync(v => v.Id == request.VendorId, ct)
            ?? throw new NotFoundException($"Vendor with ID '{request.VendorId}' was not found.");

        var rcm = RcmSelfInvoice.Create(
            request.VoucherDate,
            vendor.Id,
            vendor.Name,
            vendor.GstNumber,
            request.Section,
            request.ServiceCategory,
            request.SupplierInvoiceNumber,
            request.SupplierInvoiceDate,
            request.SupplyType,
            request.PlaceOfSupplyStateCode,
            request.PlaceOfSupplyStateName,
            request.HsnSacCode,
            request.ItemDescription,
            request.TaxableAmount,
            request.GstRate,
            request.IsItcEligible,
            request.PaymentMode,
            request.PaymentReference,
            request.BankAccountId,
            request.Notes,
            request.CustomVoucherNumber);

        // Auto-post General Ledger Journal Entry if ledger accounts are available
        try
        {
            var expenseAccount = request.ExpenseAccountId.HasValue
                ? await db.ChartAccounts.FirstOrDefaultAsync(a => a.Id == request.ExpenseAccountId.Value, ct)
                : await db.ChartAccounts.FirstOrDefaultAsync(a => a.Type == LedgerAccountType.Expense, ct);

            ChartAccount? creditAccount = null;
            if (request.PaymentMode == "Bank" && request.BankAccountId.HasValue)
            {
                creditAccount = await db.ChartAccounts.FirstOrDefaultAsync(a => a.Id == request.BankAccountId.Value, ct);
            }

            if (creditAccount == null)
            {
                creditAccount = await db.ChartAccounts.FirstOrDefaultAsync(a => a.Type == LedgerAccountType.Asset && a.NormalBalance == LedgerBalanceSide.Debit, ct)
                    ?? await db.ChartAccounts.FirstOrDefaultAsync(a => a.Type == LedgerAccountType.Liability, ct);
            }

            if (expenseAccount != null && creditAccount != null)
            {
                var lines = new List<(Guid AccountId, decimal Debit, decimal Credit, string? Memo)>
                {
                    (expenseAccount.Id, rcm.TaxableAmount, 0m, $"RCM Expense: {rcm.ItemDescription} ({rcm.Section})"),
                    (creditAccount.Id, 0m, rcm.TaxableAmount, $"RCM Payment to {vendor.Name} via {rcm.PaymentMode}")
                };

                // If tax liability applies, record RCM tax accrual and matching ITC claim
                if (rcm.TotalTaxAmount > 0)
                {
                    var taxLiabilityAccount = await db.ChartAccounts.FirstOrDefaultAsync(a => a.Type == LedgerAccountType.Liability && a.Code.Contains("TAX"), ct)
                        ?? creditAccount;
                    var itcAccount = await db.ChartAccounts.FirstOrDefaultAsync(a => a.Type == LedgerAccountType.Asset && a.Code.Contains("ITC"), ct)
                        ?? expenseAccount;

                    if (taxLiabilityAccount != null && itcAccount != null && taxLiabilityAccount.Id != itcAccount.Id)
                    {
                        lines.Add((itcAccount.Id, rcm.TotalTaxAmount, 0m, $"RCM Input Tax Credit Claim ({rcm.VoucherNumber})"));
                        lines.Add((taxLiabilityAccount.Id, 0m, rcm.TotalTaxAmount, $"RCM Output Tax Liability ({rcm.VoucherNumber})"));
                    }
                }

                var journal = JournalEntry.Post(
                    rcm.VoucherDate,
                    $"RCM Self-Invoice {rcm.VoucherNumber} - {vendor.Name}",
                    "RcmSelfInvoice",
                    rcm.Id.ToString(),
                    lines);

                db.JournalEntries.Add(journal);
                db.JournalLines.AddRange(journal.Lines);
                rcm.SetJournalEntryId(journal.Id);
            }
        }
        catch
        {
            // If automated journal fails due to tenant chart-of-accounts customization, voucher is still created successfully
        }

        db.RcmSelfInvoices.Add(rcm);
        await db.SaveChangesAsync(ct);

        return rcm.Id;
    }
}

