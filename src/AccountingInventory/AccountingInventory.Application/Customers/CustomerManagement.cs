using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Sales.Products;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Customers;

public sealed record CustomerRecord(Guid Id, string Name, string Mobile, string Address, string? Email,
    bool IsActive, int SalesCount);

public sealed record GetCustomersQuery(int Page = 1, int PageSize = 20, string? Search = null,
    string? SortBy = null, string? SortDirection = "asc") : IRequest<PagedResult<CustomerRecord>>;

public sealed class GetCustomersQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetCustomersQuery, PagedResult<CustomerRecord>>
{
    public async Task<PagedResult<CustomerRecord>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(customer => customer.Name.ToLower().Contains(search)
                || customer.Mobile.ToLower().Contains(search)
                || (customer.Email != null && customer.Email.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var sorted = GridSorting.Apply(query, request.SortBy, request.SortDirection, selectors: new SortSelectors<Customer>
        {
            ["salesCount"] = customer => db.SalesProducts.Count(sale => sale.CustomerId == customer.Id) + db.SalesInvoices.Count(i => i.CustomerId == customer.Id),
        });
        var customers = await sorted.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(customer => new CustomerRecord(customer.Id, customer.Name, customer.Mobile,
                customer.Address, customer.Email, customer.IsActive,
                db.SalesProducts.Count(sale => sale.CustomerId == customer.Id) + db.SalesInvoices.Count(i => i.CustomerId == customer.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerRecord>(customers, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}

public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerRecord>;

public sealed class GetCustomerByIdQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetCustomerByIdQuery, CustomerRecord>
{
    public async Task<CustomerRecord> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");
        var salesCount = await db.SalesProducts.CountAsync(sale => sale.CustomerId == customer.Id, cancellationToken) + await db.SalesInvoices.CountAsync(i => i.CustomerId == customer.Id, cancellationToken);
        return new CustomerRecord(customer.Id, customer.Name, customer.Mobile, customer.Address,
            customer.Email, customer.IsActive, salesCount);
    }
}

public sealed record CreateCustomerCommand(string Name, string Mobile, string Address, string? Email)
    : IRequest<CustomerRecord>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Mobile).NotEmpty().MaximumLength(20);
        RuleFor(command => command.Address).NotEmpty().MaximumLength(500);
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
    }
}

public sealed class CreateCustomerCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateCustomerCommand, CustomerRecord>
{
    public async Task<CustomerRecord> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var mobile = request.Mobile.Trim();
        if (await db.Customers.AnyAsync(customer => customer.Mobile.ToLower() == mobile.ToLower(), cancellationToken))
            throw new ConflictException($"A customer with mobile number '{mobile}' already exists.");
        var customer = Customer.Create(request.Name, mobile, request.Address, request.Email);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return new CustomerRecord(customer.Id, customer.Name, customer.Mobile, customer.Address,
            customer.Email, customer.IsActive, 0);
    }
}

public sealed record UpdateCustomerCommand(Guid Id, string Name, string Mobile, string Address, string? Email)
    : IRequest<CustomerRecord>;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Mobile).NotEmpty().MaximumLength(20);
        RuleFor(command => command.Address).NotEmpty().MaximumLength(500);
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
    }
}

public sealed class UpdateCustomerCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<UpdateCustomerCommand, CustomerRecord>
{
    public async Task<CustomerRecord> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");
        var mobile = request.Mobile.Trim();
        if (await db.Customers.AnyAsync(item => item.Id != customer.Id
            && item.Mobile.ToLower() == mobile.ToLower(), cancellationToken))
            throw new ConflictException($"A customer with mobile number '{mobile}' already exists.");
        customer.UpdateContactDetails(request.Name, mobile, request.Address, request.Email);
        await db.SaveChangesAsync(cancellationToken);
        var salesCount = await db.SalesProducts.CountAsync(sale => sale.CustomerId == customer.Id, cancellationToken) + await db.SalesInvoices.CountAsync(i => i.CustomerId == customer.Id, cancellationToken);
        return new CustomerRecord(customer.Id, customer.Name, customer.Mobile, customer.Address,
            customer.Email, customer.IsActive, salesCount);
    }
}

