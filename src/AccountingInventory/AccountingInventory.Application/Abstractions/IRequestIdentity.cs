namespace AccountingInventory.Application.Abstractions;

public interface IRequestIdentity
{
    Guid? UserId { get; }
}
