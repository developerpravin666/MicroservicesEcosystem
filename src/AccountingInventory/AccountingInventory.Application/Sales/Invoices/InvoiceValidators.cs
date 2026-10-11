using FluentValidation;
namespace AccountingInventory.Application.Sales.Invoices;

public sealed class CreateSalesInvoiceValidator : AbstractValidator<CreateSalesInvoiceCommand>
{
    public CreateSalesInvoiceValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerMobile).NotEmpty().MaximumLength(20);
        RuleFor(x => x.CustomerAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CustomerEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));
        RuleFor(x => x.CustomerGstin).Matches(@"^\d{2}[A-Z]{5}\d{4}[A-Z][A-Z0-9]Z[A-Z0-9]$").When(x => !string.IsNullOrWhiteSpace(x.CustomerGstin));
        RuleFor(x => x.PlaceOfSupplyStateCode).Must(x => x == null || AccountingInventory.Domain.Entities.GstStates.StateMap.ContainsKey(x));
        RuleFor(x => x.SupplyType).Must(x => !x.HasValue || Enum.IsDefined(x.Value));
        RuleFor(x => x.InvoiceDate).NotEmpty();
        RuleFor(x => x.PaymentTermsDays).InclusiveBetween(0,3650);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().Must(x => x != null && x.Count <= 500);
        RuleForEach(x => x.Lines).SetValidator(new InvoiceLineValidator());
        RuleFor(x => x.InitialPayment).Must(x => x == null || x.Amount > 0);
    }
}
public sealed class InvoiceLineValidator : AbstractValidator<CreateSalesInvoiceLineCommandDto>
{
    public InvoiceLineValidator()
    {
        RuleFor(x => x.ItemType).IsInEnum();
        RuleFor(x => x.ItemDescription).NotEmpty().MaximumLength(500);
        RuleFor(x => x.HsnSac).Matches(@"^(\d{4}|\d{6}|\d{8})$").When(x => !string.IsNullOrWhiteSpace(x.HsnSac));
        RuleFor(x => x.UnitOfMeasure).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Quantity).GreaterThan(0).PrecisionScale(18,4,true);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).PrecisionScale(18,2,true);
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).PrecisionScale(18,2,true);
    }
}
