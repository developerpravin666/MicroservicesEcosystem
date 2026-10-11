using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Payments.Commands.RecordSalesPayment;

public sealed class RecordSalesPaymentCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<RecordSalesPaymentCommand, SalesPaymentSummary>
{
    public async Task<SalesPaymentSummary> Handle(RecordSalesPaymentCommand request, CancellationToken cancellationToken)
    {
        if (!await db.SalesProducts.AnyAsync(sale => sale.Id == request.SalesProductId, cancellationToken))
            throw new NotFoundException($"Sales product '{request.SalesProductId}' was not found.");
        if (await db.SalesProducts.AnyAsync(sale => sale.Id == request.SalesProductId && sale.IsReturned, cancellationToken))
            throw new ConflictException("Payment terms cannot be changed for a returned sale.");

        if (request.PaymentMode == "Finance")
        {
            var vendorId = request.FinanceVendorId
                ?? throw new NotFoundException("A finance company must be selected.");
            if (!await db.FinanceVendors.AnyAsync(vendor => vendor.Id == vendorId && vendor.IsActive, cancellationToken))
                throw new NotFoundException($"Finance company '{vendorId}' was not found or is inactive.");
        }

        var payment = await db.SalesPayments.SingleOrDefaultAsync(
            item => item.SalesProductId == request.SalesProductId, cancellationToken);
        if (payment is null)
        {
            payment = SalesPayment.Create(request.SalesProductId, request.PaymentMode, request.FinanceVendorId,
                request.DownPayment, request.NumberOfEmi, request.EmiAmount, request.HasInsurance,
                request.InsuranceAmount, request.FirstInstallmentDate);
            db.SalesPayments.Add(payment);
        }
        else
        {
            payment.Update(request.SalesProductId, request.PaymentMode, request.FinanceVendorId,
                request.DownPayment, request.NumberOfEmi, request.EmiAmount, request.HasInsurance,
                request.InsuranceAmount, request.FirstInstallmentDate);
        }

        await db.SaveChangesAsync(cancellationToken);
        var financeVendorName = payment.FinanceVendorId is { } selectedVendorId
            ? await db.FinanceVendors.AsNoTracking().Where(vendor => vendor.Id == selectedVendorId)
                .Select(vendor => vendor.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new SalesPaymentSummary(payment.Id, payment.SalesProductId, payment.PaymentMode,
            payment.FinanceVendorId, financeVendorName, payment.DownPayment, payment.NumberOfEmi,
            payment.EmiAmount, payment.HasInsurance, payment.InsuranceAmount, payment.FirstInstallmentDate);
    }
}
