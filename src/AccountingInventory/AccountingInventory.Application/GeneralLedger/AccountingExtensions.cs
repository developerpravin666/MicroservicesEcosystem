using System.Text.Json;
using System.Text.Json.Nodes;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record ApprovalSummary(Guid Id, ApprovalAction Action, string ResourceId, string Summary, Guid? RequestedBy,
    Guid? DecidedBy, ApprovalStatus Status, string? DecisionNote, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);
public sealed record GetAccountingApprovalsQuery(ApprovalStatus? Status = null) : IRequest<IReadOnlyList<ApprovalSummary>>;
public sealed record SubmitAccountingApprovalCommand(ApprovalAction Action, string ResourceId, string Summary,
    string PayloadJson, Guid? RequestedBy) : IRequest<ApprovalSummary>;
public sealed record DecideAccountingApprovalCommand(Guid Id, bool Approve, string? Note, Guid? Reviewer) : IRequest<ApprovalSummary>;

public sealed class AccountingApprovalHandler(IAccountingInventoryDbContext db) :
    IRequestHandler<GetAccountingApprovalsQuery, IReadOnlyList<ApprovalSummary>>,
    IRequestHandler<SubmitAccountingApprovalCommand, ApprovalSummary>,
    IRequestHandler<DecideAccountingApprovalCommand, ApprovalSummary>
{
    public async Task<IReadOnlyList<ApprovalSummary>> Handle(GetAccountingApprovalsQuery request, CancellationToken ct)
        => await db.AccountingApprovals.AsNoTracking().Where(x => !request.Status.HasValue || x.Status == request.Status)
            .OrderByDescending(x => x.CreatedAt).Select(x => new ApprovalSummary(x.Id, x.Action, x.ResourceId, x.Summary,
                x.RequestedBy, x.DecidedBy, x.Status, x.DecisionNote, x.CreatedAt, x.DecidedAt)).ToListAsync(ct);

    public async Task<ApprovalSummary> Handle(SubmitAccountingApprovalCommand request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Action) || request.PayloadJson.Length > 32000)
            throw new ConflictException("Approval action or payload is invalid.");
        try { using var _ = JsonDocument.Parse(request.PayloadJson); }
        catch (JsonException) { throw new ConflictException("Approval payload must be valid JSON."); }
        var item = AccountingApproval.Submit(request.Action, request.ResourceId, request.Summary, request.PayloadJson, request.RequestedBy);
        db.AccountingApprovals.Add(item); await db.SaveChangesAsync(ct); return Map(item);
    }

    public async Task<ApprovalSummary> Handle(DecideAccountingApprovalCommand request, CancellationToken ct)
    {
        await AccountingPermissionGate.EnsureAsync(db, request.Reviewer, "accounting.approve", ct);
        var item = await db.AccountingApprovals.SingleOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Accounting approval request was not found.");
        try { item.Decide(request.Approve, request.Reviewer, request.Note, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        await db.SaveChangesAsync(ct); return Map(item);
    }
    internal static ApprovalSummary Map(AccountingApproval x) => new(x.Id, x.Action, x.ResourceId, x.Summary,
        x.RequestedBy, x.DecidedBy, x.Status, x.DecisionNote, x.CreatedAt, x.DecidedAt);
}

public static class ApprovalGate
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static async Task<AccountingApproval?> ValidateAsync(IAccountingInventoryDbContext db, Guid? id,
        ApprovalAction action, string resourceId, object payload, CancellationToken ct)
    {
        if (!id.HasValue) return null;
        var item = await db.AccountingApprovals.SingleOrDefaultAsync(x => x.Id == id.Value, ct)
            ?? throw new NotFoundException("Approval request was not found.");
        var submitted = JsonNode.Parse(item.PayloadJson);
        var actual = JsonNode.Parse(JsonSerializer.Serialize(payload, JsonOptions));
        if (item.Status != ApprovalStatus.Approved || item.Action != action || item.ResourceId != resourceId || !JsonNode.DeepEquals(submitted, actual))
            throw new ConflictException("The approval is not approved for these exact details.");
        item.MarkApplied(DateTimeOffset.UtcNow);
        return item;
    }
}

