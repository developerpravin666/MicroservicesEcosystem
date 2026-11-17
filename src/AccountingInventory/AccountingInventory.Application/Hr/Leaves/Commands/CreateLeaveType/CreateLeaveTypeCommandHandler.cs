using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Leaves.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Leaves.Commands.CreateLeaveType;

public sealed class CreateLeaveTypeCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<CreateLeaveTypeCommandHandler> logger)
    : IRequestHandler<CreateLeaveTypeCommand, LeaveTypeDto>
{
    public async Task<LeaveTypeDto> Handle(CreateLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await db.LeaveTypes.AnyAsync(lt => lt.Code == code && !lt.IsDeleted, cancellationToken);
        if (exists)
        {
            logger.LogWarning("Leave type code '{Code}' already exists.", code);
            throw new ConflictException($"Leave type code '{code}' already exists.");
        }

        var lt = LeaveType.Create(request.Name, request.Code, request.DaysAllowedPerYear, request.IsPaid, request.IsCarryForward, request.Description);
        db.LeaveTypes.Add(lt);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Leave type '{Code}' ({Name}) created.", lt.Code, lt.Name);
        return new LeaveTypeDto(lt.Id, lt.Name, lt.Code, lt.DaysAllowedPerYear, lt.IsPaid, lt.IsCarryForward, lt.Description, lt.IsActive);
    }
}
