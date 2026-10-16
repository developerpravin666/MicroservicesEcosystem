using FluentValidation;

namespace AccountingInventory.Application.GeneralLedger.Commands.CreateContraVoucher;

public sealed class CreateContraVoucherCommandValidator : AbstractValidator<CreateContraVoucherCommand>
{
    public CreateContraVoucherCommandValidator()
    {
        RuleFor(x => x.VoucherDate).NotEmpty().WithMessage("Voucher date is required.");
        RuleFor(x => x.FromLedgerId).NotEmpty().WithMessage("Source ledger (From Account) is required.");
        RuleFor(x => x.ToLedgerId).NotEmpty().WithMessage("Destination ledger (To Account) is required.");
        RuleFor(x => x).Must(x => x.FromLedgerId != x.ToLedgerId)
            .WithMessage("Source and Destination ledgers must be different accounts.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Transfer amount must be greater than zero.");
        RuleFor(x => x.ReferenceNumber).MaximumLength(50);
        RuleFor(x => x.Narration).MaximumLength(500);
        RuleFor(x => x.InstrumentType).MaximumLength(50);
    }
}

