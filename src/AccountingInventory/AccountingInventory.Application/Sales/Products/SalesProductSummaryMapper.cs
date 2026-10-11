using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Products;

internal static class SalesProductSummaryMapper
{
    public static async Task<IReadOnlyList<SalesProductSummary>> MapAsync(
        IAccountingInventoryDbContext db,
        IReadOnlyCollection<SalesProduct> sales,
        CancellationToken cancellationToken)
    {
        var productIds = sales
            .Select(sale => Guid.TryParse(sale.ProductId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var products = await db.Products.AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new
            {
                product.Id,
                Brand = db.Brands.Where(item => item.Id == product.BrandId)
                    .Select(item => item.Name).FirstOrDefault() ?? string.Empty,
                Model = db.ProductModels.Where(item => item.Id == product.ProductModelId)
                    .Select(item => item.Name).FirstOrDefault() ?? string.Empty,
                Variant = db.Variants.Where(item => item.Id == product.VariantId)
                    .Select(item => item.Name).FirstOrDefault() ?? string.Empty,
                Color = db.Colors.Where(item => item.Id == product.ColorId)
                    .Select(item => item.Name).FirstOrDefault() ?? string.Empty,
                product.SerialNumber,
                product.SerialNumber1
            })
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        var saleIds = sales.Select(sale => sale.Id).ToArray();
        var snapshots = await db.InvoiceSnapshots.AsNoTracking().Where(x => x.Kind == "Sale" && saleIds.Contains(x.SourceId))
            .ToDictionaryAsync(x => x.SourceId, cancellationToken);
        var customerIds = sales.Select(sale => sale.CustomerId).Distinct().ToArray();
        var customers = await db.Customers.AsNoTracking()
            .Where(customer => customerIds.Contains(customer.Id))
            .ToDictionaryAsync(customer => customer.Id, cancellationToken);

        var payments = await db.SalesPayments.AsNoTracking()
            .Where(payment => saleIds.Contains(payment.SalesProductId))
            .ToDictionaryAsync(payment => payment.SalesProductId, cancellationToken);

        var receipts = await db.SalesReceipts.AsNoTracking()
            .Where(receipt => saleIds.Contains(receipt.SalesProductId) && receipt.IsActive)
            .GroupBy(receipt => receipt.SalesProductId)
            .Select(g => new { SalesProductId = g.Key, TotalPaid = g.Sum(r => r.Amount) })
            .ToDictionaryAsync(x => x.SalesProductId, x => x.TotalPaid, cancellationToken);

        var financeVendorIds = payments.Values
            .Where(p => p.FinanceVendorId.HasValue)
            .Select(p => p.FinanceVendorId!.Value)
            .Distinct()
            .ToArray();

        var financeVendors = await db.FinanceVendors.AsNoTracking()
            .Where(v => financeVendorIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Name, cancellationToken);

        return sales.Select(sale =>
        {
            var product = Guid.TryParse(sale.ProductId, out var id) && products.TryGetValue(id, out var match)
                ? match
                : null;
            var serialNumber = product?.SerialNumber ?? product?.SerialNumber1 ?? string.Empty;
            customers.TryGetValue(sale.CustomerId, out var customer);
            snapshots.TryGetValue(sale.Id, out var snapshot);
            var productName = product is null
                ? string.Empty
                : string.Join(" - ", new[] { product.Brand, product.Model, product.Variant, product.Color }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
            if (!string.IsNullOrWhiteSpace(serialNumber))
                productName = $"{productName} — {serialNumber}";

            payments.TryGetValue(sale.Id, out var paymentRecord);
            var receiptPaid = receipts.GetValueOrDefault(sale.Id, 0m);

            decimal totalPaid = receiptPaid;
            if (paymentRecord != null && paymentRecord.DownPayment.HasValue && receiptPaid == 0m)
            {
                totalPaid += paymentRecord.DownPayment.Value;
            }

            var balanceAmount = Math.Max(0m, sale.TotalAmount - totalPaid);
            var financeVendorName = paymentRecord?.FinanceVendorId.HasValue == true && financeVendors.TryGetValue(paymentRecord.FinanceVendorId.Value, out var fvName)
                ? fvName
                : null;

            return new SalesProductSummary(
                sale.Id,
                sale.ProductId,
                snapshot?.ProductName ?? productName,
                snapshot?.SerialNumber ?? serialNumber,
                sale.CustomerId,
                snapshot?.PartyName ?? customer?.Name ?? string.Empty,
                snapshot?.PartyMobile ?? customer?.Mobile ?? string.Empty,
                snapshot?.PartyAddress ?? customer?.Address ?? string.Empty,
                snapshot is not null ? snapshot.PartyEmail : customer?.Email,
                sale.SaleDate,
                sale.PaymentTermsDays,
                sale.DueDate,
                sale.ProductPrice,
                sale.SellingPrice,
                sale.Discount,
                sale.TaxId,
                sale.CgstRate,
                sale.SgstRate,
                sale.TaxableAmount,
                sale.CgstAmount,
                sale.SgstAmount,
                sale.TotalAmount,
                sale.IsActive,
                paymentRecord?.PaymentMode,
                sale.BillNumber,
                PaidAmount: totalPaid,
                BalanceAmount: balanceAmount,
                DownPayment: paymentRecord?.DownPayment,
                NumberOfEmi: paymentRecord?.NumberOfEmi,
                EmiAmount: paymentRecord?.EmiAmount,
                FirstInstallmentDate: paymentRecord?.FirstInstallmentDate,
                FinanceVendorName: financeVendorName);
        }).ToArray();
    }
}
