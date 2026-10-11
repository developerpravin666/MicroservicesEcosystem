using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Api.Endpoints;

public record ToggleFeatureFlagRequest(string Key, bool IsEnabled, Guid? TenantId = null);
public record ToggleAllFeatureFlagRequest(string? Key, bool IsEnabled, bool SyncAllTenants = true);
public record ResetTenantOverrideRequest(string Key, Guid TenantId);
public record SaveFeatureFlagRequest(string Key, string Name, string? Description, bool IsEnabled, string? Category, Guid? TenantId = null);

public record TenantFlagOverrideDto(Guid TenantId, string TenantName, string TenantSlug, bool IsEnabled, DateTimeOffset? ModifiedAt);
public record TenantSummaryDto(Guid Id, string Name, string Slug);

public record FeatureFlagDto(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    bool IsEnabled,
    string Category,
    Guid? TenantId,
    DateTimeOffset? ModifiedAt,
    bool? GlobalEnabled = null,
    int? OverridesCount = 0,
    List<TenantFlagOverrideDto>? Overrides = null);

public static class FeatureFlagEndpoints
{
    public static RouteGroupBuilder MapFeatureFlagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Feature Flags");

        // 1. GET /api/features: Client-side resolution (checks X-Tenant-Id header or ?tenantId=)
        group.MapGet("/features", async (ITenantDirectoryContext directory, HttpContext httpContext, CancellationToken ct) =>
        {
            Guid? tenantId = null;
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) &&
                Guid.TryParse(tenantHeader.ToString(), out var tid))
            {
                tenantId = tid;
            }
            else if (httpContext.Request.Query.TryGetValue("tenantId", out var qTid) &&
                Guid.TryParse(qTid.ToString(), out var parsedQ))
            {
                tenantId = parsedQ;
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
                    items.Add(new FeatureFlagDto(
                        flag.Id,
                        flag.Key,
                        flag.Name,
                        flag.Description,
                        flag.IsEnabled,
                        flag.Category,
                        flag.TenantId,
                        flag.ModifiedAt,
                        GlobalEnabled: flag.TenantId == null ? flag.IsEnabled : null));
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
                    null,
                    GlobalEnabled: true));
            }

            return Results.Ok(new { flags = dict, items });
        })
        .WithName("GetFeatureFlags")
        .AllowAnonymous();

        // 2. GET /api/features/{key}: Individual flag query
        group.MapGet("/features/{key}", async (string key, ITenantDirectoryContext directory, HttpContext httpContext, CancellationToken ct) =>
        {
            Guid? tenantId = null;
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) &&
                Guid.TryParse(tenantHeader.ToString(), out var tid))
            {
                tenantId = tid;
            }
            else if (httpContext.Request.Query.TryGetValue("tenantId", out var qTid) &&
                Guid.TryParse(qTid.ToString(), out var parsedQ))
            {
                tenantId = parsedQ;
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

        // 3. Admin endpoints
        var adminGroup = app.MapGroup("/api/admin/features").WithTags("Admin Feature Flags").AllowAnonymous();

        // GET /api/admin/features?tenantId=...
        adminGroup.MapGet("/", async (ITenantDirectoryContext directory, HttpContext httpContext, CancellationToken ct) =>
        {
            Guid? filterTenantId = null;
            if (httpContext.Request.Query.TryGetValue("tenantId", out var qTid) &&
                Guid.TryParse(qTid.ToString(), out var parsedQ))
            {
                filterTenantId = parsedQ;
            }

            var tenants = await directory.Tenants
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new TenantSummaryDto(t.Id, t.Name, t.Slug))
                .ToListAsync(ct);

            var tenantMap = tenants.ToDictionary(t => t.Id, t => t);

            var allFlags = await directory.FeatureFlags
                .AsNoTracking()
                .ToListAsync(ct);

            // Ensure baseline voice_typing flag exists globally
            if (!allFlags.Any(f => f.Key.Equals("voice_typing", StringComparison.OrdinalIgnoreCase) && f.TenantId == null))
            {
                var defaultVoice = FeatureFlag.Create(
                    "voice_typing",
                    "Voice Typing & Speech Dictation",
                    "Enables automatic speech-to-text dictation across input fields and form controls via Web Speech API",
                    isEnabled: true,
                    category: "Accessibility",
                    tenantId: null);

                directory.FeatureFlags.Add(defaultVoice);
                await directory.SaveChangesAsync(ct);
                allFlags.Add(defaultVoice);
            }

            var distinctKeys = allFlags.Select(f => f.Key).Distinct(StringComparer.OrdinalIgnoreCase);
            var items = new List<FeatureFlagDto>();
            var resolvedDict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in distinctKeys)
            {
                var globalFlag = allFlags.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && f.TenantId == null);
                var tenantOverrides = allFlags
                    .Where(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && f.TenantId != null)
                    .Select(f => new TenantFlagOverrideDto(
                        f.TenantId!.Value,
                        tenantMap.TryGetValue(f.TenantId.Value, out var t) ? t.Name : "Tenant " + f.TenantId.Value.ToString()[..8],
                        tenantMap.TryGetValue(f.TenantId.Value, out var ts) ? ts.Slug : "",
                        f.IsEnabled,
                        f.ModifiedAt))
                    .ToList();

                bool effectiveEnabled;
                DateTimeOffset? currentModifiedAt = globalFlag?.ModifiedAt;

                if (filterTenantId.HasValue)
                {
                    var matchOverride = tenantOverrides.FirstOrDefault(o => o.TenantId == filterTenantId.Value);
                    if (matchOverride != null)
                    {
                        effectiveEnabled = matchOverride.IsEnabled;
                        currentModifiedAt = matchOverride.ModifiedAt;
                    }
                    else
                    {
                        effectiveEnabled = globalFlag?.IsEnabled ?? true;
                    }
                }
                else
                {
                    effectiveEnabled = globalFlag?.IsEnabled ?? true;
                }

                resolvedDict[key] = effectiveEnabled;

                var sampleFlag = globalFlag ?? allFlags.First(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                items.Add(new FeatureFlagDto(
                    sampleFlag.Id,
                    sampleFlag.Key,
                    sampleFlag.Name,
                    sampleFlag.Description,
                    effectiveEnabled,
                    sampleFlag.Category,
                    filterTenantId,
                    currentModifiedAt,
                    GlobalEnabled: globalFlag?.IsEnabled ?? true,
                    OverridesCount: tenantOverrides.Count,
                    Overrides: tenantOverrides));
            }

            items = items.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();

            return Results.Ok(new
            {
                flags = resolvedDict,
                items,
                tenants,
                rawOverrides = allFlags.Where(f => f.TenantId != null).ToList()
            });
        })
        .WithName("AdminGetAllFeatureFlags");

        // POST /api/admin/features/toggle: Toggle for a particular tenant OR global default
        adminGroup.MapPost("/toggle", async (ToggleFeatureFlagRequest request, ITenantDirectoryContext directory, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Key))
            {
                return Results.BadRequest(new { message = "Feature flag key is required." });
            }

            var normalizedKey = request.Key.Trim().ToLowerInvariant();
            var existing = await directory.FeatureFlags
                .FirstOrDefaultAsync(f => f.Key == normalizedKey && f.TenantId == request.TenantId, ct);

            string targetScopeDescription;
            if (request.TenantId.HasValue)
            {
                var tenant = await directory.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TenantId.Value, ct);
                targetScopeDescription = tenant != null ? $"Tenant '{tenant.Name}'" : $"Tenant ID '{request.TenantId}'";
            }
            else
            {
                targetScopeDescription = "Global (All Tenants Default)";
            }

            if (existing != null)
            {
                existing.IsEnabled = request.IsEnabled;
                existing.ModifiedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                // Inherit name/description/category from global if creating a tenant override
                var globalTemplate = await directory.FeatureFlags
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Key == normalizedKey && f.TenantId == null, ct);

                existing = FeatureFlag.Create(
                    normalizedKey,
                    globalTemplate?.Name ?? (normalizedKey == "voice_typing" ? "Voice Typing & Speech Dictation" : normalizedKey),
                    globalTemplate?.Description ?? (normalizedKey == "voice_typing" ? "Enables automatic speech-to-text dictation across input fields and form controls via Web Speech API" : null),
                    request.IsEnabled,
                    category: globalTemplate?.Category ?? (normalizedKey == "voice_typing" ? "Accessibility" : "General"),
                    tenantId: request.TenantId);

                directory.FeatureFlags.Add(existing);
            }

            await directory.SaveChangesAsync(ct);
            return Results.Ok(new
            {
                success = true,
                flag = existing,
                message = $"Feature flag '{existing.Key}' set to {(existing.IsEnabled ? "Enabled" : "Disabled")} for {targetScopeDescription}."
            });
        })
        .WithName("AdminToggleFeatureFlag");

        // POST /api/admin/features/toggle-all: Toggle flag for ALL tenants at once
        adminGroup.MapPost("/toggle-all", async (ToggleAllFeatureFlagRequest request, ITenantDirectoryContext directory, CancellationToken ct) =>
        {
            var normalizedKey = request?.Key?.Trim().ToLowerInvariant();

            var query = directory.FeatureFlags.AsQueryable();
            if (!string.IsNullOrWhiteSpace(normalizedKey) && normalizedKey != "all")
            {
                query = query.Where(f => f.Key == normalizedKey);
            }

            var flagsToUpdate = await query.ToListAsync(ct);

            // If a specific flag key was targeted and no global row existed, create it
            if (!string.IsNullOrWhiteSpace(normalizedKey) && normalizedKey != "all" && !flagsToUpdate.Any(f => f.TenantId == null))
            {
                var globalFlag = FeatureFlag.Create(
                    normalizedKey,
                    normalizedKey == "voice_typing" ? "Voice Typing & Speech Dictation" : normalizedKey,
                    normalizedKey == "voice_typing" ? "Enables automatic speech-to-text dictation across input fields and form controls via Web Speech API" : null,
                    request.IsEnabled,
                    category: normalizedKey == "voice_typing" ? "Accessibility" : "General",
                    tenantId: null);

                directory.FeatureFlags.Add(globalFlag);
                flagsToUpdate.Add(globalFlag);
            }

            int count = 0;
            foreach (var flag in flagsToUpdate)
            {
                flag.IsEnabled = request.IsEnabled;
                flag.ModifiedAt = DateTimeOffset.UtcNow;
                count++;
            }

            await directory.SaveChangesAsync(ct);

            var keyName = !string.IsNullOrWhiteSpace(normalizedKey) && normalizedKey != "all" ? $"'{normalizedKey}'" : "ALL flags";
            return Results.Ok(new
            {
                success = true,
                updatedCount = count,
                message = $"Feature flag {keyName} has been set to {(request.IsEnabled ? "Enabled" : "Disabled")} across ALL tenants ({count} records updated)."
            });
        })
        .WithName("AdminToggleAllFeatureFlags");

        // POST /api/admin/features/reset-override: Remove tenant custom override so it inherits global default
        adminGroup.MapPost("/reset-override", async (ResetTenantOverrideRequest request, ITenantDirectoryContext directory, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Key))
            {
                return Results.BadRequest(new { message = "Key is required." });
            }

            var normalizedKey = request.Key.Trim().ToLowerInvariant();
            var existing = await directory.FeatureFlags
                .FirstOrDefaultAsync(f => f.Key == normalizedKey && f.TenantId == request.TenantId, ct);

            if (existing != null)
            {
                directory.FeatureFlags.Remove(existing);
                await directory.SaveChangesAsync(ct);
            }

            return Results.Ok(new
            {
                success = true,
                message = $"Tenant custom override for '{normalizedKey}' removed. Tenant will now inherit global default."
            });
        })
        .WithName("AdminResetTenantOverride");

        // POST /api/admin/features/save: Create or edit flag metadata
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
