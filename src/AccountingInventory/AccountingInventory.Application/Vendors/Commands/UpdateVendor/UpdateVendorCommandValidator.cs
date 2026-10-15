using FluentValidation;

namespace AccountingInventory.Application.Vendors.Commands.UpdateVendor;

public sealed class UpdateVendorCommandValidator : AbstractValidator<UpdateVendorCommand>
{
    public UpdateVendorCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Mobile).MaximumLength(20);
        RuleFor(x => x.Email).MaximumLength(150);
        RuleFor(x => x.GstNumber).MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}
