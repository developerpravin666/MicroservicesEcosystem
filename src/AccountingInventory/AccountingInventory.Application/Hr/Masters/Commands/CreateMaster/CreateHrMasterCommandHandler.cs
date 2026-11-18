using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Masters.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Masters.Commands.CreateMaster;

public sealed class CreateHrMasterCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<CreateHrMasterCommandHandler> logger)
    : IRequestHandler<CreateHrMasterCommand, HrMasterItemDto>
{
    public async Task<HrMasterItemDto> Handle(CreateHrMasterCommand request, CancellationToken cancellationToken)
    {
        var category = request.Category.Trim().ToUpperInvariant();
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();

        var codeExists = await db.HrMasterItems.AnyAsync(
            m => m.Category == category && m.Code == code && !m.IsDeleted,
            cancellationToken);

        if (codeExists)
        {
            logger.LogWarning("Master item '{Code}' already exists in category '{Category}'.", code, category);
            throw new ConflictException($"Master item with code '{code}' already exists in category '{category}'.");
        }

        var nameExists = await db.HrMasterItems.AnyAsync(
            m => m.Category == category && m.Name.ToLower() == name.ToLower() && !m.IsDeleted,
            cancellationToken);

        if (nameExists)
        {
            logger.LogWarning("Master item with name '{Name}' already exists in category '{Category}'.", name, category);
            throw new ConflictException($"A master item with name '{name}' already exists in category '{category}'.");
        }

        var item = HrMasterItem.Create(category, name, code, request.Description, request.IsDefault);
        db.HrMasterItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Master item '{Code}' ({Name}) created under '{Category}'.", item.Code, item.Name, item.Category);

        return new HrMasterItemDto(item.Id, item.Category, item.Name, item.Code, item.Description, item.IsDefault, item.IsActive);
    }
}
