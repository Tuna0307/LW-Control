using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

// IMPLEMENTATION POLICY: this is a bounded first-live-result adapter that
// starts the already-proven current-client resource probe from the rebuilt app.
// It deliberately does not masquerade as the unrecovered LWBridge pipe grammar.
internal sealed class LiveResourceProbeCommandService : INativeAsyncCommandService
{
    private readonly MapDataStore store;
    private readonly string helperPath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private int? currentServerId;
    private string scanRunId = string.Empty;
    private string phase = "idle";
    private string scanMode = "normal";
    private string? lastError;
    private bool isReading;
    private long lastCapturedAt;
    private int acquisitionOrdinal;
    private JsonElement? lastHelperResult;

    public LiveResourceProbeCommandService(MapDataStore store, string? helperPath = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.helperPath = helperPath ?? Path.Combine(
            AppContext.BaseDirectory, "LiveResourceProbe", "run_live_resource_probe.py");
    }

    public int? CurrentServerId => currentServerId;
    public JsonElement? LastHelperResult => lastHelperResult;
    public string LiveResultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LWBridgeRebuild", "live-resource", "result.json");

    public bool IsProbeOnline
    {
        get
        {
            bool gameRunning;
            try
            {
                Process[] processes = Process.GetProcessesByName("LastWar");
                gameRunning = processes.Length > 0;
                foreach (Process process in processes) process.Dispose();
            }
            catch
            {
                return false;
            }
            if (!gameRunning) return false;

            string heartbeatPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild", "live-resource", "heartbeat.json");
            try
            {
                if (!File.Exists(heartbeatPath) || DateTime.UtcNow - File.GetLastWriteTimeUtc(heartbeatPath) > TimeSpan.FromSeconds(5))
                    return false;
                using JsonDocument heartbeat = JsonDocument.Parse(File.ReadAllBytes(heartbeatPath));
                return heartbeat.RootElement.TryGetProperty("probeVersion", out JsonElement version) &&
                    version.ValueKind == JsonValueKind.String &&
                    version.GetString() == "lwbridge-live-resource-probe-1";
            }
            catch
            {
                return false;
            }
        }
    }

    public bool CanHandle(string command) => command is "map_scan_start" or "map_scan_status";

    public async Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        if (command == "map_scan_status")
            return CreateStatus();
        if (command != "map_scan_start")
            throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED", $"Live resource adapter cannot handle '{command}'.");

