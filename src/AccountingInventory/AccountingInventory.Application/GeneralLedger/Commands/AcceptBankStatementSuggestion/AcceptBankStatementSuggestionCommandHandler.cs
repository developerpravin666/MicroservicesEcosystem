using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Application.GeneralLedger.Common;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger.Commands.AcceptBankStatementSuggestion;

public sealed class AcceptBankStatementSuggestionCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<AcceptBankStatementSuggestionCommand, BankReconciliationSummary>
{
    public async Task<BankReconciliationSummary> Handle(AcceptBankStatementSuggestionCommand request, CancellationToken ct)
    {
        var reconciliation = await db.BankReconciliations
            .SingleOrDefaultAsync(r => r.Id == request.ReconciliationId, ct)
            ?? throw new NotFoundException("Bank reconciliation was not found.");

        if (reconciliation.IsFinalized)
            throw new ConflictException("Finalized reconciliations cannot be modified.");

        var statementLine = await db.BankStatementLines
            .SingleOrDefaultAsync(l => l.Id == request.StatementLineId && l.BankReconciliationId == reconciliation.Id, ct)
            ?? throw new NotFoundException("Statement line was not found.");

        if (statementLine.JournalLineId.HasValue)
            throw new ConflictException("Statement line is already matched.");

        var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(db, ct);

        if (request.SuggestionType == "CustomerReceipt")
        {
            if (statementLine.Amount <= 0)
                throw new ConflictException("Customer receipt requires a positive statement amount.");

            var invoice = await db.SalesInvoices
                .SingleOrDefaultAsync(i => i.Id == request.EntityId, ct)
                ?? throw new NotFoundException($"Sales invoice '{request.EntityId}' was not found.");

            if (invoice.IsCancelled)
                throw new ConflictException("Cannot record payment against a cancelled invoice.");

            var paymentAmount = decimal.Round(Math.Min(statementLine.Amount, invoice.Balance), 2, MidpointRounding.AwayFromZero);
            invoice.RecordPayment(paymentAmount);

            var receipt = SalesInvoiceReceipt.Create(
                invoice.Id,
                paymentAmount,
                "Bank Transfer",
                statementLine.TransactionDate,
                statementLine.Reference,
                "Auto-matched from bank statement suggestion");

            db.SalesInvoiceReceipts.Add(receipt);

            var journal = JournalEntry.Post(
                statementLine.TransactionDate,
                $"Customer receipt {invoice.BillNumber} (Bank Feed)",
                "SalesInvoiceReceipt",
                receipt.Id.ToString(),
                [
                    (reconciliation.AccountId, paymentAmount, 0m, statementLine.Reference ?? "Bank Deposit"),
                    (ledgerAccounts["1100"], 0m, paymentAmount, "Receivable settled")
                ]);

            LedgerPosting.Add(db, journal);
            await LedgerPosting.EnsurePeriodOpenAsync(db, statementLine.TransactionDate, ct);

            var bankLine = journal.Lines.First(l => l.AccountId == reconciliation.AccountId);
            statementLine.Match(bankLine.Id);
        }
        else if (request.SuggestionType == "VendorPayment")
        {
            if (statementLine.Amount >= 0)
                throw new ConflictException("Vendor payment requires a negative statement amount (withdrawal).");

            var absAmount = decimal.Round(Math.Abs(statementLine.Amount), 2, MidpointRounding.AwayFromZero);
            var billNum = request.BillOrReferenceNumber ?? statementLine.Reference ?? $"BANK-{statementLine.TransactionDate:yyyyMMdd}";

            var purchasePayment = PurchasePayment.Create(
                request.EntityId,
                billNum,
                absAmount,
                "Bank Transfer",
                statementLine.TransactionDate,
                statementLine.Reference,
                "Auto-matched from bank statement suggestion");

            db.PurchasePayments.Add(purchasePayment);

            var journal = JournalEntry.Post(
                statementLine.TransactionDate,
                $"Supplier payment {billNum} (Bank Feed)",
                "PurchasePayment",
                purchasePayment.Id.ToString(),
                [
                    (ledgerAccounts["2000"], absAmount, 0m, "Supplier payable settled"),
                    (reconciliation.AccountId, 0m, absAmount, statementLine.Reference ?? "Bank Withdrawal")
                ]);

            LedgerPosting.Add(db, journal);
            await LedgerPosting.EnsurePeriodOpenAsync(db, statementLine.TransactionDate, ct);

            var bankLine = journal.Lines.First(l => l.AccountId == reconciliation.AccountId);
            statementLine.Match(bankLine.Id);
        }

        await db.SaveChangesAsync(ct);
        return await BankReconciliationMapper.MapAsync(db, reconciliation.Id, ct);
    }
}

