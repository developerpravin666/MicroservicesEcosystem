using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Purchases.Accounting;

public sealed class RecordPurchasePaymentCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<RecordPurchasePaymentCommand, PurchasePaymentSummary>
{
    public async Task<PurchasePaymentSummary> Handle(RecordPurchasePaymentCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Vendors.AnyAsync(vendor => vendor.Id == request.VendorId, cancellationToken))
            throw new NotFoundException($"Vendor '{request.VendorId}' was not found.");

        var billNumber = request.BillNumber.Trim();
        var normalized = billNumber.ToLower();
        var products = AccountingInventory.Application.Purchases.Accounting.PurchaseStockRows.Query(db).Where(product => product.VendorId == request.VendorId
            && product.BillNumber != null && product.BillNumber.ToLower() == normalized);
        if (await products.AnyAsync(x => x.IsOpeningStock, cancellationToken))
            throw new ConflictException("Opening supplier invoices must be settled through Opening Balances outstanding items.");
        var billTotal = await products.SumAsync(product => product.TotalAmount, cancellationToken);
        billTotal -= await db.InvoiceCorrections.Where(x => x.Kind == "Purchase" && x.PartyId == request.VendorId && x.BillNumber.ToLower() == normalized)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        if (!await products.AnyAsync(cancellationToken))
            throw new NotFoundException($"Purchase bill '{billNumber}' was not found.");

        if (await products.AnyAsync(product => product.PurchaseDate > request.PaymentDate, cancellationToken))
            throw new ConflictException("The payment date cannot precede the purchase invoice date.");
        var paid = await db.PurchasePayments.Where(payment => payment.VendorId == request.VendorId
                && payment.BillNumber.ToLower() == normalized)
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
        var balance = Math.Max(0m, billTotal - paid);
        if (request.Amount > balance)
            throw new ConflictException($"Payment exceeds the outstanding balance of {balance:0.00}.");

        var payment = PurchasePayment.Create(request.VendorId, billNumber, request.Amount,
            request.PaymentMode, request.PaymentDate, request.ReferenceNumber, request.Note);
        db.PurchasePayments.Add(payment);
        var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(db, cancellationToken);
        LedgerPosting.Add(db, LedgerPosting.ForPurchasePayment(payment, ledgerAccounts));
        await LedgerPosting.EnsurePeriodOpenAsync(db, payment.PaymentDate, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new PurchasePaymentSummary(payment.Id, payment.Amount, payment.PaymentMode,
            payment.PaymentDate, payment.ReferenceNumber, payment.Note);
    }
}
