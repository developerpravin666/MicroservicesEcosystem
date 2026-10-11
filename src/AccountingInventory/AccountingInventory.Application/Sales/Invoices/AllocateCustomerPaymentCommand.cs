using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Invoices;

public sealed record InvoiceAllocationItemDto(
    Guid InvoiceId,
    decimal AllocatedAmount);

public sealed record AllocateCustomerPaymentCommand(
    Guid CustomerId,
    decimal TotalPaymentAmount,
    string PaymentMode,
    DateOnly PaymentDate,
    string? ReferenceNumber = null,
    string? Note = null,
    bool AutoAllocateFifo = true,
    IReadOnlyList<InvoiceAllocationItemDto>? SpecificAllocations = null) : IRequest<PaymentAllocationResultDto>;

public sealed record InvoiceAllocationDetailDto(
    Guid InvoiceId,
    string BillNumber,
    DateOnly InvoiceDate,
    decimal TotalAmount,
    decimal PreviousBalance,
    decimal AmountAllocated,
    decimal RemainingBalance,
    string NewStatus,
    Guid ReceiptId);

public sealed record PaymentAllocationResultDto(
    Guid CustomerId,
    string CustomerName,
    decimal TotalPaymentAmount,
    decimal TotalAllocated,
    decimal UnallocatedAdvance,
    string PaymentMode,
    DateOnly PaymentDate,
    string? ReferenceNumber,
    IReadOnlyList<InvoiceAllocationDetailDto> Allocations);

public sealed record UnpaidCustomerInvoiceDto(
    Guid InvoiceId,
    string BillNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Balance,
    int DaysOverdue);

public sealed record CustomerUnpaidInvoicesSummaryDto(
    Guid CustomerId,
    string CustomerName,
    string CustomerMobile,
    decimal TotalOutstanding,
    IReadOnlyList<UnpaidCustomerInvoiceDto> Invoices);

public sealed record GetCustomerUnpaidInvoicesQuery(Guid CustomerId) : IRequest<CustomerUnpaidInvoicesSummaryDto>;

public sealed class GetCustomerUnpaidInvoicesQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetCustomerUnpaidInvoicesQuery, CustomerUnpaidInvoicesSummaryDto>
{
    public async Task<CustomerUnpaidInvoicesSummaryDto> Handle(GetCustomerUnpaidInvoicesQuery request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(i => i.CustomerId == request.CustomerId && !i.IsCancelled && i.Balance > 0)
            .OrderBy(i => i.InvoiceDate)
            .ThenBy(i => i.CreatedAt)
            .Select(i => new UnpaidCustomerInvoiceDto(
                i.Id,
                i.BillNumber,
                i.InvoiceDate,
                i.DueDate,
                i.TotalAmount,
                i.AmountPaid,
                i.Balance,
                today.DayNumber > i.DueDate.DayNumber ? today.DayNumber - i.DueDate.DayNumber : 0
            ))
            .ToListAsync(cancellationToken);

        var totalOutstanding = invoices.Sum(i => i.Balance);

        return new CustomerUnpaidInvoicesSummaryDto(
            customer.Id,
            customer.Name,
            customer.Mobile,
            totalOutstanding,
            invoices);
    }
}

public sealed class AllocateCustomerPaymentCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<AllocateCustomerPaymentCommand, PaymentAllocationResultDto>
{
    public async Task<PaymentAllocationResultDto> Handle(AllocateCustomerPaymentCommand request, CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty) throw new ArgumentException("Customer ID is required.");
        if (request.TotalPaymentAmount <= 0) throw new ArgumentOutOfRangeException(nameof(request.TotalPaymentAmount), "Payment amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.PaymentMode)) throw new ArgumentException("Payment mode is required.");

        InvoicePaymentRules.Validate(request.TotalPaymentAmount, request.PaymentMode, request.PaymentDate, request.ReferenceNumber, request.Note);
        if (!request.AutoAllocateFifo && request.SpecificAllocations is { } specs)
        {
            if (specs.Any(s => s.AllocatedAmount < 0 || s.AllocatedAmount != decimal.Round(s.AllocatedAmount, 2))
                || specs.Select(s => s.InvoiceId).Distinct().Count() != specs.Count)
                throw new ConflictException("Allocations must have unique invoice IDs and nonnegative two-decimal amounts.");
            if (specs.Sum(s => s.AllocatedAmount) > request.TotalPaymentAmount)
                throw new ConflictException("Total allocation cannot exceed the payment received.");
        }
        var customer = await db.Customers
            .SingleOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var roundedTotalPayment = decimal.Round(request.TotalPaymentAmount, 2, MidpointRounding.AwayFromZero);

        // Fetch all active unpaid invoices for this customer
        var unpaidInvoices = await db.SalesInvoices
            .Where(i => i.CustomerId == request.CustomerId && !i.IsCancelled && i.Balance > 0)
            .OrderBy(i => i.InvoiceDate)
            .ThenBy(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(db, cancellationToken);
        var allocationDetails = new List<InvoiceAllocationDetailDto>();

        decimal totalAllocated = 0m;

        if (request.AutoAllocateFifo)
        {
            var remaining = roundedTotalPayment;

            foreach (var inv in unpaidInvoices)
            {
                if (remaining <= 0) break;

                if (request.PaymentDate < inv.InvoiceDate) throw new ConflictException("Payment date cannot precede an allocated invoice date.");
                var prevBalance = inv.Balance;
                var alloc = Math.Min(prevBalance, remaining);

                inv.RecordPayment(alloc);
                remaining = decimal.Round(remaining - alloc, 2, MidpointRounding.AwayFromZero);
                totalAllocated = decimal.Round(totalAllocated + alloc, 2, MidpointRounding.AwayFromZero);

                var noteText = !string.IsNullOrWhiteSpace(request.Note)
                    ? $"FIFO Allocation - {request.Note}"
                    : "Auto FIFO Payment Allocation";

                var receipt = SalesInvoiceReceipt.Create(
                    inv.Id,
                    alloc,
                    request.PaymentMode,
                    request.PaymentDate,
                    request.ReferenceNumber,
                    noteText);

                db.SalesInvoiceReceipts.Add(receipt);
                LedgerPosting.Add(db, LedgerPosting.ForSalesReceipt(receipt, ledgerAccounts));

                allocationDetails.Add(new InvoiceAllocationDetailDto(
                    inv.Id,
                    inv.BillNumber,
                    inv.InvoiceDate,
                    inv.TotalAmount,
                    prevBalance,
                    alloc,
                    inv.Balance,
                    inv.PaymentStatus,
                    receipt.Id));
            }
        }
        else if (request.SpecificAllocations != null && request.SpecificAllocations.Count > 0)
        {
            var invoiceMap = unpaidInvoices.ToDictionary(i => i.Id);

            foreach (var spec in request.SpecificAllocations.Where(s => s.AllocatedAmount > 0))
            {
                if (!invoiceMap.TryGetValue(spec.InvoiceId, out var inv))
                    throw new NotFoundException($"Unpaid invoice '{spec.InvoiceId}' not found for customer.");

                if (request.PaymentDate < inv.InvoiceDate) throw new ConflictException("Payment date cannot precede an allocated invoice date.");
                var prevBalance = inv.Balance;
                var alloc = decimal.Round(spec.AllocatedAmount, 2, MidpointRounding.AwayFromZero);

                if (alloc > prevBalance)
                    throw new ConflictException($"Allocated amount {alloc} exceeds invoice balance {prevBalance} for bill {inv.BillNumber}.");

                inv.RecordPayment(alloc);
                totalAllocated = decimal.Round(totalAllocated + alloc, 2, MidpointRounding.AwayFromZero);

                var receipt = SalesInvoiceReceipt.Create(
                    inv.Id,
                    alloc,
                    request.PaymentMode,
                    request.PaymentDate,
                    request.ReferenceNumber,
                    request.Note);

                db.SalesInvoiceReceipts.Add(receipt);
                LedgerPosting.Add(db, LedgerPosting.ForSalesReceipt(receipt, ledgerAccounts));

                allocationDetails.Add(new InvoiceAllocationDetailDto(
                    inv.Id,
                    inv.BillNumber,
                    inv.InvoiceDate,
                    inv.TotalAmount,
                    prevBalance,
                    alloc,
                    inv.Balance,
                    inv.PaymentStatus,
                    receipt.Id));
            }
        }

        var unallocatedAdvance = decimal.Round(Math.Max(0m, roundedTotalPayment - totalAllocated), 2, MidpointRounding.AwayFromZero);

        // If there's an unallocated advance payment beyond all outstanding invoices, post On-Account journal
        if (unallocatedAdvance > 0)
        {
            var advance = CustomerAdvance.Create(customer.Id, unallocatedAdvance, request.PaymentMode, request.PaymentDate, request.ReferenceNumber);
            db.CustomerAdvances.Add(advance);
            var cashAccount = request.PaymentMode == "Cash" ? "1000" : "1010";
            var advanceJournal = JournalEntry.Post(
                request.PaymentDate,
                $"Customer on-account advance: {customer.Name}",
                "CustomerAdvance",
                advance.Id.ToString(),
                [
                    (ledgerAccounts[cashAccount], unallocatedAdvance, 0m, $"Unallocated customer receipt via {request.PaymentMode}"),
                    (ledgerAccounts["1100"], 0m, unallocatedAdvance, "On-Account AR credit advance")
                ]);

            LedgerPosting.Add(db, advanceJournal);
        }

        await LedgerPosting.EnsurePeriodOpenAsync(db, request.PaymentDate, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new PaymentAllocationResultDto(
            customer.Id,
            customer.Name,
            roundedTotalPayment,
            totalAllocated,
            unallocatedAdvance,
            request.PaymentMode,
            request.PaymentDate,
            request.ReferenceNumber,
            allocationDetails);
    }
}

