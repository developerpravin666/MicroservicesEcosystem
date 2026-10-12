using System.Net;
using System.Text.RegularExpressions;

namespace AccountingInventory.Application.Abstractions;

public static class RegistrationEmailTemplateRenderer
{
    public static readonly string[] Placeholders = [
        "StoreName", "StoreSlug", "OwnerName", "OwnerEmail", "OwnerMobile", "Address", "StateCode", "Gstin", "ApprovalsUrl"
    ];

    public static string Render(string template, IReadOnlyDictionary<string, string> values, bool html)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        return Regex.Replace(template, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            var entry = values.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            var value = entry.Value ?? "";
            return html ? WebUtility.HtmlEncode(value) : value.Replace('\r', ' ').Replace('\n', ' ');
        });
    }

    public static string? Validate(RegistrationEmailTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Subject) || template.Subject.Length > 500 || template.Subject.Contains('\r') || template.Subject.Contains('\n'))
            return "Enter a subject of at most 500 characters without line breaks.";
        if (string.IsNullOrWhiteSpace(template.HtmlBody) || template.HtmlBody.Length > 50000)
            return "Enter an HTML message of at most 50,000 characters.";

        foreach (Match match in Regex.Matches(template.Subject + template.HtmlBody, @"\{\{([^{}]*)\}\}"))
        {
            var name = match.Groups[1].Value.Trim();
            if (!Placeholders.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Unknown placeholder: {match.Value}. Supported placeholders: {string.Join(", ", Placeholders.Select(p => $"{{{{{p}}}}}"))}";
            }
        }

        return null;
    }
}
