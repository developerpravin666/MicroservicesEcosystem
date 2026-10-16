using FluentValidation;

namespace AccountingInventory.Application.Inventory.Godowns.Commands.UpdateGodown;

public sealed class UpdateGodownCommandValidator : AbstractValidator<UpdateGodownCommand>
{
    public UpdateGodownCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Godown ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Godown name is required.")
            .MaximumLength(100).WithMessage("Godown name cannot exceed 100 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Godown code is required.")
            .MaximumLength(20).WithMessage("Godown code cannot exceed 20 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Godown address cannot exceed 500 characters.");
    }
}

