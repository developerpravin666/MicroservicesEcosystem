using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Masters.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Masters.Queries.GetMasters;

public sealed class GetHrMastersQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetHrMastersQuery, IReadOnlyList<HrMasterItemDto>>
{
    public async Task<IReadOnlyList<HrMasterItemDto>> Handle(GetHrMastersQuery request, CancellationToken cancellationToken)
    {
        var query = db.HrMasterItems.AsNoTracking().Where(m => !m.IsDeleted && m.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var cat = request.Category.Trim().ToUpperInvariant();
            query = query.Where(m => m.Category == cat);
        }

        return await query
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .Select(m => new HrMasterItemDto(m.Id, m.Category, m.Name, m.Code, m.Description, m.IsDefault, m.IsActive))
            .ToListAsync(cancellationToken);
    }
}
