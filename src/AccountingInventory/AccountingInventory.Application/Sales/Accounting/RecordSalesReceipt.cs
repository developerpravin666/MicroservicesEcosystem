using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Accounting;

public sealed record RecordSalesReceiptCommand(
    Guid SalesProductId, decimal Amount, string PaymentMode, DateOnly PaymentDate,
    string? ReferenceNumber, string? Note) : IRequest<SalesReceiptSummary>;

public sealed class RecordSalesReceiptValidator : AbstractValidator<RecordSalesReceiptCommand>
{
    public RecordSalesReceiptValidator()
    {
        RuleFor(command => command.SalesProductId).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(command => command.PaymentMode).Must(mode => mode is "Cash" or "UPI" or "OnlineTransfer" or "Cheque" or "Other");
        RuleFor(command => command.PaymentDate).NotEmpty();
        RuleFor(command => command.ReferenceNumber).MaximumLength(100);
        RuleFor(command => command.Note).MaximumLength(500);
        RuleFor(command => command.ReferenceNumber).NotEmpty()
            .When(command => command.PaymentMode is "UPI" or "OnlineTransfer" or "Cheque")
            .WithMessage("A transaction or cheque reference is required for this payment method.");
    }
}

public sealed class RecordSalesReceiptHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<RecordSalesReceiptCommand, SalesReceiptSummary>
{
    public async Task<SalesReceiptSummary> Handle(RecordSalesReceiptCommand request, CancellationToken cancellationToken)
    {
        var sale = await db.SalesProducts.SingleOrDefaultAsync(item => item.Id == request.SalesProductId, cancellationToken)
            ?? throw new NotFoundException($"Sales bill '{request.SalesProductId}' was not found.");
        if (!sale.IsActive || sale.IsReturned) throw new ConflictException("Receipts cannot be recorded against an inactive or returned sale.");
        if (request.PaymentDate < sale.SaleDate)
            throw new ConflictException("The receipt date cannot precede the invoice date.");
        var total = sale.TotalAmount;
        var amountPaid = await db.SalesReceipts.Where(receipt => receipt.SalesProductId == sale.Id)
            .SumAsync(receipt => (decimal?)receipt.Amount, cancellationToken) ?? 0m;
        var balance = Math.Max(0m, total - amountPaid);
        if (request.Amount > balance)
            throw new ConflictException($"Payment exceeds the remaining balance of {balance:0.00}.");

        var receipt = SalesReceipt.Create(sale.Id, request.Amount, request.PaymentMode,
            request.PaymentDate, request.ReferenceNumber, request.Note);
        db.SalesReceipts.Add(receipt);
        var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(db, cancellationToken);
        LedgerPosting.Add(db, LedgerPosting.ForSalesReceipt(receipt, ledgerAccounts));
        await LedgerPosting.EnsurePeriodOpenAsync(db, receipt.PaymentDate, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new SalesReceiptSummary(receipt.Id, receipt.Amount, receipt.PaymentMode, receipt.PaymentDate,
            receipt.ReferenceNumber, receipt.Note);
    }
}
