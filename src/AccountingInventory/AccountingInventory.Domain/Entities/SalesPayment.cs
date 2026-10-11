using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class SalesPayment : AggregateRoot
{
    public Guid SalesProductId { get; private set; }
    public string PaymentMode { get; private set; } = null!;
    public Guid? FinanceVendorId { get; private set; }
    public decimal? DownPayment { get; private set; }
    public int? NumberOfEmi { get; private set; }
    public decimal? EmiAmount { get; private set; }
    public bool? HasInsurance { get; private set; }
    public decimal? InsuranceAmount { get; private set; }
    public DateOnly? FirstInstallmentDate { get; private set; }

    public static SalesPayment Create(Guid salesProductId, string paymentMode, Guid? financeVendorId,
        decimal? downPayment, int? numberOfEmi, decimal? emiAmount, bool? hasInsurance,
        decimal? insuranceAmount, DateOnly? firstInstallmentDate)
    {
        var payment = new SalesPayment { Id = Guid.NewGuid(), IsActive = true };
        payment.Update(salesProductId, paymentMode, financeVendorId, downPayment, numberOfEmi,
            emiAmount, hasInsurance, insuranceAmount, firstInstallmentDate);
        return payment;
    }

    public void Update(Guid salesProductId, string paymentMode, Guid? financeVendorId,
        decimal? downPayment, int? numberOfEmi, decimal? emiAmount, bool? hasInsurance,
        decimal? insuranceAmount, DateOnly? firstInstallmentDate)
    {
        SalesProductId = salesProductId;
        PaymentMode = paymentMode;
        if (paymentMode == "Finance")
        {
            FinanceVendorId = financeVendorId;
            DownPayment = downPayment;
            NumberOfEmi = numberOfEmi;
            EmiAmount = emiAmount;
            HasInsurance = hasInsurance;
            InsuranceAmount = hasInsurance == true ? insuranceAmount : 0m;
            FirstInstallmentDate = firstInstallmentDate;
        }
        else
        {
            FinanceVendorId = null;
            DownPayment = null;
            NumberOfEmi = null;
            EmiAmount = null;
            HasInsurance = null;
            InsuranceAmount = null;
            FirstInstallmentDate = null;
        }
    }

    private SalesPayment() { }
}
