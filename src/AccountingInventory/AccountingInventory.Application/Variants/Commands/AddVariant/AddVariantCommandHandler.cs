using System.Text.RegularExpressions;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Variants.Commands.AddVariant;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Variants.Commands.AddVariant;

/// <summary>
/// The entire variant add flow, with canonical deduplication:
/// </summary>
public sealed class AddVariantCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<AddVariantCommandHandler> logger)
    : IRequestHandler<AddVariantCommand, AddVariantResult>
{
    public static string NormalizeVariantName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Standard";
        var trimmed = name.Trim();
        if (string.Equals(trimmed, "standard", StringComparison.OrdinalIgnoreCase)) return "Standard";

        // RAM + Storage pattern: e.g. 8+128G, 8+128GB, 8/128, 8GB+128GB, (8+128G), 8 + 128 GB
        var matchRamStorage = Regex.Match(trimmed, @"^\(?\s*(\d+)\s*(?:gb|g)?\s*[\+/]\s*(\d+)\s*(?:gb|g)?\s*\)?$", RegexOptions.IgnoreCase);
        if (matchRamStorage.Success)
        {
            var ram = matchRamStorage.Groups[1].Value;
            var storage = matchRamStorage.Groups[2].Value;
            return $"{ram}+{storage}GB";
        }

        // Storage-only pattern: e.g. 128GB, 128G, 128 GB, 256GB, 512GB
        var matchStorage = Regex.Match(trimmed, @"^\(?\s*(\d+)\s*(?:gb|g)\s*\)?$", RegexOptions.IgnoreCase);
        if (matchStorage.Success)
        {
            var storage = matchStorage.Groups[1].Value;
            return $"{storage}GB";
        }

        var matchTb = Regex.Match(trimmed, @"^\(?\s*(\d+)\s*(?:tb|t)\s*\)?$", RegexOptions.IgnoreCase);
        if (matchTb.Success)
        {
            var tb = matchTb.Groups[1].Value;
            return $"{tb}TB";
        }

        return trimmed.Replace("(", "").Replace(")", "").Trim();
    }

    public async Task<AddVariantResult> Handle(AddVariantCommand request, CancellationToken cancellationToken)
    {
        var rawName = (request.Name ?? "").Trim();
        var canonicalName = NormalizeVariantName(rawName);
        var normalizedLower = canonicalName.ToLowerInvariant();
        var rawLower = rawName.ToLowerInvariant();

        // 1. Direct case-insensitive database check
        var existing = await accountingInventoryDbContext.Variants
            .FirstOrDefaultAsync(b => b.Name.ToLower() == normalizedLower || b.Name.ToLower() == rawLower, cancellationToken);

        if (existing != null)
        {
            logger.LogInformation("Variant '{Name}' already exists (ID: {VariantId}). Reusing existing record without duplicating.", existing.Name, existing.Id);
            return new AddVariantResult(existing.Id, existing.Name, existing.Description ?? "");
        }

        // 2. Canonical equivalence check against active variants
        var activeVariants = await accountingInventoryDbContext.Variants
            .AsNoTracking()
            .Select(b => new { b.Id, b.Name, b.Description })
            .ToListAsync(cancellationToken);

        var canonicalMatch = activeVariants.FirstOrDefault(b =>
            NormalizeVariantName(b.Name).Equals(canonicalName, StringComparison.OrdinalIgnoreCase) ||
            b.Name.Replace(" ", "").Replace("+", "").Replace("/", "").Equals(canonicalName.Replace(" ", "").Replace("+", "").Replace("/", ""), StringComparison.OrdinalIgnoreCase));

        if (canonicalMatch != null)
        {
            logger.LogInformation("Variant '{Name}' matches existing canonical variant '{ExistingName}' (ID: {VariantId}). Reusing existing record.", rawName, canonicalMatch.Name, canonicalMatch.Id);
            return new AddVariantResult(canonicalMatch.Id, canonicalMatch.Name, canonicalMatch.Description ?? "");
        }

        // 3. Insert newly discovered canonical variant
        var variant = Variant.Create(canonicalName, request.Description);
        variant.Activate();
        accountingInventoryDbContext.Variants.Add(variant);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Variant {VariantId} - ({Name}) added successfully.", variant.Id, variant.Name);
        return new AddVariantResult(variant.Id, variant.Name, variant.Description ?? "");
    }
}
