using AccountingInventory.Application.Common;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Colors.Commands.DeleteColor;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Colors.Commands.DeleteColor;

public sealed class DeleteColorCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<DeleteColorCommandHandler> logger)
    : IRequestHandler<DeleteColorCommand>
{
    public async Task Handle(DeleteColorCommand request, CancellationToken cancellationToken)
    {
        var color = await accountingInventoryDbContext.Colors
            .SingleOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"A color with ID '{request.Id}' was not found.");

        await MasterReferenceIntegrity.EnsureDeletableAsync(accountingInventoryDbContext, "Color", request.Id, cancellationToken);
        color.Delete();
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Color {ColorId} ({ColorName}) deleted.", color.Id, color.Name);
    }
}