public sealed record DeleteCustomerCommand(Guid Id) : IRequest;

public sealed class DeleteCustomerCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<DeleteCustomerCommand>
{
    public async Task Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");
        if (await db.SalesProducts.IgnoreQueryFilters().AnyAsync(sale => sale.CustomerId == customer.Id, cancellationToken))
            throw new ConflictException("Customers with sales history cannot be deleted. Edit the customer or keep the history intact.");
        if (await db.SalesInvoices.IgnoreQueryFilters().AnyAsync(i => i.CustomerId == customer.Id, cancellationToken)
            || await db.CustomerAdvances.IgnoreQueryFilters().AnyAsync(a => a.CustomerId == customer.Id, cancellationToken)
            || await db.OpeningSubledgerBalances.AnyAsync(o => o.Kind == "Customer" && o.PartyId == customer.Id, cancellationToken))
            throw new ConflictException("Customers with financial history cannot be deleted.");
        customer.Delete();
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CustomerPaymentHistory(Guid Id, decimal Amount, string PaymentMode, DateOnly PaymentDate,
    string? ReferenceNumber, string? Note);

public sealed record CustomerSaleHistory(Guid Id, string BillNumber, DateOnly SaleDate, string ProductName,
    string SerialNumber, decimal SellingPrice, decimal Discount, decimal TotalAmount, decimal AmountPaid,
    decimal Balance, string? PaymentPlan, IReadOnlyList<CustomerPaymentHistory> Payments, bool IsMultiLineInvoice = false);

public sealed record CustomerHistory(CustomerRecord Customer, decimal TotalSales, decimal TotalReceived,
    decimal OutstandingBalance, IReadOnlyList<CustomerSaleHistory> Sales);

public sealed record GetCustomerHistoryQuery(Guid CustomerId) : IRequest<CustomerHistory>;

public sealed class GetCustomerHistoryQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetCustomerHistoryQuery, CustomerHistory>
{
    public async Task<CustomerHistory> Handle(GetCustomerHistoryQuery request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");
        var sales = await db.SalesProducts.AsNoTracking()
            .Where(sale => sale.CustomerId == customer.Id)
            .OrderByDescending(sale => sale.SaleDate).ThenByDescending(sale => sale.CreatedAt)
            .ToListAsync(cancellationToken);
        var saleSummaries = await SalesProductSummaryMapper.MapAsync(db, sales, cancellationToken);
        var saleIds = sales.Select(sale => sale.Id).ToArray();
        var plans = await db.SalesPayments.AsNoTracking().Where(plan => saleIds.Contains(plan.SalesProductId))
            .ToDictionaryAsync(plan => plan.SalesProductId, plan => new
            {
                plan.PaymentMode, plan.DownPayment, plan.NumberOfEmi, plan.EmiAmount,
                plan.HasInsurance, plan.InsuranceAmount
            }, cancellationToken);
        var receipts = await db.SalesReceipts.AsNoTracking().Where(receipt => saleIds.Contains(receipt.SalesProductId))
            .OrderByDescending(receipt => receipt.PaymentDate).ThenByDescending(receipt => receipt.CreatedAt)
            .Select(receipt => new { receipt.SalesProductId, Payment = new CustomerPaymentHistory(receipt.Id,
                receipt.Amount, receipt.PaymentMode, receipt.PaymentDate, receipt.ReferenceNumber, receipt.Note) })
            .ToListAsync(cancellationToken);
        var receiptGroups = receipts.GroupBy(item => item.SalesProductId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CustomerPaymentHistory>)group.Select(item => item.Payment).ToArray());
        var summaryById = saleSummaries.ToDictionary(summary => summary.Id);
        var saleHistory = sales.Select(sale =>
        {
            var summary = summaryById[sale.Id];
            var payments = receiptGroups.GetValueOrDefault(sale.Id) ?? [];
            var total = sale.IsReturned ? 0m : sale.TotalAmount;
            var amountPaid = payments.Sum(payment => payment.Amount);
            var paymentPlan = plans.TryGetValue(sale.Id, out var plan)
                ? plan.PaymentMode == "Finance"
                    ? $"Finance · down payment {plan.DownPayment.GetValueOrDefault():N2} · {plan.NumberOfEmi.GetValueOrDefault()} × {plan.EmiAmount.GetValueOrDefault():N2}"
                        + (plan.HasInsurance == true ? $" · insurance {plan.InsuranceAmount.GetValueOrDefault():N2}" : string.Empty)
                    : plan.PaymentMode
                : summary.PaymentMode;
            return new CustomerSaleHistory(sale.Id, sale.BillNumber, sale.SaleDate, summary.ProductName,
                summary.SerialNumber, sale.SellingPrice, sale.Discount, total, amountPaid,
                Math.Max(0m, total - amountPaid), paymentPlan, payments);
        }).ToArray();

