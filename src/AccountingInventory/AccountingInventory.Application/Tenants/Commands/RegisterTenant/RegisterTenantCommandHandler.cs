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
    IRegistrationEmailTemplateStore templateStore,
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

        var values = new Dictionary<string, string> {
            ["StoreName"] = request.Name,
            ["StoreSlug"] = normalizedSlug,
            ["OwnerName"] = request.OwnerName ?? "Store Owner",
            ["OwnerEmail"] = request.OwnerEmail ?? "N/A",
            ["OwnerMobile"] = request.OwnerMobile ?? "N/A",
            ["Address"] = request.Address ?? "N/A",
            ["StateCode"] = request.StateCode ?? "N/A",
            ["Gstin"] = request.Gstin ?? "N/A",
            ["ApprovalsUrl"] = approvalsUrl
        };

        // 1. Send Thank You email to store owner
        if (!string.IsNullOrWhiteSpace(request.OwnerEmail))
        {
            var storeTemplate = await templateStore.GetAsync(RegistrationEmailTemplate.StoreRegistrationKey, cancellationToken);
            var thankYouSubject = RegistrationEmailTemplateRenderer.Render(storeTemplate.Subject, values, false);
            var thankYouBody = RegistrationEmailTemplateRenderer.Render(storeTemplate.HtmlBody, values, true);
            await emailSender.SendAsync(request.OwnerEmail, thankYouSubject, thankYouBody, cancellationToken);
        }

        // 2. Send Approval Request email to Platform Administrator
        var adminEmail = configuration["Email:AdminEmail"] ?? "admin@siddhi-mobile.local";
        var adminTemplate = await templateStore.GetAsync(RegistrationEmailTemplate.AdminApprovalRequestKey, cancellationToken);
        var adminSubject = RegistrationEmailTemplateRenderer.Render(adminTemplate.Subject, values, false);
        var adminBody = RegistrationEmailTemplateRenderer.Render(adminTemplate.HtmlBody, values, true);
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
