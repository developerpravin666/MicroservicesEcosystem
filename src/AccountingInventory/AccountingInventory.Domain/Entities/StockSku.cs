using BuildingBlocks.Domain;
namespace AccountingInventory.Domain.Entities;
public sealed class StockSku : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string HsnSac { get; private set; } = null!;
    public string UnitOfMeasure { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal InventoryValue { get; private set; }
    public DateOnly? LastMovementDate { get; private set; }
    public static StockSku Create(string code, string name, string hsn, string unit)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 40 || string.IsNullOrWhiteSpace(name) || name.Length > 200
            || !System.Text.RegularExpressions.Regex.IsMatch(hsn ?? "", @"^(\d{4}|\d{6}|\d{8})$")
            || string.IsNullOrWhiteSpace(unit) || unit.Length > 10) throw new ArgumentException("Provide SKU code, name, HSN and unit.");
        return new() { Id = Guid.NewGuid(), IsActive = true, Code = code.Trim().ToUpperInvariant(), Name = name.Trim(), HsnSac = hsn!, UnitOfMeasure = unit.Trim().ToUpperInvariant() };
    }
    public void StageOpening() { IsActive=false; }
    public void ActivateOpening() { IsActive=true; }
    public void Receive(decimal quantity, decimal cost, DateOnly date)
    {
        Validate(quantity, date);
        if (cost < 0 || decimal.Round(cost, 2) != cost) throw new ArgumentException("Stock cost must be nonnegative with two decimals.");
        Quantity += quantity; InventoryValue += cost; LastMovementDate = date;
    }
    public void RecordDiscardedReturn(decimal quantity, DateOnly date) { Validate(quantity,date); LastMovementDate=date; }
    public decimal Issue(decimal quantity, DateOnly date)
    {
        Validate(quantity, date);
        if (quantity > Quantity) throw new ArgumentException("Insufficient SKU stock.");
        var cost = quantity == Quantity ? InventoryValue : decimal.Round(InventoryValue * quantity / Quantity, 2, MidpointRounding.AwayFromZero);
        Quantity -= quantity; InventoryValue -= cost; LastMovementDate = date; return cost;
    }
    private void Validate(decimal quantity, DateOnly date)
    {
        if (!IsActive || date == default || quantity <= 0 || decimal.Round(quantity, 4) != quantity)
            throw new ArgumentException("Active SKU, valid date and positive quantity with up to four decimals are required.");
        if (LastMovementDate.HasValue && date < LastMovementDate.Value)
            throw new ArgumentException("SKU movements cannot predate the latest movement. Use chronological stock dates.");
    }
}
public sealed class SkuMovement : AggregateRoot
{
    public Guid SkuId { get; private set; }
    public DateOnly MovementDate { get; private set; }
    public string Kind { get; private set; } = null!;
    public Guid SourceId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal InventoryValue { get; private set; }
    public Guid? VendorId { get; private set; }
    public string? BillNumber { get; private set; }
    public DateOnly DueDate { get; private set; }
    public int PaymentTermsDays { get; private set; }
    public decimal CgstRate { get; private set; }
    public decimal SgstRate { get; private set; }
    public decimal IgstRate { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Description { get; private set; } = null!;
    public static SkuMovement Create(Guid sku, DateOnly date, string kind, Guid source, decimal quantity, decimal value, string description,
        Guid? vendor = null, string? bill = null, int terms = 0, decimal cgst = 0, decimal sgst = 0, decimal igst = 0)
    {
        var c = decimal.Round(value * cgst / 100, 2, MidpointRounding.AwayFromZero);
        var s = decimal.Round(value * sgst / 100, 2, MidpointRounding.AwayFromZero);
        var i = decimal.Round(value * igst / 100, 2, MidpointRounding.AwayFromZero);
        return new() { Id = Guid.NewGuid(), IsActive = true, SkuId = sku, MovementDate = date, Kind = kind, SourceId = source,
            Quantity = quantity, InventoryValue = value, Description = description, VendorId = vendor, BillNumber = bill,
            PaymentTermsDays = terms, DueDate = date.AddDays(terms), CgstRate = cgst, SgstRate = sgst, IgstRate = igst,
            CgstAmount = c, SgstAmount = s, IgstAmount = i, TotalAmount = value + c + s + i };
    }
}
