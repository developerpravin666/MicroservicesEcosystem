using FluentValidation;

namespace AccountingInventory.Application.Users.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.TenantSlug).MaximumLength(48);
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty();
    }
}
