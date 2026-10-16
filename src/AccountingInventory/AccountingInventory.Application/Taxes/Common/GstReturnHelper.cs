using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.Taxes.Common;

/// <summary>
/// Helper utilities for GST return calculations and classifications.
/// </summary>
public static class GstReturnHelper
{
    public const decimal B2CLargeThreshold = 250000.00m;

    /// <summary>
    /// Formats a DateOnly as DD-MM-YYYY required by the GST Portal.
    /// </summary>
    public static string ToGstDateFormat(this DateOnly date) => date.ToString("dd-MM-yyyy");

    /// <summary>
    /// Formats return period code (e.g. MMYYYY, like "102026").
    /// </summary>
    public static string ToGstPeriodFormat(this DateOnly date) => date.ToString("MMyyyy");

    /// <summary>
    /// Checks if a GSTIN is structurally valid (15 alphanumeric characters).
    /// </summary>
    public static bool IsValidGstin(string? gstin)
    {
        if (string.IsNullOrWhiteSpace(gstin)) return false;
        var trimmed = gstin.Trim();
        if (trimmed.Length != 15) return false;
        return trimmed.All(char.IsLetterOrDigit);
    }

    /// <summary>
    /// Extracts 2-digit state code from GSTIN or state name.
    /// </summary>
    public static string ResolveStateCode(string? gstin, string? stateCode, string? stateName)
    {
        var fromGstin = GstStates.ExtractStateCode(gstin);
        if (!string.IsNullOrWhiteSpace(fromGstin)) return fromGstin;

        var fromCode = GstStates.ExtractStateCode(stateCode);
        if (!string.IsNullOrWhiteSpace(fromCode)) return fromCode;

        var fromName = GstStates.ExtractStateCode(stateName);
        if (!string.IsNullOrWhiteSpace(fromName)) return fromName;

        return "97"; // Other Territory / default fallback
    }

    /// <summary>
    /// Maps unit string to official UQC code for GST Portal Table 12.
    /// Standard UQCs: NOS (Numbers), PCS (Pieces), BOX (Box), KGS (Kilograms), MTR (Meters), PAC (Packs), SET (Sets).
    /// </summary>
    public static string NormalizeUqc(string? uom)
    {
        if (string.IsNullOrWhiteSpace(uom)) return "NOS";
        var upper = uom.Trim().ToUpperInvariant();
        return upper switch
        {
            "PIECES" or "PC" or "PCS" => "PCS",
            "NUMBERS" or "NO" or "NOS" => "NOS",
            "BOX" or "BOXES" or "BOX" => "BOX",
            "PACKS" or "PACK" or "PAC" => "PAC",
            "KILOGRAMS" or "KG" or "KGS" => "KGS",
            "METERS" or "METER" or "MTR" => "MTR",
            "SETS" or "SET" => "SET",
            _ => "OTH"
        };
    }

    /// <summary>
    /// Validates the 15-character GSTIN checksum using the official Indian GSTIN Modulo 36 checksum algorithm.
    /// Format: 2 digits (state) + 10 chars (PAN) + 1 char (entity code) + 'Z' + 1 check digit.
    /// </summary>
    public static bool CheckGstinChecksum(string? gstin)
    {
        if (string.IsNullOrWhiteSpace(gstin)) return false;
        var trimmed = gstin.Trim().ToUpperInvariant();
        if (trimmed.Length != 15) return false;

        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        // Check format basics
        for (var i = 0; i < 15; i++)
        {
            if (chars.IndexOf(trimmed[i]) < 0) return false;
        }

        // Mod 36 checksum calculation
        var factor = 2;
        var sum = 0;
        var checkCodePoint = chars.Length; // 36

        for (var i = 13; i >= 0; i--)
        {
            var codePoint = chars.IndexOf(trimmed[i]);
            var addend = factor * codePoint;

            // Factor alternates between 1 and 2
            factor = (factor == 2) ? 1 : 2;

            // Sum the digits of the addend as expressed in base "checkCodePoint"
            addend = (addend / checkCodePoint) + (addend % checkCodePoint);
            sum += addend;
        }

        var remainder = sum % checkCodePoint;
        var checkCode = (checkCodePoint - remainder) % checkCodePoint;
        var calculatedCheckChar = chars[checkCode];

        return calculatedCheckChar == trimmed[14];
    }
}

