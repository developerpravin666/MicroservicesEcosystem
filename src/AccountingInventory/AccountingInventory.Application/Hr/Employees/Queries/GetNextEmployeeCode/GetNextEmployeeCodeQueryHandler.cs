using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Employees.Queries.GetNextEmployeeCode;

public sealed class GetNextEmployeeCodeQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetNextEmployeeCodeQuery, string>
{
    public async Task<string> Handle(GetNextEmployeeCodeQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var prefix = $"{now.Year:D4}-{now.Month:D2}-";

        var existingCodes = await db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeCode.StartsWith(prefix))
            .Select(e => e.EmployeeCode)
            .ToListAsync(cancellationToken);

        var maxSeq = 0;
        foreach (var code in existingCodes)
        {
            var suffix = code.Substring(prefix.Length);
            if (int.TryParse(suffix, out var seq) && seq > maxSeq)
            {
                maxSeq = seq;
            }
        }

        return $"{prefix}{(maxSeq + 1):D6}";
    }
}
