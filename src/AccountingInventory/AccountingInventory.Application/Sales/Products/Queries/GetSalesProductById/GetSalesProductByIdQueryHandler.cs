using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Products.Queries.GetSalesProductById;

public sealed class GetSalesProductByIdQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetSalesProductByIdQuery, SalesProductSummary>
{
    public async Task<SalesProductSummary> Handle(GetSalesProductByIdQuery request, CancellationToken cancellationToken)
    {
        var sale = await db.SalesProducts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Sales product '{request.Id}' was not found.");
        return (await SalesProductSummaryMapper.MapAsync(db, [sale], cancellationToken))[0];
    }
}
