namespace AccountingInventory.Application.Users;

/// <summary>Shared password requirements used by registration and password updates.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const string RequiredMessage = "Password is required.";
    public const string MinimumLengthMessage = "Password must be at least 8 characters long.";
    public const string UppercaseMessage = "Password must contain at least one uppercase letter.";
    public const string LowercaseMessage = "Password must contain at least one lowercase letter.";
    public const string DigitMessage = "Password must contain at least one digit.";

    public static IReadOnlyList<string> GetErrors(string? password)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password)) errors.Add(RequiredMessage);
        password ??= string.Empty;

        if (password.Length < MinimumLength) errors.Add(MinimumLengthMessage);
        if (!password.Any(c => c is >= 'A' and <= 'Z')) errors.Add(UppercaseMessage);
        if (!password.Any(c => c is >= 'a' and <= 'z')) errors.Add(LowercaseMessage);
        if (!password.Any(c => c is >= '0' and <= '9')) errors.Add(DigitMessage);
        return errors;
    }
}
