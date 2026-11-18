using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Masters.Models;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Masters.Commands.UpdateMaster;

public sealed class UpdateHrMasterCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<UpdateHrMasterCommandHandler> logger)
    : IRequestHandler<UpdateHrMasterCommand, HrMasterItemDto>
{
    public async Task<HrMasterItemDto> Handle(UpdateHrMasterCommand request, CancellationToken cancellationToken)
    {
        var item = await db.HrMasterItems.FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted, cancellationToken)
            ?? throw new NotFoundException($"Master item with ID '{request.Id}' was not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Master name is required.");

        var trimmedName = request.Name.Trim();

        // Validate duplicate name within the same category
        var nameExists = await db.HrMasterItems.AnyAsync(
            m => m.Category == item.Category && m.Id != item.Id && m.Name.ToLower() == trimmedName.ToLower() && !m.IsDeleted,
            cancellationToken);

        if (nameExists)
        {
            logger.LogWarning("Master item with name '{Name}' already exists in category '{Category}'.", trimmedName, item.Category);
            throw new ConflictException($"A master item with name '{trimmedName}' already exists in category '{item.Category}'.");
        }

        item.Update(trimmedName, request.Description, request.IsDefault, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Master item '{Id}' ({Name}) updated successfully.", item.Id, item.Name);

        return new HrMasterItemDto(item.Id, item.Category, item.Name, item.Code, item.Description, item.IsDefault, item.IsActive);
    }
}
