using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.AuditLogs;

public sealed record AuditLogSummary(Guid Id, string TableName, string RecordId, string Action,
    string? OldValue, string? NewValue, Guid? CreatedBy, string? CreatedByName,
    DateTimeOffset CreatedDate, string TenantSchema);

public sealed record GetAuditLogsQuery(int Page = 1, int PageSize = 20, string? Search = null,
    string? TableName = null, string? Action = null, Guid? ChangedBy = null,
    string? RecordId = null, DateOnly? FromDate = null, DateOnly? ToDate = null,
    string? SortBy = null, string? SortDirection = "desc") : IRequest<PagedResult<AuditLogSummary>>;

public sealed record AuditLogModule(string TableName, int Count);

public sealed class GetAuditLogsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogSummary>>
{
    public async Task<PagedResult<AuditLogSummary>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.TableName))
            query = query.Where(log => log.TableName == request.TableName);
        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(log => log.Action == request.Action);
        if (request.ChangedBy.HasValue)
            query = query.Where(log => log.CreatedBy == request.ChangedBy);
        if (!string.IsNullOrWhiteSpace(request.RecordId))
            query = query.Where(log => log.RecordId.Contains(request.RecordId.Trim()));
        if (request.FromDate.HasValue)
        {
            var from = new DateTimeOffset(request.FromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(log => log.CreatedDate >= from);
        }
        if (request.ToDate.HasValue)
        {
            var before = new DateTimeOffset(request.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(log => log.CreatedDate < before);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = $"%{request.Search.Trim()}%";
            var matchingUsers = await db.Users.AsNoTracking()
                .Where(user => EF.Functions.Like(user.UserName, search) || EF.Functions.Like(user.Email, search))
                .Select(user => user.Id).ToArrayAsync(cancellationToken);
            query = query.Where(log => EF.Functions.Like(log.TableName, search)
                || EF.Functions.Like(log.RecordId, search)
                || EF.Functions.Like(log.Action, search)
                || (log.OldValue != null && EF.Functions.Like(log.OldValue, search))
                || (log.NewValue != null && EF.Functions.Like(log.NewValue, search))
                || (log.CreatedBy.HasValue && matchingUsers.Contains(log.CreatedBy.Value)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var sorted = GridSorting.Apply(query, request.SortBy, request.SortDirection, "CreatedDate", true,
            new SortSelectors<AuditLog>
            {
                ["createdByName"] = log => db.Users.Where(user => user.Id == log.CreatedBy).Select(user => user.UserName).FirstOrDefault(),
            });
        var rows = sorted.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(log => new AuditLogSummary(
            log.Id, log.TableName, log.RecordId, log.Action, log.OldValue, log.NewValue,
            log.CreatedBy,
            db.Users.Where(user => user.Id == log.CreatedBy).Select(user => user.UserName).FirstOrDefault(),
            log.CreatedDate, log.TenantSchema));
        var items = await rows.ToListAsync(cancellationToken);

        return new PagedResult<AuditLogSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}

public sealed record GetAuditLogModulesQuery() : IRequest<IReadOnlyList<AuditLogModule>>;

public sealed class GetAuditLogModulesQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetAuditLogModulesQuery, IReadOnlyList<AuditLogModule>>
{
    public async Task<IReadOnlyList<AuditLogModule>> Handle(GetAuditLogModulesQuery request, CancellationToken cancellationToken)
        => await db.AuditLogs.AsNoTracking().GroupBy(log => log.TableName)
            .OrderBy(group => group.Key)
            .Select(group => new AuditLogModule(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
}
