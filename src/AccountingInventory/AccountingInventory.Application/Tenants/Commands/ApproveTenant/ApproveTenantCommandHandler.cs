using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain.MultiTenancy;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Tenants.Commands.ApproveTenant;

/// <summary>
/// Executes full tenant onboarding upon administrator approval:
///   1. Validates pending status.
///   2. Provisions a dedicated PostgreSQL schema (CREATE SCHEMA + EF Core migrations).
///   3. Seeds the standard Chart of Accounts (1000..5000 double-entry system accounts).
///   4. Seeds standard Indian GST tax rates (0%, 5%, 12%, 18%, 28%).
///   5. Seeds store invoicing configuration (CustomerBillSettings).
///   6. Creates and activates the store owner account with full accounting permissions.
///   7. Activates the tenant and dispatches an approval notification email with login details.
/// </summary>
public sealed class ApproveTenantCommandHandler(
    ITenantDirectoryContext tenantDirectory,
    IAccountingInventoryDbContext accInvDbContext,
    ITenantSchemaProvisioner schemaProvisioner,
    ITenantContextAccessor tenantContextAccessor,
    IEmailSender emailSender,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    ILogger<ApproveTenantCommandHandler> logger)
    : IRequestHandler<ApproveTenantCommand, ApproveTenantResult>
{
    public async Task<ApproveTenantResult> Handle(ApproveTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantDirectory.Tenants
            .SingleOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant with ID '{request.TenantId}' was not found.");

        if (tenant.Status != TenantStatus.PendingApproval && tenant.Status != TenantStatus.PendingProvisioning)
        {
            throw new ConflictException($"Tenant '{tenant.Name}' is in status '{tenant.Status}' and cannot be approved.");
        }

        logger.LogInformation(
            "Approving tenant {TenantId} ('{Name}'). Provisioning database schema '{SchemaName}'...",
            tenant.Id, tenant.Name, tenant.SchemaName);

        // 1. Physically provision the PostgreSQL schema and apply all EF Core migrations
        await schemaProvisioner.ProvisionAsync(tenant.Id, tenant.SchemaName, cancellationToken);

        // 2. Point tenant context to the new schema and reset connection
        tenantContextAccessor.SetTenant(tenant.Id, tenant.SchemaName);
        await accInvDbContext.ResetConnectionAsync(cancellationToken);

        // 3. Seed Chart of Accounts (1000 Cash, 1010 Bank, 1100 AR, 1200 Inventory, 2000 AP, 2100 GST, 3100 Equity, 4000 Sales, 5000 COGS)
        await LedgerPosting.EnsureSystemAccountsAsync(accInvDbContext, cancellationToken);

        // 4. Seed Standard Indian GST Tax Slabs (0%, 5%, 12%, 18%, 28%)
        if (!await accInvDbContext.Taxes.AnyAsync(cancellationToken))
        {
            accInvDbContext.Taxes.AddRange(
                Tax.Create(0m, 0m),       // 0% Nil Rated
                Tax.Create(2.5m, 2.5m),   // 5% GST
                Tax.Create(6m, 6m),       // 12% GST
                Tax.Create(9m, 9m),       // 18% GST (Standard retail electronics/telecom rate)
                Tax.Create(14m, 14m)      // 28% GST
            );
        }

        // 5. Seed Customer Bill / Invoice Template Settings
        if (!await accInvDbContext.CustomerBillSettings.AnyAsync(cancellationToken))
        {
            var billSettings = CustomerBillSettings.Create(
                companyName: tenant.Name,
                companyAddress: !string.IsNullOrWhiteSpace(tenant.Address) ? tenant.Address : "Store Counter",
                companyMobile: tenant.OwnerMobile ?? "",
                companyEmail: tenant.OwnerEmail,
                taxRegistrationNumber: tenant.Gstin,
                billTitle: "TAX INVOICE",
                footerNote: "Thank you for shopping with us! Terms: Goods once sold can be exchanged within 7 days with original invoice.",
                paperSize: "A4",
                showCustomerEmail: true,
                showSerialNumber: true,
                showDiscount: true,
                showPaymentHistory: true,
                showBalanceDue: true,
                stateCode: tenant.StateCode ?? "27",
                stateName: null);

            accInvDbContext.CustomerBillSettings.Add(billSettings);
        }

        // 6. Seed Owner User & Grant Administrator Accounting Permissions
        var ownerUsername = !string.IsNullOrWhiteSpace(tenant.OwnerName)
            ? tenant.OwnerName.Trim().ToLowerInvariant().Replace(" ", ".")
            : "admin";
        var ownerEmail = tenant.OwnerEmail ?? $"{tenant.Slug}@store.local";
        var ownerMobile = tenant.OwnerMobile ?? "9999999999";

        var existingOwner = await accInvDbContext.Users.FirstOrDefaultAsync(u => u.IsOwner, cancellationToken);
        if (existingOwner is null)
        {
            var owner = User.Create(ownerUsername, ownerMobile, ownerEmail);
            owner.SetOwner(true);
            owner.Activate();

            if (!string.IsNullOrWhiteSpace(tenant.InitialPasswordHash))
            {
                owner.SetPasswordHash(tenant.InitialPasswordHash);
                owner.AcceptInvitation();
            }
            else
            {
                var defaultHash = passwordHasher.HashPassword(owner, "Passw0rd!123");
                owner.SetPasswordHash(defaultHash);
                owner.AcceptInvitation();
            }

            accInvDbContext.Users.Add(owner);

            // Grant all built-in accounting permissions
            foreach (var code in AccountingPermissionGate.Codes)
            {
                accInvDbContext.AccountingUserPermissions.Add(
                    AccountingUserPermission.Grant(owner.Id, code, owner.Id));
            }
        }

        await accInvDbContext.SaveChangesAsync(cancellationToken);

        // 7. Mark tenant Active in shared registry
        tenant.Approve();
        await tenantDirectory.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Tenant {TenantId} ('{Name}') fully approved, migrated and seeded with owner user '{OwnerUsername}'.",
            tenant.Id, tenant.Name, ownerUsername);

        // 8. Send Approval confirmation email to store owner
        if (!string.IsNullOrWhiteSpace(tenant.OwnerEmail))
        {
            var baseUrl = (configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
            var loginUrl = $"{baseUrl}/login";
            var passwordNote = string.IsNullOrWhiteSpace(tenant.InitialPasswordHash)
                ? "Your initial temporary password is: <strong>Passw0rd!123</strong> (Please change it after first sign-in)."
                : "You can sign in using the password specified during registration.";

            var readySubject = $"Your Store Workspace is Approved & Ready! · {tenant.Name}";
            var readyBody = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; line-height: 1.6;"">
    <h2 style=""color: #16a34a;"">Congratulations! Your Store Workspace is Ready</h2>
    <p>Hello <strong>{tenant.OwnerName ?? "Store Owner"}</strong>,</p>
    <p>We are excited to let you know that your store <strong>{tenant.Name}</strong> has been approved! Your isolated cloud database, chart of accounts, and GST settings have been completely set up.</p>
    
    <div style=""background: #f8fafc; border: 1px solid #e2e8f0; padding: 14px; border-radius: 6px; margin: 16px 0;"">
        <h4 style=""margin-top: 0; color: #0f172a;"">Your Sign-In Credentials</h4>
        <p style=""margin: 4px 0;""><strong>Store Slug:</strong> <code style=""background: #e2e8f0; padding: 2px 6px; border-radius: 4px; font-weight: bold; color: #1e40af;"">{tenant.Slug}</code></p>
        <p style=""margin: 4px 0;""><strong>Username:</strong> <strong>{ownerUsername}</strong></p>
        <p style=""margin: 4px 0;"">{passwordNote}</p>
    </div>

    <p style=""margin-top: 24px;"">
        <a href=""{loginUrl}"" style=""background: #16a34a; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">Sign in to Your Store</a>
    </p>

    <hr style=""border: 0; border-top: 1px solid #e2e8f0; margin: 24px 0;"" />
    <p style=""color: #64748b; font-size: 13px;"">Siddhi Mobile Accounting & Inventory SaaS · Automated Onboarding</p>
</div>";
            await emailSender.SendAsync(tenant.OwnerEmail, readySubject, readyBody, cancellationToken);
        }

        return new ApproveTenantResult(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.SchemaName,
            tenant.Status.ToString(),
            ownerUsername,
            "Tenant approved! Database schema migrated, seed ledgers created, and owner account activated.");
    }
}

