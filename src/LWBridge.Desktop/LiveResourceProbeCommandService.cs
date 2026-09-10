using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

// IMPLEMENTATION POLICY: this is a bounded first-live-result adapter that
// starts the already-proven current-client resource probe from the rebuilt app.
// It deliberately does not masquerade as the unrecovered LWBridge pipe grammar.
internal sealed class LiveResourceProbeCommandService : INativeAsyncCommandService
{
    private const string ExpectedProbeVersion = "lwbridge-live-resource-probe-1";
    private const string ExpectedPackageSha256 = "09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace";
    private const string ExpectedXluaSha256 = "21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f";
    private const string ExpectedAssemblyCSharpSha256 = "871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd";
    private readonly MapDataStore store;
    private readonly string helperPath;
    private readonly TimeSpan helperSupervisionTimeout;
    private readonly string? gameRoot;
    private readonly bool requireCurrentClientEvidence;
    private readonly object stateGate = new();
    private int? currentServerId;
    private string scanRunId = string.Empty;
    private string phase = "idle";
    private string scanMode = "normal";
    private string? lastError;
    private bool isReading;
    private long lastCapturedAt;
    private int? acquisitionOrdinal;
    private JsonElement? lastHelperResult;
    private string? lastResultPath;
    private CancellationTokenSource? activeCancellation;
    private Process? activeHelperProcess;
    private bool closed;

    public LiveResourceProbeCommandService(
        MapDataStore store,
        string? helperPath = null,
        TimeSpan? helperSupervisionTimeout = null,
        string? gameRoot = null,
        bool? requireCurrentClientEvidence = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.helperPath = helperPath ?? Path.Combine(
            AppContext.BaseDirectory, "LiveResourceProbe", "run_live_resource_probe.py");
        this.helperSupervisionTimeout = helperSupervisionTimeout ?? TimeSpan.FromSeconds(135);
        this.gameRoot = string.IsNullOrWhiteSpace(gameRoot) ? null : Path.GetFullPath(gameRoot);
        this.requireCurrentClientEvidence = requireCurrentClientEvidence ?? helperPath is null;
        if (this.helperSupervisionTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(helperSupervisionTimeout));
    }

