using System.Text;
using BuildingBlocks.Domain.MultiTenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Security;

/// <summary>
/// Host-level security registration. Called EXACTLY ONCE per process — by whichever
/// Program.cs is acting as the actual host, whether that's a single standalone
/// service's Api project or the AllInOneHost combining several services together.
/// Individual service modules (AddIdentityModule, AddInventoryModule, ...) never call
/// this themselves; they only register their own business/persistence concerns, which
/// keeps this safe to combine without "authentication scheme already registered"
/// conflicts when several modules share one process.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPlatformSecurity(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextAccessor>(sp => sp.GetRequiredService<TenantContext>());

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

        services.AddSingleton(jwtOptions);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("AuthenticatedUser", p => p.RequireAuthenticatedUser());

        return services;
    }

    /// <summary>
    /// Wires the authentication → tenant-resolution → authorization middleware chain.
    /// Order matters: authentication must run first (populates HttpContext.User from
    /// the JWT), then tenant resolution (reads the "tenant_id"/"tenant_schema" claims
    /// into ITenantContext for the rest of the pipeline — including
    /// TenantSchemaConnectionInterceptor, which points every DB connection at the
    /// right tenant's PostgreSQL schema), then authorization (evaluates policies,
    /// including RequirePermission checks).
    /// </summary>
    public static WebApplication UsePlatformSecurity(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseMiddleware<TenantContextMiddleware>();
        app.UseAuthorization();

        return app;
    }
}
