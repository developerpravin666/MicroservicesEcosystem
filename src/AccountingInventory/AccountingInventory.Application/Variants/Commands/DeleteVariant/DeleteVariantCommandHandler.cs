using AccountingInventory.Application.Common;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Variants.Commands.DeleteVariant;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Variants.Commands.DeleteVariant;

public sealed class DeleteVariantCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<DeleteVariantCommandHandler> logger)
    : IRequestHandler<DeleteVariantCommand>
{
    public async Task Handle(DeleteVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await accountingInventoryDbContext.Variants
            .SingleOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"A variant with ID '{request.Id}' was not found.");

        await MasterReferenceIntegrity.EnsureDeletableAsync(accountingInventoryDbContext, "Variant", request.Id, cancellationToken);
        variant.Delete();
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Variant {VariantId} ({VariantName}) deleted.", variant.Id, variant.Name);
    }
}
