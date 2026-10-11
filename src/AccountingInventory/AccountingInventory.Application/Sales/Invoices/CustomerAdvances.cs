using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace AccountingInventory.Application.Sales.Invoices;

public sealed record ApplyCustomerAdvanceCommand(Guid AdvanceId, Guid InvoiceId, decimal Amount, DateOnly ApplicationDate) : IRequest<Guid>;
public sealed record RefundCustomerAdvanceCommand(Guid AdvanceId, decimal Amount, DateOnly PaymentDate, string PaymentMode, string? ReferenceNumber) : IRequest<Guid>;
public sealed record GetCustomerAdvancesQuery(Guid CustomerId) : IRequest<IReadOnlyList<CustomerAdvance>>;
public sealed class CustomerAdvancesHandler(IAccountingInventoryDbContext db) :
    IRequestHandler<ApplyCustomerAdvanceCommand, Guid>, IRequestHandler<RefundCustomerAdvanceCommand, Guid>,
    IRequestHandler<GetCustomerAdvancesQuery, IReadOnlyList<CustomerAdvance>>
{
    public async Task<IReadOnlyList<CustomerAdvance>> Handle(GetCustomerAdvancesQuery q, CancellationToken ct)
        => await db.CustomerAdvances.AsNoTracking().Where(a => a.CustomerId == q.CustomerId && a.RemainingAmount > 0).OrderBy(a => a.PaymentDate).ToListAsync(ct);
    public async Task<Guid> Handle(ApplyCustomerAdvanceCommand q, CancellationToken ct)
    {
        var advance = await db.CustomerAdvances.SingleOrDefaultAsync(a => a.Id == q.AdvanceId, ct) ?? throw new NotFoundException("Advance not found.");
        var invoice = await db.SalesInvoices.SingleOrDefaultAsync(i => i.Id == q.InvoiceId, ct) ?? throw new NotFoundException("Invoice not found.");
        if (invoice.CustomerId != advance.CustomerId || invoice.IsCancelled) throw new ConflictException("Choose an active invoice for the same customer.");
        if (q.ApplicationDate < advance.PaymentDate || q.ApplicationDate < invoice.InvoiceDate) throw new ConflictException("Advance application cannot precede the receipt or invoice.");
        await LedgerPosting.EnsurePeriodOpenAsync(db, q.ApplicationDate, ct);
        advance.Consume(q.Amount); invoice.RecordPayment(q.Amount);
        var receipt = SalesInvoiceReceipt.Create(invoice.Id, q.Amount, "Advance", q.ApplicationDate, advance.ReferenceNumber, "Existing advance applied", advance.Id);
        db.SalesInvoiceReceipts.Add(receipt);
        // Cash and AR were posted when the advance was received. Application only allocates that credit.
        await db.SaveChangesAsync(ct); return receipt.Id;
    }
    public async Task<Guid> Handle(RefundCustomerAdvanceCommand q, CancellationToken ct)
    {
        var advance = await db.CustomerAdvances.SingleOrDefaultAsync(a => a.Id == q.AdvanceId, ct) ?? throw new NotFoundException("Advance not found.");
        if (q.PaymentDate < advance.PaymentDate) throw new ConflictException("Refund cannot precede the advance receipt.");
        await LedgerPosting.EnsurePeriodOpenAsync(db, q.PaymentDate, ct);
        var refund = CustomerAdvanceRefund.Create(advance.Id, q.Amount, q.PaymentDate, q.PaymentMode, q.ReferenceNumber);
        advance.Consume(q.Amount); db.CustomerAdvanceRefunds.Add(refund);
        var accounts = await LedgerPosting.EnsureSystemAccountsAsync(db, ct);
        LedgerPosting.Add(db, JournalEntry.Post(q.PaymentDate, "Customer advance refund", "CustomerAdvanceRefund", refund.Id.ToString(),
            [(accounts["1100"], q.Amount, 0m, (string?)"Advance refunded"), (accounts[q.PaymentMode == "Cash" ? "1000" : "1010"], 0m, q.Amount, q.ReferenceNumber)]));
        await db.SaveChangesAsync(ct); return refund.Id;
    }
}
