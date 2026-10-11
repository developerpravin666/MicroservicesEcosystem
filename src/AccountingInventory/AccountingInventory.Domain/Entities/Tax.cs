using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class Tax : AggregateRoot
{
    public decimal Cgst { get; private set; }
    public decimal Sgst { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal Igst => TotalTax;

    public static Tax Create(decimal cgst, decimal sgst)
        => new() { Id = Guid.NewGuid(), IsActive = true, Cgst = cgst, Sgst = sgst, TotalTax = cgst + sgst };

    public void Update(decimal cgst, decimal sgst, bool isActive)
    {
        Cgst = cgst;
        Sgst = sgst;
        TotalTax = cgst + sgst;
        IsActive = isActive;
    }

    public void Delete() { IsDeleted = true; IsActive = false; }

    private Tax() { }
}
