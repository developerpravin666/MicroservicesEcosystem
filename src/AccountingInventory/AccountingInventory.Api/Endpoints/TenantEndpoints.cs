using BuildingBlocks.Security;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Tenants.Commands.ApproveTenant;
using AccountingInventory.Application.Tenants.Commands.ReactivateTenant;
using AccountingInventory.Application.Tenants.Commands.RegisterTenant;
using AccountingInventory.Application.Tenants.Commands.RejectTenant;
using AccountingInventory.Application.Tenants.Commands.SuspendTenant;
using AccountingInventory.Application.Tenants.Queries.GetPendingTenants;
using AccountingInventory.Application.Tenants.Queries.GetTenantBySlug;
using AccountingInventory.Application.Tenants.Queries.GetTenants;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountingInventory.Api.Endpoints;

public static class TenantEndpoints
{
    public static RouteGroupBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants").WithTags("Tenants");

        // Self-service tenant signup: registers store and sends confirmation + approval emails
        group.MapPost("/register", async (RegisterTenantCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/tenants/{result.TenantId}", result);
            })
            .WithName("RegisterTenant").AllowAnonymous()
            .Produces<RegisterTenantResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // List stores waiting for administrator approval (Supports initial offline cold-start bootstrapping)
        group.MapGet("/pending", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetPendingTenantsQuery(), ct)))
            .WithName("GetPendingTenants").AllowAnonymous()
            .Produces<IReadOnlyList<PendingTenantDto>>();

        // Administrator approval: triggers automatic schema creation, EF migrations & seed data
        group.MapPost("/{id:guid}/approve", async (Guid id, ApproveTenantRequest? request, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ApproveTenantCommand(id, request?.AssignedModules), ct)))
            .WithName("ApproveTenant").AllowAnonymous()
            .Produces<ApproveTenantResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Administrator rejection with reason
        group.MapPost("/{id:guid}/reject", async (Guid id, RejectTenantRequest request, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new RejectTenantCommand(id, request?.Reason), ct)))
            .WithName("RejectTenant").AllowAnonymous()
            .Produces<RejectTenantResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/by-slug/{slug}", async (string slug, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetTenantBySlugQuery(slug), ct)))
            .WithName("GetTenantBySlug").AllowAnonymous()
            .Produces<TenantSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        async Task<IResult> GetAllTenants(ITenantDirectoryContext directory, CancellationToken ct)
        {
            var tenants = await directory.Tenants
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new AdminTenantDto(
                    t.Id,
                    t.Name,
                    t.Slug,
                    t.SchemaName,
                    t.Status.ToString(),
                    t.OwnerName,
                    t.OwnerEmail,
                    t.OwnerMobile,
                    t.StateCode,
                    t.Gstin,
                    t.Address,
                    t.RejectionReason,
                    t.IsActive,
                    t.CreatedAt,
                    t.SubscribedModules))
                .ToListAsync(ct);
            return Results.Ok(tenants);
        }

        group.MapGet("", GetAllTenants).WithName("GetTenantsRoot").AllowAnonymous();
        group.MapGet("/all", GetAllTenants).WithName("GetAllTenants").AllowAnonymous().Produces<IReadOnlyList<AdminTenantDto>>();

        // Super Admin update assigned modules for any tenant
        group.MapPut("/{id:guid}/modules", async (Guid id, UpdateTenantModulesRequest request, ITenantDirectoryContext directory, CancellationToken ct) =>
        {
            var tenant = await directory.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (tenant == null)
            {
                return Results.NotFound(new { message = $"Tenant with ID '{id}' was not found." });
            }

            var modulesList = request.Modules ?? [];
            var normalizedModules = string.Join(",", modulesList.Select(m => m.Trim().ToLowerInvariant()));
            tenant.SetSubscribedModules(normalizedModules);

            var hasAccountingInventory = modulesList.Any(m => m.Equals("accounting_inventory", StringComparison.OrdinalIgnoreCase) || m.Equals("accounting", StringComparison.OrdinalIgnoreCase));
            var hasHrPayroll = modulesList.Any(m => m.Equals("hr_payroll", StringComparison.OrdinalIgnoreCase) || m.Equals("smart_hr", StringComparison.OrdinalIgnoreCase));

            var moduleFlagMap = new Dictionary<string, (string Name, string Description, string Category, bool Enabled)>(StringComparer.OrdinalIgnoreCase)
            {
                ["inventory"] = ("Inventory & Stock Management", "Enables products, serialized IMEI stock tracking, godowns, valuation, and purchases", "Inventory & POS", hasAccountingInventory),
                ["accounting"] = ("Double-Entry General Ledger", "Enables chart of accounts, journal vouchers, balance sheet, party statements, and GST returns", "Accounting", hasAccountingInventory),
                ["smart_hr"] = ("Smart HR & Employee Directory", "Enables employee master, workforce records, designations, departments, documents, and staff directory", "Human Resources", hasHrPayroll),
                ["leave_management"] = ("Leave Management & Approvals", "Enables leave balances, leave types, employee leave requests, and approval workflows", "Human Resources", hasHrPayroll),
                ["payroll"] = ("Smart Payroll & Compensation", "Enables salary structures, monthly payroll run batches, payslip generation, and disbursements", "Human Resources", hasHrPayroll),
            };

            var existingFlags = await directory.FeatureFlags.Where(f => f.TenantId == id).ToListAsync(ct);
            foreach (var (key, meta) in moduleFlagMap)
            {
                var existing = existingFlags.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.IsEnabled = meta.Enabled;
                    existing.ModifiedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    directory.FeatureFlags.Add(FeatureFlag.Create(
                        key,
                        meta.Name,
                        meta.Description,
                        isEnabled: meta.Enabled,
                        category: meta.Category,
                        tenantId: id));
                }
            }

            await directory.SaveChangesAsync(ct);
            return Results.Ok(new
            {
                success = true,
                subscribedModules = normalizedModules,
                message = $"Modules updated successfully for tenant '{tenant.Name}'."
            });
        })
        .WithName("UpdateTenantModules").AllowAnonymous();

        group.MapGet("/metrics", async (ITenantDirectoryContext directory, IConfiguration config, CancellationToken ct) =>
        {
            var total = await directory.Tenants.CountAsync(ct);
            var pending = await directory.Tenants.CountAsync(t => t.Status == TenantStatus.PendingApproval || t.Status == TenantStatus.PendingProvisioning, ct);
            var active = await directory.Tenants.CountAsync(t => t.Status == TenantStatus.Active && t.IsActive, ct);
            var suspended = await directory.Tenants.CountAsync(t => t.Status == TenantStatus.Suspended, ct);
            var rejected = await directory.Tenants.CountAsync(t => t.Status == TenantStatus.Rejected, ct);
            var isOffline = bool.TryParse(config["Deployment:OfflineMode"], out var off) && off ||
                            bool.TryParse(config["Email:OfflineMode"], out var eoff) && eoff;

            return Results.Ok(new TenantMetricsDto(total, pending, active, suspended, rejected, isOffline));
        })
        .WithName("GetTenantMetrics").AllowAnonymous()
        .Produces<TenantMetricsDto>();

        group.MapGet("/migrations/status", async (ITenantDirectoryContext directory, CancellationToken ct) =>
        {
            var activeSchemas = await directory.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive && !t.IsDeleted)
                .Select(t => new TenantSchemaStatusDto(t.Id, t.Name, t.Slug, t.SchemaName, t.Status.ToString(), true))
                .ToListAsync(ct);

            return Results.Ok(new MigrationStatusResult("tenant", activeSchemas, DateTimeOffset.UtcNow));
        })
        .WithName("GetMigrationStatus").AllowAnonymous()
        .Produces<MigrationStatusResult>();

        group.MapPost("/migrations/apply", async (IServiceProvider services, CancellationToken ct) =>
        {
            await services.ApplyTenantSchemaMigrationsAsync(ct);
            return Results.Ok(new { success = true, message = "Migrations successfully executed across all active tenant schemas." });
        })
        .WithName("ApplyMigrations").AllowAnonymous();

        group.MapPost("/{id:guid}/suspend", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new SuspendTenantCommand(id), ct)))
            .WithName("SuspendTenant").AllowAnonymous();

        group.MapPost("/{id:guid}/reactivate", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ReactivateTenantCommand(id), ct)))
            .WithName("ReactivateTenant").AllowAnonymous();

        group.MapGet("/system-settings", async (ITenantDirectoryContext directory, IConfiguration config, CancellationToken ct) =>
        {
            var total = await directory.Tenants.CountAsync(ct);
            var active = await directory.Tenants.CountAsync(t => t.Status == TenantStatus.Active && t.IsActive, ct);
            var pending = await directory.Tenants.CountAsync(t => t.Status == TenantStatus.PendingApproval || t.Status == TenantStatus.PendingProvisioning, ct);
            var suspended = await directory.Tenants.CountAsync(t => t.Status == TenantStatus.Suspended, ct);

            var isOffline = bool.TryParse(config["Deployment:OfflineMode"], out var off) && off ||
                            bool.TryParse(config["Email:OfflineMode"], out var eoff) && eoff;

            var connStr = config.GetConnectionString("AccountingInventoryDb")
                ?? throw new InvalidOperationException("ConnectionStrings:AccountingInventoryDb must be configured.");
            var dbBuilder = new NpgsqlConnectionStringBuilder(connStr);

            return Results.Ok(new AdminSystemSettingsDto(
                OfflineMode: isOffline,
                DatabaseProvider: "PostgreSQL (Npgsql)",
                DatabaseHost: string.IsNullOrWhiteSpace(dbBuilder.Host) ? "localhost" : dbBuilder.Host,
                DatabasePort: dbBuilder.Port > 0 ? dbBuilder.Port : 5432,
                DatabaseName: string.IsNullOrWhiteSpace(dbBuilder.Database) ? "accounting_inventory" : dbBuilder.Database,
                MasterSchema: "tenant",
                EmailProvider: isOffline ? "Simulated Console (Offline Mode)" : "SMTP",
                AdminEmail: config["Email:AdminEmail"] ?? "developer.pravin666@gmail.com",
                ProductOwnerUsername: config["ProductOwner:Username"] ?? "admin",
                TotalTenants: total,
                ActiveTenants: active,
                PendingApprovals: pending,
                SuspendedTenants: suspended,
                SystemVersion: "1.4.0",
                ServerTimeUtc: DateTimeOffset.UtcNow
            ));
        })
        .WithName("GetTenantSystemSettings").AllowAnonymous();

        return group;
    }
}

public sealed record RejectTenantRequest(string? Reason = null);
public sealed record ApproveTenantRequest(List<string>? AssignedModules = null);
public sealed record UpdateTenantModulesRequest(List<string>? Modules = null);

public sealed record AdminTenantDto(
    Guid Id,
    string Name,
    string Slug,
    string SchemaName,
    string Status,
    string? OwnerName,
    string? OwnerEmail,
    string? OwnerMobile,
    string? StateCode,
    string? Gstin,
    string? Address,
    string? RejectionReason,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? SubscribedModules = null);

public sealed record TenantMetricsDto(
    int TotalTenants,
    int PendingApprovals,
    int ActiveTenants,
    int SuspendedTenants,
    int RejectedTenants,
    bool OfflineMode);

public sealed record TenantSchemaStatusDto(
    Guid TenantId,
    string Name,
    string Slug,
    string SchemaName,
    string Status,
    bool IsMigrated);

public sealed record MigrationStatusResult(
    string MasterSchema,
    IReadOnlyList<TenantSchemaStatusDto> TenantSchemas,
    DateTimeOffset Timestamp);