    public int? CurrentServerId => currentServerId;
    public JsonElement? LastHelperResult => lastHelperResult;
    public string LiveResultPath => lastResultPath ?? Path.Combine(
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

    public bool CanHandle(string command) =>
        command is "map_scan_start" or "map_scan_stop" or "map_scan_status";

    public async Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        if (command == "map_scan_status")
            return CreateStatus();
        if (command == "map_scan_stop")
        {
            RequestCancellation();
            return CreateStatus();
        }
        if (command != "map_scan_start")
            throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED", $"Live resource adapter cannot handle '{command}'.");

        MapScanStartOptions options = MapScanContract.NormalizeStart(payload);
        if (options.SelectedTypes.Count != 1 || options.SelectedTypes[0] != "resource")
        {
            throw new BridgeCommandException(
                "LIVE_RESOURCE_TYPES_UNSUPPORTED",
                "The bounded live adapter currently supports a resource-only scan. Other map kinds remain fail-closed.");
        }

        CancellationTokenSource operationCancellation;
        string operationId;
        lock (stateGate)
        {
            if (closed)
                throw new BridgeCommandException("MAP_SCAN_CLOSED", "The Map Data window is closing and cannot start another acquisition.");
            if (isReading)
                throw new BridgeCommandException("MAP_SCAN_ALREADY_RUNNING", "A bounded live resource acquisition is already running.");

            cancellationToken.ThrowIfCancellationRequested();
            operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            activeCancellation = operationCancellation;
            isReading = true;
            phase = "reading";
            scanMode = options.ScanMode;
            lastError = null;
            lastHelperResult = null;
            lastResultPath = null;
            scanRunId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            operationId = scanRunId;
        }
        DateTimeOffset operationStartedAtUtc = DateTimeOffset.UtcNow;
        int? expectedServerId;
        lock (stateGate)
            expectedServerId = currentServerId;

        using CancellationTokenRegistration cancellationRegistration = operationCancellation.Token.Register(() =>
        {
            lock (stateGate)
            {
                if (isReading && ReferenceEquals(activeCancellation, operationCancellation) && phase == "reading")
                    phase = "cancelling";
            }
        });

        try
        {
            try
            {
                string resultPath = await RunHelperAsync(operationId).ConfigureAwait(false);
                operationCancellation.Token.ThrowIfCancellationRequested();
                lock (stateGate)
                {
                    if (closed || !ReferenceEquals(activeCancellation, operationCancellation))
                        throw new OperationCanceledException(operationCancellation.Token);
                }

                FirstLiveResultImport imported = ImportCorrelatedResult(
                    store,
                    resultPath,
                    operationId,
                    out int? parsedOrdinal,
                    expectedServerId: expectedServerId,
                    operationStartedAtUtc: operationStartedAtUtc);
                operationCancellation.Token.ThrowIfCancellationRequested();

                lock (stateGate)
                {
                    if (closed || !ReferenceEquals(activeCancellation, operationCancellation))
                        throw new OperationCanceledException(operationCancellation.Token);
                    currentServerId = imported.ServerId;
                    lastCapturedAt = imported.CapturedAtUnixMilliseconds;
                    acquisitionOrdinal = parsedOrdinal;
                    phase = "idle";
                    isReading = false;
                }
                return CreateStatus();
            }
            catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
            {
                lock (stateGate)
                {
                    if (activeHelperProcess is null && ReferenceEquals(activeCancellation, operationCancellation))
                    {
                        phase = "idle";
                        lastError = null;
                    }
                }
                throw;
            }
            catch (BridgeCommandException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lock (stateGate)
                {
                    phase = activeHelperProcess is null ? "error" : "helper_stuck";
                    lastError = ex.Message;
                }
                throw new BridgeCommandException(
                    "LIVE_RESOURCE_ACQUISITION_FAILED",
                    "The bounded current-game resource acquisition failed.",
                    new { error = ex.Message, scanRunId = operationId });
            }
        }
        finally
        {
            lock (stateGate)
            {
                if (activeHelperProcess is null && ReferenceEquals(activeCancellation, operationCancellation))
                {
                    isReading = false;
                    activeCancellation = null;
                    operationCancellation.Dispose();
                }
            }
        }
    }

