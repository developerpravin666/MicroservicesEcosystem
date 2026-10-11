using System.Text.RegularExpressions;

namespace BuildingBlocks.Domain;

/// <summary>
/// Validates a PostgreSQL schema name used for schema-per-tenant isolation. Lives in
/// this dependency-free shared kernel (rather than an infrastructure package) because
/// it's needed in TWO places that must never depend on each other: TenantService's
/// Domain layer (which DERIVES a schema name from a tenant's slug when the tenant is
/// created) and every tenant-scoped service's Infrastructure layer (which
/// DEFENSIVELY RE-VALIDATES a schema name before ever interpolating it into raw SQL —
/// e.g. `SET search_path` or `CREATE SCHEMA` — even though it always originates from
/// a trusted source, as a deliberate defense-in-depth measure against SQL injection).
///
/// Only lowercase ASCII letters, digits, and underscores are allowed, and the name
/// must start with a letter — this keeps every generated identifier safely inside
/// PostgreSQL's 63-byte unquoted-identifier limit and avoids any character that would
/// require special quoting/escaping.
/// </summary>
public static partial class TenantSchemaNameValidator
{
    private const int MaxLength = 63;

    [GeneratedRegex("^[a-z][a-z0-9_]{0,62}$")]
    private static partial Regex NamePattern();

    public static bool IsValid(string? schemaName)
        => !string.IsNullOrWhiteSpace(schemaName)
           && schemaName.Length <= MaxLength
           && NamePattern().IsMatch(schemaName);

    public static string EnsureValid(string? schemaName)
    {
        if (!IsValid(schemaName))
        {
            throw new DomainInvalidSchemaNameException(schemaName);
        }

        return schemaName!;
    }

    /// <summary>
    /// Derives a safe, guaranteed-unique schema name from a tenant's name and id
    /// (e.g. "Acme Corp" + 3f2a1b4c-... → "tenant_acme_corp_3f2a1b4c"). Used once, at
    /// tenant-creation time, by AccountingInventory. Including a slice of the tenant's id
    /// guarantees uniqueness even if two tenants normalize to the same name (e.g.
    /// "Acme" and "Acme!!!" both become "acme") — slug uniqueness is still enforced
    /// separately at the database level as a human-facing safeguard, but the schema
    /// name itself never depends on it being right.
    /// </summary>
    public static string BuildSchemaName(string name, Guid tenantId)
    {
        var normalized = SlugNormalizationPattern()
            .Replace(name.Trim().ToLowerInvariant(), "_")
            .Trim('_');

        var idSuffix = tenantId.ToString("N")[..8];

        // Reserve room for "tenant_" + "_" + the 8-char id suffix, then truncate the
        // name portion (never the id suffix) if the combination would exceed
        // PostgreSQL's 63-byte identifier limit — this keeps every schema name both
        // valid and unique regardless of how long the tenant's name is.
        const string prefix = "tenant_";
        var reserved = prefix.Length + 1 + idSuffix.Length;
        var maxNameLength = Math.Max(1, MaxLength - reserved);

        if (string.IsNullOrEmpty(normalized))
        {
            normalized = "tenant";
        }

        if (normalized.Length > maxNameLength)
        {
            normalized = normalized[..maxNameLength].TrimEnd('_');
        }

        return EnsureValid($"{prefix}{normalized}_{idSuffix}");
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugNormalizationPattern();
}

public sealed class DomainInvalidSchemaNameException(string? attemptedValue)
    : DomainException($"'{attemptedValue}' is not a valid tenant schema name.");
