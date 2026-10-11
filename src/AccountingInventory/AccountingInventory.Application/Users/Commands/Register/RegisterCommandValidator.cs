using FluentValidation;
using AccountingInventory.Application.Users;

namespace AccountingInventory.Application.Users.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(64);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(PasswordPolicy.RequiredMessage)
            .MinimumLength(PasswordPolicy.MinimumLength).WithMessage(PasswordPolicy.MinimumLengthMessage)
            .Matches("[A-Z]").WithMessage(PasswordPolicy.UppercaseMessage)
            .Matches("[a-z]").WithMessage(PasswordPolicy.LowercaseMessage)
            .Matches("[0-9]").WithMessage(PasswordPolicy.DigitMessage);
    }
}
