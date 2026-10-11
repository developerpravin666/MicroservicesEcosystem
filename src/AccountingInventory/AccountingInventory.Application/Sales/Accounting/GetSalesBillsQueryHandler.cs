using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Sales.Products;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Accounting;

public sealed record GetSalesBillsQuery(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, string? SortDirection = null)
    : IRequest<PagedResult<SalesBillSummary>>;

public sealed record SalesBillSummary(
    Guid Id, string BillNumber, string ProductName, string SerialNumber, string CustomerName,
    string CustomerMobile, string CustomerAddress, string? CustomerEmail, DateOnly BillDate, int PaymentTermsDays, DateOnly DueDate,
    decimal SellingPrice, decimal Discount, decimal TaxableAmount, decimal CgstRate, decimal CgstAmount,
    decimal SgstRate, decimal SgstAmount, decimal TotalAmount, decimal AmountPaid, decimal Balance,
    string PaymentStatus);

public sealed record SalesReceiptSummary(
    Guid Id, decimal Amount, string PaymentMode, DateOnly PaymentDate, string? ReferenceNumber, string? Note);

public sealed record SalesBillDetails(SalesBillSummary Bill, IReadOnlyList<SalesReceiptSummary> Payments);

public sealed record GetSalesBillDetailsQuery(Guid SalesProductId) : IRequest<SalesBillDetails>;

public sealed class GetSalesBillsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetSalesBillsQuery, PagedResult<SalesBillSummary>>
{
    public async Task<PagedResult<SalesBillSummary>> Handle(GetSalesBillsQuery request, CancellationToken cancellationToken)
    {
        var query = db.SalesProducts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            var customerIds = await db.Customers.AsNoTracking()
                .Where(customer => customer.Name.ToLower().Contains(search) || customer.Mobile.Contains(search))
                .Select(customer => customer.Id).ToArrayAsync(cancellationToken);
            var matchingProductIds = await db.Products.AsNoTracking()
                .Where(product => product.SerialNumber.ToLower().Contains(search)
                    || (product.SerialNumber1 != null && product.SerialNumber1.ToLower().Contains(search))
                    || db.Brands.Any(brand => brand.Id == product.BrandId && brand.Name.ToLower().Contains(search))
                    || db.ProductModels.Any(model => model.Id == product.ProductModelId && model.Name.ToLower().Contains(search))
                    || db.Variants.Any(variant => variant.Id == product.VariantId && variant.Name.ToLower().Contains(search))
                    || db.Colors.Any(color => color.Id == product.ColorId && color.Name.ToLower().Contains(search)))
                .Select(product => product.Id.ToString()).ToArrayAsync(cancellationToken);
            query = query.Where(sale => sale.BillNumber.ToLower().Contains(search)
                || customerIds.Contains(sale.CustomerId)
                || matchingProductIds.Contains(sale.ProductId));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var sales = await SalesGridSorting.Apply(db, query, request.SortBy, request.SortDirection)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var saleSummaries = await SalesProductSummaryMapper.MapAsync(db, sales, cancellationToken);
        var saleIds = sales.Select(sale => sale.Id).ToArray();
        var paidRows = await db.SalesReceipts.AsNoTracking().Where(receipt => saleIds.Contains(receipt.SalesProductId))
            .GroupBy(receipt => receipt.SalesProductId)
            .Select(group => new { SaleId = group.Key, AmountPaid = group.Sum(receipt => receipt.Amount) })
            .ToDictionaryAsync(row => row.SaleId, row => row.AmountPaid, cancellationToken);

        var summariesById = saleSummaries.ToDictionary(sale => sale.Id);
        var items = sales.Select(sale => ToBill(sale, summariesById[sale.Id], paidRows.GetValueOrDefault(sale.Id))).ToArray();
        return new PagedResult<SalesBillSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    internal static SalesBillSummary ToBill(
        AccountingInventory.Domain.Entities.SalesProduct sale,
        SalesProductSummary summary,
        decimal amountPaid)
    {
        var total = sale.TotalAmount;
        var balance = sale.IsReturned ? 0 : Math.Max(0m, total - amountPaid);
        var status = sale.IsReturned ? "Returned" : balance == 0m ? "Paid" : amountPaid > 0m ? "Partially paid" : "Unpaid";
        return new SalesBillSummary(sale.Id, summary.BillNumber, summary.ProductName, summary.SerialNumber,
            summary.CustomerName, summary.CustomerMobile, summary.CustomerAddress, summary.CustomerEmail,
            sale.SaleDate, sale.PaymentTermsDays, sale.DueDate, sale.SellingPrice, sale.Discount, sale.TaxableAmount, sale.CgstRate,
            sale.CgstAmount, sale.SgstRate, sale.SgstAmount, total, amountPaid, balance, status);
    }
}

public sealed class GetSalesBillDetailsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetSalesBillDetailsQuery, SalesBillDetails>
{
    public async Task<SalesBillDetails> Handle(GetSalesBillDetailsQuery request, CancellationToken cancellationToken)
    {
        var sale = await db.SalesProducts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.SalesProductId, cancellationToken)
            ?? throw new NotFoundException($"Sales bill '{request.SalesProductId}' was not found.");
        var summary = (await SalesProductSummaryMapper.MapAsync(db, [sale], cancellationToken))[0];
        var payments = await db.SalesReceipts.AsNoTracking()
            .Where(receipt => receipt.SalesProductId == sale.Id)
            .OrderByDescending(receipt => receipt.PaymentDate).ThenByDescending(receipt => receipt.CreatedAt)
            .Select(receipt => new SalesReceiptSummary(receipt.Id, receipt.Amount, receipt.PaymentMode,
                receipt.PaymentDate, receipt.ReferenceNumber, receipt.Note))
            .ToListAsync(cancellationToken);
        var bill = GetSalesBillsQueryHandler.ToBill(sale, summary, payments.Sum(payment => payment.Amount));
        return new SalesBillDetails(bill, payments);
    }
}
