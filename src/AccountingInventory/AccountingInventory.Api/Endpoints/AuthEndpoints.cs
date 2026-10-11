using System.Security.Claims;
using AccountingInventory.Application.Users.Commands.Login;
using AccountingInventory.Application.Users.Commands.Refresh;
using AccountingInventory.Application.Users.Commands.Register;
using AccountingInventory.Application.Users;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AccountingInventory.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", async (RegisterCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/auth/users/{result.UserId}", result);
            })
            .WithName("Register")
            .AllowAnonymous()
            // Registration has no access token yet, but still needs a tenant. This
            // filter resolves X-Tenant-Id to the registry-owned schema before the
            // MediatR handler resolves its tenant DbContext.
            .AddEndpointFilter(new TenantHeaderEndpointFilter(allowBootstrapRegistration: true))
            .WithMetadata(new RequiresTenantIdHeaderAttribute())
            .Produces<RegisterResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(command, ct)))
            .WithName("Login").AllowAnonymous()
            //.AddEndpointFilter<TenantHeaderEndpointFilter>()
            //.WithMetadata(new RequiresTenantIdHeaderAttribute())
            .Produces<LoginResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/admin-login", (AdminLoginRequest request, IConfiguration configuration,
            ITokenService tokenService) =>
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
        .WithName("AdminLogin")
        .AllowAnonymous()
        .Produces<AdminLoginResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", async (RefreshTokenCommand command, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(command, ct)))
            .WithName("RefreshToken").AllowAnonymous()
            .Produces<RefreshTokenResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, ITenantDirectoryContext directory,
            IAccountingInventoryDbContext db, ITenantContextAccessor tenantAccessor, ITokenService tokens,
            IEmailSender email, IConfiguration configuration, CancellationToken ct) =>
        {
            var slug = request.TenantSlug.Trim().ToLowerInvariant();
            var tenant = await directory.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Slug == slug, ct);
            if (tenant is not null && tenant.IsActive && tenant.Status == TenantStatus.Active)
            {
                tenantAccessor.SetTenant(tenant.Id, tenant.SchemaName);
                await db.ResetConnectionAsync(ct);
                var normalizedEmail = request.Email.Trim().ToLowerInvariant();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && u.IsActive, ct);
                if (user is not null)
                {
                    var token = tokens.CreatePasswordResetToken(user.Id, tenant.Id, TimeSpan.FromMinutes(30));
                    var baseUrl = (configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
                    var link = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(token)}";
                    await email.SendAsync(user.Email, "Reset your password",
                        $"<p>Use the link below to choose a new password. It expires in 30 minutes.</p><p><a href=\"{link}\">Reset password</a></p>", ct);
                }
            }
            // return Results.Ok(new { message = "If an account matches those details, a password reset link has been sent." });
            return Results.Ok(new { message = "If an account matches those details, a password reset link has been sent." });
        }).WithName("ForgotPassword").AllowAnonymous();

        group.MapPost("/reset-password", async (ResetPasswordRequest request, ITenantDirectoryContext directory,
            IAccountingInventoryDbContext db, ITenantContextAccessor tenantAccessor, ITokenService tokens,
            IPasswordHasher<User> passwordHasher, CancellationToken ct) =>
        {
            var principal = tokens.ValidatePasswordResetToken(request.Token);
            if (principal is null) return Results.BadRequest(new { message = "This password reset link is invalid or has expired." });
            var tenant = await directory.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == principal.TenantId, ct);
            if (tenant is null || !tenant.IsActive || tenant.Status != TenantStatus.Active)
                return Results.BadRequest(new { message = "This password reset link is invalid or has expired." });
            tenantAccessor.SetTenant(tenant.Id, tenant.SchemaName);
            await db.ResetConnectionAsync(ct);
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == principal.UserId && u.IsActive, ct);
            if (user is null) return Results.BadRequest(new { message = "This password reset link is invalid or has expired." });
            var passwordErrors = PasswordPolicy.GetErrors(request.Password);
            if (passwordErrors.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = passwordErrors.ToArray() });
            user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
            user.RevokeRefreshToken();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Your password has been changed. You can now sign in." });
        }).WithName("ResetPassword").AllowAnonymous();

        group.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext httpContext,
            IPasswordHasher<User> passwordHasher, CancellationToken ct) =>
        {
            if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();
            var db = httpContext.RequestServices.GetRequiredService<IAccountingInventoryDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null) return Results.Unauthorized();

            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
            if (verification == PasswordVerificationResult.Failed)
                return Results.BadRequest(new { message = "Current password is incorrect." });
            var passwordErrors = PasswordPolicy.GetErrors(request.NewPassword);
            if (passwordErrors.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["newPassword"] = passwordErrors.ToArray()
                });
            if (request.CurrentPassword == request.NewPassword)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["newPassword"] = ["Choose a password different from your current password."]
                });

            user.SetPasswordHash(passwordHasher.HashPassword(user, request.NewPassword));
            user.RevokeRefreshToken();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Your password has been changed. Please sign in again." });
        })
            .WithName("ChangePassword")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>();

        group.MapGet("/me", (HttpContext httpContext) =>
            {
                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var roles = httpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value);
                var permissions = httpContext.User.FindAll("permission").Select(c => c.Value);
                var tenantId = httpContext.User.FindFirstValue("tenant_id");

                return Results.Ok(new { userId, tenantId, roles, permissions });
            })
            .WithName("Me")
            .RequireAuthorization("AuthenticatedUser")
            // Unlike login/register/refresh, this is a tenant-scoped operation.
            // Resolve the schema from the tenant registry rather than trusting the
            // schema claim embedded in the access token.
            .AddEndpointFilter<TenantHeaderEndpointFilter>();

        return group;
    }

}

public sealed record ForgotPasswordRequest(string TenantSlug, string Email);
public sealed record ResetPasswordRequest(string Token, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AdminLoginRequest(string Username, string Password);
public sealed record AdminLoginResult(
    Guid UserId,
    Guid TenantId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string Username,
    string Email,
    bool IsProductOwner,
    string[] Roles,
    string[] Permissions);