        MapScanStartOptions options = MapScanContract.NormalizeStart(payload);
        if (options.SelectedTypes.Count != 1 || options.SelectedTypes[0] != "resource")
        {
            throw new BridgeCommandException(
                "LIVE_RESOURCE_TYPES_UNSUPPORTED",
                "The bounded live adapter currently supports a resource-only scan. Other map kinds remain fail-closed.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (isReading)
                throw new BridgeCommandException("MAP_SCAN_ALREADY_RUNNING", "A bounded live resource acquisition is already running.");

            isReading = true;
            phase = "reading";
            scanMode = options.ScanMode;
            lastError = null;
            lastHelperResult = null;
            scanRunId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

            try
            {
                string resultPath = await RunHelperAsync(scanRunId).ConfigureAwait(false);
                FirstLiveResultImport imported = ImportCorrelatedResult(store, resultPath, scanRunId, out int parsedOrdinal);

                currentServerId = imported.ServerId;
                lastCapturedAt = imported.CapturedAtUnixMilliseconds;
                acquisitionOrdinal = parsedOrdinal;
                phase = "idle";
                isReading = false;
                return CreateStatus();
            }
            catch (BridgeCommandException)
            {
                throw;
            }
            catch (Exception ex)
            {
                phase = "error";
                lastError = ex.Message;
                throw new BridgeCommandException(
                    "LIVE_RESOURCE_ACQUISITION_FAILED",
                    "The bounded current-game resource acquisition failed.",
                    new { error = ex.Message, scanRunId });
            }
            finally
            {
                isReading = false;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    internal static FirstLiveResultImport ImportCorrelatedResult(
        MapDataStore store,
        string resultPath,
        string requestId,
        out int acquisitionOrdinal)
    {
        using JsonDocument resultDocument = JsonDocument.Parse(File.ReadAllBytes(resultPath));
        JsonElement root = resultDocument.RootElement;
        if (!root.TryGetProperty("requestId", out JsonElement requestIdValue) ||
            requestIdValue.ValueKind != JsonValueKind.String ||
            !string.Equals(requestIdValue.GetString(), requestId, StringComparison.Ordinal) ||
            !root.TryGetProperty("state", out JsonElement stateValue) ||
            stateValue.ValueKind != JsonValueKind.String ||
            stateValue.GetString() != "proven")
        {
            throw new InvalidDataException("Live resource result did not match the app request correlation contract.");
        }
        if (!root.TryGetProperty("requestRoute", out JsonElement routeValue) ||
            routeValue.ValueKind != JsonValueKind.String ||
            routeValue.GetString() != "WorldPointManager.StartViewRequest+UpdateViewRequest(true)")
        {
            throw new InvalidDataException("Live resource result did not prove the recovered current-view request route.");
        }

        acquisitionOrdinal = root.TryGetProperty("acquisitionOrdinal", out JsonElement ordinalValue) &&
            ordinalValue.TryGetInt32(out int parsedOrdinal) ? parsedOrdinal : 0;
        return FirstLiveResultImporter.ImportOneResource(store, resultPath);
    }

    public object CreateStatus()
    {
        // IMPLEMENTATION POLICY: retain the recovered status field names needed
        // by the UI, but preserve metrics this one-view route does not measure as
        // null rather than inventing ordinary full-scan values.
        return new
        {
            serverId = currentServerId ?? 0,
            serverIdSource = currentServerId.HasValue ? "current_live_resource_probe" : "none",
            scanRunId,
            isReading,
            phase,
            selectedTypes = new[] { "resource" },
            totalBlocks = (int?)null,
            readBlocks = (int?)null,
            unreadBlocks = (int?)null,
            failedBlocks = (int?)null,
            inflightBlocks = (int?)null,
            scanMode,
            concurrency = (int?)null,
            retryCount = (int?)null,
            scanRate = (double?)null,
            progressPercent = (double?)null,
            nativeCaptureReady = (bool?)null,
            nativePendingRecords = (int?)null,
            nativeDroppedRecords = (int?)null,
            homeServerId = (int?)null,
            seasonServerIds = (int[]?)null,
            truckMatchServerIds = (int[]?)null,
            lastError,
            liveResourceCapturedAt = lastCapturedAt > 0 ? lastCapturedAt : (long?)null,
            liveResourceAcquisitionOrdinal = acquisitionOrdinal > 0 ? acquisitionOrdinal : (int?)null,
        };
    }

    private async Task<string> RunHelperAsync(string requestId)
    {
        if (!File.Exists(helperPath))
            throw new FileNotFoundException("Live resource helper was not deployed with LWBridge.Desktop.", helperPath);

        var start = new ProcessStartInfo
        {
            FileName = "python",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(helperPath) ?? AppContext.BaseDirectory,
        };
        start.ArgumentList.Add(helperPath);
        start.ArgumentList.Add("--request-id");
        start.ArgumentList.Add(requestId);
        start.ArgumentList.Add("--timeout-seconds");
        // IMPLEMENTATION POLICY: outer launch/acquisition bound; the in-game
        // response itself has its separately documented eight-second bound.
        start.ArgumentList.Add("120");
        start.ArgumentList.Add("--restart-unmanaged");

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Python live-resource helper could not be started.");
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        // Once the helper starts, allow it to reach its own bounded exit so its
        // restoration finally-path cannot be interrupted by UI cancellation.
        await process.WaitForExitAsync().ConfigureAwait(false);
        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);

        JsonDocument helper;
        try
        {
            helper = JsonDocument.Parse(stdout.Trim());
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Live resource helper returned non-JSON output (exit {process.ExitCode}): {stderr.Trim()}", ex);
        }
        using (helper)
        {
            JsonElement root = helper.RootElement;
            if (process.ExitCode != 0 || !root.TryGetProperty("ok", out JsonElement okValue) || okValue.ValueKind != JsonValueKind.True)
            {
                string message = root.TryGetProperty("error", out JsonElement errorValue) && errorValue.ValueKind == JsonValueKind.String
                    ? errorValue.GetString() ?? "unknown live-resource helper error"
                    : stderr.Trim();
                throw new InvalidOperationException(message);
            }
            if (!root.TryGetProperty("requestId", out JsonElement requestValue) ||
                requestValue.ValueKind != JsonValueKind.String ||
                requestValue.GetString() != requestId)
                throw new InvalidDataException("Live resource helper response requestId did not match the app request.");
            if (!root.TryGetProperty("resultPath", out JsonElement pathValue) || pathValue.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Live resource helper did not return its correlated result path.");
            string? resultPath = pathValue.GetString();
            if (string.IsNullOrWhiteSpace(resultPath) || !File.Exists(resultPath))
                throw new FileNotFoundException("Live resource helper result file is unavailable.", resultPath);
            lastHelperResult = root.Clone();
            return Path.GetFullPath(resultPath);
        }
    }
}
