using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes;

public sealed class TaxRateResolver(IReadOnlyList<Tax> versions)
{
    public static async Task<TaxRateResolver> LoadAsync(IAccountingInventoryDbContext db, CancellationToken ct)
        => new(await db.Taxes.AsNoTracking().ToListAsync(ct));
    public Tax? Resolve(Guid? selection, DateOnly taxDate)
    {
        if (!selection.HasValue || selection == Guid.Empty) return null;
        var selected = versions.SingleOrDefault(t => t.Id == selection || t.ProfileId == selection && t.Id == t.ProfileId)
            ?? versions.FirstOrDefault(t => t.ProfileId == selection)
            ?? throw new ConflictException("Selected tax profile does not exist in this business.");
        var rate = versions.Where(t => t.ProfileId == selected.ProfileId && t.EffectiveFrom <= taxDate)
            .OrderByDescending(t => t.EffectiveFrom).FirstOrDefault()
            ?? throw new ConflictException($"No GST rate is configured for '{selected.Name}' on {taxDate}.");
        if (!rate.IsActive) throw new ConflictException($"The GST rate for '{selected.Name}' on {taxDate} is inactive.");
        return rate;
    }
    public Guid? ProfileId(Guid? selection) => selection.HasValue && selection != Guid.Empty
        ? versions.FirstOrDefault(t => t.Id == selection || t.ProfileId == selection)?.ProfileId
            ?? throw new ConflictException("Selected tax profile does not exist in this business.") : null;
}
