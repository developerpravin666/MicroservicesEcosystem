using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class UnitOfMeasure : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Symbol { get; private set; } = null!;
    public bool IsBaseUnit { get; private set; } = true;
    public Guid? BaseUnitId { get; private set; }
    public decimal? ConversionRate { get; private set; }
    public string? Description { get; private set; }

    public static UnitOfMeasure Create(
        string code,
        string name,
        string symbol,
        bool isBaseUnit = true,
        Guid? baseUnitId = null,
        decimal? conversionRate = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Unit code is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Unit name is required.");
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Unit symbol is required.");

        if (!isBaseUnit)
        {
            if (!baseUnitId.HasValue || baseUnitId.Value == Guid.Empty)
                throw new ArgumentException("Alternate unit requires a valid base unit reference.");
            if (!conversionRate.HasValue || conversionRate.Value <= 0)
                throw new ArgumentException("Conversion rate must be positive for alternate units.");
        }

        return new UnitOfMeasure
        {
            Id = Guid.NewGuid(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Symbol = symbol.Trim(),
            IsBaseUnit = isBaseUnit,
            BaseUnitId = isBaseUnit ? null : baseUnitId,
            ConversionRate = isBaseUnit ? null : conversionRate,
            Description = description?.Trim(),
            IsActive = true
        };
    }

    public void Update(
        string name,
        string symbol,
        bool isBaseUnit,
        Guid? baseUnitId,
        decimal? conversionRate,
        string? description,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Unit name is required.");
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Unit symbol is required.");

        if (!isBaseUnit)
        {
            if (!baseUnitId.HasValue || baseUnitId.Value == Guid.Empty)
                throw new ArgumentException("Alternate unit requires a valid base unit reference.");
            if (!conversionRate.HasValue || conversionRate.Value <= 0)
                throw new ArgumentException("Conversion rate must be positive for alternate units.");
        }

        Name = name.Trim();
        Symbol = symbol.Trim();
        IsBaseUnit = isBaseUnit;
        BaseUnitId = isBaseUnit ? null : baseUnitId;
        ConversionRate = isBaseUnit ? null : conversionRate;
        Description = description?.Trim();
        IsActive = isActive;
    }

    private UnitOfMeasure() { }
}

