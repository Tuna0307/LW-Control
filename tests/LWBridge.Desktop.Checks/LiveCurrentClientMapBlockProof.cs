using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveCurrentClientMapBlockProof
{
    private const int ExpectedServerId = 2212;
    private const int WorldSize = 1000;
    private const int KnownX = 119;
    private const int KnownY = 107;

    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService(
            "current-block-live-proof",
            gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startResult = await lifecycle.InvokeAsync(
                "profile_instance_start",
                empty.RootElement,
                operationCts.Token).ConfigureAwait(false);
            JsonElement startJson = JsonSerializer.SerializeToElement(startResult, JsonOptions.Default);
            instanceId = startJson.GetProperty("instanceId").GetString();
            OverviewMapScanSession session = lifecycle.GetReadyMapScanSession() ??
                throw new InvalidOperationException("Production lifecycle did not expose a ready owned map-scan session.");

            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context = await source.GetCurrentContextAsync(operationCts.Token)
                .ConfigureAwait(false);
            if (context.ServerId != ExpectedServerId || context.WorldId != 0 ||
                context.TileWidth != WorldSize || context.TileHeight != WorldSize)
                throw new InvalidDataException(
                    $"Live map context changed: server={context.ServerId}, world={context.WorldId}, size={context.TileWidth}x{context.TileHeight}.");
            var request = new MapScanExecutionRequest(
                "live_current_client_aoi_proof",
                ExpectedServerId,
                0,
                WorldSize,
                WorldSize,
                ["resource"],
                1,
                1);
            MapScanTargetBlock block = MapScanTraversal.Build(WorldSize, WorldSize)
                .Single(candidate =>
                    candidate.MinX <= KnownX && candidate.MaxX >= KnownX &&
                    candidate.MinY <= KnownY && candidate.MaxY >= KnownY);
            MapScanBlockCapture capture = await source.CaptureAsync(
                request,
                block,
                operationCts.Token).ConfigureAwait(false);
            object manualScan = await LiveManualMapScanProof.RunAsync(
                source, context, operationCts.Token).ConfigureAwait(false);
            AoiDiagnosticObservation[] aoiDiagnostics =
            [
                await RunAoiDiagnosticAsync(session, 0, 0, operationCts.Token).ConfigureAwait(false),
                await RunAoiDiagnosticAsync(session, 1, 0, operationCts.Token).ConfigureAwait(false),
                await RunAoiDiagnosticAsync(session, 0, 1, operationCts.Token).ConfigureAwait(false),
            ];
            using JsonDocument checkpoint = JsonDocument.Parse(capture.PayloadJson);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "production_lifecycle_current_client_block_source",
                sessionId = session.SessionId,
                gamePid = session.GamePid,
                liveContext = context,
                manualScan,
                aoiDiagnostics,
                block = new
                {
                    block.BlockIndex,
                    block.MinX,
                    block.MinY,
                    block.MaxX,
                    block.MaxY,
                },
                checkpoint = checkpoint.RootElement.Clone(),
                records = capture.Records.Select(record => new
                {
                    record.RecordKey,
                    record.Kind,
                    record.ServerId,
                    record.Name,
                    record.DataJson,
                }).ToArray(),
            }, JsonOptions.Default));
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop",
                        stopPayload.RootElement,
                        stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_PROOF_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    internal static async Task RunRuntimeDiagnosticOnlyAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService(
            "current-runtime-live-proof",
            gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startResult = await lifecycle.InvokeAsync(
                "profile_instance_start",
                empty.RootElement,
                operationCts.Token).ConfigureAwait(false);
            JsonElement startJson = JsonSerializer.SerializeToElement(startResult, JsonOptions.Default);
            instanceId = startJson.GetProperty("instanceId").GetString();
            OverviewMapScanSession session = lifecycle.GetReadyMapScanSession() ??
                throw new InvalidOperationException("Production lifecycle did not expose a ready owned runtime session.");
            RuntimeDiagnosticObservation observation = await RunRuntimeDiagnosticAsync(
                session,
                operationCts.Token).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "production_lifecycle_current_client_runtime_objects",
                sessionId = session.SessionId,
                gamePid = session.GamePid,
                runtime = observation,
            }, JsonOptions.Default));
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop",
                        stopPayload.RootElement,
                        stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_RUNTIME_DIAGNOSTIC_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    private static async Task<RuntimeDiagnosticObservation> RunRuntimeDiagnosticAsync(
        OverviewMapScanSession session,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-resource");
        Directory.CreateDirectory(root);
        string requestId = "runtime" + Guid.NewGuid().ToString("N");
        string commandPath = Path.Combine(root, "runtime-diagnostic.txt");
        string resultPath = Path.Combine(root, "runtime-diagnostic-result.json");
        string temporaryPath = commandPath + ".tmp-" + Guid.NewGuid().ToString("N");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            "probeVersion=lwbridge-live-resource-probe-2",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid}",
            string.Empty,
        });
        try
        {
            await File.WriteAllTextAsync(temporaryPath, command, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, commandPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(resultPath, cancellationToken).ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(bytes);
                JsonElement rootElement = document.RootElement;
                if (ReadString(rootElement, "requestId") != requestId)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (RequireInt(rootElement, "schemaVersion") != 1 ||
                    ReadString(rootElement, "probeVersion") != "lwbridge-live-resource-probe-2" ||
                    ReadString(rootElement, "profileId") != session.ProfileId ||
                    ReadString(rootElement, "launchSessionId") != session.SessionId ||
                    ReadString(rootElement, "challenge") != session.Challenge ||
                    RequireInt(rootElement, "gamePid") != session.GamePid)
                {
                    throw new InvalidDataException("Runtime diagnostic did not match the owned game session.");
                }
                string state = ReadString(rootElement, "state") ?? string.Empty;
                if (state != "proven")
                    throw new InvalidDataException(
                        "Runtime diagnostic failed: " + (ReadString(rootElement, "error") ?? "unknown error"));

                return new RuntimeDiagnosticObservation(
                    ReadObjectShape(rootElement, "globalGameEntry"),
                    ReadObjectShape(rootElement, "csGameEntry"),
                    ReadObjectShape(rootElement, "luaEntry"),
                    ReadObjectShape(rootElement, "gameMain"),
                    ReadObjectShape(rootElement, "dataCenter"),
                    ReadObjectShape(rootElement, "globalEntryNetwork"),
                    ReadObjectShape(rootElement, "globalEntryData"),
                    ReadObjectShape(rootElement, "globalEntryPlayer"),
                    ReadObjectShape(rootElement, "csEntryNetwork"),
                    ReadObjectShape(rootElement, "csEntryData"),
                    ReadObjectShape(rootElement, "csEntryPlayer"),
                    ReadObjectShape(rootElement, "luaEntryPlayer"),
                    ReadObjectShape(rootElement, "luaEntryNetwork"),
                    ReadObjectShape(rootElement, "luaEntryData"),
                    ReadObjectShape(rootElement, "luaEntryGameEntry"),
                    ReadObjectShape(rootElement, "gameMainGameEntry"),
                    ReadObjectShape(rootElement, "dataCenterPlayer"),
                    ReadObjectShape(rootElement, "globalNetworkManager"),
                    ReadObjectShape(rootElement, "globalCustomNetworkManager"),
                    ReadString(rootElement, "capturedAt"));
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
            catch (JsonException) { }
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Runtime diagnostic did not return a correlated result.");
    }

    private static async Task<AoiDiagnosticObservation> RunAoiDiagnosticAsync(
        OverviewMapScanSession session,
        int cellX,
        int cellY,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-resource");
        Directory.CreateDirectory(root);
        string requestId = "aoi" + Guid.NewGuid().ToString("N");
        string commandPath = Path.Combine(root, "aoi-diagnostic.txt");
        string resultPath = Path.Combine(root, "aoi-diagnostic-result.json");
        string temporaryPath = commandPath + ".tmp-" + Guid.NewGuid().ToString("N");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            "probeVersion=lwbridge-live-resource-probe-2",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid}",
            $"cellX={cellX}",
            $"cellY={cellY}",
            $"tileCount={WorldSize}",
            string.Empty,
        });
        try
        {
            await File.WriteAllTextAsync(temporaryPath, command, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, commandPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(resultPath, cancellationToken).ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(bytes);
                JsonElement rootElement = document.RootElement;
                if (ReadString(rootElement, "requestId") != requestId)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                ValidateAoiIdentity(rootElement, session, cellX, cellY);
                string state = ReadString(rootElement, "state") ?? string.Empty;
                if (state != "proven")
                    throw new InvalidDataException(
                        "Read-only AOI diagnostic failed: " + (ReadString(rootElement, "error") ?? "unknown error"));
                return new AoiDiagnosticObservation(
                    cellX,
                    cellY,
                    RequireInt(rootElement, "currentLod"),
                    RequireInt(rootElement, "serverLod"),
                    RequireIntArray(rootElement, "lwAoiBlockSizeArray"),
                    ReadOptionalInt(rootElement, "lwAoiBlockSize"),
                    ReadOptionalInt(rootElement, "lwAoiBlockCount"),
                    ReadOptionalInt(rootElement, "msgViewIndexCount"),
                    ReadOptionalInt(rootElement, "addViewIndexCount"),
                    ReadOptionalInt(rootElement, "curViewIndexCount"),
                    RequireInt(rootElement, "aoiIndex"),
                    ReadOptionalDouble(rootElement, "centerX"),
                    ReadOptionalDouble(rootElement, "centerY"),
                    ReadOptionalDouble(rootElement, "centerZ"),
                    ReadString(rootElement, "method") ?? string.Empty,
                    ReadString(rootElement, "capturedAt"));
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
            catch (JsonException) { }
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Read-only AOI diagnostic did not return a correlated result.");
    }

    private static void ValidateAoiIdentity(
        JsonElement root,
        OverviewMapScanSession session,
        int cellX,
        int cellY)
    {
        if (RequireInt(root, "schemaVersion") != 1 ||
            ReadString(root, "probeVersion") != "lwbridge-live-resource-probe-2" ||
            ReadString(root, "profileId") != session.ProfileId ||
            ReadString(root, "launchSessionId") != session.SessionId ||
            ReadString(root, "challenge") != session.Challenge ||
            RequireInt(root, "gamePid") != session.GamePid ||
            RequireInt(root, "cellX") != cellX ||
            RequireInt(root, "cellY") != cellY ||
            RequireInt(root, "tileCount") != WorldSize)
        {
            throw new InvalidDataException("Read-only AOI diagnostic did not match the owned game session.");
        }
    }

    private static int RequireInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed
            : throw new InvalidDataException($"AOI diagnostic field '{name}' is missing or invalid.");

    private static int? ReadOptionalInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : null;

    private static double? ReadOptionalDouble(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double parsed) ? parsed : null;

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int[] RequireIntArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"AOI diagnostic field '{name}' is missing or invalid.");
        return value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
    }

    private static RuntimeObjectShape ReadObjectShape(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Runtime diagnostic field '{name}' is missing or invalid.");
        bool exists = value.TryGetProperty("exists", out JsonElement existsElement) &&
            existsElement.ValueKind == JsonValueKind.True;
        return new RuntimeObjectShape(
            exists,
            ReadString(value, "luaType"),
            ReadString(value, "reflectedType"));
    }

    private sealed record RuntimeDiagnosticObservation(
        RuntimeObjectShape GlobalGameEntry,
        RuntimeObjectShape CsGameEntry,
        RuntimeObjectShape LuaEntry,
        RuntimeObjectShape GameMain,
        RuntimeObjectShape DataCenter,
        RuntimeObjectShape GlobalEntryNetwork,
        RuntimeObjectShape GlobalEntryData,
        RuntimeObjectShape GlobalEntryPlayer,
        RuntimeObjectShape CsEntryNetwork,
        RuntimeObjectShape CsEntryData,
        RuntimeObjectShape CsEntryPlayer,
        RuntimeObjectShape LuaEntryPlayer,
        RuntimeObjectShape LuaEntryNetwork,
        RuntimeObjectShape LuaEntryData,
        RuntimeObjectShape LuaEntryGameEntry,
        RuntimeObjectShape GameMainGameEntry,
        RuntimeObjectShape DataCenterPlayer,
        RuntimeObjectShape GlobalNetworkManager,
        RuntimeObjectShape GlobalCustomNetworkManager,
        string? CapturedAt);

    private sealed record RuntimeObjectShape(
        bool Exists,
        string? LuaType,
        string? ReflectedType);

    private sealed record AoiDiagnosticObservation(
        int CellX,
        int CellY,
        int CurrentLod,
        int ServerLod,
        int[] AoiBlockSizes,
        int? RuntimeBlockSize,
        int? RuntimeBlockCount,
        int? MessageViewIndexCount,
        int? AddedViewIndexCount,
        int? CurrentViewIndexCount,
        int AoiIndex,
        double? CenterX,
        double? CenterY,
        double? CenterZ,
        string Method,
        string? CapturedAt);
}
