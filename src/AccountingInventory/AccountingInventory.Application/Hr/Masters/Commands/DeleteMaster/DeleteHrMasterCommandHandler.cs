using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Masters.Commands.DeleteMaster;

public sealed class DeleteHrMasterCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<DeleteHrMasterCommandHandler> logger)
    : IRequestHandler<DeleteHrMasterCommand, Unit>
{
    public async Task<Unit> Handle(DeleteHrMasterCommand request, CancellationToken cancellationToken)
    {
        var item = await db.HrMasterItems.FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted, cancellationToken)
            ?? throw new NotFoundException($"Master item with ID '{request.Id}' was not found.");

        item.Delete();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Master item '{Id}' ({Name}) marked as deleted.", item.Id, item.Name);

        return Unit.Value;
    }
}
