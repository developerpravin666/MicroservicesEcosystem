using System.Net.Mail;
using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Security;

namespace AccountingInventory.Api.Endpoints;

public static class EmailEndpoints
{
    public static void MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
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
        //.RequireAuthorization("AuthenticatedUser")
        //.RequirePermission("Tenants.Manage")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status502BadGateway);
    }
}

public sealed record TestEmailRequest(string? To = "patidarpravin19@gmail.com");
