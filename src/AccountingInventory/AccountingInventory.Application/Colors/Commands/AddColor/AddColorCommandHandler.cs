using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Colors.Commands.AddColor;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Colors.Commands.AddColor;

/// <summary>
/// The entire color add flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class AddColorCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<AddColorCommandHandler> logger)
    : IRequestHandler<AddColorCommand, AddColorResult>
{

    public async Task<AddColorResult> Handle(AddColorCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Name.Trim().ToLowerInvariant();

        var colorTaken = await accountingInventoryDbContext.Colors.AnyAsync(b => b.Name == request.Name, cancellationToken);

        if (colorTaken)
        {
            logger.LogWarning("Color registration rejected: name {Name} already in use.", request.Name);
            throw new ConflictException($"A color with name '{request.Name}' already exists.");
        }

        var color = Color.Create (request.Name, request.Description);

        color.Activate();
        accountingInventoryDbContext.Colors.Add(color);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Color {ColorId} - ({Name}) added successfully.",
            color.Id, color.Name);

        return new AddColorResult(color.Id, color.Name, color.Description!);
    }
}
