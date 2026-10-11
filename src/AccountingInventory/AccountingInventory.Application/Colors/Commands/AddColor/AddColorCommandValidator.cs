using FluentValidation;

namespace AccountingInventory.Application.Colors.Commands.AddColor;

public sealed class AddColorCommandValidator : AbstractValidator<AddColorCommand>
{
    public AddColorCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
    }
    
}
