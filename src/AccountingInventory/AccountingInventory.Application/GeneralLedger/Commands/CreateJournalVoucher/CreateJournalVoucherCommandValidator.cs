using FluentValidation;

namespace AccountingInventory.Application.GeneralLedger.Commands.CreateJournalVoucher;

public sealed class CreateJournalVoucherCommandValidator : AbstractValidator<CreateJournalVoucherCommand>
{
    public CreateJournalVoucherCommandValidator()
    {
        RuleFor(x => x.VoucherDate).NotEmpty().WithMessage("Voucher date is required.");
        RuleFor(x => x.Narration).NotEmpty().MaximumLength(500).WithMessage("Voucher narration is required.");
        RuleFor(x => x.ReferenceNumber).MaximumLength(100);
        RuleFor(x => x.Lines).NotNull().Must(lines => lines.Count >= 2)
            .WithMessage("A journal voucher requires at least two lines.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.LedgerId).NotEmpty().WithMessage("Ledger account is required.");
            line.RuleFor(l => l.DebitAmount).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.CreditAmount).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.LineNarration).MaximumLength(250);
        });
        RuleFor(x => x).Must(x => x.Lines != null && x.Lines.All(l =>
            (decimal.Round(l.DebitAmount, 2, MidpointRounding.AwayFromZero) > 0) !=
            (decimal.Round(l.CreditAmount, 2, MidpointRounding.AwayFromZero) > 0)))
            .WithMessage("Each journal line must have either a positive debit amount or a positive credit amount.");
        RuleFor(x => x).Must(x => x.Lines != null &&
            decimal.Round(x.Lines.Sum(l => l.DebitAmount), 2, MidpointRounding.AwayFromZero) ==
            decimal.Round(x.Lines.Sum(l => l.CreditAmount), 2, MidpointRounding.AwayFromZero) &&
            x.Lines.Sum(l => l.DebitAmount) > 0)
            .WithMessage("Total debits and total credits must be equal and greater than zero.");
    }
}

