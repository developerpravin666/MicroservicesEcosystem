using FluentValidation;

namespace AccountingInventory.Application.Tenants.Commands.RegisterTenant;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(48)
            .Matches("^[a-zA-Z][a-zA-Z0-9-]*$")
            .WithMessage("Slug must start with a letter and contain only letters, digits, and hyphens.");
    }
}
