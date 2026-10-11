using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using AccountingInventory.Application.Sales.Invoices;
using AccountingInventory.Application.Inventory;
using AccountingInventory.Application.GeneralLedger;
using Microsoft.AspNetCore.Http;

internal static class TransactionChecks
{
    public static async Task RunAsync(string connectionString)
    {
        // A committed scratch schema is necessary for two independent sessions.
        // It is never a tenant schema and is removed in finally.
        var schema = "transaction_check_" + Guid.NewGuid().ToString("N");
        await using var control = new NpgsqlConnection(connectionString);
        await control.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", control))
            await create.ExecuteNonQueryAsync();
        try
        {
            var isolated = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
            var options = new DbContextOptionsBuilder<AccountingInventoryDbContext>().UseNpgsql(isolated)
                .UseSnakeCaseNamingConvention().Options;
            var provider = new ProbeTenant(schema);
            var owner = User.Create("owner", "9000000000", "owner@example.com");
            owner.SetOwner(true);
            owner.SetPasswordHash("test-only");
            await using (var setup = new AccountingInventoryDbContext(options, provider))
            {
                await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());
                setup.Users.Add(owner); await setup.SaveChangesAsync();
                await setup.Database.ExecuteSqlRawAsync("CREATE TABLE concurrency_balance (id integer PRIMARY KEY, paid numeric(18,2) NOT NULL); INSERT INTO concurrency_balance VALUES (1, 0)");
            }
            var arrivals = 0;
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Attempt(string code)
            {
                await using var db = new AccountingInventoryDbContext(options, provider);
                var behavior = new BusinessTransactionBehavior<BalanceProbeCommand, int>(db, new ProbeIdentity(owner.Id));
                try
                {
                    await behavior.Handle(new(), async cancellationToken =>
                    {
                        var paid = await db.Database.SqlQueryRaw<decimal>("SELECT paid AS \"Value\" FROM concurrency_balance WHERE id = 1").SingleAsync(cancellationToken);
                        if (paid + 80 > 100) throw new ConflictException("Overpayment");
                        if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
                        await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
                        db.ChartAccounts.Add(ChartAccount.Create(code, "Atomic write probe", LedgerAccountType.Asset, LedgerBalanceSide.Debit));
                        await db.SaveChangesAsync(cancellationToken);
                        await db.Database.ExecuteSqlRawAsync("UPDATE concurrency_balance SET paid = paid + 80 WHERE id = 1", cancellationToken);
                        return 1;
                    }, default);
                    return true;
                }
                catch (ConflictException) { return false; }
            }
            var outcomes = await Task.WhenAll(Attempt("PROBE-A"), Attempt("PROBE-B"));
            await using var verification = new AccountingInventoryDbContext(options, provider);
            var paid = await verification.Database.SqlQueryRaw<decimal>("SELECT paid AS \"Value\" FROM concurrency_balance WHERE id = 1").SingleAsync();
            if (outcomes.Count(success => success) != 1 || paid != 80 || await verification.ChartAccounts.CountAsync() != 1)
                throw new Exception("Serializable writes must reject a concurrent overpayment and roll back every write from the rejected request.");
            var unauthorized = new BusinessTransactionBehavior<BalanceProbeCommand, int>(verification, new ProbeIdentity(Guid.NewGuid()));
            var invoked = false;
            try
            {
                await unauthorized.Handle(new(), ct => { invoked = true; return Task.FromResult(1); }, default);
                throw new Exception("A staff member without a grant must be denied.");
            }
            catch (ForbiddenException) { }
            if (invoked) throw new Exception("Unauthorized handlers must not run.");
            var customer = Customer.Create("Retry customer", "8123456789", "Address", null);
            verification.Customers.Add(customer); await verification.SaveChangesAsync();
            await verification.Database.ExecuteSqlRawAsync("CREATE SEQUENCE retry_counter; CREATE FUNCTION generate_sales_bill_number(integer) RETURNS text LANGUAGE sql AS $$ SELECT 'RETRY-' || nextval('retry_counter')::text $$");
            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            http.HttpContext.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString();
            var command = new CreateSalesInvoiceCommand(customer.Name, customer.Mobile, customer.Address, null, new(2026,2,1), 0, null,
                [new(InvoiceItemType.Service, null, "Retry service", null, null, 1, 100)], new(10, "Cash", new(2026,2,1)));
            async Task<SalesInvoiceDetailsDto> Submit(CreateSalesInvoiceCommand request)
            {
                await using var retryDb = new AccountingInventoryDbContext(options, provider);
                return await new BusinessTransactionBehavior<CreateSalesInvoiceCommand, SalesInvoiceDetailsDto>(retryDb, new ProbeIdentity(owner.Id), http)
                    .Handle(request, ct => new CreateSalesInvoiceCommandHandler(retryDb).Handle(request, ct), default);
            }
            var first = await Submit(command); var repeated = await Submit(command);
            if (first.Invoice.Id != repeated.Invoice.Id || await verification.SalesInvoices.CountAsync() != 1
                || await verification.SalesInvoiceReceipts.CountAsync() != 1 || await verification.Set<BusinessRequest>().CountAsync() != 1)
                throw new Exception("A committed invoice/payment retry must return the saved result without receiving cash twice.");
            try {
                await Submit(command with { Notes = "Different payload" });
                throw new Exception("Reusing a retry key with different details must fail.");
            } catch (ConflictException) { }
            var deniedReplay = new BusinessTransactionBehavior<CreateSalesInvoiceCommand, SalesInvoiceDetailsDto>(verification, new ProbeIdentity(Guid.NewGuid()), http);
            try {
                await deniedReplay.Handle(command, ct => Task.FromResult(first), default);
                throw new Exception("Replay must still check current permissions.");
            } catch (ForbiddenException) { }
            var note = await new InvoiceCorrectionHandler(verification, new ProbeIdentity(owner.Id))
                .Handle(new ReturnInvoiceCommand("Sale", first.Invoice.Id, new(2026, 2, 2), "Retry refund fixture", "Restock"), default);
            http.HttpContext.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString();
            var refundRequest = new RefundCorrectionCommand(note.Id, new(2026, 2, 2), 10, "Cash", null);
            async Task<CorrectionRefundSummary> Refund(Guid actor)
            {
                await using var refundDb = new AccountingInventoryDbContext(options, provider);
                var identity = new ProbeIdentity(actor);
                return await new BusinessTransactionBehavior<RefundCorrectionCommand, CorrectionRefundSummary>(refundDb, identity, http)
                    .Handle(refundRequest, ct => new InvoiceCorrectionHandler(refundDb, identity).Handle(refundRequest, ct), default);
            }
            var refund = await Refund(owner.Id); var refundReplay = await Refund(owner.Id);
            if (refundReplay != refund || await verification.CorrectionRefunds.CountAsync() != 1)
                throw new Exception("Refund replay must preserve its response and post cash only once.");
            var staff = User.Create("no-refund-access", "9000000001", "staff@example.com");
            verification.Users.Add(staff); await verification.SaveChangesAsync();
            try {
                await Refund(staff.Id);
                throw new Exception("Refund replay requires the source-specific permission.");
            } catch (ForbiddenException) { }
            var stockSku = StockSku.Create("CONCURRENT", "Concurrency stock", "8504", "NOS");
            stockSku.Receive(1, 10, new(2026, 2, 3));
            verification.StockSkus.Add(stockSku); await verification.SaveChangesAsync();
            var stockArrivals = 0;
            var stockReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> ConsumeStock()
            {
                await using var stockDb = new AccountingInventoryDbContext(options, provider);
                var request = new WriteOffSkuStockCommand(stockSku.Id, new(2026, 2, 3), 1, "Concurrent stock issue");
                try {
                    await new BusinessTransactionBehavior<WriteOffSkuStockCommand, Guid>(stockDb, new ProbeIdentity(owner.Id))
                        .Handle(request, async ct => {
                            _ = await stockDb.StockSkus.SingleAsync(s => s.Id == stockSku.Id, ct);
                            if (Interlocked.Increment(ref stockArrivals) == 2) stockReady.TrySetResult();
                            await stockReady.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
                            return await new SkuInventoryHandler(stockDb).Handle(request, ct);
                        }, default);
                    return true;
                } catch (ConflictException) { return false; }
            }
            var stockOutcomes = await Task.WhenAll(ConsumeStock(), ConsumeStock());
            var stockBalance = await verification.StockSkus.AsNoTracking().SingleAsync(s => s.Id == stockSku.Id);
            if (stockOutcomes.Count(ok => ok) != 1 || stockBalance.Quantity != 0 || stockBalance.InventoryValue != 0
                || await verification.SkuMovements.CountAsync(m => m.SkuId == stockSku.Id) != 1)
                throw new Exception("Concurrent SKU deductions must consume stock once and roll back the rejected movement and journal.");
            Console.WriteLine("PASS: concurrent SKU deductions preserve stock and atomic movement posting.");
            Console.WriteLine("PASS: concurrent transaction conflict, complete rollback and pipeline authorization.");
            Console.WriteLine("PASS: committed invoice/receipt replay, payload mismatch rejection and replay authorization.");
            Console.WriteLine("PASS: refund replay response, single cash posting and source permission denial.");
        }
        finally
        {
            // schema is generated above from a fixed prefix and a GUID, never user input.
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", control);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
    private sealed record BalanceProbeCommand : IRequest<int>;
    private sealed class ProbeTenant(string schema) : ITenantProvider { public string SchemaName => schema; }
    private sealed class ProbeIdentity(Guid userId) : IRequestIdentity { public Guid? UserId => userId; }
}
