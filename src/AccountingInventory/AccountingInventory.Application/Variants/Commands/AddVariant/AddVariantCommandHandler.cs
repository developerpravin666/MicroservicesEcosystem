using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Variants.Commands.AddVariant;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Variants.Commands.AddVariant;

/// <summary>
/// The entire variant add flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class AddVariantCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<AddVariantCommandHandler> logger)
    : IRequestHandler<AddVariantCommand, AddVariantResult>
{

    public async Task<AddVariantResult> Handle(AddVariantCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Name.Trim().ToLowerInvariant();

        var variantTaken = await accountingInventoryDbContext.Variants.AnyAsync(b => b.Name == request.Name, cancellationToken);

        if (variantTaken)
        {
            logger.LogWarning("Variant registration rejected: name {Name} already in use.", request.Name);
            throw new ConflictException($"A variant with name '{request.Name}' already exists.");
        }

        var variant = Variant.Create (request.Name, request.Description);

        variant.Activate();
        accountingInventoryDbContext.Variants.Add(variant);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Variant {VariantId} - ({Name}) added successfully.",
            variant.Id, variant.Name);

        return new AddVariantResult(variant.Id, variant.Name, variant.Description!);
    }
}
