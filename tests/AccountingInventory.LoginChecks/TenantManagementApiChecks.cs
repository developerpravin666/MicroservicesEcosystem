using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AccountingInventory.Api.Endpoints;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Infrastructure.Security;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class TenantManagementApiChecks
{
    public static async Task RunAsync()
    {
        var options = new JwtOptions { Issuer = "management-check", Audience = "management-check",
            SigningKey = "management-check-only-signing-key-at-least-32-characters" };
        var tokens = new TokenService(options);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var administration = new TestAdministration();
        builder.Services.AddSingleton<ITenantAdministration>(administration);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
            jwt.TokenValidationParameters = new TokenValidationParameters {
                ValidIssuer = options.Issuer, ValidAudience = options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapTenantManagementEndpoints();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var id = Guid.NewGuid();
        var path = $"/api/admin/tenants/{id}/owner-password";
        async Task Expect(HttpStatusCode code, string? token, string password)
        {
            client.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.PostAsJsonAsync(path, new { password });
            if (response.StatusCode != code) throw new Exception($"Expected {code}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
        string Token(Guid tenant, string[] roles, string[] permissions) => tokens.GenerateTokenPair(
            Guid.NewGuid(), "actor", tenant, "public", roles, permissions).AccessToken;
        await Expect(HttpStatusCode.Unauthorized, null, "Valid@123456");
        await Expect(HttpStatusCode.Forbidden, Token(id, [], []), "Valid@123456");
        await Expect(HttpStatusCode.Forbidden, Token(id, ["ProductOwner"], ["Tenants.Manage"]), "Valid@123456");
        await Expect(HttpStatusCode.Forbidden, Token(Guid.Empty, ["ProductOwner"], []), "Valid@123456");
        var admin = Token(Guid.Empty, ["ProductOwner"], ["Tenants.Manage"]);
        await Expect(HttpStatusCode.BadRequest, admin, "short");
        if (administration.Resets != 0) throw new Exception("Rejected request changed a password.");
        await Expect(HttpStatusCode.OK, admin, "Valid@123456");
        if (administration.Resets != 1 || administration.LastId != id) throw new Exception("Wrong tenant password target.");
        using var invalidProfile = await client.PutAsJsonAsync($"/api/admin/tenants/{id}/profile",
            new UpdateTenantProfileRequest("", "Owner", "bad-email", "", "invalid", "bad", new string('x', 501)));
        if (invalidProfile.StatusCode != HttpStatusCode.BadRequest || administration.Updates != 0)
            throw new Exception("Invalid profile accepted.");
        using var validProfile = await client.PutAsJsonAsync($"/api/admin/tenants/{id}/profile",
            new UpdateTenantProfileRequest("Store", "Owner", "owner@example.com", "9000000001", "23", null, "Address"));
        if (validProfile.StatusCode != HttpStatusCode.OK || administration.Updates != 1)
            throw new Exception("Valid profile rejected.");
        await app.StopAsync();
        Console.WriteLine("PASS: tenant management HTTP authorization, target isolation, profile and password validation.");
    }
    private sealed class TestAdministration : ITenantAdministration
    {
        public int Resets; public int Updates; public Guid LastId;
        public Task UpdateProfileAsync(Guid id, UpdateTenantProfileRequest profile, CancellationToken ct)
        { Updates++; LastId = id; return Task.CompletedTask; }
        public Task ResetOwnerPasswordAsync(Guid id, string password, CancellationToken ct)
        { Resets++; LastId = id; return Task.CompletedTask; }
    }
}
