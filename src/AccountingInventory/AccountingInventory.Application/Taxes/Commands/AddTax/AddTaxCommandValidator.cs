using FluentValidation;

namespace AccountingInventory.Application.Taxes.Commands.AddTax;

public sealed class AddTaxCommandValidator : AbstractValidator<AddTaxCommand>
{
    public AddTaxCommandValidator()
    {
        RuleFor(x => x.Cgst).InclusiveBetween(0, 100);
        RuleFor(x => x.Sgst).InclusiveBetween(0, 100);
    }
}
