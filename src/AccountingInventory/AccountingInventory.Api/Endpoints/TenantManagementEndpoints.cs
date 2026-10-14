using System.Net.Mail;
using System.Text.RegularExpressions;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Users;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AccountingInventory.Api.Endpoints;

public static class TenantManagementEndpoints
{
    public static void MapTenantManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/tenants")
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser()
                .RequireRole("ProductOwner", "SuperAdmin")
                .RequireClaim(TenantClaimTypes.TenantId, Guid.Empty.ToString())
                .RequireClaim(PermissionClaimTypes.Permission, "Tenants.Manage"));
        group.MapPut("/{id:guid}/profile", async (Guid id, UpdateTenantProfileRequest request,
            ITenantAdministration administration, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            Required(request.Name, "name", 200, errors);
            Required(request.OwnerName, "ownerName", 150, errors);
            Required(request.OwnerEmail, "ownerEmail", 150, errors);
            Required(request.OwnerMobile, "ownerMobile", 20, errors);
            var email = request.OwnerEmail?.Trim() ?? "";
            if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
                errors["ownerEmail"] = ["Enter one valid owner email address."];
            if (!string.IsNullOrWhiteSpace(request.StateCode) && !Regex.IsMatch(request.StateCode.Trim(), @"^\d{2}$"))
                errors["stateCode"] = ["Enter a two-digit state code."];
            if (!string.IsNullOrWhiteSpace(request.Gstin) && !Regex.IsMatch(request.Gstin.Trim().ToUpperInvariant(), @"^\d{2}[A-Z]{5}\d{4}[A-Z][A-Z0-9]Z[A-Z0-9]$"))
                errors["gstin"] = ["Enter a valid 15-character GSTIN."];
            if (request.Address?.Trim().Length > 500) errors["address"] = ["Address must be 500 characters or fewer."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            await administration.UpdateProfileAsync(id, request, ct);
            return Results.Ok(new { message = "Tenant profile and owner login details updated." });
        }).WithName("UpdateTenantProfile");
        group.MapPost("/{id:guid}/owner-password", async (Guid id, ResetTenantPasswordRequest request,
            ITenantAdministration administration, CancellationToken ct) =>
        {
            var errors = PasswordPolicy.GetErrors(request.Password);
            if (errors.Count > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = errors.ToArray() });
            await administration.ResetOwnerPasswordAsync(id, request.Password, ct);
            return Results.Ok(new { message = "Owner password reset. Existing refresh tokens have been revoked." });
        }).WithName("ResetTenantOwnerPassword");
    }

    private static void Required(string? value, string key, int maximum, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors[key] = ["This field is required."];
        else if (value.Trim().Length > maximum) errors[key] = [$"Use {maximum} characters or fewer."];
    }
}
