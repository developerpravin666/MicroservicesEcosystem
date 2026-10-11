using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Colors.Commands.UpdateColorCommand;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Colors.Commands.UpdateColorCommand;

/// <summary>
/// The entire color update flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class UpdateColorCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<UpdateColorCommandHandler> logger)
    : IRequestHandler<UpdateColorCommand, UpdateColorResult>
{

    public async Task<UpdateColorResult> Handle(UpdateColorCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Name.Trim().ToLowerInvariant();

        var colorTaken = await accountingInventoryDbContext.Colors.AnyAsync(t => t.Id == request.Id, cancellationToken);

        if (!colorTaken)
        {
            logger.LogWarning("Color not found: ID {Id}.", request.Id);
            throw new NotFoundException($"A color with ID '{request.Id}' was not found.");
        }

        var color = Color.Update(request.Id, request.Name, request.Description, request.IsActive);

        accountingInventoryDbContext.Colors.Update(color);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Color {ColorId} - ({Name}) - {Description} update successfully.",
            color.Id, color.Name, color.Description);

        return new UpdateColorResult(color.Id, color.Name, color.Description!, color.IsActive);
    }
}
