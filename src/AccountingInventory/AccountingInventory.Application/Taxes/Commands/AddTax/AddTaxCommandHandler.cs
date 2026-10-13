using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Taxes;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application.Exceptions;

namespace AccountingInventory.Application.Taxes.Commands.AddTax;

public sealed class AddTaxCommandHandler(IAccountingInventoryDbContext db) : IRequestHandler<AddTaxCommand, TaxSummary>
{
    public async Task<TaxSummary> Handle(AddTaxCommand request, CancellationToken cancellationToken)
    {
        var resolver = await TaxRateResolver.LoadAsync(db, cancellationToken);
        var profileId = resolver.ProfileId(request.ProfileId);
        var date = request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (profileId.HasValue && await db.Taxes.AnyAsync(t => t.ProfileId == profileId && t.EffectiveFrom == date, cancellationToken))
            throw new ConflictException("A rate version already exists for this profile and effective date.");
        var tax = Tax.Create(request.Cgst, request.Sgst, request.Name, date, profileId, request.Treatment);
        db.Taxes.Add(tax);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException e) when (e.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        { throw new ConflictException("A rate version already exists for this profile and effective date."); }
        return new(tax.Id, tax.Cgst, tax.Sgst, tax.TotalTax, tax.IsActive, tax.CreatedAt, tax.CreatedBy, tax.ModifiedAt, tax.ModifiedBy, tax.Name, tax.ProfileId, tax.EffectiveFrom, tax.Treatment);
    }
}
