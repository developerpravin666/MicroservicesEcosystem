using FluentValidation;

namespace AccountingInventory.Application.Vendors.Commands.AddVendor;

public sealed class AddVendorCommandValidator : AbstractValidator<AddVendorCommand>
{
    public AddVendorCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Mobile).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}
