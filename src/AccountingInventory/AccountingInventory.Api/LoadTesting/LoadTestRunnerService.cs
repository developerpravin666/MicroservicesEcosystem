using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AccountingInventory.Api.LoadTesting;

public sealed class LoadTestRunnerService : ILoadTestRunner, IDisposable
{
    private readonly ILogger<LoadTestRunnerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _historyFilePath;
    private readonly object _lock = new();

    private CancellationTokenSource? _activeCts;
    private LoadTestRunSummary? _currentTest;
    private Task? _runningTask;
    private readonly List<LoadTestRunSummary> _history = new();

    public LoadTestRunnerService(ILogger<LoadTestRunnerService> logger, IConfiguration configuration, IWebHostEnvironment env)
    {
        _logger = logger;
        _configuration = configuration;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 2000,
            EnableMultipleHttp2Connections = true,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        var logsDir = Path.Combine(env.ContentRootPath, "logs");
        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }
        _historyFilePath = Path.Combine(logsDir, "load_tests_history.json");

        LoadHistoryFromDisk();
    }

    public IReadOnlyList<LoadTestScenario> GetScenarios()
    {
        return
        [
            new(
                Id: "health-baseline",
                Name: "Health Check Baseline",
                Description: "Tests raw Kestrel HTTP pipeline and reverse proxy routing without database load.",
                Category: "Baseline",
                DefaultEndpoint: "/health",
                Method: "GET",
                DefaultVUs: 25,
                DefaultDurationSeconds: 15,
                DefaultRampUpSeconds: 2,
                RequiresAuth: false,
                RequiresTenant: false),

            new(
                Id: "platform-metrics",
                Name: "Platform KPI Metrics",
                Description: "Measures Tenant Directory database queries and KPI aggregation under concurrent requests.",
                Category: "Multi-Tenant",
                DefaultEndpoint: "/api/admin/metrics",
                Method: "GET",
                DefaultVUs: 30,
                DefaultDurationSeconds: 20,
                DefaultRampUpSeconds: 3,
                RequiresAuth: false,
                RequiresTenant: false),

            new(
                Id: "audit-modules-query",
                Name: "Audit Modules (Schema Isolation)",
                Description: "Benchmarks multi-tenant PostgreSQL schema resolution and dynamic table count queries.",
                Category: "Multi-Tenant",
                DefaultEndpoint: "/api/audit-logs/modules",
                Method: "GET",
                DefaultVUs: 20,
                DefaultDurationSeconds: 20,
                DefaultRampUpSeconds: 3,
                RequiresAuth: true,
                RequiresTenant: true),

            new(
                Id: "product-catalog-read",
                Name: "Product Catalog Search & Read",
                Description: "Simulates store operators concurrently querying inventory product catalogs with pagination.",
                Category: "Inventory",
                DefaultEndpoint: "/api/products?page=1&pageSize=20",
                Method: "GET",
                DefaultVUs: 25,
                DefaultDurationSeconds: 30,
                DefaultRampUpSeconds: 5,
                RequiresAuth: true,
                RequiresTenant: true),

            new(
                Id: "high-concurrency-stress",
                Name: "High Concurrency Peak Stress",
                Description: "Pushes 100 concurrent virtual users through the system to test thread pools & connection limits.",
                Category: "Stress",
                DefaultEndpoint: "/health",
                Method: "GET",
                DefaultVUs: 100,
                DefaultDurationSeconds: 30,
                DefaultRampUpSeconds: 5,
                RequiresAuth: false,
                RequiresTenant: false),

            new(
                Id: "custom",
                Name: "Custom User Scenario",
                Description: "User-defined HTTP endpoint, payload, headers, concurrency, and duration.",
                Category: "Custom",
                DefaultEndpoint: "/health",
                Method: "GET",
                DefaultVUs: 20,
                DefaultDurationSeconds: 15,
                DefaultRampUpSeconds: 3,
                RequiresAuth: false,
                RequiresTenant: false)
        ];
    }

    public LoadTestRunSummary? GetCurrentTest()
    {
        lock (_lock)
        {
            return _currentTest;
        }
    }

    public IReadOnlyList<LoadTestRunSummary> GetHistory()
    {
        lock (_lock)
        {
            return _history.OrderByDescending(h => h.StartedAtUtc).ToList();
        }
    }

    public LoadTestRunSummary? GetHistoryById(Guid runId)
    {
        lock (_lock)
        {
            return _history.FirstOrDefault(h => h.RunId == runId);
        }
    }

    public bool DeleteHistory(Guid runId)
    {
        lock (_lock)
        {
            var item = _history.FirstOrDefault(h => h.RunId == runId);
            if (item is null) return false;
            _history.Remove(item);
            SaveHistoryToDisk();
            return true;
        }
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
            SaveHistoryToDisk();
        }
    }

    public Task<LoadTestRunSummary> StartTestAsync(LoadTestConfig config, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_currentTest is not null && _currentTest.Status == LoadTestStatus.Running)
            {
                throw new InvalidOperationException("A load test is already in progress. Stop the current run before starting another.");
            }

            // Fallback base URL resolution
            var resolvedBaseUrl = !string.IsNullOrWhiteSpace(config.TargetBaseUrl)
                ? config.TargetBaseUrl.TrimEnd('/')
                : "https://localhost:64115"; // Default via ApiGateway

            var summary = new LoadTestRunSummary
            {
                RunId = config.RunId,
                ScenarioName = string.IsNullOrWhiteSpace(config.Name) ? "Load Test" : config.Name,
                TargetEndpoint = config.TargetEndpoint,
                TargetBaseUrl = resolvedBaseUrl,
                Method = config.Method.ToUpperInvariant(),
                Status = LoadTestStatus.Running,
                StartedAtUtc = DateTimeOffset.UtcNow,
                ConfiguredDurationSeconds = Math.Clamp(config.DurationSeconds, 5, 300),
                ConfiguredVUs = Math.Clamp(config.VirtualUsers, 1, 500)
            };

            _currentTest = summary;
            _activeCts = new CancellationTokenSource();

            var testCts = _activeCts;
            _runningTask = Task.Run(() => ExecuteLoadTestAsync(config, summary, resolvedBaseUrl, testCts.Token));

            return Task.FromResult(summary);
        }
    }

    public bool StopActiveTest()
    {
        lock (_lock)
        {
            if (_activeCts is not null && !_activeCts.IsCancellationRequested)
            {
                _activeCts.Cancel();
                if (_currentTest is not null && _currentTest.Status == LoadTestStatus.Running)
                {
                    _currentTest.Status = LoadTestStatus.Aborted;
                }
                return true;
            }
            return false;
        }
    }

    public async Task<QuickPingResult> QuickPingAsync(QuickPingRequest request, CancellationToken cancellationToken = default)
    {
        var resolvedBaseUrl = !string.IsNullOrWhiteSpace(request.TargetBaseUrl)
            ? request.TargetBaseUrl.TrimEnd('/')
            : "https://localhost:64115";

        var fullUrl = CombineUrl(resolvedBaseUrl, request.TargetEndpoint);
        var sw = Stopwatch.StartNew();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, fullUrl);
            if (!string.IsNullOrWhiteSpace(request.AuthToken))
            {
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {request.AuthToken.Trim()}");
            }
            if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
            {
                req.Headers.TryAddWithoutValidation("X-Tenant-Id", request.TenantId.Value.ToString());
            }

            if (request.BypassRateLimiter)
            {
                req.Headers.TryAddWithoutValidation("X-Load-Test", "true");
            }

            using var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            sw.Stop();

            return new QuickPingResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                Message = $"HTTP {(int)response.StatusCode} {response.StatusCode} in {sw.Elapsed.TotalMilliseconds:F1}ms",
                TargetUrl = fullUrl
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new QuickPingResult
            {
                Success = false,
                StatusCode = 0,
                LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                Message = ex.Message,
                TargetUrl = fullUrl
            };
        }
    }

    private async Task ExecuteLoadTestAsync(
        LoadTestConfig config,
        LoadTestRunSummary summary,
        string baseUrl,
        CancellationToken ct)
    {
        var fullTargetUrl = CombineUrl(baseUrl, config.TargetEndpoint);
        var duration = TimeSpan.FromSeconds(summary.ConfiguredDurationSeconds);
        var rampUp = TimeSpan.FromSeconds(Math.Clamp(config.RampUpSeconds, 0, summary.ConfiguredDurationSeconds - 1));
        var testSw = Stopwatch.StartNew();

        using var timeoutCts = new CancellationTokenSource(duration);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var runToken = linkedCts.Token;

        var latencySamples = new ConcurrentBag<double>();
        var statusCodes = new ConcurrentDictionary<int, int>();
        var errors = new ConcurrentQueue<string>();
        long totalBytes = 0;
        int activeUsers = 0;
        int totalRequests = 0;
        int successRequests = 0;
        int failedRequests = 0;

        // Background metric sampler ticker (ticks every 1 second)
        var samplerTask = Task.Run(async () =>
        {
            var prevTotal = 0;
            var prevSuccess = 0;
            var prevFailed = 0;
            var process = Process.GetCurrentProcess();
            var prevCpuTime = process.TotalProcessorTime;
            var prevTime = DateTime.UtcNow;

            while (!runToken.IsCancellationRequested && testSw.Elapsed < duration)
            {
                try
                {
                    await Task.Delay(1000, runToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var now = DateTime.UtcNow;
                var currentTotal = Volatile.Read(ref totalRequests);
                var currentSuccess = Volatile.Read(ref successRequests);
                var currentFailed = Volatile.Read(ref failedRequests);

                var rps = currentTotal - prevTotal;
                var successRps = currentSuccess - prevSuccess;
                var errorRps = currentFailed - prevFailed;

                prevTotal = currentTotal;
                prevSuccess = currentSuccess;
                prevFailed = currentFailed;

                // Process CPU & Memory
                process.Refresh();
                var currentCpuTime = process.TotalProcessorTime;
                var cpuUsage = Math.Clamp(
                    ((currentCpuTime - prevCpuTime).TotalMilliseconds /
                     ((now - prevTime).TotalMilliseconds * Environment.ProcessorCount)) * 100.0,
                    0.0, 100.0);
                prevCpuTime = currentCpuTime;
                prevTime = now;

                var memoryMb = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 1);

                // Latency snapshot for this second
                var sampleList = latencySamples.TakeLast(Math.Max(10, rps * 2)).ToList();
                var avgLat = sampleList.Count > 0 ? sampleList.Average() : 0.0;
                var minLat = sampleList.Count > 0 ? sampleList.Min() : 0.0;
                var maxLat = sampleList.Count > 0 ? sampleList.Max() : 0.0;

                var second = (int)testSw.Elapsed.TotalSeconds;

                var point = new LoadTestTelemetryPoint
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Second = second,
                    ActiveVirtualUsers = Volatile.Read(ref activeUsers),
                    RequestsInSecond = rps,
                    SuccessInSecond = successRps,
                    ErrorsInSecond = errorRps,
                    AvgLatencyMs = Math.Round(avgLat, 2),
                    MinLatencyMs = Math.Round(minLat, 2),
                    MaxLatencyMs = Math.Round(maxLat, 2),
                    CpuPercentage = Math.Round(cpuUsage, 1),
                    MemoryMb = memoryMb
                };

                lock (_lock)
                {
                    summary.ElapsedSeconds = second;
                    summary.CurrentActiveVUs = point.ActiveVirtualUsers;
                    summary.TotalRequests = currentTotal;
                    summary.SuccessfulRequests = currentSuccess;
                    summary.FailedRequests = currentFailed;
                    summary.CurrentRps = rps;
                    summary.PeakRps = Math.Max(summary.PeakRps, rps);
                    summary.SuccessRate = currentTotal > 0 ? Math.Round((currentSuccess * 100.0) / currentTotal, 2) : 100.0;
                    summary.Telemetry.Add(point);
                }
            }
        }, runToken);

        // Concurrency Workers
        var workerTasks = new List<Task>();
        var totalVUs = summary.ConfiguredVUs;
        var rampDelayPerUserMs = rampUp.TotalMilliseconds > 0 && totalVUs > 1
            ? (int)(rampUp.TotalMilliseconds / totalVUs)
            : 0;

        for (int i = 0; i < totalVUs; i++)
        {
            var userIndex = i;
            workerTasks.Add(Task.Run(async () =>
            {
                if (rampDelayPerUserMs > 0 && userIndex > 0)
                {
                    var delay = rampDelayPerUserMs * userIndex;
                    try
                    {
                        await Task.Delay(delay, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                Interlocked.Increment(ref activeUsers);

                try
                {
                    while (!runToken.IsCancellationRequested && testSw.Elapsed < duration)
                    {
                        var reqStart = Stopwatch.GetTimestamp();
                        try
                        {
                            using var req = new HttpRequestMessage(new HttpMethod(summary.Method), fullTargetUrl);

                            if (!string.IsNullOrWhiteSpace(config.AuthToken))
                            {
                                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {config.AuthToken.Trim()}");
                            }

                            if (config.TenantId.HasValue && config.TenantId.Value != Guid.Empty)
                            {
                                req.Headers.TryAddWithoutValidation("X-Tenant-Id", config.TenantId.Value.ToString());
                            }

                            if (config.BypassRateLimiter)
                            {
                                req.Headers.TryAddWithoutValidation("X-Load-Test", "true");
                            }

                            if (config.CustomHeaders is not null)
                            {
                                foreach (var (k, v) in config.CustomHeaders)
                                {
                                    if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(v))
                                    {
                                        req.Headers.TryAddWithoutValidation(k, v);
                                    }
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(config.RequestBody) &&
                                (req.Method == HttpMethod.Post || req.Method == HttpMethod.Put))
                            {
                                req.Content = new StringContent(config.RequestBody, Encoding.UTF8, "application/json");
                            }

                            using var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, runToken);
                            var elapsedMs = Stopwatch.GetElapsedTime(reqStart).TotalMilliseconds;

                            latencySamples.Add(elapsedMs);
                            var code = (int)response.StatusCode;
                            statusCodes.AddOrUpdate(code, 1, (_, current) => current + 1);

                            if (response.Content.Headers.ContentLength.HasValue)
                            {
                                Interlocked.Add(ref totalBytes, response.Content.Headers.ContentLength.Value);
                            }

                            Interlocked.Increment(ref totalRequests);

                            if (response.IsSuccessStatusCode)
                            {
                                Interlocked.Increment(ref successRequests);
                            }
                            else
                            {
                                Interlocked.Increment(ref failedRequests);
                                if (errors.Count < 25)
                                {
                                    errors.Enqueue($"HTTP {code} ({response.StatusCode}) from {fullTargetUrl}");
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            var elapsedMs = Stopwatch.GetElapsedTime(reqStart).TotalMilliseconds;
                            latencySamples.Add(elapsedMs);

                            statusCodes.AddOrUpdate(0, 1, (_, current) => current + 1);
                            Interlocked.Increment(ref totalRequests);
                            Interlocked.Increment(ref failedRequests);

                            if (errors.Count < 25)
                            {
                                errors.Enqueue($"{ex.GetType().Name}: {ex.Message}");
                            }
                        }

                        // Minimal yield to allow cooperative async execution
                        await Task.Yield();
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref activeUsers);
                }
            }, runToken));
        }

        try
        {
            await Task.WhenAll(workerTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected on manual cancel
        }

        try
        {
            await samplerTask;
        }
        catch (OperationCanceledException)
        {
        }

        testSw.Stop();

        // Finalize statistics
        var allSamples = latencySamples.ToList();
        allSamples.Sort();

        lock (_lock)
        {
            summary.CompletedAtUtc = DateTimeOffset.UtcNow;
            summary.ElapsedSeconds = Math.Min(summary.ConfiguredDurationSeconds, Math.Max(1, (int)testSw.Elapsed.TotalSeconds));
            summary.CurrentActiveVUs = 0;
            summary.TotalRequests = Volatile.Read(ref totalRequests);
            summary.SuccessfulRequests = Volatile.Read(ref successRequests);
            summary.FailedRequests = Volatile.Read(ref failedRequests);
            summary.TotalBytesReceived = Interlocked.Read(ref totalBytes);
            summary.SuccessRate = summary.TotalRequests > 0
                ? Math.Round((summary.SuccessfulRequests * 100.0) / summary.TotalRequests, 2)
                : 100.0;
            summary.AverageRps = Math.Round((double)summary.TotalRequests / summary.ElapsedSeconds, 2);
            summary.CurrentRps = 0;

            summary.StatusCodes = new Dictionary<int, int>(statusCodes);
            summary.Errors = errors.Distinct().Take(20).ToList();

            if (allSamples.Count > 0)
            {
                summary.MinLatencyMs = Math.Round(allSamples[0], 2);
                summary.MaxLatencyMs = Math.Round(allSamples[^1], 2);
                summary.AvgLatencyMs = Math.Round(allSamples.Average(), 2);
                summary.P50LatencyMs = Math.Round(GetPercentile(allSamples, 0.50), 2);
                summary.P90LatencyMs = Math.Round(GetPercentile(allSamples, 0.90), 2);
                summary.P95LatencyMs = Math.Round(GetPercentile(allSamples, 0.95), 2);
                summary.P99LatencyMs = Math.Round(GetPercentile(allSamples, 0.99), 2);
            }

            if (summary.Status == LoadTestStatus.Running)
            {
                summary.Status = LoadTestStatus.Completed;
            }

            // Push to history (up to last 50)
            _history.Insert(0, summary);
            if (_history.Count > 50)
            {
                _history.RemoveAt(_history.Count - 1);
            }

            SaveHistoryToDisk();
        }

        _logger.LogInformation("Load test '{Scenario}' completed: {Total} requests, {AvgRps} req/s, {AvgLat}ms avg latency, {Success}% success",
            summary.ScenarioName, summary.TotalRequests, summary.AverageRps, summary.AvgLatencyMs, summary.SuccessRate);
    }

    private static double GetPercentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return sorted[0];

        double index = (sorted.Count - 1) * percentile;
        int low = (int)Math.Floor(index);
        int high = (int)Math.Ceiling(index);

        if (low == high) return sorted[low];

        double fraction = index - low;
        return sorted[low] + (sorted[high] - sorted[low]) * fraction;
    }

    private static string CombineUrl(string baseUrl, string endpoint)
    {
        baseUrl = baseUrl.TrimEnd('/');
        endpoint = endpoint.StartsWith('/') ? endpoint : "/" + endpoint;
        return baseUrl + endpoint;
    }

    private void LoadHistoryFromDisk()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = File.ReadAllText(_historyFilePath);
                var items = JsonSerializer.Deserialize<List<LoadTestRunSummary>>(json);
                if (items is not null)
                {
                    _history.Clear();
                    _history.AddRange(items.Take(50));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load load testing history from disk.");
        }
    }

    private void SaveHistoryToDisk()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_history, options);
            File.WriteAllText(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist load testing history to disk.");
        }
    }

    public void Dispose()
    {
        _activeCts?.Dispose();
        _httpClient.Dispose();
    }
}