        var customerRecord = new CustomerRecord(customer.Id, customer.Name, customer.Mobile,
            customer.Address, customer.Email, customer.IsActive, sales.Count);
        var invoices = await db.SalesInvoices.AsNoTracking().Include(i => i.Lines).Where(i => i.CustomerId == customer.Id).ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(i => i.Id).ToArray();
        var invoiceReceipts = await db.SalesInvoiceReceipts.AsNoTracking().Where(r => invoiceIds.Contains(r.InvoiceId)).ToListAsync(cancellationToken);
        saleHistory = saleHistory.Concat(invoices.Select(i => new CustomerSaleHistory(i.Id, i.BillNumber, i.InvoiceDate,
            string.Join("; ", i.Lines.Select(l => l.ItemDescription)), string.Join(", ", i.Lines.Select(l => l.SerialNumber).Where(s => s != null)),
            i.SubTotal, i.Discount, i.IsCancelled ? 0m : i.TotalAmount, i.AmountPaid, i.Balance, null,
            invoiceReceipts.Where(r => r.InvoiceId == i.Id).Select(r => new CustomerPaymentHistory(r.Id, r.Amount, r.PaymentMode, r.PaymentDate, r.ReferenceNumber, r.Note)).ToArray(), true)))
            .OrderByDescending(s => s.SaleDate).ToArray();
        customerRecord = customerRecord with { SalesCount = saleHistory.Length };
        var advances = await db.CustomerAdvances.AsNoTracking().Where(a => a.CustomerId == customer.Id).ToListAsync(cancellationToken);
        var advanceIds = advances.Select(a => a.Id).ToArray();
        var advanceRefunds = await db.CustomerAdvanceRefunds.Where(r => advanceIds.Contains(r.AdvanceId))
            .SumAsync(r => (decimal?)r.Amount, cancellationToken) ?? 0m;
        var correctionRefunds = await (from r in db.CorrectionRefunds join n in db.InvoiceCorrections on r.CorrectionId equals n.Id
            where n.Kind == "Sale" && n.PartyId == customer.Id select (decimal?)r.Amount).SumAsync(cancellationToken) ?? 0m;
        var netReceived = receipts.Sum(r => r.Payment.Amount) + invoiceReceipts.Where(r => r.AdvanceId == null).Sum(r => r.Amount)
            + advances.Sum(a => a.Amount) - advanceRefunds - correctionRefunds;
        return new CustomerHistory(customerRecord, saleHistory.Sum(sale => sale.TotalAmount),
            netReceived, saleHistory.Sum(sale => sale.Balance), saleHistory);
    }
}
