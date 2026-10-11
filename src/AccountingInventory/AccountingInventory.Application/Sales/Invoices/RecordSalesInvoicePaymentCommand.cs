using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Invoices;

public sealed record RecordSalesInvoicePaymentCommand(
    Guid InvoiceId,
    decimal Amount,
    string PaymentMode,
    DateOnly PaymentDate,
    string? ReferenceNumber = null,
    string? Note = null) : IRequest<SalesInvoiceReceiptDto>;

public sealed class RecordSalesInvoicePaymentCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<RecordSalesInvoicePaymentCommand, SalesInvoiceReceiptDto>
{
    public async Task<SalesInvoiceReceiptDto> Handle(RecordSalesInvoicePaymentCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices
            .SingleOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException($"Sales invoice '{request.InvoiceId}' was not found.");

        if (invoice.IsCancelled)
            throw new ConflictException("Cannot record payment against a cancelled invoice.");

        if (request.PaymentDate < invoice.InvoiceDate)
            throw new ConflictException("Payment date cannot precede invoice date.");

        var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(db, cancellationToken);
        invoice.RecordPayment(request.Amount);

        var receipt = SalesInvoiceReceipt.Create(
            invoice.Id,
            request.Amount,
            request.PaymentMode,
            request.PaymentDate,
            request.ReferenceNumber,
            request.Note);

        db.SalesInvoiceReceipts.Add(receipt);
        LedgerPosting.Add(db, LedgerPosting.ForSalesReceipt(receipt, ledgerAccounts));
        await LedgerPosting.EnsurePeriodOpenAsync(db, receipt.PaymentDate, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new SalesInvoiceReceiptDto(
            receipt.Id,
            receipt.Amount,
            receipt.PaymentMode,
            receipt.PaymentDate,
            receipt.ReferenceNumber,
            receipt.Note);
    }
}

