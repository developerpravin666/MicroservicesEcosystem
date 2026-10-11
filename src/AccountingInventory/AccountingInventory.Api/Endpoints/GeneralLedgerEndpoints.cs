using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class GeneralLedgerEndpoints
{
    public static RouteGroupBuilder MapGeneralLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/general-ledger")
            .WithTags("General Ledger")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapGet("/accounts", async ([FromQuery] bool? activeOnly, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetLedgerAccountsQuery(activeOnly ?? true), ct)))
            .WithName("GetLedgerAccounts").Produces<IReadOnlyList<LedgerAccountSummary>>();

        group.MapPost("/accounts", async (CreateLedgerAccountCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/general-ledger/accounts", await sender.Send(command, ct)))
            .WithName("CreateLedgerAccount");

        group.MapPost("/accounts/initialize", async (ISender sender, CancellationToken ct) =>
            Results.Ok(new { created = await sender.Send(new InitializeChartOfAccountsCommand(), ct) }))
            .WithName("InitializeChartOfAccounts");

        group.MapPost("/journals", async (PostJournalCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/general-ledger/journals", await sender.Send(command, ct)))
            .WithName("PostJournal");

        group.MapPost("/journals/{id:guid}/reverse", async (Guid id, ReverseJournalRequest request,
                ISender sender, CancellationToken ct) =>
            Results.Created("/api/general-ledger/journals",
                await sender.Send(new ReverseJournalCommand(id, request.ReversalDate, request.Reason), ct)))
            .WithName("ReverseJournal");

        group.MapGet("/journals", async ([FromQuery] int? page, [FromQuery] int? pageSize,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetJournalsQuery(page ?? 1, pageSize ?? 20), ct)))
            .WithName("GetJournals").Produces<PagedResult<JournalSummary>>();

        group.MapGet("/trial-balance", async ([FromQuery] DateOnly? asOf, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetTrialBalanceQuery(asOf), ct)))
            .WithName("GetTrialBalance").Produces<TrialBalanceSummary>();

        group.MapGet("/financial-statements", async ([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetFinancialStatementsQuery(from, to), ct)))
            .WithName("GetFinancialStatements").Produces<FinancialStatementsSummary>();

        group.MapGet("/cash-flow", async ([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetCashFlowStatementQuery(from, to), ct)))
            .WithName("GetCashFlowStatement").Produces<CashFlowStatementSummary>();

        group.MapPost("/opening-balances", async (ImportOpeningBalancesCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/general-ledger/journals", await sender.Send(command, ct)))
            .WithName("ImportOpeningBalances");

        group.MapGet("/aging", async ([FromQuery] DateOnly? asOfDate, [FromQuery] int? page,
                [FromQuery] int? pageSize, [FromQuery] string? search, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetAgingReportQuery(asOfDate, page ?? 1, pageSize ?? 20, search), ct)))
            .WithName("GetReceivablesPayablesAging").Produces<AgingReportSummary>();

        group.MapGet("/tax-report", async ([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetTaxReportQuery(from, to), ct)))
            .WithName("GetTaxReport").Produces<TaxReportSummary>();

        group.MapGet("/party-statement", async (
                [FromQuery] string partyType,
                [FromQuery] Guid partyId,
                [FromQuery] DateOnly? from,
                [FromQuery] DateOnly? to,
                ISender sender,
                CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetPartyStatementQuery(partyType, partyId, from, to), ct)))
            .WithName("GetPartyStatement").Produces<PartyStatementOfAccountDto>();

        var periods = app.MapGroup("/api/accounting-periods")
            .WithTags("Accounting Periods")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());
        periods.MapGet("", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetAccountingPeriodsQuery(), ct)))
            .WithName("GetAccountingPeriods").Produces<IReadOnlyList<AccountingPeriodSummary>>();
        periods.MapPost("", async (CreateAccountingPeriodCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/accounting-periods", await sender.Send(command, ct)))
            .WithName("CreateAccountingPeriod");
        periods.MapPost("/{id:guid}/close", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new CloseAccountingPeriodCommand(id), ct)))
            .WithName("CloseAccountingPeriod");

        var reconciliation = app.MapGroup("/api/bank-reconciliations")
            .WithTags("Bank Reconciliation")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());
        reconciliation.MapGet("", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetBankReconciliationsQuery(), ct)))
            .WithName("GetBankReconciliations");
        reconciliation.MapPost("", async (CreateBankReconciliationCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/bank-reconciliations", await sender.Send(command, ct)))
            .WithName("CreateBankReconciliation");
        reconciliation.MapGet("/{id:guid}/unmatched-ledger", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetUnmatchedBankJournalLinesQuery(id), ct)))
            .WithName("GetUnmatchedBankJournalLines");
        reconciliation.MapPost("/{id:guid}/match", async (Guid id, MatchBankReconciliationRequest request, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new MatchBankStatementLineCommand(id, request.StatementLineId, request.JournalLineId), ct)))
            .WithName("MatchBankStatementLine");
        reconciliation.MapPost("/{id:guid}/finalize", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new FinalizeBankReconciliationCommand(id), ct)))
            .WithName("FinalizeBankReconciliation");

        return group;
    }
}

public sealed record ReverseJournalRequest(DateOnly ReversalDate, string? Reason = null);
public sealed record MatchBankReconciliationRequest(Guid StatementLineId, Guid JournalLineId);
