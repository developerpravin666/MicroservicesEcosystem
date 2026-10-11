using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.Sales.Products;

internal static class SalesGridSorting
{
    public static IQueryable<SalesProduct> Apply(IAccountingInventoryDbContext db,
        IQueryable<SalesProduct> sales, string? sortBy, string? sortDirection)
    {
        var rows = sales.Select(sale => new
        {
            Sale = sale,
            sale.Id, sale.BillNumber, sale.SaleDate, sale.CreatedAt, sale.DueDate, sale.PaymentTermsDays,
            BillDate = sale.SaleDate,
            sale.ProductPrice, sale.SellingPrice, sale.Discount, sale.IsActive,
            CustomerName = db.Customers.Where(customer => customer.Id == sale.CustomerId).Select(customer => customer.Name).FirstOrDefault(),
            CustomerMobile = db.Customers.Where(customer => customer.Id == sale.CustomerId).Select(customer => customer.Mobile).FirstOrDefault(),
            CustomerEmail = db.Customers.Where(customer => customer.Id == sale.CustomerId).Select(customer => customer.Email).FirstOrDefault(),
            SerialNumber = db.Products.Where(product => product.Id.ToString() == sale.ProductId).Select(product => product.SerialNumber).FirstOrDefault(),
            ProductName = db.Products.Where(product => product.Id.ToString() == sale.ProductId).Select(product =>
                (db.Brands.Where(item => item.Id == product.BrandId).Select(item => item.Name).FirstOrDefault() ?? "") + " - " +
                (db.ProductModels.Where(item => item.Id == product.ProductModelId).Select(item => item.Name).FirstOrDefault() ?? "") + " - " +
                (db.Variants.Where(item => item.Id == product.VariantId).Select(item => item.Name).FirstOrDefault() ?? "") + " - " +
                (db.Colors.Where(item => item.Id == product.ColorId).Select(item => item.Name).FirstOrDefault() ?? "") + " — " + product.SerialNumber)
                .FirstOrDefault(),
            PaymentMode = db.SalesPayments.Where(payment => payment.SalesProductId == sale.Id).Select(payment => payment.PaymentMode).FirstOrDefault(),
            TotalAmount = sale.TotalAmount,
            AmountPaid = db.SalesReceipts.Where(receipt => receipt.SalesProductId == sale.Id).Sum(receipt => (decimal?)receipt.Amount) ?? 0m,
        }).Select(row => new
        {
            row.Sale, row.Id, row.BillNumber, row.SaleDate, row.CreatedAt, row.BillDate, row.DueDate, row.PaymentTermsDays,
            row.ProductPrice, row.SellingPrice, row.Discount, row.IsActive,
            row.CustomerName, row.CustomerMobile, row.CustomerEmail, row.SerialNumber,
            row.ProductName, row.PaymentMode, row.TotalAmount, row.AmountPaid,
            Balance = Math.Max(0m, row.TotalAmount - row.AmountPaid),
            PaymentStatus = row.TotalAmount <= row.AmountPaid ? "Paid" : row.AmountPaid > 0m ? "Partially paid" : "Unpaid",
        });
        return GridSorting.Apply(rows, sortBy, sortDirection, "SaleDate,CreatedAt", true).Select(row => row.Sale);
    }
}
