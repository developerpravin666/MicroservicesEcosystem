using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record OpeningBalanceLineInput(Guid AccountId, decimal Debit, decimal Credit, string? Memo = null);
public sealed record OpeningPartyInput(string Kind, Guid PartyId, DateOnly DueDate, string Reference, decimal Amount);
public sealed record ImportOpeningBalancesCommand(DateOnly CutoverDate, IReadOnlyCollection<OpeningBalanceLineInput> Lines,
    IReadOnlyCollection<OpeningPartyInput>? Parties = null, IReadOnlyCollection<Guid>? StockProductIds = null)
    : IRequest<JournalSummary>;

public sealed class ImportOpeningBalancesValidator : AbstractValidator<ImportOpeningBalancesCommand>
{
    public ImportOpeningBalancesValidator()
    {
        RuleFor(command => command.CutoverDate).NotEmpty();
        RuleForEach(command => command.Parties).ChildRules(party => {
            party.RuleFor(x => x.Kind).Must(x => x is "Customer" or "Vendor");
            party.RuleFor(x => x.PartyId).NotEmpty(); party.RuleFor(x => x.DueDate).NotEmpty();
            party.RuleFor(x => x.Reference).NotEmpty().MaximumLength(100);
            party.RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18,2,true);
        });
        RuleFor(command => command.Lines).NotNull().Must(lines => lines.Count > 0)
            .WithMessage("Add at least one opening account balance.");
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(item => item.AccountId).NotEmpty();
            line.RuleFor(item => item.Debit).GreaterThanOrEqualTo(0);
            line.RuleFor(item => item.Credit).GreaterThanOrEqualTo(0);
            line.RuleFor(item => item.Memo).MaximumLength(250);
        });
        RuleFor(command => command).Must(command => command.Lines != null && command.Lines.All(line =>
            (decimal.Round(line.Debit, 2, MidpointRounding.AwayFromZero) > 0)
            != (decimal.Round(line.Credit, 2, MidpointRounding.AwayFromZero) > 0)))
            .WithMessage("Each opening balance must have a positive debit or credit amount.");
    }
}

