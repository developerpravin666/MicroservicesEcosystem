namespace AccountingInventory.Application.GeneralLedger.Common;

public sealed record AutoMatchResultDto(
    int MatchedCount,
    decimal TotalMatchedAmount,
    IReadOnlyList<string> MatchDetails);

