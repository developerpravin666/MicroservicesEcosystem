using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
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
            if (snapshot is not null) return Results.Ok(snapshot);
            if (kind == "Sale")
            {
                var invoice = await db.SalesInvoices.Include(i => i.Lines).AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct);
                if (invoice is not null)
                {
                    var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == invoice.CustomerId, ct);
                    var itemsSummary = string.Join("; ", invoice.Lines.Select(l => $"{l.ItemDescription} x{l.Quantity}"));
                    var serialsSummary = string.Join(", ", invoice.Lines.Where(l => !string.IsNullOrWhiteSpace(l.SerialNumber)).Select(l => l.SerialNumber!));
                    return Results.Ok(InvoiceSnapshot.Capture("Sale", invoice.Id, customer?.Name ?? "Customer", customer?.Mobile, customer?.Address, customer?.Email, itemsSummary, serialsSummary, "{}", reconstructed: true));
                }
                var sale = await db.SalesProducts.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
                if (sale is not null)
                {
                    var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == sale.CustomerId, ct);
                    return Results.Ok(InvoiceSnapshot.Capture("Sale", sale.Id, customer?.Name ?? "Customer", customer?.Mobile, customer?.Address, customer?.Email, sale.BillNumber, "", "{}", reconstructed: true));
                }
            }
            else if (kind == "Purchase")
            {
                var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
                if (product is not null)
                {
                    var vendor = await db.Vendors.AsNoTracking().SingleOrDefaultAsync(v => v.Id == product.VendorId, ct);
                    var model = await db.ProductModels.AsNoTracking().SingleOrDefaultAsync(m => m.Id == product.ProductModelId, ct);
                    var brand = await db.Brands.AsNoTracking().SingleOrDefaultAsync(b => b.Id == product.BrandId, ct);
                    var desc = $"{brand?.Name} {model?.Name}".Trim();
                    return Results.Ok(InvoiceSnapshot.Capture("Purchase", product.Id, vendor?.Name ?? "Vendor", vendor?.Mobile, vendor?.Address, vendor?.Email, desc, product.SerialNumber ?? "", "{}", reconstructed: true));
                }
                var skuMovement = await db.SkuMovements.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
                if (skuMovement is not null && skuMovement.VendorId.HasValue)
                {
                    var vendor = await db.Vendors.AsNoTracking().SingleOrDefaultAsync(v => v.Id == skuMovement.VendorId.Value, ct);
                    var sku = await db.StockSkus.AsNoTracking().SingleOrDefaultAsync(s => s.Id == skuMovement.SkuId, ct);
                    return Results.Ok(InvoiceSnapshot.Capture("Purchase", skuMovement.Id, vendor?.Name ?? "Vendor", vendor?.Mobile, vendor?.Address, vendor?.Email, sku?.Name ?? "Accessory / SKU", sku?.Code ?? "", "{}", reconstructed: true));
                }
            }
            return Results.NotFound();
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
