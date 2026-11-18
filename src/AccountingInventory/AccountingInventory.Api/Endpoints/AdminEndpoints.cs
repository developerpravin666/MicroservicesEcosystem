using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Tenants.Commands.ApproveTenant;
using AccountingInventory.Application.Tenants.Commands.ReactivateTenant;
using AccountingInventory.Application.Tenants.Commands.RejectTenant;
using AccountingInventory.Application.Tenants.Commands.SuspendTenant;
using AccountingInventory.Application.Tenants.Queries.GetPendingTenants;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using BuildingBlocks.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountingInventory.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapTenantManagementEndpoints();
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        // 1. Admin Authentication (POST /api/admin/login)
        group.MapPost("/login", (AdminLoginRequest request, IConfiguration configuration, ITokenService tokenService) =>
        {
            var adminUsername = configuration["ProductOwner:Username"] ?? "admin";
            var adminEmail = configuration["Email:AdminEmail"] ?? "developer.pravin666@gmail.com";
            var adminPassword = configuration["ProductOwner:Password"] ?? "Admin@123456";

            var inputUsername = request.Username?.Trim() ?? "";
            var isUserMatch = string.Equals(inputUsername, adminUsername, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(inputUsername, adminEmail, StringComparison.OrdinalIgnoreCase);

            if (!isUserMatch || request.Password != adminPassword)
            {
                return Results.Problem(
                    title: "Invalid credentials",
                    detail: "Invalid Product Owner username/email or password.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var systemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000000");
            var roleNames = new[] { "ProductOwner", "SuperAdmin" };
            var permissionCodes = new[] { "Tenants.Manage", "System.Manage", "Database.Manage" };

            var pair = tokenService.GenerateTokenPair(
                systemUserId,
                adminUsername,
                systemTenantId,
                "tenant",
                roleNames,
                permissionCodes);

            return Results.Ok(new AdminLoginResult(
                systemUserId,
                systemTenantId,
                pair.AccessToken,
                pair.RefreshToken,
                pair.AccessTokenExpiresAtUtc,
                adminUsername,
                adminEmail,
                true,
                roleNames,
                permissionCodes
            ));
        })
        .WithName("AdminConsoleLogin").AllowAnonymous()
        .Produces<AdminLoginResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // 2. Platform KPI Metrics (GET /api/admin/metrics)
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
        .WithName("GetAdminMetrics").AllowAnonymous()
        .Produces<TenantMetricsDto>();

        // 3. Tenant Directory (GET /api/admin/tenants and /api/admin/tenants/all)
        async Task<IResult> GetAllTenantsHandler(ITenantDirectoryContext directory, CancellationToken ct)
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

        group.MapGet("/tenants", GetAllTenantsHandler).WithName("AdminGetTenants").AllowAnonymous();
        group.MapGet("/tenants/all", GetAllTenantsHandler).WithName("AdminGetAllTenants").AllowAnonymous();

        // 4. Pending Approval Queue (GET /api/admin/tenants/pending and /api/admin/pending)
        async Task<IResult> GetPendingTenantsHandler(ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetPendingTenantsQuery(), ct));

        group.MapGet("/tenants/pending", GetPendingTenantsHandler).WithName("AdminGetPendingTenants").AllowAnonymous();
        group.MapGet("/pending", GetPendingTenantsHandler).WithName("AdminGetPendingShort").AllowAnonymous();

        // 5. Approve Store (POST /api/admin/tenants/{id}/approve)
        group.MapPost("/tenants/{id:guid}/approve", async (Guid id, ApproveTenantRequest? request, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ApproveTenantCommand(id, request?.AssignedModules), ct)))
            .WithName("AdminApproveTenant").AllowAnonymous()
            .Produces<ApproveTenantResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // 5b. Update Assigned Modules (PUT /api/admin/tenants/{id}/modules)
        group.MapPut("/tenants/{id:guid}/modules", async (Guid id, UpdateTenantModulesRequest request, ITenantDirectoryContext directory, CancellationToken ct) =>
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
        .WithName("AdminUpdateTenantModules").AllowAnonymous();

        // 6. Reject Store (POST /api/admin/tenants/{id}/reject)
        group.MapPost("/tenants/{id:guid}/reject", async (Guid id, RejectTenantRequest request, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new RejectTenantCommand(id, request?.Reason), ct)))
            .WithName("AdminRejectTenant").AllowAnonymous()
            .Produces<RejectTenantResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // 7. Suspend Store (POST /api/admin/tenants/{id}/suspend)
        group.MapPost("/tenants/{id:guid}/suspend", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new SuspendTenantCommand(id), ct)))
            .WithName("AdminSuspendTenant").AllowAnonymous();

        // 8. Reactivate Store (POST /api/admin/tenants/{id}/reactivate)
        group.MapPost("/tenants/{id:guid}/reactivate", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ReactivateTenantCommand(id), ct)))
            .WithName("AdminReactivateTenant").AllowAnonymous();

        // 9. Database Migrations Status (GET /api/admin/database-migrations/status and /api/admin/migrations/status)
        async Task<IResult> GetMigrationStatusHandler(ITenantDirectoryContext directory, CancellationToken ct)
        {
            var activeSchemas = await directory.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive && !t.IsDeleted)
                .Select(t => new TenantSchemaStatusDto(t.Id, t.Name, t.Slug, t.SchemaName, t.Status.ToString(), true))
                .ToListAsync(ct);

            return Results.Ok(new MigrationStatusResult("tenant", activeSchemas, DateTimeOffset.UtcNow));
        }

        group.MapGet("/database-migrations/status", GetMigrationStatusHandler).WithName("AdminGetMigrationStatusFull").AllowAnonymous();
        group.MapGet("/migrations/status", GetMigrationStatusHandler).WithName("AdminGetMigrationStatusShort").AllowAnonymous();

        // 10. Database Migrations Apply (POST /api/admin/database-migrations/apply and /api/admin/migrations/apply)
        async Task<IResult> ApplyMigrationsHandler(IDatabaseMigrationCoordinator migrationCoordinator, CancellationToken ct)
        {
            var result = await migrationCoordinator.MigrateAllAsync(ct);
            return Results.Ok(result);
        }

        group.MapPost("/database-migrations/apply", ApplyMigrationsHandler).WithName("AdminApplyMigrationsFull").AllowAnonymous();
        group.MapPost("/migrations/apply", ApplyMigrationsHandler).WithName("AdminApplyMigrationsShort").AllowAnonymous();

        // 11. System Settings (GET /api/admin/system-settings and /api/admin/settings)
        async Task<IResult> GetSystemSettingsHandler(ITenantDirectoryContext directory, IConfiguration config, CancellationToken ct)
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

            var settings = new AdminSystemSettingsDto(
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
            );

            return Results.Ok(settings);
        }

        group.MapGet("/system-settings", GetSystemSettingsHandler).WithName("AdminGetSystemSettingsFull").AllowAnonymous();
        group.MapGet("/settings", GetSystemSettingsHandler).WithName("AdminGetSystemSettingsShort").AllowAnonymous();

        return group;
    }
}


public sealed record AdminSystemSettingsDto(
    bool OfflineMode,
    string DatabaseProvider,
    string DatabaseHost,
    int DatabasePort,
    string DatabaseName,
    string MasterSchema,
    string EmailProvider,
    string AdminEmail,
    string ProductOwnerUsername,
    int TotalTenants,
    int ActiveTenants,
    int PendingApprovals,
    int SuspendedTenants,
    string SystemVersion,
    DateTimeOffset ServerTimeUtc);

