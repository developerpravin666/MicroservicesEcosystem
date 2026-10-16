using FluentValidation;

namespace AccountingInventory.Application.Inventory.Godowns.Commands.CreateGodown;

public sealed class CreateGodownCommandValidator : AbstractValidator<CreateGodownCommand>
{
    public CreateGodownCommandValidator()
    {
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

