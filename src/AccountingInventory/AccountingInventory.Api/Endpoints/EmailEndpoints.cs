using System.Net.Mail;
using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Security;

namespace AccountingInventory.Api.Endpoints;

public static class EmailEndpoints
{
    public static void MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/api/admin/email-templates")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireRole("ProductOwner", "SuperAdmin")
                .RequireClaim(TenantClaimTypes.TenantId, Guid.Empty.ToString()));

        adminGroup.MapGet("/", () => Results.Ok(new[]
        {
            new
            {
                key = RegistrationEmailTemplate.StoreRegistrationKey,
                name = "Store Welcome Email",
                description = "Sent to the store owner upon successful store registration.",
                recipientType = "Store Owner"
            },
            new
            {
                key = RegistrationEmailTemplate.AdminApprovalRequestKey,
                name = "Admin Approval Request Email",
                description = "Sent to the platform administrator when a new store is registered and pending review.",
                recipientType = "Platform Admin"
            }
        }));

        adminGroup.MapGet("/{templateKey}", async (string templateKey, IRegistrationEmailTemplateStore store, CancellationToken ct) =>
        {
            var normalizedKey = NormalizeTemplateKey(templateKey);
            var template = await store.GetAsync(normalizedKey, ct);
            var defaultTemplate = RegistrationEmailTemplate.GetDefault(normalizedKey);
            var isCustomized = template.Subject != defaultTemplate.Subject ||
                               template.HtmlBody != defaultTemplate.HtmlBody;

            return Results.Ok(new
            {
                key = normalizedKey,
                template.Subject,
                template.HtmlBody,
                defaultSubject = defaultTemplate.Subject,
                defaultHtmlBody = defaultTemplate.HtmlBody,
                isCustomized,
                placeholders = RegistrationEmailTemplateRenderer.Placeholders
            });
        });

        adminGroup.MapPut("/{templateKey}", async (string templateKey, RegistrationEmailTemplate template, IRegistrationEmailTemplateStore store, CancellationToken ct) =>
        {
            var normalizedKey = NormalizeTemplateKey(templateKey);
            var error = RegistrationEmailTemplateRenderer.Validate(template);
            if (error is not null) return Results.BadRequest(new { message = error });
            await store.SaveAsync(normalizedKey, template, ct);
            return Results.Ok(new { message = "Email template updated successfully." });
        });

        adminGroup.MapPost("/{templateKey}/preview", (string templateKey, RegistrationEmailTemplate template) =>
        {
            var error = RegistrationEmailTemplateRenderer.Validate(template);
            if (error is not null) return Results.BadRequest(new { message = error });

            var values = BuildSampleValues();
            return Results.Ok(new
            {
                subject = RegistrationEmailTemplateRenderer.Render(template.Subject, values, false),
                htmlBody = RegistrationEmailTemplateRenderer.Render(template.HtmlBody, values, true)
            });
        });

        adminGroup.MapPost("/{templateKey}/reset", async (string templateKey, IRegistrationEmailTemplateStore store, CancellationToken ct) =>
        {
            var normalizedKey = NormalizeTemplateKey(templateKey);
            await store.ResetAsync(normalizedKey, ct);
            return Results.Ok(new
            {
                message = "Template reset to default.",
                template = RegistrationEmailTemplate.GetDefault(normalizedKey)
            });
        });

        adminGroup.MapPost("/{templateKey}/test-send", async (string templateKey, SendTestTemplateEmailRequest request, IEmailSender email, CancellationToken ct) =>
        {
            var recipient = request.To?.Trim();
            if (string.IsNullOrWhiteSpace(recipient) || !MailAddress.TryCreate(recipient, out var address) || address.Address != recipient)
            {
                return Results.BadRequest(new { message = "Please enter a valid recipient email address." });
            }

            var values = BuildSampleValues(recipient, request.StoreName);
            var subject = RegistrationEmailTemplateRenderer.Render(request.Subject, values, false);
            var body = RegistrationEmailTemplateRenderer.Render(request.HtmlBody, values, true);

            await email.SendAsync(recipient, subject, body, ct);
            return Results.Ok(new { message = $"Test email dispatched to {recipient}." });
        });

        var templates = app.MapGroup("/api/admin/email-templates/store-registration")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireRole("ProductOwner", "SuperAdmin")
                .RequireClaim(TenantClaimTypes.TenantId, Guid.Empty.ToString()));

        templates.MapGet("/", async (IRegistrationEmailTemplateStore store, CancellationToken ct) =>
        {
            var template = await store.GetAsync(RegistrationEmailTemplate.StoreRegistrationKey, ct);
            var isCustomized = template.Subject != RegistrationEmailTemplate.DefaultStoreRegistration.Subject ||
                               template.HtmlBody != RegistrationEmailTemplate.DefaultStoreRegistration.HtmlBody;

            return Results.Ok(new
            {
                template.Subject,
                template.HtmlBody,
                defaultSubject = RegistrationEmailTemplate.DefaultStoreRegistration.Subject,
                defaultHtmlBody = RegistrationEmailTemplate.DefaultStoreRegistration.HtmlBody,
                isCustomized,
                placeholders = RegistrationEmailTemplateRenderer.Placeholders
            });
        });

        templates.MapPut("/", async (RegistrationEmailTemplate template, IRegistrationEmailTemplateStore store, CancellationToken ct) =>
        {
            var error = RegistrationEmailTemplateRenderer.Validate(template);
            if (error is not null) return Results.BadRequest(new { message = error });
            await store.SaveAsync(RegistrationEmailTemplate.StoreRegistrationKey, template, ct);
            return Results.Ok(new { message = "Registration welcome email template updated successfully." });
        });

        templates.MapPost("/preview", (RegistrationEmailTemplate template) =>
        {
            var error = RegistrationEmailTemplateRenderer.Validate(template);
            if (error is not null) return Results.BadRequest(new { message = error });

            var values = BuildSampleValues();
            return Results.Ok(new
            {
                subject = RegistrationEmailTemplateRenderer.Render(template.Subject, values, false),
                htmlBody = RegistrationEmailTemplateRenderer.Render(template.HtmlBody, values, true)
            });
        });

        templates.MapPost("/reset", async (IRegistrationEmailTemplateStore store, CancellationToken ct) =>
        {
            await store.ResetAsync(RegistrationEmailTemplate.StoreRegistrationKey, ct);
            return Results.Ok(new
            {
                message = "Template reset to default.",
                template = RegistrationEmailTemplate.DefaultStoreRegistration
            });
        });

        templates.MapPost("/test-send", async (SendTestTemplateEmailRequest request, IEmailSender email, CancellationToken ct) =>
        {
            var recipient = request.To?.Trim();
            if (string.IsNullOrWhiteSpace(recipient) || !MailAddress.TryCreate(recipient, out var address) || address.Address != recipient)
            {
                return Results.BadRequest(new { message = "Please enter a valid recipient email address." });
            }

            var values = BuildSampleValues(recipient, request.StoreName);
            var subject = RegistrationEmailTemplateRenderer.Render(request.Subject, values, false);
            var body = RegistrationEmailTemplateRenderer.Render(request.HtmlBody, values, true);

            await email.SendAsync(recipient, subject, body, ct);
            return Results.Ok(new { message = $"Test welcome email dispatched to {recipient}." });
        });

        app.MapPost("/api/email/test", async (TestEmailRequest request, IEmailSender email,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var recipient = request.To?.Trim();
            if (string.IsNullOrWhiteSpace(recipient) || recipient.Length > 254 ||
                recipient.Contains('\r') || recipient.Contains('\n') ||
                !MailAddress.TryCreate(recipient, out var address) || address.Address != recipient)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["to"] = ["Enter one valid recipient email address."]
                });

            try
            {
                var sent = await email.SendTestAsync(recipient, ct);
                return Results.Ok(new
                {
                    sent,
                    status = sent ? "smtpAccepted" : "offline",
                    message = sent ? "SMTP accepted the test email. Check the recipient inbox."
                        : "Offline mode or an unconfigured SMTP host prevented sending."
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                loggerFactory.CreateLogger("EmailTest").LogWarning(ex, "Test email failed via SMTP.");
                return Results.Problem("Test email could not be sent. Check the server logs and email settings.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithTags("Email")
        .WithName("SendTestEmail")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    private static string NormalizeTemplateKey(string? templateKey)
    {
        var trimmed = templateKey?.Trim().ToLowerInvariant().Replace('-', '_');
        return trimmed == RegistrationEmailTemplate.AdminApprovalRequestKey
            ? RegistrationEmailTemplate.AdminApprovalRequestKey
            : RegistrationEmailTemplate.StoreRegistrationKey;
    }

    private static Dictionary<string, string> BuildSampleValues(string? recipient = null, string? storeName = null) => new()
    {
        ["StoreName"] = string.IsNullOrWhiteSpace(storeName) ? "Siddhi Electronics & Mobile" : storeName.Trim(),
        ["StoreSlug"] = "siddhi-electronics",
        ["OwnerName"] = "Pravin Patidar",
        ["OwnerEmail"] = string.IsNullOrWhiteSpace(recipient) ? "storeowner@example.com" : recipient.Trim(),
        ["OwnerMobile"] = "+91 98765 43210",
        ["Address"] = "Shop #12, MG Road, Indore",
        ["StateCode"] = "23",
        ["Gstin"] = "23ABCDE1234F1Z5",
        ["ApprovalsUrl"] = "http://localhost:5173/settings/tenant-approvals"
    };
}

public sealed record SendTestTemplateEmailRequest(
    string To,
    string Subject,
    string HtmlBody,
    string? StoreName = null);

public sealed record TestEmailRequest(string? To = "patidarpravin19@gmail.com");
