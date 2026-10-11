namespace AccountingInventory.Domain.Entities;

public enum GstSupplyType
{
    IntraState = 0, // In-state: CGST + SGST
    InterState = 1  // Out-of-state: IGST
}

public static class GstStates
{
    public static readonly IReadOnlyDictionary<string, string> StateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "01", "Jammu and Kashmir" },
        { "02", "Himachal Pradesh" },
        { "03", "Punjab" },
        { "04", "Chandigarh" },
        { "05", "Uttarakhand" },
        { "06", "Haryana" },
        { "07", "Delhi" },
        { "08", "Rajasthan" },
        { "09", "Uttar Pradesh" },
        { "10", "Bihar" },
        { "11", "Sikkim" },
        { "12", "Arunachal Pradesh" },
        { "13", "Nagaland" },
        { "14", "Manipur" },
        { "15", "Mizoram" },
        { "16", "Tripura" },
        { "17", "Meghalaya" },
        { "18", "Assam" },
        { "19", "West Bengal" },
        { "20", "Jharkhand" },
        { "21", "Odisha" },
        { "22", "Chhattisgarh" },
        { "23", "Madhya Pradesh" },
        { "24", "Gujarat" },
        { "26", "Dadra and Nagar Haveli and Daman and Diu" },
        { "27", "Maharashtra" },
        { "29", "Karnataka" },
        { "30", "Goa" },
        { "31", "Lakshadweep" },
        { "32", "Kerala" },
        { "33", "Tamil Nadu" },
        { "34", "Puducherry" },
        { "35", "Andaman and Nicobar Islands" },
        { "36", "Telangana" },
        { "37", "Andhra Pradesh" },
        { "38", "Ladakh" },
        { "97", "Other Territory" }
    };

    /// <summary>Extracts the 2-digit GST state code from a 15-character GSTIN or state name.</summary>
    public static string? ExtractStateCode(string? gstinOrState)
    {
        if (string.IsNullOrWhiteSpace(gstinOrState)) return null;
        var trimmed = gstinOrState.Trim();

        // 1. If starts with 2 digits (e.g. 27AABCU9603R1ZM or "27")
        if (trimmed.Length >= 2 && char.IsDigit(trimmed[0]) && char.IsDigit(trimmed[1]))
        {
            var code = trimmed[..2];
            if (StateMap.ContainsKey(code)) return code;
        }

        // 2. If it's a state name, find matching code
        foreach (var pair in StateMap)
        {
            if (string.Equals(pair.Value, trimmed, StringComparison.OrdinalIgnoreCase))
                return pair.Key;
        }

        return null;
    }

    public static string? GetStateName(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode)) return null;
        return StateMap.TryGetValue(stateCode.Trim(), out var name) ? name : null;
    }

    /// <summary>
    /// Determines whether a transaction is Intra-State (CGST+SGST) or Inter-State (IGST)
    /// based on the supplier's state and place of supply (customer's state).
    /// </summary>
    public static GstSupplyType DetermineSupplyType(string? supplierStateOrGstin, string? customerStateOrGstin)
    {
        var supplierCode = ExtractStateCode(supplierStateOrGstin);
        var customerCode = ExtractStateCode(customerStateOrGstin);

        // If either state is not provided, default to Intra-State (standard retail flow)
        if (string.IsNullOrWhiteSpace(supplierCode) || string.IsNullOrWhiteSpace(customerCode))
            return GstSupplyType.IntraState;

        return string.Equals(supplierCode, customerCode, StringComparison.OrdinalIgnoreCase)
            ? GstSupplyType.IntraState
            : GstSupplyType.InterState;
    }
}