/// <summary>Imports balance-sheet opening balances and posts the difference to retained earnings.</summary>
public sealed class ImportOpeningBalancesHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<ImportOpeningBalancesCommand, JournalSummary>
{
    public async Task<JournalSummary> Handle(ImportOpeningBalancesCommand request, CancellationToken ct)
    {
        var sourceId = request.CutoverDate.ToString("yyyy-MM-dd");
        if (await db.JournalEntries.AnyAsync(entry => entry.SourceType == "OpeningBalances", ct))
            throw new ConflictException("Opening balances have already been imported. Use an approved correction for subsequent changes.");
        if (await db.JournalEntries.AnyAsync(entry => entry.JournalDate <= request.CutoverDate, ct))
            throw new ConflictException("Cutover import requires no existing journals on/before the cutover date, to prevent double-counting.");
        await LedgerPosting.EnsurePeriodOpenAsync(db, request.CutoverDate, ct);

        var rounded = request.Lines.Select(line => (line.AccountId,
            Debit: decimal.Round(line.Debit, 2, MidpointRounding.AwayFromZero),
            Credit: decimal.Round(line.Credit, 2, MidpointRounding.AwayFromZero),
            Memo: string.IsNullOrWhiteSpace(line.Memo) ? null : line.Memo.Trim())).ToArray();
        var ids = rounded.Select(line => line.AccountId).Append(Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
        var accounts = await db.ChartAccounts.AsNoTracking().Where(account => ids.Contains(account.Id) && account.IsActive)
            .ToDictionaryAsync(account => account.Id, ct);
        if (accounts.Count != ids.Length) throw new ConflictException("Every opening balance must reference an active account.");
        if (accounts.Values.Any(account => account.Type is LedgerAccountType.Revenue or LedgerAccountType.Expense))
            throw new ConflictException("Opening balances must use balance-sheet accounts; import prior earnings to retained earnings.");
        var parties = request.Parties ?? [];
        if (parties.GroupBy(x => new { x.Kind, x.PartyId, Reference = x.Reference.Trim().ToLower() }).Any(x => x.Count() > 1))
            throw new ConflictException("Opening references must be unique for each party.");
        foreach (var item in parties)
        {
            _ = OpeningSubledgerBalance.Create(Guid.NewGuid(), item.Kind, item.PartyId, request.CutoverDate, item.DueDate, item.Reference, item.Amount);
            var exists = item.Kind == "Customer" ? await db.Customers.AnyAsync(x => x.Id == item.PartyId && x.IsActive, ct)
                : await db.Vendors.AnyAsync(x => x.Id == item.PartyId && x.IsActive, ct);
            if (!exists) throw new ConflictException("Opening balances require active customers/vendors in this tenant.");
        }
        decimal Net(string code) => rounded.Where(x => accounts[x.AccountId].Code == code).Sum(x => x.Debit - x.Credit);
        if (Net("1100") != parties.Where(x => x.Kind == "Customer").Sum(x => x.Amount)
            || -Net("2000") != parties.Where(x => x.Kind == "Vendor").Sum(x => x.Amount))
            throw new ConflictException("Customer/vendor opening items must exactly match accounts 1100/2000.");
        var stockIds = (request.StockProductIds ?? []).Distinct().ToArray();
        if (await db.Products.AnyAsync(x => x.IsOpeningStock && !x.IsActive && x.PurchaseDate > request.CutoverDate, ct))
            throw new ConflictException("All staged opening stock purchase dates must be on/before cutover.");
        var stock = await db.Products.Where(x => stockIds.Contains(x.Id) && (x.IsActive || x.IsOpeningStock) && !x.IsSold && x.PurchaseDate <= request.CutoverDate).ToListAsync(ct);
        var openingSkus = await db.StockSkus.Where(s => !s.IsActive && db.SkuMovements.Any(m => m.SkuId == s.Id && m.Kind == "OpeningStock")).ToListAsync(ct);
        if(openingSkus.Any(s => s.LastMovementDate > request.CutoverDate)) throw new ConflictException("Opening SKU stock dates must be on/before cutover.");
        if (stock.Count != stockIds.Length || Net("1200") != openingSkus.Sum(s => s.InventoryValue) + stock.Sum(x => decimal.Round(x.PurchasePrice - x.Discount, 2, MidpointRounding.AwayFromZero)))
            throw new ConflictException("Opening stock must be available at cutover and exactly match the net cost in account 1200.");
        var existingStock = await db.Products.Where(x => (x.IsActive || x.IsOpeningStock) && !x.IsSold && x.PurchaseDate <= request.CutoverDate).Select(x => x.Id).ToArrayAsync(ct);
        if (existingStock.Except(stockIds).Any()) throw new ConflictException("Include every available stock unit at cutover in the opening reconciliation.");

        var debit = rounded.Sum(line => line.Debit);
        var credit = rounded.Sum(line => line.Credit);
        var difference = decimal.Round(debit - credit, 2, MidpointRounding.AwayFromZero);
        if (difference != 0)
        {
            var retained = await db.ChartAccounts.SingleOrDefaultAsync(account => account.Code == "3100" && account.IsActive, ct)
                ?? throw new ConflictException("Initialize the chart of accounts first; retained earnings account 3100 is required.");
            rounded = rounded.Append((retained.Id, Debit: difference < 0 ? -difference : 0m,
                Credit: difference > 0 ? difference : 0m, Memo: (string?)"Opening balance offset")).ToArray();
            accounts[retained.Id] = retained;
        }

        var entry = JournalEntry.Post(request.CutoverDate, "Opening balance cutover", "OpeningBalances", sourceId,
            rounded.Select(line => (line.AccountId, line.Debit, line.Credit, line.Memo)).ToArray());
        db.JournalEntries.Add(entry);
        db.JournalLines.AddRange(entry.Lines);
        foreach(var sku in openingSkus) sku.ActivateOpening();
        foreach (var product in stock) product.ActivateOpeningStock();
        db.OpeningSubledgerBalances.AddRange(parties.Select(x => OpeningSubledgerBalance.Create(entry.Id, x.Kind,
            x.PartyId, request.CutoverDate, x.DueDate, x.Reference.ToLowerInvariant(), x.Amount)));
        await db.SaveChangesAsync(ct);
        var result = entry.Lines.Select(line => new JournalLineSummary(line.AccountId, accounts[line.AccountId].Code,
            accounts[line.AccountId].Name, line.Debit, line.Credit, line.Memo)).ToArray();
        return new(entry.Id, entry.JournalNumber, entry.JournalDate, entry.Description, entry.SourceType,
            entry.SourceId, entry.PostedAt, null, false, result, result.Sum(line => line.Debit), result.Sum(line => line.Credit));
    }
}
