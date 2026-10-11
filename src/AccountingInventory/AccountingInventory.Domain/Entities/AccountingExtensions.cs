using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public enum ApprovalStatus { Pending, Approved, Rejected, Applied }
public enum ApprovalAction { Journal, ClosePeriod, InventoryWriteOff, FinancialCorrection }

public sealed class AccountingUserPermission : AggregateRoot
{
    public Guid UserId { get; private set; }
    public string PermissionCode { get; private set; } = null!;
    public Guid? GrantedBy { get; private set; }
    public static AccountingUserPermission Grant(Guid userId, string code, Guid? grantor)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code) || code.Length > 80) throw new ArgumentException("Permission grant is invalid.");
        return new() { Id = Guid.NewGuid(), IsActive = true, UserId = userId, PermissionCode = code.Trim(), GrantedBy = grantor };
    }
    private AccountingUserPermission() { }
}

public sealed class AccountingApproval : AggregateRoot
{
    public ApprovalAction Action { get; private set; }
    public string ResourceId { get; private set; } = null!;
    public string Summary { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public Guid? RequestedBy { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public string? DecisionNote { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public DateTimeOffset? AppliedAt { get; private set; }
    public long Revision { get; private set; }

    public static AccountingApproval Submit(ApprovalAction action, string resourceId, string summary, string payload, Guid? requester)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || resourceId.Trim().Length > 128 || string.IsNullOrWhiteSpace(summary) || summary.Trim().Length > 300 || string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Approval action, resource, summary, and payload are required.");
        return new() { Id = Guid.NewGuid(), IsActive = true, Action = action, ResourceId = resourceId.Trim(),
            Summary = summary.Trim(), PayloadJson = payload, RequestedBy = requester, Status = ApprovalStatus.Pending };
    }

    public void Decide(bool approve, Guid? reviewer, string? note, DateTimeOffset now)
    {
        if (Status != ApprovalStatus.Pending) throw new InvalidOperationException("Only pending requests can be decided.");
        if (RequestedBy.HasValue && reviewer == RequestedBy) throw new InvalidOperationException("The requester cannot approve their own request.");
        Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        DecidedBy = reviewer; DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim(); DecidedAt = now;
        Revision++;
    }
    public void MarkApplied(DateTimeOffset now)
    {
        if (Status != ApprovalStatus.Approved) throw new InvalidOperationException("Only approved requests can be applied.");
        Status = ApprovalStatus.Applied; AppliedAt = now;
        Revision++;
    }
    private AccountingApproval() { }
}

public sealed class AccountingDimension : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string DimensionType { get; private set; } = null!;
    public static AccountingDimension Create(string code, string name, string type) => new()
    {
        Id = Guid.NewGuid(), IsActive = true, Code = Required(code, 30), Name = Required(name, 120), DimensionType = Required(type, 30)
    };
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max
        ? throw new ArgumentException($"Value is required and limited to {max} characters.") : value.Trim();
    private AccountingDimension() { }
}

public sealed class SupportingDocument : AggregateRoot
{
    public string ResourceType { get; private set; } = null!;
    public string ResourceId { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public string StorageReference { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid? AddedBy { get; private set; }
    public static SupportingDocument Create(string type, string id, string file, string contentType, string reference, string? description, Guid? addedBy)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Length > 50 || string.IsNullOrWhiteSpace(id) || id.Length > 128 ||
            string.IsNullOrWhiteSpace(file) || file.Length > 255 || string.IsNullOrWhiteSpace(contentType) || contentType.Length > 120 ||
            string.IsNullOrWhiteSpace(reference) || reference.Length > 1000 || (description?.Length ?? 0) > 500)
            throw new ArgumentException("Document metadata is invalid.");
        return new() { Id = Guid.NewGuid(), IsActive = true, ResourceType = type.Trim(), ResourceId = id.Trim(), FileName = file.Trim(),
            ContentType = contentType.Trim(), StorageReference = reference.Trim(), Description = description?.Trim(), AddedBy = addedBy };
    }
    private SupportingDocument() { }
}

public sealed class FixedAsset : AggregateRoot
{
    public string AssetNumber { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public DateOnly AcquisitionDate { get; private set; }
    public decimal AcquisitionCost { get; private set; }
    public decimal SalvageValue { get; private set; }
    public int UsefulLifeMonths { get; private set; }
    public decimal AccumulatedDepreciation { get; private set; }
    public Guid AssetAccountId { get; private set; }
    public Guid DepreciationExpenseAccountId { get; private set; }
    public Guid AccumulatedDepreciationAccountId { get; private set; }
    public DateOnly? DisposedDate { get; private set; }
    public decimal BookValue => AcquisitionCost - AccumulatedDepreciation;
    public static FixedAsset Create(string number, string name, DateOnly date, decimal cost, decimal salvage, int life,
        Guid assetAccount, Guid expenseAccount, Guid accumulatedAccount)
    {
        if (string.IsNullOrWhiteSpace(number) || number.Length > 40 || string.IsNullOrWhiteSpace(name) || name.Length > 150 ||
            date == default || cost <= 0 || salvage < 0 || salvage >= cost || life <= 0 ||
            assetAccount == Guid.Empty || expenseAccount == Guid.Empty || accumulatedAccount == Guid.Empty)
            throw new ArgumentException("Fixed asset details are invalid.");
        return new() { Id = Guid.NewGuid(), IsActive = true, AssetNumber = number.Trim(), Name = name.Trim(), AcquisitionDate = date,
            AcquisitionCost = decimal.Round(cost, 2), SalvageValue = decimal.Round(salvage, 2), UsefulLifeMonths = life,
            AssetAccountId = assetAccount, DepreciationExpenseAccountId = expenseAccount, AccumulatedDepreciationAccountId = accumulatedAccount };
    }
    public decimal Depreciate(int months)
    {
        if (!IsActive || DisposedDate.HasValue || months <= 0) throw new InvalidOperationException("Asset cannot be depreciated for this period.");
        var amount = decimal.Round((AcquisitionCost - SalvageValue) / UsefulLifeMonths * months, 2, MidpointRounding.AwayFromZero);
        amount = Math.Min(amount, BookValue - SalvageValue);
        if (amount <= 0) throw new InvalidOperationException("Asset is fully depreciated.");
        AccumulatedDepreciation += amount;
        return amount;
    }
    public (decimal Proceeds, decimal BookValue) Dispose(DateOnly date, decimal proceeds)
    {
        if (!IsActive || DisposedDate.HasValue || date < AcquisitionDate || proceeds < 0) throw new InvalidOperationException("Asset disposal details are invalid.");
        DisposedDate = date; IsActive = false; return (proceeds, BookValue);
    }
    private FixedAsset() { }
}

public sealed class AccountBudget : AggregateRoot
{
    public Guid AccountId { get; private set; }
    public Guid? DimensionId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public decimal Amount { get; private set; }
    public string? Notes { get; private set; }
    public static AccountBudget Create(Guid accountId, Guid? dimensionId, DateOnly start, DateOnly end, decimal amount, string? notes)
    {
        if (accountId == Guid.Empty || start == default || end < start || amount < 0 || (notes?.Length ?? 0) > 300)
            throw new ArgumentException("Budget details are invalid.");
        return new() { Id = Guid.NewGuid(), IsActive = true, AccountId = accountId, DimensionId = dimensionId,
            StartDate = start, EndDate = end, Amount = decimal.Round(amount, 2), Notes = notes?.Trim() };
    }
    private AccountBudget() { }
}
