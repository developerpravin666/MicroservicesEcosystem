using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Purchases.Products.Commands.ActivateProduct;

public sealed record ActivateProductCommand(Guid Id, bool IsActive = true) : IRequest<bool>;

public sealed class ActivateProductCommandHandler(IAccountingInventoryDbContext dbContext)
    : IRequestHandler<ActivateProductCommand, bool>
{
    public async Task<bool> Handle(ActivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Product with ID '{request.Id}' was not found.");

        if (product.IsSold)
        {
            throw new ConflictException("Sold inventory cannot be modified.");
        }

        product.SetActive(request.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

