using FluentValidation;

namespace AccountingInventory.Application.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleForEach(x => x.PermissionCodes).NotEmpty().MaximumLength(200);
    }
}
