using FluentValidation;

namespace AccountingInventory.Application.Roles.Commands.UpdateRole;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleForEach(x => x.PermissionCodes).NotEmpty().MaximumLength(200);
    }
}
