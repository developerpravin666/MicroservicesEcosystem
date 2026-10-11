using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Variants.Commands.UpdateVariantCommand;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Variants.Commands.UpdateVariantCommand;

/// <summary>
/// The entire variant update flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class UpdateVariantCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<UpdateVariantCommandHandler> logger)
    : IRequestHandler<UpdateVariantCommand, UpdateVariantResult>
{

    public async Task<UpdateVariantResult> Handle(UpdateVariantCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Name.Trim().ToLowerInvariant();

        var variantTaken = await accountingInventoryDbContext.Variants.AnyAsync(t => t.Id == request.Id, cancellationToken);

        if (!variantTaken)
        {
            logger.LogWarning("Variant not found: ID {Id}.", request.Id);
            throw new NotFoundException($"A variant with ID '{request.Id}' was not found.");
        }

        var variant = Variant.Update(request.Id, request.Name, request.Description, request.IsActive);

        accountingInventoryDbContext.Variants.Update(variant);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Variant {VariantId} - ({Name}) - {Description} update successfully.",
            variant.Id, variant.Name, variant.Description);

        return new UpdateVariantResult(variant.Id, variant.Name, variant.Description!, variant.IsActive);
    }
}
