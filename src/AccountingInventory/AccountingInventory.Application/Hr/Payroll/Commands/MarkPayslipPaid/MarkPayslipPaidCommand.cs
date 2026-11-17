using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Commands.MarkPayslipPaid;

public sealed record MarkPayslipPaidCommand(
    Guid PayslipId,
    string? PaymentMode = null,
    string? PaymentReference = null) : IRequest<PayslipDto>;
