using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Tenants.Commands.RejectTenant;

public sealed class RejectTenantCommandHandler(
    ITenantDirectoryContext tenantDirectory,
    IEmailSender emailSender,
    ILogger<RejectTenantCommandHandler> logger)
    : IRequestHandler<RejectTenantCommand, RejectTenantResult>
{
    public async Task<RejectTenantResult> Handle(RejectTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantDirectory.Tenants
            .SingleOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant with ID '{request.TenantId}' was not found.");

        if (tenant.Status != TenantStatus.PendingApproval)
        {
            throw new ConflictException($"Tenant '{tenant.Name}' is in status '{tenant.Status}' and cannot be rejected.");
        }

        tenant.Reject(request.Reason);
        await tenantDirectory.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Tenant {TenantId} ('{Name}') was rejected. Reason: {Reason}",
            tenant.Id, tenant.Name, request.Reason ?? "No reason provided");

        if (!string.IsNullOrWhiteSpace(tenant.OwnerEmail))
        {
            var subject = $"Update Regarding Your Store Registration · {tenant.Name}";
            var body = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; line-height: 1.6;"">
    <h2 style=""color: #e11d48;"">Store Registration Update</h2>
    <p>Hello <strong>{tenant.OwnerName ?? "Store Owner"}</strong>,</p>
    <p>Thank you for your interest in Siddhi Mobile Accounting & Inventory. We have reviewed your registration for <strong>{tenant.Name}</strong>.</p>
    <p>Unfortunately, your store registration could not be approved at this time.</p>
    {(!string.IsNullOrWhiteSpace(request.Reason) ? $@"<div style=""background: #fff1f2; border: 1px solid #fecdd3; padding: 12px 16px; border-radius: 6px; margin: 16px 0;""><p style=""margin: 0; color: #9f1239;""><strong>Reason:</strong> {request.Reason}</p></div>" : "")}
    <p>If you believe this is in error or would like to submit corrected details, please contact platform administration.</p>
    <p style=""color: #64748b; font-size: 13px;"">Siddhi Mobile Accounting & Inventory SaaS</p>
</div>";
            await emailSender.SendAsync(tenant.OwnerEmail, subject, body, cancellationToken);
        }

        return new RejectTenantResult(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenant.RejectionReason,
            "Tenant registration was rejected.");
    }
}

