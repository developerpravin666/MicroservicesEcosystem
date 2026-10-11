namespace AccountingInventory.Domain.Entities;

public enum TenantStatus
{
    PendingProvisioning = 0,
    Active = 1,
    Suspended = 2,
    PendingApproval = 3,
    Rejected = 4,
}
