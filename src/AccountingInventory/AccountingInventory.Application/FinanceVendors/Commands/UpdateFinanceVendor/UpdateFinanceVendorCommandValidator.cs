using FluentValidation;

namespace AccountingInventory.Application.FinanceVendors.Commands.UpdateFinanceVendor;

public sealed class UpdateFinanceVendorCommandValidator : AbstractValidator<UpdateFinanceVendorCommand>
{
    public UpdateFinanceVendorCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(64);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.Mobile).MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256);
        RuleFor(x => x.ContactName).MaximumLength(100);
        RuleFor(x => x.ContactMobile).MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
