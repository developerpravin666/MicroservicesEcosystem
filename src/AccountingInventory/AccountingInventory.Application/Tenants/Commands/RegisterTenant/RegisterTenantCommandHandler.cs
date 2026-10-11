using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain.MultiTenancy;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Tenants.Commands.RegisterTenant;

/// <summary>
/// Tenant self-service registration handler.
/// Creates the tenant record in PendingApproval status and dispatches notification emails
/// to both the registrant (thank you note) and the platform administrator (approval prompt).
/// Physical database provisioning and seed data execution occur upon administrator approval.
/// </summary>
public sealed class RegisterTenantCommandHandler(
    ITenantDirectoryContext tenantDirectory,
    IEmailSender emailSender,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    ILogger<RegisterTenantCommandHandler> logger)
    : IRequestHandler<RegisterTenantCommand, RegisterTenantResult>
{
    public async Task<RegisterTenantResult> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var normalizedSlug = request.Slug.Trim().ToLowerInvariant();

        var slugTaken = await tenantDirectory.Tenants.AnyAsync(t => t.Slug == normalizedSlug, cancellationToken);
        if (slugTaken)
        {
            logger.LogWarning("Tenant registration rejected: slug '{Slug}' is already in use.", normalizedSlug);
            throw new ConflictException($"A tenant with slug '{normalizedSlug}' already exists. Please choose a different slug.");
        }

        string? initialPasswordHash = null;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var dummyUser = User.Create(
                request.OwnerName ?? "owner",
                request.OwnerMobile ?? "0000000000",
                request.OwnerEmail ?? $"{normalizedSlug}@store.local");
            initialPasswordHash = passwordHasher.HashPassword(dummyUser, request.Password);
        }

        var tenant = Tenant.Create(
            name: request.Name,
            slug: normalizedSlug,
            ownerName: request.OwnerName,
            ownerEmail: request.OwnerEmail,
            ownerMobile: request.OwnerMobile,
            initialPasswordHash: initialPasswordHash,
            stateCode: request.StateCode,
            gstin: request.Gstin,
            address: request.Address,
            requireApproval: true);

        tenantDirectory.Tenants.Add(tenant);
        await tenantDirectory.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Tenant {TenantId} ('{Name}', slug: '{Slug}') registered. Status: PendingApproval.",
            tenant.Id, tenant.Name, tenant.Slug);

        var baseUrl = (configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var approvalsUrl = $"{baseUrl}/settings/tenant-approvals";

        // 1. Send Thank You email to store owner
        if (!string.IsNullOrWhiteSpace(request.OwnerEmail))
        {
            var thankYouSubject = $"Registration Received · {request.Name}";
            var thankYouBody = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; line-height: 1.6;"">
    <h2 style=""color: #2563eb;"">Thank you for registering your store!</h2>
    <p>Hello <strong>{request.OwnerName ?? "Store Owner"}</strong>,</p>
    <p>We have received your registration for <strong>{request.Name}</strong>.</p>
    <p>Your workspace is currently under review by our administration. Once approved, your store database will be provisioned automatically and you will receive a confirmation email to start using the system.</p>
    <div style=""background: #f1f5f9; padding: 12px 16px; border-radius: 6px; margin: 16px 0;"">
        <p style=""margin: 4px 0;""><strong>Store Name:</strong> {request.Name}</p>
        <p style=""margin: 4px 0;""><strong>Store Slug:</strong> {normalizedSlug}</p>
        <p style=""margin: 4px 0;""><strong>Contact Email:</strong> {request.OwnerEmail}</p>
        <p style=""margin: 4px 0;""><strong>Mobile:</strong> {request.OwnerMobile ?? "N/A"}</p>
    </div>
    <p style=""color: #64748b; font-size: 13px;"">Siddhi Mobile Accounting & Inventory SaaS</p>
</div>";
            await emailSender.SendAsync(request.OwnerEmail, thankYouSubject, thankYouBody, cancellationToken);
        }

        // 2. Send Approval Request email to Platform Administrator
        var adminEmail = configuration["Email:AdminEmail"] ?? "admin@siddhi-mobile.local";
        var adminSubject = $"[Action Required] New Store Registration: {request.Name}";
        var adminBody = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; line-height: 1.6;"">
    <h2 style=""color: #0f172a;"">New Store Registration Pending Approval</h2>
    <p>A new store has registered and is waiting for your review and approval:</p>
    <div style=""background: #f8fafc; border: 1px solid #e2e8f0; padding: 14px; border-radius: 6px; margin: 16px 0;"">
        <p style=""margin: 4px 0;""><strong>Store Name:</strong> {request.Name}</p>
        <p style=""margin: 4px 0;""><strong>Store Slug:</strong> {normalizedSlug}</p>
        <p style=""margin: 4px 0;""><strong>Owner Name:</strong> {request.OwnerName ?? "N/A"}</p>
        <p style=""margin: 4px 0;""><strong>Owner Email:</strong> {request.OwnerEmail ?? "N/A"}</p>
        <p style=""margin: 4px 0;""><strong>Owner Mobile:</strong> {request.OwnerMobile ?? "N/A"}</p>
        <p style=""margin: 4px 0;""><strong>State / GSTIN:</strong> {request.StateCode ?? "N/A"} / {request.Gstin ?? "N/A"}</p>
    </div>
    <p style=""margin-top: 20px;"">
        <a href=""{approvalsUrl}"" style=""background: #2563eb; color: #ffffff; padding: 10px 18px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">Review in Admin Panel</a>
    </p>
</div>";
        await emailSender.SendAsync(adminEmail, adminSubject, adminBody, cancellationToken);

        return new RegisterTenantResult(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.SchemaName,
            tenant.Status.ToString(),
            "Registration submitted successfully! Your account is pending administrator review and approval.");
    }
}
