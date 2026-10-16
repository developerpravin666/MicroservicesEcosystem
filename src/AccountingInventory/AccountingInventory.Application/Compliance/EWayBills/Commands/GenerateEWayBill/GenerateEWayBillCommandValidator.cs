using FluentValidation;

namespace AccountingInventory.Application.Compliance.EWayBills.Commands.GenerateEWayBill;

public sealed class GenerateEWayBillCommandValidator : AbstractValidator<GenerateEWayBillCommand>
{
    public GenerateEWayBillCommandValidator()
    {
        RuleFor(x => x.SalesInvoiceId)
            .NotEmpty().WithMessage("Sales Invoice ID is required.");

        RuleFor(x => x.DistanceKm)
            .GreaterThan(0).WithMessage("Distance in km must be greater than 0.")
            .LessThanOrEqualTo(4000).WithMessage("Distance cannot exceed 4000 km.");

        RuleFor(x => x.TransMode)
            .InclusiveBetween(1, 4).WithMessage("Transport mode must be 1 (Road), 2 (Rail), 3 (Air), or 4 (Ship).");

        RuleFor(x => x.VehicleType)
            .Must(vt => vt == "R" || vt == "O")
            .WithMessage("Vehicle type must be 'R' (Regular) or 'O' (Over Dimensional Cargo).");

        RuleFor(x => x.DocType)
            .NotEmpty().WithMessage("Doc Type is required.");

        RuleFor(x => x.SupplyType)
            .NotEmpty().WithMessage("Supply Type is required.");
    }
}

