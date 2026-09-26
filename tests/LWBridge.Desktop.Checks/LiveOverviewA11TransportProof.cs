using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveOverviewA11TransportProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "FunFly",
            "Last War-Survival Game");
        string expectedClientPath =
            LWBridgeControlPipeClientPathContract
                .BuildExpectedGameExecutablePath(gameRoot);
        if (!File.Exists(expectedClientPath))
        {
            throw new FileNotFoundException(
                "A11 live proof requires the selected Last War executable.",
                expectedClientPath);
        }

        if (Process.GetProcessesByName("LastWar").Length != 0)
        {
            throw new InvalidOperationException(
                "A11 live proof requires Last War to be stopped before managed launch.");
        }

        var config = new LocalConfigStore(persistent: false);
        string profileId = config.Snapshot.ProfileId;
        using var host = new LWBridgeControlPipeHostState();
        Task listener = host.StartRpcTransport(
            OverviewLifecycleService.BridgeVersion,
            expectedClientPath);

        using var lifecycle = new OverviewLifecycleService(
            profileId,
            gameRoot,
            config: config,
            bridgeHostState: host,
            enableBridgeControlPipeLaunchBinding: true);
        var backend = new LWBridgeBackend(
            config,
            overviewLifecycle: lifecycle,
            bridgeHostState: host);

        string? instanceId = null;
        int? gamePid = null;
        string? connectionState = null;
        string? luaResultKind = null;
        int? luaResultPropertyCount = null;
        int pendingBefore = -1;
        int pendingAfter = -1;
        bool xluaOnline = false;
        bool exactGetStatusSucceeded = false;
        bool genericCallBlocked = false;
        bool stopped = false;
        bool routeUnregistered = false;
        bool restoreValidated = false;
        Exception? operationError = null;

        using var startTimeout =
            new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? start;
            try
            {
                start = await lifecycle.InvokeAsync(
                        "profile_instance_start",
                        empty.RootElement.Clone(),
                        startTimeout.Token)
                    .ConfigureAwait(false);
            }
            catch (BridgeCommandException error)
            {
                Console.WriteLine(
                    "A11_START_FAILURE " +
                    JsonSerializer.Serialize(
                        new
                        {
                            error.Code,
                            error.Message,
                            error.Details,
                        },
                        JsonOptions.Default));
                throw;
            }
            JsonElement startJson = JsonSerializer.SerializeToElement(
                start,
                JsonOptions.Default);
            instanceId = startJson.GetProperty("instanceId").GetString();
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new InvalidDataException(
                    "A11 managed launch did not return an owned instance id.");
            }

            JsonElement lifecycleStatus =
                JsonSerializer.SerializeToElement(
                    lifecycle.CreateInstanceStatus(),
                    JsonOptions.Default);
            if (lifecycleStatus.GetProperty("phase").GetString() !=
                    "running" ||
                lifecycleStatus.GetProperty("connectionState").GetString() !=
                    "connected")
            {
                throw new InvalidDataException(
                    "A11 managed launch did not reach running/connected.");
            }

            gamePid = lifecycleStatus.GetProperty("pid").GetInt32();
            connectionState =
                lifecycleStatus.GetProperty("connectionState").GetString();

            string readyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild",
                "overview-bridge",
                "ready.json");
            using (JsonDocument readyDocument =
                   JsonDocument.Parse(await File.ReadAllTextAsync(readyPath).ConfigureAwait(false)))
            {
                JsonElement readyRoot = readyDocument.RootElement;
                if (!readyRoot.TryGetProperty("pipeTransport", out JsonElement pipeTransport) ||
                    pipeTransport.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException(
                        "A11 live readiness did not expose the bounded pipe transport capability diagnostic.");
                }
                Console.WriteLine(
                    "A11_PIPE_CAPABILITIES " +
                    JsonSerializer.Serialize(pipeTransport, JsonOptions.Default));
            }

            try
            {
                await WaitUntilAsync(
                        () => host.ConnectedRouteCount == 1,
                        TimeSpan.FromSeconds(30),
                        "authenticated production control-pipe route")
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Console.WriteLine(
                    "A11_HOST_HANDSHAKE_DIAGNOSTIC " +
                    JsonSerializer.Serialize(
                        new
                        {
                            pendingRegistrations = host.PendingRegistrationCount,
                            connectedRoutes = host.ConnectedRouteCount,
                            serverInstances = host.ServerInstanceCount,
                            firstInstanceFlagUses = host.FirstInstanceFlagUseCount,
                            connectInitialDisposition = host.LastConnectInitialDisposition,
                            connectInitialError = host.LastConnectInitialError,
                            failedConnects = host.FailedConnectCount,
                            rejectedHandshakes = host.RejectedHandshakeCount,
                            authenticatedSessions = host.AuthenticatedSessionCount,
                            lastHandshakeError = host.LastHandshakeError,
                        },
                        JsonOptions.Default));
                throw;
            }

            JsonElement profilePayload =
                JsonSerializer.SerializeToElement(
                    new { profileId });
            JsonElement before =
                JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                            "get_status",
                            profilePayload,
                            CancellationToken.None)
                        .ConfigureAwait(false),
                    JsonOptions.Default);

            if (!before.TryGetProperty(
                    "pending",
                    out JsonElement beforePending) ||
                beforePending.ValueKind != JsonValueKind.Number ||
                !beforePending.TryGetInt32(out pendingBefore))
            {
                throw new InvalidDataException(
                    "A11 live get_status.pending was not numeric.");
            }

            xluaOnline =
                before.GetProperty("xluaOnline").GetBoolean();
            if (!xluaOnline || pendingBefore != 0)
            {
                throw new InvalidDataException(
                    "A11 live status was not ready with zero pending calls before getStatus.");
            }

            JsonElement callPayload =
                JsonSerializer.SerializeToElement(
                    new
                    {
                        profileId,
                        fnName = "getStatus",
                        args = new { },
                    },
                    JsonOptions.Default);
            object? callResult = await backend.InvokeAsync(
                    "call_lua",
                    callPayload,
                    CancellationToken.None)
                .ConfigureAwait(false);
            exactGetStatusSucceeded = true;

            JsonElement? resultElement =
                callResult is null
                    ? null
                    : JsonSerializer.SerializeToElement(
                        callResult,
                        JsonOptions.Default);
            luaResultKind =
                resultElement?.ValueKind.ToString() ?? "Null";
            if (resultElement is { ValueKind: JsonValueKind.Object } obj)
            {
                luaResultPropertyCount =
                    obj.EnumerateObject().Count();
            }

            JsonElement after =
                JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                            "get_status",
                            profilePayload,
                            CancellationToken.None)
                        .ConfigureAwait(false),
                    JsonOptions.Default);
            pendingAfter =
                after.GetProperty("pending").GetInt32();
            if (pendingAfter != 0)
            {
                throw new InvalidDataException(
                    "A11 pending call registry did not return to zero after getStatus.");
            }

            try
            {
                _ = await backend.InvokeAsync(
                        "call_lua",
                        JsonSerializer.SerializeToElement(
                            new
                            {
                                profileId,
                                fnName = "otherFunction",
                                args = new { },
                            }),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (BridgeCommandException error)
                when (error.Code == "COMMAND_NOT_IMPLEMENTED")
            {
                genericCallBlocked = true;
            }

            if (!genericCallBlocked)
            {
                throw new InvalidDataException(
                    "A11 live proof unexpectedly enabled generic call_lua.");
            }
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            instanceId ??=
                lifecycle.GetReadyMapScanSession()?.SessionId;

            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopTimeout =
                    new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload =
                    JsonDocument.Parse(
                        JsonSerializer.Serialize(
                            new { instanceId },
                            JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                            "profile_instance_stop",
                            stopPayload.RootElement.Clone(),
                            stopTimeout.Token)
                        .ConfigureAwait(false);

                    JsonElement stopStatus =
                        JsonSerializer.SerializeToElement(
                            lifecycle.CreateInstanceStatus(),
                            JsonOptions.Default);
                    stopped =
                        stopStatus.GetProperty("phase").GetString() ==
                        "stopped";
                    routeUnregistered =
                        host.PendingRegistrationCount == 0 &&
                        host.ConnectedRouteCount == 0;
                    restoreValidated = stopped;
                    if (!stopped || !routeUnregistered)
                    {
                        throw new InvalidDataException(
                            "A11 managed stop did not clear lifecycle and bridge route.");
                    }
                }
                catch when (operationError is not null)
                {
                    Console.Error.WriteLine(
                        "LIVE_A11_STOP_FAILED_AFTER_OPERATION_ERROR");
                }
            }

            await host.StopRpcTransportAsync()
                .ConfigureAwait(false);
            await listener.WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
        }

        bool gameRunning =
            Process.GetProcessesByName("LastWar").Length != 0;
        if (gameRunning)
        {
            throw new InvalidDataException(
                "A11 proof left Last War running after managed stop.");
        }

        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    diagnosticOk = true,
                    finding = "LWB-R7-127",
                    profile = "privacy-redacted",
                    managedLaunch = new
                    {
                        ownedInstance = !string.IsNullOrWhiteSpace(instanceId),
                        pidObserved = gamePid.HasValue &&
                            gamePid.Value > 0,
                        connectionState,
                        authenticatedRouteCount = 1,
                    },
                    readOnlyRpc = new
                    {
                        xluaOnline,
                        pendingBefore,
                        exactGetStatusSucceeded,
                        luaResultKind,
                        luaResultPropertyCount,
                        pendingAfter,
                        genericCallBlocked,
                    },
                    managedStop = new
                    {
                        stopped,
                        routeUnregistered,
                        restoreValidated,
                        gameRunningAfterStop = gameRunning,
                    },
                    safety = new
                    {
                        mapScan = false,
                        scout = false,
                        collect = false,
                        attack = false,
                        claim = false,
                        plunder = false,
                        message = false,
                        share = false,
                    },
                },
                JsonOptions.Default));
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string description)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    "A11 live proof timed out waiting for " +
                    description + ".");
            }

            await Task.Delay(100)
                .ConfigureAwait(false);
        }
    }
}
