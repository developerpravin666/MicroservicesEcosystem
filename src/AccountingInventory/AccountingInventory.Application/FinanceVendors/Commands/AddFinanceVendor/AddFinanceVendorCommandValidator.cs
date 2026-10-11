using FluentValidation;

namespace AccountingInventory.Application.FinanceVendors.Commands.AddFinanceVendor;

public sealed class AddFinanceVendorCommandValidator : AbstractValidator<AddFinanceVendorCommand>
{
    public AddFinanceVendorCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.Mobile).MaximumLength(20);
        RuleFor(x => x.Email).MaximumLength(256);
        RuleFor(x => x.ContactName).MaximumLength(100);
        RuleFor(x => x.ContactMobile).MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