    internal static FirstLiveResultImport ImportCorrelatedResult(
        MapDataStore store,
        string resultPath,
        string requestId,
        out int? acquisitionOrdinal,
        Func<string, byte[]>? readAllBytes = null,
        int? expectedServerId = null,
        DateTimeOffset? operationStartedAtUtc = null,
        DateTimeOffset? nowUtc = null)
    {
        byte[] resultBytes = (readAllBytes ?? File.ReadAllBytes)(resultPath);
        using JsonDocument resultDocument = JsonDocument.Parse(resultBytes);
        JsonElement root = resultDocument.RootElement;
        if (!root.TryGetProperty("schemaVersion", out JsonElement schemaValue) ||
            !schemaValue.TryGetInt32(out int schemaVersion) || schemaVersion != 1 ||
            !root.TryGetProperty("probeVersion", out JsonElement probeVersionValue) ||
            probeVersionValue.ValueKind != JsonValueKind.String ||
            probeVersionValue.GetString() != ExpectedProbeVersion)
        {
            throw new InvalidDataException("Live resource result did not match the supported probe schema/version.");
        }
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
        if (!root.TryGetProperty("source", out JsonElement sourceValue) ||
            sourceValue.ValueKind != JsonValueKind.String ||
            sourceValue.GetString() != "WorldPointManager._pointInfos")
        {
            throw new InvalidDataException("Live resource result did not identify the proven WorldPointManager._pointInfos source.");
        }
        if (!root.TryGetProperty("capturedAt", out JsonElement capturedAtValue) ||
            capturedAtValue.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(
                capturedAtValue.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset capturedAt))
        {
            throw new InvalidDataException("Live resource result did not contain a valid capture timestamp.");
        }
        if (operationStartedAtUtc.HasValue && capturedAt < operationStartedAtUtc.Value.AddSeconds(-2))
            throw new InvalidDataException("Live resource result predates the active app acquisition and is stale.");
        DateTimeOffset freshnessNow = nowUtc ?? DateTimeOffset.UtcNow;
        if (capturedAt > freshnessNow.AddSeconds(5))
            throw new InvalidDataException("Live resource result capture timestamp is implausibly in the future.");

        if (!root.TryGetProperty("point_records", out JsonElement records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Live resource result is missing point_records.");
        JsonElement? resource = null;
        foreach (JsonElement candidate in records.EnumerateArray())
        {
            if (candidate.ValueKind == JsonValueKind.Object &&
                candidate.TryGetProperty("kind", out JsonElement kind) &&
                kind.ValueKind == JsonValueKind.String && kind.GetString() == "resource_point")
            {
                resource = candidate;
                break;
            }
        }
        if (!resource.HasValue ||
            !resource.Value.TryGetProperty("serverId", out JsonElement serverValue) ||
            !serverValue.TryGetInt32(out int serverId) || serverId is < 1 or > 99999)
        {
            throw new InvalidDataException("Live resource result does not contain a supported positive serverId.");
        }
        if (!resource.Value.TryGetProperty("source", out JsonElement pointSource) ||
            pointSource.ValueKind != JsonValueKind.String ||
            pointSource.GetString() != "WorldPointManager._pointInfos")
        {
            throw new InvalidDataException("Live resource point does not identify the proven WorldPointManager._pointInfos source.");
        }
        if (expectedServerId.HasValue && serverId != expectedServerId.Value)
            throw new InvalidDataException("Live resource result belongs to a different server than the active bounded session.");

        acquisitionOrdinal = root.TryGetProperty("acquisitionOrdinal", out JsonElement ordinalValue) &&
            ordinalValue.TryGetInt32(out int parsedOrdinal) && parsedOrdinal > 0 ? parsedOrdinal : null;
        return FirstLiveResultImporter.ImportOneResource(store, resultBytes, resultPath);
    }

    public object CreateStatus()
    {
        int? server;
        string runId;
        bool reading;
        string currentPhase;
        string currentScanMode;
        string? error;
        long capturedAt;
        int? ordinal;
        lock (stateGate)
        {
            server = currentServerId;
            runId = scanRunId;
            reading = isReading;
            currentPhase = phase;
            currentScanMode = scanMode;
            error = lastError;
            capturedAt = lastCapturedAt;
            ordinal = acquisitionOrdinal;
        }
        // IMPLEMENTATION POLICY: retain the recovered status field names needed
        // by the UI, but preserve metrics this one-view route does not measure as
        // null rather than inventing ordinary full-scan values.
        return new
        {
            serverId = server,
            serverIdSource = server.HasValue ? "current_live_resource_probe" : "none",
            scanRunId = runId,
            isReading = reading,
            phase = currentPhase,
            selectedTypes = new[] { "resource" },
            totalBlocks = (int?)null,
            readBlocks = (int?)null,
            unreadBlocks = (int?)null,
            failedBlocks = (int?)null,
            inflightBlocks = (int?)null,
            scanMode = currentScanMode,
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
            lastError = error,
            liveResourceCapturedAt = capturedAt > 0 ? capturedAt : (long?)null,
            liveResourceAcquisitionOrdinal = ordinal,
        };
    }

    public void Close()
    {
        CancellationTokenSource? cancellation;
        lock (stateGate)
        {
            closed = true;
            cancellation = activeCancellation;
            if (isReading)
                phase = "cancelling";
        }
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private void RequestCancellation()
    {
        CancellationTokenSource? cancellation;
        lock (stateGate)
        {
            cancellation = activeCancellation;
            if (isReading)
                phase = "cancelling";
        }
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private async Task<string> RunHelperAsync(string requestId)
    {
        if (!File.Exists(helperPath))
            throw new FileNotFoundException("Live resource helper was not deployed with LWBridge.Desktop.", helperPath);
        if (requireCurrentClientEvidence && gameRoot is null)
            throw new InvalidOperationException("No validated Last War installation is selected for the bounded live resource acquisition.");

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
        if (gameRoot is not null)
        {
            start.ArgumentList.Add("--game-root");
            start.ArgumentList.Add(gameRoot);
        }

        Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Python live-resource helper could not be started.");
        lock (stateGate)
        {
            if (closed)
            {
                process.Dispose();
                throw new OperationCanceledException();
            }
            activeHelperProcess = process;
        }
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        Task exitTask = process.WaitForExitAsync();
        Task completed = await Task.WhenAny(exitTask, Task.Delay(helperSupervisionTimeout)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, exitTask))
        {
            _ = ObserveLateHelperExitAsync(process, exitTask, stdoutTask, stderrTask);
            throw new TimeoutException(
                $"Live resource helper exceeded the supervised {helperSupervisionTimeout.TotalSeconds:0.#}-second process lifetime. Cleanup is still owned by that helper; no late result will be imported.");
        }
        await exitTask.ConfigureAwait(false);
        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);
        lock (stateGate)
        {
            if (ReferenceEquals(activeHelperProcess, process))
                activeHelperProcess = null;
        }

        try
        {
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
                if (!root.TryGetProperty("probeVersion", out JsonElement helperProbeVersion) ||
                    helperProbeVersion.ValueKind != JsonValueKind.String ||
                    helperProbeVersion.GetString() != ExpectedProbeVersion)
                    throw new InvalidDataException("Live resource helper response probeVersion did not match the supported build.");
                if (requireCurrentClientEvidence)
                    ValidateCurrentClientHelperEvidence(root);
                if (!root.TryGetProperty("resultPath", out JsonElement pathValue) || pathValue.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("Live resource helper did not return its correlated result path.");
                string? resultPath = pathValue.GetString();
                if (string.IsNullOrWhiteSpace(resultPath) || !File.Exists(resultPath))
                    throw new FileNotFoundException("Live resource helper result file is unavailable.", resultPath);
                string fullResultPath = Path.GetFullPath(resultPath);
                if (requireCurrentClientEvidence)
                {
                    string expectedResultPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "LWBridgeRebuild", "live-resource", "results", requestId + ".json");
                    if (!string.Equals(fullResultPath, Path.GetFullPath(expectedResultPath), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Live resource helper did not return the request-owned immutable result path.");
                }
                lock (stateGate)
                {
                    lastHelperResult = root.Clone();
                    lastResultPath = fullResultPath;
                }
                return fullResultPath;
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void ValidateCurrentClientHelperEvidence(JsonElement root)
    {
        if (!root.TryGetProperty("mode", out JsonElement mode) ||
            mode.ValueKind != JsonValueKind.String || mode.GetString() != "install_launch_restore")
        {
            throw new InvalidDataException(
                "Loaded-probe reuse is not accepted without a separately verified owned session; production requires a fresh hash-gated install/launch/restore helper run.");
        }
        if (!root.TryGetProperty("currentClient", out JsonElement current) || current.ValueKind != JsonValueKind.Object ||
            !MatchesString(current, "packageSha256", ExpectedPackageSha256) ||
            !MatchesString(current, "xluaSha256", ExpectedXluaSha256) ||
            !MatchesString(current, "assemblyCSharpSha256", ExpectedAssemblyCSharpSha256))
        {
            throw new InvalidDataException("Live resource helper did not prove the supported current-client package/xLua/Assembly-CSharp fingerprint.");
        }
        if (!root.TryGetProperty("restore", out JsonElement restore) || restore.ValueKind != JsonValueKind.Object ||
            !restore.TryGetProperty("restored", out JsonElement restored) || restored.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("installedFilesChanged", out JsonElement changed) || changed.ValueKind != JsonValueKind.False)
        {
            throw new InvalidDataException("Live resource helper did not prove exact post-acquisition restoration before import.");
        }
    }

    private static bool MatchesString(JsonElement value, string propertyName, string expected) =>
        value.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.String &&
        string.Equals(property.GetString(), expected, StringComparison.OrdinalIgnoreCase);

    private async Task ObserveLateHelperExitAsync(
        Process process,
        Task exitTask,
        Task<string> stdoutTask,
        Task<string> stderrTask)
    {
        try
        {
            await exitTask.ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch
        {
            // The foreground request has already reported the supervised helper
            // failure. This continuation exists only to release owned state.
        }
        finally
        {
            CancellationTokenSource? cancellation = null;
            lock (stateGate)
            {
                if (ReferenceEquals(activeHelperProcess, process))
                {
                    activeHelperProcess = null;
                    isReading = false;
                    cancellation = activeCancellation;
                    activeCancellation = null;
                    if (phase == "helper_stuck" || phase == "cancelling")
                        phase = lastError is null ? "idle" : "error";
                }
            }
            cancellation?.Dispose();
            process.Dispose();
        }
    }
}
