using System.Text.Json.Serialization;

namespace AccountingInventory.Api.LoadTesting;

public enum LoadTestStatus
{
    Idle,
    Running,
    Completed,
    Aborted,
    Failed
}

public sealed record LoadTestScenario(
    string Id,
    string Name,
    string Description,
    string Category,
    string DefaultEndpoint,
    string Method,
    int DefaultVUs,
    int DefaultDurationSeconds,
    int DefaultRampUpSeconds,
    bool RequiresAuth,
    bool RequiresTenant);

public sealed class LoadTestConfig
{
    public Guid RunId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Ad-hoc Load Test";
    public string ScenarioId { get; set; } = "custom";
    public string TargetEndpoint { get; set; } = "/health";
    public string TargetBaseUrl { get; set; } = "";
    public string Method { get; set; } = "GET";
    public int VirtualUsers { get; set; } = 20;
    public int DurationSeconds { get; set; } = 15;
    public int RampUpSeconds { get; set; } = 3;
    public Guid? TenantId { get; set; }
    public string? AuthToken { get; set; }
    public Dictionary<string, string>? CustomHeaders { get; set; }
    public string? RequestBody { get; set; }
    public bool BypassRateLimiter { get; set; } = true;
}

public sealed class LoadTestTelemetryPoint
{
    public DateTimeOffset TimestampUtc { get; set; }
    public int Second { get; set; }
    public int ActiveVirtualUsers { get; set; }
    public int RequestsInSecond { get; set; }
    public int SuccessInSecond { get; set; }
    public int ErrorsInSecond { get; set; }
    public double AvgLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double CpuPercentage { get; set; }
    public double MemoryMb { get; set; }
}

public sealed class LoadTestRunSummary
{
    public Guid RunId { get; set; }
    public string ScenarioName { get; set; } = string.Empty;
    public string TargetEndpoint { get; set; } = string.Empty;
    public string TargetBaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LoadTestStatus Status { get; set; } = LoadTestStatus.Idle;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public int ConfiguredDurationSeconds { get; set; }
    public int ElapsedSeconds { get; set; }
    public int ConfiguredVUs { get; set; }
    public int CurrentActiveVUs { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double SuccessRate { get; set; }
    public double CurrentRps { get; set; }
    public double AverageRps { get; set; }
    public double PeakRps { get; set; }
    public double AvgLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double P50LatencyMs { get; set; }
    public double P90LatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public long TotalBytesReceived { get; set; }
    public Dictionary<int, int> StatusCodes { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<LoadTestTelemetryPoint> Telemetry { get; set; } = new();
}

public sealed class QuickPingRequest
{
    public string TargetEndpoint { get; set; } = "/health";
    public string TargetBaseUrl { get; set; } = "";
    public Guid? TenantId { get; set; }
    public string? AuthToken { get; set; }
    public bool BypassRateLimiter { get; set; } = true;
}

public sealed class QuickPingResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public double LatencyMs { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
}

