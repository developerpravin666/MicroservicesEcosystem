namespace AccountingInventory.Application.Abstractions;

public sealed record RegistrationEmailTemplate(string Subject, string HtmlBody)
{
    public const string StoreRegistrationKey = "store_registration";
    public const string AdminApprovalRequestKey = "admin_approval_request";

    public static RegistrationEmailTemplate DefaultStoreRegistration => new("Registration Received · {{StoreName}}", """
<div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; line-height: 1.6; color: #334155;">
    <div style="background: #2563eb; padding: 20px; border-radius: 8px 8px 0 0; text-align: center;">
        <h2 style="color: #ffffff; margin: 0; font-size: 22px;">Thank you for registering your store!</h2>
    </div>
    <div style="border: 1px solid #e2e8f0; border-top: none; padding: 24px; border-radius: 0 0 8px 8px; background: #ffffff;">
        <p style="font-size: 15px; margin-top: 0;">Hello <strong>{{OwnerName}}</strong>,</p>
        <p style="font-size: 14px;">We have received your registration for <strong>{{StoreName}}</strong>.</p>
        <p style="font-size: 14px;">Your workspace is currently under review by our administration. Once approved, your store database will be provisioned automatically and you will receive a confirmation email to start using the system.</p>
        <div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 14px 18px; border-radius: 6px; margin: 20px 0;">
            <p style="margin: 4px 0; font-size: 13px;"><strong>Store Name:</strong> {{StoreName}}</p>
            <p style="margin: 4px 0; font-size: 13px;"><strong>Store Slug:</strong> {{StoreSlug}}</p>
            <p style="margin: 4px 0; font-size: 13px;"><strong>Contact Email:</strong> {{OwnerEmail}}</p>
            <p style="margin: 4px 0; font-size: 13px;"><strong>Mobile:</strong> {{OwnerMobile}}</p>
        </div>
        <p style="color: #64748b; font-size: 12px; margin-bottom: 0;">Siddhi Mobile Accounting & Inventory SaaS</p>
    </div>
</div>
""");

    public static RegistrationEmailTemplate DefaultAdminApprovalRequest => new("[Action Required] New Store Registration: {{StoreName}}", """
<div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; line-height: 1.6; color: #334155;">
    <div style="background: #0f172a; padding: 20px; border-radius: 8px 8px 0 0; text-align: center;">
        <h2 style="color: #ffffff; margin: 0; font-size: 20px;">New Store Registration Pending Approval</h2>
    </div>
    <div style="border: 1px solid #e2e8f0; border-top: none; padding: 24px; border-radius: 0 0 8px 8px; background: #ffffff;">
        <p style="font-size: 14px; margin-top: 0;">A new store has registered and is waiting for administrator review and approval:</p>
        <div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 14px 18px; border-radius: 6px; margin: 18px 0;">
            <p style="margin: 5px 0; font-size: 13px;"><strong>Store Name:</strong> {{StoreName}}</p>
            <p style="margin: 5px 0; font-size: 13px;"><strong>Store Slug:</strong> {{StoreSlug}}</p>
            <p style="margin: 5px 0; font-size: 13px;"><strong>Owner Name:</strong> {{OwnerName}}</p>
            <p style="margin: 5px 0; font-size: 13px;"><strong>Owner Email:</strong> {{OwnerEmail}}</p>
            <p style="margin: 5px 0; font-size: 13px;"><strong>Owner Mobile:</strong> {{OwnerMobile}}</p>
            <p style="margin: 5px 0; font-size: 13px;"><strong>State / GSTIN:</strong> {{StateCode}} / {{Gstin}}</p>
            <p style="margin: 5px 0; font-size: 13px;"><strong>Address:</strong> {{Address}}</p>
        </div>
        <p style="margin-top: 24px; margin-bottom: 8px;">
            <a href="{{ApprovalsUrl}}" style="background: #2563eb; color: #ffffff; padding: 11px 20px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block; font-size: 14px;">Review & Approve in Admin Panel</a>
        </p>
        <p style="color: #94a3b8; font-size: 12px; margin-top: 20px; margin-bottom: 0;">Platform Automated Notification · Siddhi SaaS Control Plane</p>
    </div>
</div>
""");

    public static RegistrationEmailTemplate Default => DefaultStoreRegistration;

    public static RegistrationEmailTemplate GetDefault(string key) =>
        key == AdminApprovalRequestKey || key == "admin-approval-request"
            ? DefaultAdminApprovalRequest
            : DefaultStoreRegistration;
}

public interface IRegistrationEmailTemplateStore
{
    Task<RegistrationEmailTemplate> GetAsync(CancellationToken ct);
    Task<RegistrationEmailTemplate> GetAsync(string key, CancellationToken ct);
    Task SaveAsync(RegistrationEmailTemplate template, CancellationToken ct);
    Task SaveAsync(string key, RegistrationEmailTemplate template, CancellationToken ct);
    Task ResetAsync(CancellationToken ct);
    Task ResetAsync(string key, CancellationToken ct);
}
