using FluentValidation;

namespace AccountingInventory.Application.Users.Commands.Refresh;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty()
            .WithMessage("AccessToken is required. Send the access token returned by the login endpoint.");
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