public static class AccountingPermissionGate
{
    public static readonly string[] Codes = ["catalog.manage", "purchases.manage", "sales.manage", "inventory.manage", "accounting.manage", "accounting.approve", "accounting.dimensions.manage", "accounting.documents.manage", "accounting.assets.manage", "accounting.budgets.manage"];
    public static async Task EnsureAsync(IAccountingInventoryDbContext db, Guid? actor, string permission, CancellationToken ct)
    {
        if (!actor.HasValue) throw new ForbiddenException("An authenticated user identity is required.");
        var owner = await db.Users.AsNoTracking().Where(x => x.IsOwner && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (owner == actor) return;
        if (!await db.AccountingUserPermissions.AnyAsync(x => x.UserId == actor && x.PermissionCode == permission && x.IsActive, ct))
            throw new ForbiddenException($"The '{permission}' permission is required for this action.");
    }
}

public sealed record AccountingPermissionSummary(Guid UserId, string UserName, string Email, IReadOnlyList<string> Permissions);
public sealed record GetAccountingPermissionsQuery(Guid? ActorId) : IRequest<IReadOnlyList<AccountingPermissionSummary>>;
public sealed record SetAccountingPermissionCommand(Guid UserId, string PermissionCode, bool Granted, Guid? ActorId) : IRequest;
public sealed class AccountingPermissionHandler(IAccountingInventoryDbContext db) :
    IRequestHandler<GetAccountingPermissionsQuery, IReadOnlyList<AccountingPermissionSummary>>,
    IRequestHandler<SetAccountingPermissionCommand>
{
    public async Task<IReadOnlyList<AccountingPermissionSummary>> Handle(GetAccountingPermissionsQuery q, CancellationToken ct)
    {
        await EnsureOwner(q.ActorId, ct);
        var users = await db.Users.AsNoTracking().OrderBy(x => x.UserName).Select(x => new { x.Id, x.UserName, x.Email }).ToListAsync(ct);
        var grants = await db.AccountingUserPermissions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        return users.Select(user => new AccountingPermissionSummary(user.Id, user.UserName, user.Email,
            grants.Where(x => x.UserId == user.Id).Select(x => x.PermissionCode).Order().ToArray())).ToArray();
    }
    public async Task Handle(SetAccountingPermissionCommand q, CancellationToken ct)
    {
        await EnsureOwner(q.ActorId, ct);
        if (!AccountingPermissionGate.Codes.Contains(q.PermissionCode, StringComparer.Ordinal) ||
            !await db.Users.AnyAsync(x => x.Id == q.UserId && x.IsActive, ct)) throw new NotFoundException("User or permission was not found.");
        var row = await db.AccountingUserPermissions.SingleOrDefaultAsync(x => x.UserId == q.UserId && x.PermissionCode == q.PermissionCode, ct);
        if (q.Granted && row is null) db.AccountingUserPermissions.Add(AccountingUserPermission.Grant(q.UserId, q.PermissionCode, q.ActorId));
        else if (row is not null) row.IsActive = q.Granted;
        await db.SaveChangesAsync(ct);
    }
    private async Task EnsureOwner(Guid? actor, CancellationToken ct)
    {
        if (!actor.HasValue) throw new ForbiddenException("An authenticated user identity is required.");
        var owner = await db.Users.AsNoTracking().Where(x => x.IsOwner && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (owner != actor) throw new ForbiddenException("Only the tenant owner can manage accounting permissions.");
    }
}

public sealed record DimensionSummary(Guid Id, string Code, string Name, string DimensionType, bool IsActive);
public sealed record GetAccountingDimensionsQuery() : IRequest<IReadOnlyList<DimensionSummary>>;
public sealed record CreateAccountingDimensionCommand(string Code, string Name, string DimensionType, Guid? ActorId = null) : IRequest<DimensionSummary>;
public sealed class AccountingDimensionHandler(IAccountingInventoryDbContext db) :
    IRequestHandler<GetAccountingDimensionsQuery, IReadOnlyList<DimensionSummary>>,
    IRequestHandler<CreateAccountingDimensionCommand, DimensionSummary>
{
    public async Task<IReadOnlyList<DimensionSummary>> Handle(GetAccountingDimensionsQuery request, CancellationToken ct)
        => await db.AccountingDimensions.AsNoTracking().OrderBy(x => x.DimensionType).ThenBy(x => x.Code)
            .Select(x => new DimensionSummary(x.Id, x.Code, x.Name, x.DimensionType, x.IsActive)).ToListAsync(ct);
    public async Task<DimensionSummary> Handle(CreateAccountingDimensionCommand request, CancellationToken ct)
    {
        await AccountingPermissionGate.EnsureAsync(db, request.ActorId, "accounting.dimensions.manage", ct);
        var code = request.Code.Trim().ToUpperInvariant(); var type = request.DimensionType.Trim().ToUpperInvariant();
        if (await db.AccountingDimensions.AnyAsync(x => x.Code == code && x.DimensionType == type, ct))
            throw new ConflictException("A dimension with this type and code already exists.");
        var item = AccountingDimension.Create(code, request.Name, type); db.AccountingDimensions.Add(item);
        await db.SaveChangesAsync(ct); return new(item.Id, item.Code, item.Name, item.DimensionType, item.IsActive);
    }
}

public sealed record SupportingDocumentSummary(Guid Id, string ResourceType, string ResourceId, string FileName,
    string ContentType, string StorageReference, string? Description, Guid? AddedBy, DateTimeOffset CreatedAt);
public sealed record GetSupportingDocumentsQuery(string ResourceType, string ResourceId) : IRequest<IReadOnlyList<SupportingDocumentSummary>>;
public sealed record AddSupportingDocumentCommand(string ResourceType, string ResourceId, string FileName,
    string ContentType, string StorageReference, string? Description, Guid? AddedBy) : IRequest<SupportingDocumentSummary>;
public sealed class SupportingDocumentHandler(IAccountingInventoryDbContext db) :
    IRequestHandler<GetSupportingDocumentsQuery, IReadOnlyList<SupportingDocumentSummary>>,
    IRequestHandler<AddSupportingDocumentCommand, SupportingDocumentSummary>
{
    public async Task<IReadOnlyList<SupportingDocumentSummary>> Handle(GetSupportingDocumentsQuery q, CancellationToken ct)
        => await db.SupportingDocuments.AsNoTracking().Where(x => x.ResourceType == q.ResourceType && x.ResourceId == q.ResourceId)
            .OrderByDescending(x => x.CreatedAt).Select(x => new SupportingDocumentSummary(x.Id, x.ResourceType, x.ResourceId,
                x.FileName, x.ContentType, x.StorageReference, x.Description, x.AddedBy, x.CreatedAt)).ToListAsync(ct);
    public async Task<SupportingDocumentSummary> Handle(AddSupportingDocumentCommand q, CancellationToken ct)
    {
        await AccountingPermissionGate.EnsureAsync(db, q.AddedBy, "accounting.documents.manage", ct);
        var item = SupportingDocument.Create(q.ResourceType, q.ResourceId, q.FileName, q.ContentType,
            q.StorageReference, q.Description, q.AddedBy);
        db.SupportingDocuments.Add(item); await db.SaveChangesAsync(ct);
        return new(item.Id, item.ResourceType, item.ResourceId, item.FileName, item.ContentType, item.StorageReference,
            item.Description, item.AddedBy, item.CreatedAt);
    }
}

public sealed record FixedAssetSummary(Guid Id, string AssetNumber, string Name, DateOnly AcquisitionDate,
    decimal AcquisitionCost, decimal SalvageValue, int UsefulLifeMonths, decimal AccumulatedDepreciation,
    decimal BookValue, Guid AssetAccountId, Guid DepreciationExpenseAccountId, Guid AccumulatedDepreciationAccountId, DateOnly? DisposedDate);
public sealed record GetFixedAssetsQuery() : IRequest<IReadOnlyList<FixedAssetSummary>>;
public sealed record AcquireFixedAssetCommand(string AssetNumber, string Name, DateOnly AcquisitionDate, decimal Cost,
    decimal SalvageValue, int UsefulLifeMonths, Guid AssetAccountId, Guid DepreciationExpenseAccountId,
    Guid AccumulatedDepreciationAccountId, Guid FundingAccountId, Guid? ActorId = null) : IRequest<FixedAssetSummary>;
public sealed record DepreciateFixedAssetCommand(Guid Id, DateOnly DepreciationDate, int Months = 1, Guid? ActorId = null) : IRequest<FixedAssetSummary>;
public sealed record DisposeFixedAssetCommand(Guid Id, DateOnly DisposalDate, decimal Proceeds, Guid ProceedsAccountId, Guid? ActorId = null) : IRequest<FixedAssetSummary>;
public sealed class FixedAssetHandler(IAccountingInventoryDbContext db) :
    IRequestHandler<GetFixedAssetsQuery, IReadOnlyList<FixedAssetSummary>>,
    IRequestHandler<AcquireFixedAssetCommand, FixedAssetSummary>,
    IRequestHandler<DepreciateFixedAssetCommand, FixedAssetSummary>,
    IRequestHandler<DisposeFixedAssetCommand, FixedAssetSummary>
{
    public async Task<IReadOnlyList<FixedAssetSummary>> Handle(GetFixedAssetsQuery q, CancellationToken ct)
        => (await db.FixedAssets.AsNoTracking().OrderBy(x => x.AssetNumber).ToListAsync(ct)).Select(Map).ToArray();

    public async Task<FixedAssetSummary> Handle(AcquireFixedAssetCommand q, CancellationToken ct)
    {
        await AccountingPermissionGate.EnsureAsync(db, q.ActorId, "accounting.assets.manage", ct);
        await LedgerPosting.EnsurePeriodOpenAsync(db, q.AcquisitionDate, ct);
        var accounts = await ActiveAccounts([q.AssetAccountId, q.DepreciationExpenseAccountId, q.AccumulatedDepreciationAccountId, q.FundingAccountId], ct);
        if (accounts[q.AssetAccountId].Type != LedgerAccountType.Asset || accounts[q.DepreciationExpenseAccountId].Type != LedgerAccountType.Expense || accounts[q.AccumulatedDepreciationAccountId].Type != LedgerAccountType.Asset)
            throw new ConflictException("Select asset, depreciation expense, and accumulated depreciation accounts of the matching account types.");
        if (q.FundingAccountId == q.AssetAccountId || accounts[q.FundingAccountId].Type is not (LedgerAccountType.Asset or LedgerAccountType.Liability))
            throw new ConflictException("Funding account must be a cash, bank, or payable account distinct from the fixed asset account.");
        if (q.AssetAccountId == q.AccumulatedDepreciationAccountId || q.DepreciationExpenseAccountId == q.AccumulatedDepreciationAccountId)
            throw new ConflictException("Fixed asset and accumulated depreciation accounts must be different.");
        var asset = FixedAsset.Create(q.AssetNumber, q.Name, q.AcquisitionDate, q.Cost, q.SalvageValue, q.UsefulLifeMonths,
            q.AssetAccountId, q.DepreciationExpenseAccountId, q.AccumulatedDepreciationAccountId);
        var entry = JournalEntry.Post(q.AcquisitionDate, $"Asset acquisition · {asset.AssetNumber}", "FixedAssetAcquisition", asset.Id.ToString(),
            [(q.AssetAccountId, q.Cost, 0m, (string?)"Asset cost"), (q.FundingAccountId, 0m, q.Cost, (string?)"Funding")]);
        db.FixedAssets.Add(asset); AddJournal(entry); await db.SaveChangesAsync(ct); return Map(asset);
    }

    public async Task<FixedAssetSummary> Handle(DepreciateFixedAssetCommand q, CancellationToken ct)
    {
        await AccountingPermissionGate.EnsureAsync(db, q.ActorId, "accounting.assets.manage", ct);
        await LedgerPosting.EnsurePeriodOpenAsync(db, q.DepreciationDate, ct);
        var asset = await db.FixedAssets.SingleOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Fixed asset was not found.");
        if (q.Months != 1 || q.DepreciationDate <= asset.AcquisitionDate)
            throw new ConflictException("Post depreciation one month at a time, after the acquisition month.");
        var source = $"{asset.Id}:{q.DepreciationDate:yyyy-MM}";
        if (await db.JournalEntries.AnyAsync(x => x.SourceType == "FixedAssetDepreciation" && x.SourceId == source, ct))
            throw new ConflictException("Depreciation has already been posted for this asset and period.");
        decimal amount; try { amount = asset.Depreciate(q.Months); } catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        var entry = JournalEntry.Post(q.DepreciationDate, $"Depreciation · {asset.AssetNumber}", "FixedAssetDepreciation", source,
            [(asset.DepreciationExpenseAccountId, amount, 0m, (string?)"Depreciation expense"), (asset.AccumulatedDepreciationAccountId, 0m, amount, (string?)"Accumulated depreciation")]);
        AddJournal(entry); await db.SaveChangesAsync(ct); return Map(asset);
    }

    public async Task<FixedAssetSummary> Handle(DisposeFixedAssetCommand q, CancellationToken ct)
    {
        await AccountingPermissionGate.EnsureAsync(db, q.ActorId, "accounting.assets.manage", ct);
        await LedgerPosting.EnsurePeriodOpenAsync(db, q.DisposalDate, ct);
        var asset = await db.FixedAssets.SingleOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Fixed asset was not found.");
        var gainId = await db.ChartAccounts.Where(x => x.Code == "4900" && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var lossId = await db.ChartAccounts.Where(x => x.Code == "5200" && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var gainLoss = q.Proceeds - asset.BookValue;
        if (gainLoss != 0 && (!gainId.HasValue || !lossId.HasValue)) throw new ConflictException("Add active disposal gain and loss accounts with codes 4900 and 5200 before disposal.");
        var accountIds = new List<Guid> { asset.AssetAccountId, asset.AccumulatedDepreciationAccountId, q.ProceedsAccountId };
        if (gainId.HasValue) accountIds.Add(gainId.Value);
        if (lossId.HasValue) accountIds.Add(lossId.Value);
        var accounts = await ActiveAccounts(accountIds.ToArray(), ct);
        if (q.ProceedsAccountId == asset.AssetAccountId || q.ProceedsAccountId == asset.AccumulatedDepreciationAccountId)
            throw new ConflictException("Select a cash or bank proceeds account distinct from the asset accounts.");
        if (accounts[q.ProceedsAccountId].Type != LedgerAccountType.Asset || accounts[q.ProceedsAccountId].NormalBalance != LedgerBalanceSide.Debit)
            throw new ConflictException("Asset disposal proceeds must be posted to an active cash or bank account.");
        var (proceeds, bookValue) = asset.Dispose(q.DisposalDate, q.Proceeds);
        var lines = new List<(Guid AccountId, decimal Debit, decimal Credit, string? Memo)>();
        if (proceeds > 0) lines.Add((q.ProceedsAccountId, proceeds, 0m, "Disposal proceeds"));
        if (asset.AccumulatedDepreciation > 0) lines.Add((asset.AccumulatedDepreciationAccountId, asset.AccumulatedDepreciation, 0m, "Clear accumulated depreciation"));
        lines.Add((asset.AssetAccountId, 0m, asset.AcquisitionCost, "Remove asset cost"));
        if (gainLoss > 0) lines.Add((gainId!.Value, 0m, gainLoss, "Gain on disposal"));
        else if (gainLoss < 0) lines.Add((lossId!.Value, -gainLoss, 0m, "Loss on disposal"));
        var journal = JournalEntry.Post(q.DisposalDate, $"Asset disposal · {asset.AssetNumber}", "FixedAssetDisposal", asset.Id.ToString(), lines);
        AddJournal(journal); await db.SaveChangesAsync(ct); return Map(asset);
    }

    private async Task<Dictionary<Guid, ChartAccount>> ActiveAccounts(Guid[] ids, CancellationToken ct)
    {
        var rows = await db.ChartAccounts.Where(x => ids.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        if (rows.Count != ids.Distinct().Count()) throw new ConflictException("Every account must be active.");
        return rows;
    }
    private void AddJournal(JournalEntry entry) { db.JournalEntries.Add(entry); db.JournalLines.AddRange(entry.Lines); }
    private static FixedAssetSummary Map(FixedAsset x) => new(x.Id, x.AssetNumber, x.Name, x.AcquisitionDate, x.AcquisitionCost,
        x.SalvageValue, x.UsefulLifeMonths, x.AccumulatedDepreciation, x.BookValue, x.AssetAccountId,
        x.DepreciationExpenseAccountId, x.AccumulatedDepreciationAccountId, x.DisposedDate);
}

public sealed record BudgetSummary(Guid Id, Guid AccountId, string AccountCode, string AccountName, Guid? DimensionId,
    string? DimensionCode, DateOnly StartDate, DateOnly EndDate, decimal Amount, decimal Actual, decimal Variance, string? Notes);
public sealed record GetBudgetVarianceQuery(DateOnly? FromDate, DateOnly? ToDate) : IRequest<IReadOnlyList<BudgetSummary>>;
public sealed record CreateAccountBudgetCommand(Guid AccountId, Guid? DimensionId, DateOnly StartDate,
    DateOnly EndDate, decimal Amount, string? Notes, Guid? ActorId = null) : IRequest<BudgetSummary>;
public sealed class AccountBudgetHandler(IAccountingInventoryDbContext db) :
    IRequestHandler<GetBudgetVarianceQuery, IReadOnlyList<BudgetSummary>>,
    IRequestHandler<CreateAccountBudgetCommand, BudgetSummary>
{
    public async Task<IReadOnlyList<BudgetSummary>> Handle(GetBudgetVarianceQuery q, CancellationToken ct)
    {
        var budgets = await (from b in db.AccountBudgets.AsNoTracking()
                             join a in db.ChartAccounts.AsNoTracking() on b.AccountId equals a.Id
                             join d in db.AccountingDimensions.AsNoTracking() on b.DimensionId equals d.Id into dimensions
                             from d in dimensions.DefaultIfEmpty()
                             where (!q.FromDate.HasValue || b.EndDate >= q.FromDate) && (!q.ToDate.HasValue || b.StartDate <= q.ToDate)
                             select new
                             {
                                 b.Id,
                                 b.AccountId,
                                 a.Code,
                                 AccountName = a.Name,
                                 b.DimensionId,
                                 DimensionCode = d == null ? null : d.Code,
                                 b.StartDate,
                                 b.EndDate,
                                 b.Amount,
                                 b.Notes,
                                 a.NormalBalance
                             }).ToListAsync(ct);
        var result = new List<BudgetSummary>();
        foreach (var b in budgets)
        {
            var actuals = await (from line in db.JournalLines.AsNoTracking()
                                 join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
                                 where line.AccountId == b.AccountId && line.DimensionId == b.DimensionId && entry.JournalDate >= b.StartDate && entry.JournalDate <= b.EndDate
                                 select line.Debit - line.Credit).SumAsync(ct);
            var actual = b.NormalBalance == LedgerBalanceSide.Debit ? actuals : -actuals;
            result.Add(new(b.Id, b.AccountId, b.Code, b.AccountName, b.DimensionId, b.DimensionCode,
                b.StartDate, b.EndDate, b.Amount, actual, b.Amount - actual, b.Notes));
        }
        return result;
    }
    public async Task<BudgetSummary> Handle(CreateAccountBudgetCommand q, CancellationToken ct)
    {
        await AccountingPermissionGate.EnsureAsync(db, q.ActorId, "accounting.budgets.manage", ct);
        if (!await db.ChartAccounts.AnyAsync(x => x.Id == q.AccountId && x.IsActive, ct)) throw new NotFoundException("Active ledger account was not found.");
        if (q.DimensionId.HasValue && !await db.AccountingDimensions.AnyAsync(x => x.Id == q.DimensionId && x.IsActive, ct)) throw new NotFoundException("Accounting dimension was not found.");
        if (await db.AccountBudgets.AnyAsync(x => x.AccountId == q.AccountId && x.DimensionId == q.DimensionId && x.StartDate == q.StartDate && x.EndDate == q.EndDate, ct))
            throw new ConflictException("A budget already exists for this account, dimension, and period.");
        var b = AccountBudget.Create(q.AccountId, q.DimensionId, q.StartDate, q.EndDate, q.Amount, q.Notes);
        db.AccountBudgets.Add(b); await db.SaveChangesAsync(ct);
        return (await Handle(new GetBudgetVarianceQuery(q.StartDate, q.EndDate), ct)).Single(x => x.Id == b.Id);
    }
}
