using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.Security;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Api.Endpoints;

public static class AccountingP0Endpoints
{
    public static void MapAccountingP0Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting").RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>().WithMetadata(new RequiresTenantIdHeaderAttribute());
        group.MapGet("/access", async (ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetMyAccountingAccessQuery(), ct)));
        group.MapGet("/opening-customers", async (IAccountingInventoryDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Customers.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Id, x.Name }).ToArrayAsync(ct)));
        group.MapGet("/corrections", async (ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetInvoiceCorrectionsQuery(), ct)));
        group.MapPost("/corrections", async (ReturnInvoiceCommand q, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(q, ct)));
        group.MapPost("/corrections/{id:guid}/refunds", async (Guid id, RefundCorrectionCommand q, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(q with { CorrectionId = id }, ct)));
        group.MapGet("/corrections/{id:guid}/refunds", async (Guid id, IAccountingInventoryDbContext db, CancellationToken ct) =>
            Results.Ok(await db.CorrectionRefunds.AsNoTracking().Where(x => x.CorrectionId == id).OrderBy(x => x.PaymentDate)
                .Select(x => new { x.Id, x.PaymentDate, x.Amount, x.PaymentMode, x.Reference }).ToArrayAsync(ct)));
        group.MapGet("/snapshots/{kind}/{id:guid}", async (string kind, Guid id, IAccountingInventoryDbContext db, CancellationToken ct) =>
        {
            var snapshot = await db.InvoiceSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Kind == kind && x.SourceId == id, ct);
            return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
        });
        group.MapGet("/opening-items", async (ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetOpeningItemsQuery(), ct)));
        group.MapGet("/opening-stock", async (DateOnly cutover, IAccountingInventoryDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Products.AsNoTracking().Where(x => (x.IsActive || x.IsOpeningStock) && !x.IsSold && x.PurchaseDate <= cutover)
                .Select(x => new { x.Id, x.SerialNumber, Cost = x.PurchasePrice - x.Discount }).ToArrayAsync(ct)));
        group.MapPost("/opening-stock", async (ImportOpeningStockCommand q, ISender sender, CancellationToken ct) =>
            Results.Ok(new { id = await sender.Send(q, ct) }));
        group.MapPost("/opening-items/{id:guid}/settlements", async (Guid id, SettleOpeningItemCommand q, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(q with { Id = id }, ct)));
        group.MapGet("/reconciliation", async (DateOnly? asOf, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetControlReconciliationQuery(asOf), ct)));
        group.MapGet("/staff", async (ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetStaffQuery(), ct)));
        group.MapPost("/staff/invitations", async (InviteRequest q, ISender sender, CancellationToken ct) =>
            Results.Ok(new { id = await sender.Send(new InviteStaffCommand(q.UserName, q.Email, q.Mobile), ct) }));
        group.MapPut("/staff/{id:guid}/status", async (Guid id, StatusRequest q, ISender sender, CancellationToken ct) =>
        { await sender.Send(new SetStaffStatusCommand(id, q.Active), ct); return Results.NoContent(); });
        group.MapPost("/owner/transfer", async (TransferOwnerCommand q, ISender sender, CancellationToken ct) =>
        { await sender.Send(q, ct); return Results.NoContent(); });

        app.MapPost("/api/auth/accept-invitation", async (AcceptStaffInvitationCommand q, ISender sender, CancellationToken ct) =>
        { await sender.Send(q, ct); return Results.NoContent(); }).AllowAnonymous()
            .AddEndpointFilter(new TenantHeaderEndpointFilter(allowInvitationAcceptance: true))
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        app.MapPost("/api/tenants/{id:guid}/invite-owner", async (Guid id, InviteRequest q, ITenantDirectoryContext directory,
            ITenantContextAccessor accessor, IAccountingInventoryDbContext db, ISender sender, CancellationToken ct) =>
        {
            var tenant = await directory.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive
                && x.Status == AccountingInventory.Domain.Entities.TenantStatus.Active, ct);
            if (tenant is null) return Results.NotFound();
            accessor.SetTenant(id, tenant.SchemaName); await db.ResetConnectionAsync(ct);
            return Results.Ok(new { id = await sender.Send(new InviteStaffCommand(q.UserName, q.Email, q.Mobile, true), ct) });
        }).RequireAuthorization().RequirePermission("Tenants.Manage");
    }
    public sealed record InviteRequest(string UserName, string Email, string Mobile);
    public sealed record StatusRequest(bool Active);
}
