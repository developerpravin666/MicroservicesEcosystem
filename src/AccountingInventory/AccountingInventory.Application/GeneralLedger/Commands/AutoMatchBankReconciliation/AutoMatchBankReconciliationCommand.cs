using AccountingInventory.Application.GeneralLedger.Common;
using MediatR;

namespace AccountingInventory.Application.GeneralLedger.Commands.AutoMatchBankReconciliation;

public sealed record AutoMatchBankReconciliationCommand(Guid ReconciliationId) : IRequest<AutoMatchResultDto>;

