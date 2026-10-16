using FluentValidation;

namespace AccountingInventory.Application.Compliance.EInvoices.Commands.GenerateEInvoice;

public sealed class GenerateEInvoiceCommandValidator : AbstractValidator<GenerateEInvoiceCommand>
{
    public GenerateEInvoiceCommandValidator()
    {
        RuleFor(x => x.SalesInvoiceId)
            .NotEmpty().WithMessage("Sales Invoice ID is required.");

        RuleFor(x => x.CustomIrn)
            .Length(64).When(x => !string.IsNullOrWhiteSpace(x.CustomIrn))
            .WithMessage("Custom IRN must be exactly 64 hexadecimal characters.");
    }
}

