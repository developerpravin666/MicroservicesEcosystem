using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccountingInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountingInventory.Infrastructure.Persistence;

// Balance/stock/period checks and all writes must observe one transaction.
// Serializable isolation rejects concurrent writes that would invalidate a check.
public sealed class BusinessTransactionBehavior<TRequest, TResponse>(AccountingInventoryDbContext db, IRequestIdentity identity, IHttpContextAccessor? http = null)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!typeof(TRequest).Name.EndsWith("Command", StringComparison.Ordinal)
            || typeof(TRequest).Namespace?.Contains(".Tenants.", StringComparison.Ordinal) == true
            || (typeof(TRequest).Namespace?.Contains(".Users.", StringComparison.Ordinal) == true
                && typeof(TRequest).Name != "RegisterCommand")
            || db.Database.CurrentTransaction is not null)
            return await next();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var requestNamespace = typeof(TRequest).Namespace ?? "";
            var permission = request is IBusinessPermissionRequest permissionRequest ? permissionRequest.BusinessPermission
                : request is RefundCorrectionCommand ? null
                : requestNamespace.Contains(".Purchases.", StringComparison.Ordinal) ? "purchases.manage"
                : requestNamespace.Contains(".Sales.", StringComparison.Ordinal) ? "sales.manage"
                : requestNamespace.Contains(".Inventory", StringComparison.Ordinal) ? "inventory.manage"
                : requestNamespace.Contains(".GeneralLedger", StringComparison.Ordinal) ? "accounting.manage"
                : requestNamespace.Contains(".Users.", StringComparison.Ordinal) ? null
                : "catalog.manage";
            if (!string.IsNullOrEmpty(permission))
                await AccountingPermissionGate.EnsureAsync(db, identity.UserId, permission, ct);
            if (request is RefundCorrectionCommand refundRequest)
            {
                var note = await db.InvoiceCorrections.AsNoTracking().SingleOrDefaultAsync(n => n.Id == refundRequest.CorrectionId, ct)
                    ?? throw new NotFoundException("Credit/debit note was not found.");
                await AccountingPermissionGate.EnsureAsync(db, identity.UserId, note.Kind == "Sale" ? "sales.manage" : "purchases.manage", ct);
            }
            // Only responses with stable DTO/primitive contracts are replayed.
            var replayable = typeof(TRequest).Name is "StageOpeningSkuStockCommand" or "CreateStockSkuCommand" or "ReceiveSkuStockCommand" or "WriteOffSkuStockCommand"
                || requestNamespace.Contains(".Sales.Invoices", StringComparison.Ordinal)
                || typeof(TRequest).Name is "RecordSalesReceiptCommand" or "RecordPurchasePaymentCommand" or "RefundCorrectionCommand" or "SettleOpeningItemCommand";
            var header = http?.HttpContext?.Request.Headers["Idempotency-Key"].ToString();
            string? key = null;
            string? hash = null;
            if (replayable && !string.IsNullOrWhiteSpace(header))
            {
                if (!Guid.TryParse(header, out var token)) throw new ConflictException("Idempotency-Key must be a UUID.");
                key = $"{identity.UserId}:{token}";
                hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(typeof(TRequest).FullName + JsonSerializer.Serialize(request))));
                var previous = await db.Set<BusinessRequest>().AsNoTracking().SingleOrDefaultAsync(r => r.RequestKey == key, ct);
                if (previous is not null)
                {
                    if (previous.RequestHash != hash) throw new ConflictException("This retry key was already used for different transaction details.");
                    var saved = JsonSerializer.Deserialize<TResponse>(previous.ResponseJson)!;
                    await transaction.CommitAsync(ct);
                    return saved;
                }
            }
            var response = await next();
            if (key is not null)
            {
                db.Set<BusinessRequest>().Add(new BusinessRequest { RequestKey = key, RequestHash = hash!, ResponseJson = JsonSerializer.Serialize(response) });
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return response;
        }
        catch (Exception exception) when (FindPostgres(exception) is { } postgres
            && postgres.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
                or PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw new ConflictException("The record or balance changed while this request was processed. Refresh and retry; no changes were saved.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static PostgresException? FindPostgres(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is PostgresException postgres) return postgres;
            exception = exception.InnerException;
        }
        return null;
    }
}
