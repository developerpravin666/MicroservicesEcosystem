using FluentValidation;

namespace AccountingInventory.Application.Taxes.Commands.UpdateTax;

public sealed class UpdateTaxCommandValidator : AbstractValidator<UpdateTaxCommand>
{
    public UpdateTaxCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Cgst).InclusiveBetween(0, 100);
        RuleFor(x => x.Sgst).InclusiveBetween(0, 100);
    }
}
