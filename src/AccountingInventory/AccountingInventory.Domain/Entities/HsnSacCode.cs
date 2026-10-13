namespace AccountingInventory.Domain.Entities;

public static class HsnSacCode
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var code = value.Trim();
        if (code.Length is not (4 or 6 or 8) || code.Any(c => c < '0' || c > '9'))
            throw new ArgumentException("HSN/SAC must contain 4, 6 or 8 digits.");
        return code;
    }
}
