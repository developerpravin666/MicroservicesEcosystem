using FluentValidation;

namespace AccountingInventory.Application.Inventory.Uom.Commands.CreateUnitOfMeasure;

public sealed class CreateUnitOfMeasureCommandValidator : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Unit code is required.")
            .MaximumLength(20).WithMessage("Unit code cannot exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Unit name is required.")
            .MaximumLength(100).WithMessage("Unit name cannot exceed 100 characters.");

        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("Unit symbol is required.")
            .MaximumLength(20).WithMessage("Unit symbol cannot exceed 20 characters.");

        RuleFor(x => x.ConversionRate)
            .GreaterThan(0).When(x => !x.IsBaseUnit)
            .WithMessage("Conversion rate must be greater than zero for alternate units.");

        RuleFor(x => x.BaseUnitId)
            .NotEmpty().When(x => !x.IsBaseUnit)
            .WithMessage("Base unit ID is required for alternate units.");
    }
}

