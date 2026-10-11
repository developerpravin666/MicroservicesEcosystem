using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Api.Endpoints;

public record ToggleFeatureFlagRequest(string Key, bool IsEnabled, Guid? TenantId = null);
public record SaveFeatureFlagRequest(string Key, string Name, string? Description, bool IsEnabled, string? Category, Guid? TenantId = null);
public record FeatureFlagDto(Guid Id, string Key, string Name, string? Description, bool IsEnabled, string Category, Guid? TenantId, DateTimeOffset? ModifiedAt);

public static class FeatureFlagEndpoints
{
    public static RouteGroupBuilder MapFeatureFlagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Feature Flags");

        // 1. GET /api/features or /api/feature-flags: Returns active feature flags
        group.MapGet("/features", async (ITenantDirectoryContext directory, HttpContext httpContext, CancellationToken ct) =>
        {
            Guid? tenantId = null;
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) &&
                Guid.TryParse(tenantHeader.ToString(), out var tid))
            {
                tenantId = tid;
            }

            var allFlags = await directory.FeatureFlags
                .AsNoTracking()
                .Where(f => f.TenantId == null || f.TenantId == tenantId)
                .ToListAsync(ct);

            var dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var items = new List<FeatureFlagDto>();

            var keys = allFlags.Select(f => f.Key).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            {
                var flag = allFlags.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && f.TenantId == tenantId)
                           ?? allFlags.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && f.TenantId == null);

                if (flag != null)
                {
                    dict[flag.Key] = flag.IsEnabled;
                    items.Add(new FeatureFlagDto(flag.Id, flag.Key, flag.Name, flag.Description, flag.IsEnabled, flag.Category, flag.TenantId, flag.ModifiedAt));
                }
            }

            // Guarantee voice_typing flag is present
            if (!dict.ContainsKey("voice_typing"))
            {
                dict["voice_typing"] = true;
                items.Add(new FeatureFlagDto(
                    Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                    "voice_typing",
                    "Voice Typing & Speech Dictation",
                    "Enables automatic speech-to-text dictation across input fields and form controls via Web Speech API",
                    true,
                    "Accessibility",
                    null,
                    null));
            }

            return Results.Ok(new { flags = dict, items });
        })
        .WithName("GetFeatureFlags")
        .AllowAnonymous();

        group.MapGet("/features/{key}", async (string key, ITenantDirectoryContext directory, HttpContext httpContext, CancellationToken ct) =>
        {
            Guid? tenantId = null;
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) &&
                Guid.TryParse(tenantHeader.ToString(), out var tid))
            {
                tenantId = tid;
            }

            var normalizedKey = key.Trim().ToLowerInvariant();
            var flag = await directory.FeatureFlags
                .AsNoTracking()
                .Where(f => f.Key == normalizedKey && (f.TenantId == tenantId || f.TenantId == null))
                .OrderByDescending(f => f.TenantId.HasValue)
                .FirstOrDefaultAsync(ct);

            var isEnabled = flag?.IsEnabled ?? true;
            return Results.Ok(new { key = normalizedKey, isEnabled, flag });
        })
        .WithName("GetFeatureFlagByKey")
        .AllowAnonymous();

        // 2. Admin endpoints: GET /api/admin/features and POST /api/admin/features/toggle
        var adminGroup = app.MapGroup("/api/admin/features").WithTags("Admin Feature Flags").AllowAnonymous();

        adminGroup.MapGet("/", async (ITenantDirectoryContext directory, CancellationToken ct) =>
        {
            var flags = await directory.FeatureFlags
                .AsNoTracking()
                .OrderBy(f => f.Category)
                .ThenBy(f => f.Name)
                .ToListAsync(ct);

            if (!flags.Any(f => f.Key.Equals("voice_typing", StringComparison.OrdinalIgnoreCase)))
            {
                var defaultVoice = FeatureFlag.Create(
                    "voice_typing",
                    "Voice Typing & Speech Dictation",
                    "Enables automatic speech-to-text dictation across input fields and form controls via Web Speech API",
                    isEnabled: true,
                    category: "Accessibility");

                directory.FeatureFlags.Add(defaultVoice);
                await directory.SaveChangesAsync(ct);
                flags.Add(defaultVoice);
            }

            var dict = flags.ToDictionary(f => f.Key, f => f.IsEnabled, StringComparer.OrdinalIgnoreCase);
            return Results.Ok(new { flags = dict, items = flags });
        })
        .WithName("AdminGetAllFeatureFlags");

        adminGroup.MapPost("/toggle", async (ToggleFeatureFlagRequest request, ITenantDirectoryContext directory, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Key))
            {
                return Results.BadRequest(new { message = "Feature flag key is required." });
            }

            var normalizedKey = request.Key.Trim().ToLowerInvariant();
            var existing = await directory.FeatureFlags
                .FirstOrDefaultAsync(f => f.Key == normalizedKey && f.TenantId == request.TenantId, ct);

            if (existing != null)
            {
                existing.IsEnabled = request.IsEnabled;
                existing.ModifiedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                existing = FeatureFlag.Create(
                    normalizedKey,
                    normalizedKey == "voice_typing" ? "Voice Typing & Speech Dictation" : normalizedKey,
                    normalizedKey == "voice_typing" ? "Enables automatic speech-to-text dictation across input fields and form controls via Web Speech API" : null,
                    request.IsEnabled,
                    category: normalizedKey == "voice_typing" ? "Accessibility" : "General",
                    tenantId: request.TenantId);

                directory.FeatureFlags.Add(existing);
            }

            await directory.SaveChangesAsync(ct);
            return Results.Ok(new { success = true, flag = existing, message = $"Feature flag '{existing.Key}' set to {(existing.IsEnabled ? "Enabled" : "Disabled")} in database." });
        })
        .WithName("AdminToggleFeatureFlag");

        adminGroup.MapPost("/save", async (SaveFeatureFlagRequest request, ITenantDirectoryContext directory, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Key) || string.IsNullOrWhiteSpace(request?.Name))
            {
                return Results.BadRequest(new { message = "Key and Name are required." });
            }

            var normalizedKey = request.Key.Trim().ToLowerInvariant();
            var existing = await directory.FeatureFlags
                .FirstOrDefaultAsync(f => f.Key == normalizedKey && f.TenantId == request.TenantId, ct);

            if (existing != null)
            {
                existing.Name = request.Name.Trim();
                existing.Description = request.Description?.Trim();
                existing.Category = !string.IsNullOrWhiteSpace(request.Category) ? request.Category.Trim() : existing.Category;
                existing.IsEnabled = request.IsEnabled;
                existing.ModifiedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                existing = FeatureFlag.Create(
                    normalizedKey,
                    request.Name,
                    request.Description,
                    request.IsEnabled,
                    category: !string.IsNullOrWhiteSpace(request.Category) ? request.Category.Trim() : "General",
                    tenantId: request.TenantId);

                directory.FeatureFlags.Add(existing);
            }

            await directory.SaveChangesAsync(ct);
            return Results.Ok(new { success = true, flag = existing });
        })
        .WithName("AdminSaveFeatureFlag");

        return group;
    }
}

