using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Payments.Queries.GetSalesPayment;

public sealed class GetSalesPaymentQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetSalesPaymentQuery, SalesPaymentSummary?>
{
    public async Task<SalesPaymentSummary?> Handle(GetSalesPaymentQuery request, CancellationToken cancellationToken)
    {
        if (!await db.SalesProducts.AnyAsync(sale => sale.Id == request.SalesProductId, cancellationToken))
            throw new NotFoundException($"Sales product '{request.SalesProductId}' was not found.");

        var payment = await db.SalesPayments.AsNoTracking()
            .Where(item => item.SalesProductId == request.SalesProductId)
            .Select(item => new
            {
                item.Id,
                item.SalesProductId,
                item.PaymentMode,
                item.FinanceVendorId,
                item.DownPayment,
                item.NumberOfEmi,
                item.EmiAmount,
                item.HasInsurance,
                item.InsuranceAmount,
                item.FirstInstallmentDate
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is null) return null;

        var financeVendorName = payment.FinanceVendorId is { } vendorId
            ? await db.FinanceVendors.AsNoTracking().Where(vendor => vendor.Id == vendorId)
                .Select(vendor => vendor.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new SalesPaymentSummary(payment.Id, payment.SalesProductId, payment.PaymentMode,
            payment.FinanceVendorId, financeVendorName, payment.DownPayment, payment.NumberOfEmi,
            payment.EmiAmount, payment.HasInsurance, payment.InsuranceAmount, payment.FirstInstallmentDate);
    }
}
