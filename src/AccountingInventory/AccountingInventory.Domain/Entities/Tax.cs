using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

/// <summary>A dated, immutable rate version belonging to a stable tax profile.</summary>
public sealed class Tax : AggregateRoot
{
    public Guid ProfileId { get; private set; }
    public string Name { get; private set; } = "GST";
    public DateOnly EffectiveFrom { get; private set; } = DateOnly.MinValue;
    public string Treatment { get; private set; } = "Taxable";
    public decimal Cgst { get; private set; }
    public decimal Sgst { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal Igst => TotalTax;
    public static readonly string[] Treatments = ["Taxable", "Exempt", "NilRated", "NonGst", "ZeroRated"];

    public static Tax Create(decimal cgst, decimal sgst, string? name = null, DateOnly? effectiveFrom = null,
        Guid? profileId = null, string treatment = "Taxable")
    {
        if (cgst < 0 || sgst < 0 || cgst + sgst > 100 || decimal.Round(cgst, 2) != cgst || decimal.Round(sgst, 2) != sgst)
            throw new ArgumentException("GST rates must total no more than 100% and have at most two decimal places.");
        if (!Treatments.Contains(treatment) || (treatment != "Taxable" && cgst + sgst != 0))
            throw new ArgumentException("Non-taxable treatments require zero rates.");
        var id = Guid.NewGuid();
        return new() { Id = id, ProfileId = profileId ?? id, Name = string.IsNullOrWhiteSpace(name) ? "GST" : name.Trim(),
            EffectiveFrom = effectiveFrom ?? DateOnly.MinValue, Treatment = treatment, IsActive = true,
            Cgst = cgst, Sgst = sgst, TotalTax = cgst + sgst };
    }
    public void Update(decimal cgst, decimal sgst, bool isActive)
    {
        if (cgst != Cgst || sgst != Sgst) throw new ArgumentException("Add a new dated rate version instead of changing an existing GST rate.");
        IsActive = isActive;
    }
    public void Rename(string name) { if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new ArgumentException("Tax profile name is required (maximum 100 characters)."); Name = name.Trim(); }
    public void Delete() { IsDeleted = true; IsActive = false; }
    private Tax() { }
}
