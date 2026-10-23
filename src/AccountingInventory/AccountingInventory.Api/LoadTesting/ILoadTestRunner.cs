namespace AccountingInventory.Api.LoadTesting;

public interface ILoadTestRunner
{
    IReadOnlyList<LoadTestScenario> GetScenarios();
    LoadTestRunSummary? GetCurrentTest();
    IReadOnlyList<LoadTestRunSummary> GetHistory();
    LoadTestRunSummary? GetHistoryById(Guid runId);
    bool DeleteHistory(Guid runId);
    void ClearHistory();
    Task<LoadTestRunSummary> StartTestAsync(LoadTestConfig config, CancellationToken cancellationToken = default);
    bool StopActiveTest();
    Task<QuickPingResult> QuickPingAsync(QuickPingRequest request, CancellationToken cancellationToken = default);
}

