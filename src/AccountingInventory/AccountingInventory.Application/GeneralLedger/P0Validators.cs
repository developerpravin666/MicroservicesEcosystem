using FluentValidation;
namespace AccountingInventory.Application.GeneralLedger;
public sealed class ReturnInvoiceValidator : AbstractValidator<ReturnInvoiceCommand>
{
    public ReturnInvoiceValidator()
    {
        RuleFor(x => x.Kind).Must(x => x is "Sale" or "Purchase"); RuleFor(x => x.SourceId).NotEmpty();
        RuleFor(x => x.NoteDate).NotEmpty(); RuleFor(x => x.Reason).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Disposition).Must(x => x is "Restock" or "WriteOff" or "Supplier");
    }
}
public sealed class RefundCorrectionValidator : AbstractValidator<RefundCorrectionCommand>
{
    public RefundCorrectionValidator()
    {
        RuleFor(x => x.PaymentDate).NotEmpty(); RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18,2,true);
        RuleFor(x => x.PaymentMode).Must(x => x is "Cash" or "Bank"); RuleFor(x => x.Reference).MaximumLength(100);
    }
}
public sealed class SettleOpeningItemValidator : AbstractValidator<SettleOpeningItemCommand>
{
    public SettleOpeningItemValidator()
    { RuleFor(x => x.PaymentDate).NotEmpty(); RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18,2,true); RuleFor(x => x.PaymentMode).Must(x => x is "Cash" or "Bank"); }
}
public sealed class InviteStaffValidator : AbstractValidator<InviteStaffCommand>
{
    public InviteStaffValidator()
    { RuleFor(x => x.UserName).NotEmpty().MaximumLength(64); RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256); RuleFor(x => x.Mobile).NotEmpty().MaximumLength(20); }
}
public sealed class AcceptStaffInvitationValidator : AbstractValidator<AcceptStaffInvitationCommand>
{
    public AcceptStaffInvitationValidator()
    { RuleFor(x => x.Token).NotEmpty().Length(64); RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128); }
}
