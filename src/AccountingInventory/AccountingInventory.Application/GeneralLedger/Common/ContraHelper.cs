using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.GeneralLedger.Common;

public static class ContraHelper
{
    public static bool IsCashOrBankAccount(ChartAccount account)
    {
        if (account.Code is "1000" or "1010") return true;
        var name = account.Name.ToLowerInvariant();
        if (name.Contains("cash") || name.Contains("bank") || name.Contains("petty")) return true;
        if (account.Code.StartsWith("10")) return true;
        return false;
    }
}

