using FluentValidation;

namespace AccountingInventory.Application.Compliance.Rcm.Commands.CreateRcmSelfInvoice;

public sealed class CreateRcmSelfInvoiceCommandValidator : AbstractValidator<CreateRcmSelfInvoiceCommand>
{
    public CreateRcmSelfInvoiceCommandValidator()
    {
        RuleFor(x => x.VoucherDate)
            .NotEmpty().WithMessage("Voucher date is required.");

        RuleFor(x => x.VendorId)
            .NotEmpty().WithMessage("Vendor ID is required.");

        RuleFor(x => x.Section)
            .NotEmpty().WithMessage("RCM Section (e.g. 9(3) or 9(4)) is required.");

        RuleFor(x => x.ItemDescription)
            .NotEmpty().WithMessage("Item / Service description is required.")
            .MaximumLength(300).WithMessage("Item description cannot exceed 300 characters.");

        RuleFor(x => x.TaxableAmount)
            .GreaterThan(0).WithMessage("Taxable amount must be greater than zero.");

        RuleFor(x => x.GstRate)
            .InclusiveBetween(0, 28).WithMessage("GST rate must be between 0% and 28%.");

        RuleFor(x => x.SupplyType)
            .InclusiveBetween(0, 1).WithMessage("Supply type must be 0 (Intra-state) or 1 (Inter-state).");
    }
}

