using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Persistence;

namespace AccountingInventory.Infrastructure.Security;

internal sealed class RequestIdentity(ICurrentUserProvider user) : IRequestIdentity
{
    public Guid? UserId => user.UserId;
}
