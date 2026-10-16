using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Opt-in packaged proof of the real profile-owned Auto scheduler. No test clock,
    // simulated provider, Manual Start, Auto Run Now, or background game controller.
    // The dispatcher must only invoke this after an exact-owned 2212 game is ready,
    // with LWBRIDGE_MAP016_AUTO_INTERVAL=1 and a >=25-minute operation deadline.
    private async Task RunMap016AutoIntervalUiProofAsync(
        CoreWebView2 core, string outputPath, string directory,
        OverviewMapScanSession proofSession, CancellationToken token)
    {
        if (Environment.GetEnvironmentVariable("LWBRIDGE_MAP016_AUTO_INTERVAL") != "1" ||
            productionPaths?.ExplicitIsolation != true ||
            mapAutoScanService is null || map317CommandService is null ||
            overviewLifecycleService is null)
            throw new InvalidOperationException("MAP016 20-minute Auto interval requires the explicit isolated packaged owner.");

        const int originalServer = 2212;
        const int targetServer = 2211;
        const int intervalMinutes = 20;
        const long intervalMilliseconds = intervalMinutes * 60_000L;
        // These 25 minutes include the full idle interval, remote acquisition,
        // native receipt checks, SQLite proof and original-server return.
        using var operationBudget = CancellationTokenSource.CreateLinkedTokenSource(token);
        operationBudget.CancelAfter(TimeSpan.FromMinutes(25));
        CancellationToken proofToken = operationBudget.Token;

        var clock = new Stopwatch();
        var samples = new List<object>(1500);
        var events = new List<object>();
        var json = new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true };
        string progressPath = Path.Combine(directory, "map016-auto-interval-progress.json");
        DateTimeOffset? enabledAt = null;
        DateTimeOffset? firstFireAt = null;
        long? firstFireElapsedMilliseconds = null;
        long? deadlineUtcMilliseconds = null;
        long lastIdleElapsedMilliseconds = 0;
        int consecutiveUnknownServerSamples = 0;
        int totalUnknownServerSamples = 0;
        string? runId = null;
        string? failure = null;
        string? cleanupFailure = null;
        bool armedByProof = false;
        bool observedScheduledRun = false;
        bool observedTargetServer = false;
        bool observedHomeReturn = false;
        string state = "preflight";
        Exception? primaryError = null;
        Exception? cleanupError = null;
        MapAutoScanSnapshot? completedAuto = null;
        int? nativeRailwayRecords = null;
        int? savedRailwayRecords = null;

        void RequireOwner()
        {
            proofToken.ThrowIfCancellationRequested();
            if (!overviewLifecycleService.MatchesOwnedMapScanSession(proofSession))
                throw new InvalidDataException("MAP016 interval proof lost its original exact-owned game session.");
        }

        async Task SaveProgressAsync()
        {
            // Atomic replacement preserves the last entire interval record if
            // the process is interrupted. This writes every five real seconds
            // during the full idle window, and again at each terminal event.
            Directory.CreateDirectory(directory);
            string data = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                proof = "real_20_minute_enabled_auto_railway_scheduler_2212_to_2211_return",
                state, genuineGame = true, controlledProvider = false,
                systemClockInjected = false, runNowCalls = 0, manualStartCalls = 0,
                originalServer, targetServer, intervalMinutes,
                proofSession.ProfileId, proofSession.SessionId,
                proofSession.GamePid, proofSession.GameStartedAtUtc,
                enabledAt, deadlineUtcMilliseconds,
                expectedMonotonicIdleMilliseconds = intervalMilliseconds,
                observedMonotonicMilliseconds = clock.ElapsedMilliseconds,
                firstFireAt, firstFireElapsedMilliseconds, lastIdleElapsedMilliseconds,
                observedScheduledRun, observedTargetServer, observedHomeReturn,
                runId, nativeRailwayRecords, savedRailwayRecords,
                completedAuto, failure, cleanupFailure, samples, events,
            }, json);
            string temporary = progressPath + ".tmp";
            await File.WriteAllTextAsync(temporary, data, CancellationToken.None);
            File.Move(temporary, progressPath, overwrite: true);
        }

        async Task CaptureAsync(string name)
        {
            RequireOwner();
            await using var file = File.Create(Path.Combine(directory, name));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        }

        try
        {
            RequireOwner();
            for (int i = 0; i < 450 && overviewLifecycleService.GetLiveServerId() != originalServer; i++)
            {
                RequireOwner();
                await Task.Delay(100, proofToken);
            }
            if (overviewLifecycleService.GetLiveServerId() != originalServer ||
                !string.Equals(overviewLifecycleService.CurrentConnectionState,
                    "connected", StringComparison.Ordinal))
                throw new InvalidDataException("MAP016 interval proof requires connected authenticated home 2212.");
            MapAutoScanSnapshot initial = await mapAutoScanService.GetSnapshotAsync(proofToken);
            JsonElement initialManual = JsonSerializer.SerializeToElement(
                map317CommandService.CreateStatus(), JsonOptions.Default);
            if (initial.Config.Enabled || initial.Running || mapAutoScanService.IsCycleBusy ||
                initial.Config.ServerIds is { Count: > 0 } ||
                map317CommandService.IsScanActive ||
                initialManual.TryGetProperty("isReading", out JsonElement initialReading) &&
                initialReading.ValueKind == JsonValueKind.True)
                throw new InvalidDataException("MAP016 interval proof requires fresh disabled Auto and idle Map owners.");

            // Show the ordinary mounted Auto screen, while leaving its Run Now
            // button and the Manual scan button untouched throughout the test.
            if (await core.ExecuteScriptAsync(
                "(()=>{const b=document.querySelectorAll('.map-scan-tabs button')[1];if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("MAP016 interval Auto page is unavailable.");
            for (int i = 0; i < 100 &&
                await core.ExecuteScriptAsync("!!document.querySelector('.map-auto-scan-card')") != "true"; i++)
            {
                RequireOwner();
                await Task.Delay(100, proofToken);
            }
            if (await core.ExecuteScriptAsync("!!document.querySelector('.map-auto-scan-card')") != "true")
                throw new InvalidDataException("MAP016 interval Auto controls did not mount.");

            DateTimeOffset beforeEnable = DateTimeOffset.UtcNow;
            clock.Start();
            armedByProof = true;
            // This IS the production scheduler with real 5-second due ticks
            // and exact Map317 server/scan ownership; no synthetic scheduling.
            MapAutoScanSnapshot configured = await mapAutoScanService.UpdateConfigAsync(
                initial.Config with
                {
                    Enabled = true,
                    IntervalMinutes = intervalMinutes,
                    ServerIds = [targetServer],
                    SelectedTypes = ["railway"],
                    ScanMode = "fast",
                    ReturnToOriginalServer = true,
                }, proofToken);
            enabledAt = DateTimeOffset.UtcNow;
            deadlineUtcMilliseconds = configured.Config.NextRunAt;
            if (!configured.Config.Enabled || configured.Config.IntervalMinutes != intervalMinutes ||
                configured.Config.ServerIds?.SequenceEqual([targetServer]) != true ||
                configured.Config.SelectedTypes?.SequenceEqual(["railway"], StringComparer.Ordinal) != true ||
                !configured.Config.ReturnToOriginalServer || configured.Config.ScanMode != "fast" ||
                configured.Running || mapAutoScanService.IsCycleBusy ||
                !Equals(initial.LastCycle, configured.LastCycle) ||
                deadlineUtcMilliseconds < beforeEnable.ToUnixTimeMilliseconds() + intervalMilliseconds ||
                deadlineUtcMilliseconds > enabledAt.Value.ToUnixTimeMilliseconds() + intervalMilliseconds + 5_000L ||
                overviewLifecycleService.GetLiveServerId() != originalServer)
                throw new InvalidDataException("MAP016 real scheduler did not arm a future 20-minute Railway-only run.");
            state = "real_interval_idle";
            events.Add(new { stage = "scheduler_armed", beforeEnable, enabledAt, deadlineUtcMilliseconds,
                configured.Config, monotonicAtArmMilliseconds = clock.ElapsedMilliseconds });
            await CaptureAsync("map016-auto-interval-armed.png");
            await SaveProgressAsync();

            long? previousSampleElapsed = null;
            for (int index = 0; ; index++)
            {
                RequireOwner();
                DateTimeOffset sampledAt = DateTimeOffset.UtcNow;
                long elapsed = clock.ElapsedMilliseconds;
                if (previousSampleElapsed is long previous && elapsed - previous > 3_500)
                    throw new InvalidDataException("MAP016 interval monitor missed per-second live status observations.");
                previousSampleElapsed = elapsed;
                MapAutoScanSnapshot auto = await mapAutoScanService.GetSnapshotAsync(proofToken);
                JsonElement manual = JsonSerializer.SerializeToElement(
                    map317CommandService.CreateStatus(), JsonOptions.Default);
                // A due scheduler can fire while either real native status read
                // is in progress. Judge first admission using the observation
                // time after both reads, then also verify the persisted cycle's
                // own timestamp against the saved deadline below.
                long observedElapsed = clock.ElapsedMilliseconds;
                DateTimeOffset observedAt = DateTimeOffset.UtcNow;
                bool manualReading = manual.TryGetProperty("isReading", out JsonElement reading) &&
                    reading.ValueKind == JsonValueKind.True;
                bool busy = auto.Running || mapAutoScanService.IsCycleBusy;
                int? liveServer = overviewLifecycleService.GetLiveServerId();
                bool autoCycleChanged = !Equals(auto.LastCycle, initial.LastCycle);
                bool fired = busy || autoCycleChanged || manualReading;
                if (auto.Config.IntervalMinutes != intervalMinutes || !auto.Config.Enabled ||
                    auto.Config.ServerIds?.SequenceEqual([targetServer]) != true ||
                    auto.Config.SelectedTypes?.SequenceEqual(["railway"], StringComparer.Ordinal) != true ||
                    !auto.Config.ReturnToOriginalServer)
                    throw new InvalidDataException("MAP016 interval Auto config changed during the owned schedule.");

                if (!observedScheduledRun)
                {
                    // Scheduler NextRunAt must remain exactly stable until its
                    // first real admission; no Run Now or hidden Manual scan.
                    if (!fired && auto.Config.NextRunAt != deadlineUtcMilliseconds)
                        throw new InvalidDataException("MAP016 interval deadline changed before admission.");
                    if (!fired)
                    {
                        lastIdleElapsedMilliseconds = elapsed;
                        // The exact-owned heartbeat can briefly be absent
                        // during normal client refresh. Unknown is not proof of
                        // a move; reject any positive foreign observation,
                        // and bound missing observations using the native Map
                        // status. Never count a missing heartbeat as a server
                        // change or as successful source restoration.
                        int? secondaryServer = ReadStatusInt32(manual, "serverId");
                        if (liveServer is > 0 && liveServer != originalServer ||
                            secondaryServer is > 0 && secondaryServer != originalServer)
                            throw new InvalidDataException("MAP016 observed another server before the 20-minute deadline.");
                        if (liveServer is null or <= 0)
                        {
                            totalUnknownServerSamples++;
                            consecutiveUnknownServerSamples++;
                            if (consecutiveUnknownServerSamples > 10 || secondaryServer != originalServer)
                                throw new InvalidDataException("MAP016 could not prove the original server during the idle interval.");
                        }
                        else consecutiveUnknownServerSamples = 0;
                    }
                    else
                    {
                        if (!busy || observedElapsed < intervalMilliseconds ||
                            observedAt.ToUnixTimeMilliseconds() < deadlineUtcMilliseconds ||
                            auto.Config.NextRunAt != deadlineUtcMilliseconds)
                            throw new InvalidDataException("MAP016 scan began before the full real 20-minute deadline, or without Auto ownership.");
                        observedScheduledRun = true;
                        firstFireAt = observedAt;
                        firstFireElapsedMilliseconds = observedElapsed;
                        state = "scheduled_cycle_active";
                        events.Add(new { stage = "first_real_scheduled_admission",
                            sampledAt = observedAt, monotonicMilliseconds = observedElapsed,
                            deadlineUtcMilliseconds,
                            auto.Running, mapAutoScanService.IsCycleBusy, liveServer });
                    }
                }
                if (observedScheduledRun)
                {
                    if (liveServer == targetServer) observedTargetServer = true;
                    if (autoCycleChanged && !busy && !manualReading &&
                        auto.LastCycle is { StartedAt: > 0 })
                    {
                        completedAuto = auto;
                        events.Add(new { stage = "scheduler_cycle_settled", sampledAt,
                            monotonicMilliseconds = elapsed, auto.LastCycle, liveServer });
                    }
                }
                // Mounted WebView2 must remain the real Auto UI. Reading it
                // never invokes a game command or schedules a scan.
                bool autoUiMounted = await core.ExecuteScriptAsync(
                    "!!document.querySelector('.map-auto-scan-card')") == "true";
                if (!autoUiMounted)
                    throw new InvalidDataException("MAP016 interval mounted Auto UI disappeared.");
                samples.Add(new
                {
                    index, utc = sampledAt, monotonicMilliseconds = elapsed,
                    elapsedSeconds = elapsed / 1_000.0, deadlineUtcMilliseconds,
                    autoEnabled = auto.Config.Enabled, autoRunning = auto.Running,
                    autoCycleBusy = busy, auto.Config.NextRunAt,
                    autoLastCycleStartedAt = auto.LastCycle?.StartedAt,
                    autoLastCycleOutcome = auto.LastCycle?.Outcome,
                    manualScanActive = map317CommandService.IsScanActive,
                    manualReading, manualPhase = ReadStatusString(manual, "phase"),
                    manualScanRunId = ReadStatusString(manual, "scanRunId"),
                    manualServerId = ReadStatusInt32(manual, "serverId"),
                    liveServerId = liveServer, autoUiMounted,
                    liveServerKnown = liveServer is > 0,
                    secondaryServerId = ReadStatusInt32(manual, "serverId"),
                    totalUnknownServerSamples,
                });
                if (index % 5 == 0 || observedScheduledRun && index % 2 == 0 ||
                    completedAuto is not null)
                    await SaveProgressAsync();
                if (completedAuto is not null) break;
                if (clock.Elapsed >= TimeSpan.FromMinutes(25))
                    throw new TimeoutException("MAP016 real interval and Railway scan exceeded 25 minutes.");
                // Aim for one new real sample per second, accounting for
                // status/JSON time without shortening actual elapsed time.
                long delayMilliseconds = 1_000 - (clock.ElapsedMilliseconds - elapsed);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, delayMilliseconds)), proofToken);
            }
            if (!observedScheduledRun || firstFireElapsedMilliseconds < intervalMilliseconds ||
                samples.Count < 1_150 || lastIdleElapsedMilliseconds < intervalMilliseconds - 3_500 ||
                completedAuto?.LastCycle is not { Outcome: "completed", LastError: null } cycle ||
                cycle.StartedAt < deadlineUtcMilliseconds ||
                cycle.OriginalServerId != originalServer ||
                !cycle.TargetServerIds.SequenceEqual([targetServer]) ||
                !cycle.CompletedServerIds.SequenceEqual([targetServer]) ||
                !observedTargetServer)
                throw new InvalidDataException("MAP016 real 20-minute schedule did not complete exactly one owned 2211 Railway cycle.");

            for (int i = 0; i < 450 && overviewLifecycleService.GetLiveServerId() != originalServer; i++)
            {
                RequireOwner();
                await Task.Delay(100, proofToken);
            }
            observedHomeReturn = overviewLifecycleService.GetLiveServerId() == originalServer;
            if (!observedHomeReturn || map317CommandService.IsScanActive)
                throw new InvalidDataException("MAP016 Auto scheduler did not restore original 2212 and idle native scan.");

            string nativePath = Path.Combine(overviewLifecycleService.LiveResourceRuntimeRoot,
                "fast-truck-scan-result.json");
            using JsonDocument nativeDocument = JsonDocument.Parse(
                await File.ReadAllTextAsync(nativePath, proofToken));
            JsonElement native = nativeDocument.RootElement;
            runId = ReadStatusString(native, "scanRunId");
            nativeRailwayRecords = ReadStatusInt32(native, "railwayRecordCount");
            if (string.IsNullOrWhiteSpace(runId) ||
                ReadStatusString(native, "state") != "proven" ||
                ReadStatusString(native, "profileId") != proofSession.ProfileId ||
                ReadStatusString(native, "launchSessionId") != proofSession.SessionId ||
                ReadStatusString(native, "challenge") != proofSession.Challenge ||
                ReadStatusInt32(native, "gamePid") != proofSession.GamePid ||
                ReadStatusInt32(native, "serverId") != targetServer ||
                ReadStatusInt32(native, "rectanglesCompleted") != 625 ||
                ReadStatusInt32(native, "unprovenRectangles") != 0 ||
                ReadStatusInt32(native, "initialRequestsSent") != 625 ||
                nativeRailwayRecords is null or < 0 ||
                !native.TryGetProperty("railwayOnly", out JsonElement railwayOnly) ||
                railwayOnly.ValueKind != JsonValueKind.True ||
                !native.TryGetProperty("railwayIncluded", out JsonElement railwayIncluded) ||
                railwayIncluded.ValueKind != JsonValueKind.True ||
                !native.TryGetProperty("cameraStable", out JsonElement stable) ||
                stable.ValueKind != JsonValueKind.True ||
                !native.TryGetProperty("curViewRestored", out JsonElement restored) ||
                restored.ValueKind != JsonValueKind.True)
                throw new InvalidDataException("MAP016 scheduled Railway did not provide all 625 exact-owned native receipts.");
            LWBridge.Map317.MapScanRun persisted =
                map317CommandService.ReadPersistedScanRunForProof(runId)
                ?? throw new InvalidDataException("MAP016 scheduled Railway run missing from real SQLite.");
            // Correlate the native artifact to THIS due cycle, not to a
            // completed Railway scan left from earlier in the same session.
            if (persisted.Id != runId || persisted.ServerId != targetServer ||
                persisted.Status != "completed" || persisted.Error is not null ||
                !persisted.SelectedTypes.SequenceEqual(["railway"], StringComparer.Ordinal) ||
                persisted.TotalBlocks != 2500 || persisted.CompletedBlocks != 2500 ||
                persisted.FailedBlocks != 0 ||
                persisted.CreatedAt < cycle.StartedAt ||
                persisted.CreatedAt < deadlineUtcMilliseconds ||
                persisted.UpdatedAt < persisted.CreatedAt)
                throw new InvalidDataException("MAP016 scheduled Railway run lacks SQLite 2500/2500 completion.");
            string timingPath = Path.Combine(overviewLifecycleService.LiveResourceRuntimeRoot,
                "map-engine-stage-metrics.json");
            using JsonDocument timingDocument = JsonDocument.Parse(
                await File.ReadAllTextAsync(timingPath, proofToken));
            JsonElement timing = timingDocument.RootElement;
            if (ReadStatusString(timing, "runId") != runId ||
                ReadStatusInt32(timing, "serverId") != targetServer ||
                ReadStatusString(timing, "scanMode") != "fast" ||
                !timing.TryGetProperty("wholeEngineMilliseconds", out JsonElement duration) ||
                !duration.TryGetDouble(out double engineMilliseconds) ||
                engineMilliseconds <= 0)
                throw new InvalidDataException("MAP016 Railway scan native timing is missing or belongs to a stale run.");
            object? search = await map317CommandService.InvokeAsync("map_search",
                JsonSerializer.SerializeToElement(new
                { kind = "railway", query = new { serverId = targetServer, page = 1, pageSize = 50 } },
                    JsonOptions.Default), proofToken);
            JsonElement rows = JsonSerializer.SerializeToElement(search, JsonOptions.Default);
            if (!rows.TryGetProperty("total", out JsonElement total) ||
                !total.TryGetInt32(out int savedTotal) || savedTotal < 0 ||
                !rows.TryGetProperty("rows", out JsonElement actualRows) ||
                actualRows.ValueKind != JsonValueKind.Array ||
                (nativeRailwayRecords == 0 && (savedTotal != 0 || actualRows.GetArrayLength() != 0)) ||
                (nativeRailwayRecords > 0 && savedTotal == 0))
                throw new InvalidDataException("MAP016 Railway native receipt and saved Map query disagree.");
            savedRailwayRecords = savedTotal;
            events.Add(new { stage = "native_sqlite_railway_verified", runId,
                nativeRailwayRecords, savedRailwayRecords, rectanglesCompleted = 625,
                persisted.TotalBlocks, persisted.CompletedBlocks, persisted.FailedBlocks,
                persisted.CreatedAt, persisted.UpdatedAt, engineMilliseconds,
                verifiedEmpty = nativeRailwayRecords == 0 && savedRailwayRecords == 0 });
            state = "qualified_pending_cleanup";
            await CaptureAsync("map016-auto-interval-returned.png");
            await SaveProgressAsync();
        }
        catch (Exception error)
        {
            primaryError = error;
            failure = error.ToString();
            state = "failed";
        }
        finally
        {
            if (armedByProof)
            {
                try
                {
                    // Disable before Stop to prevent any further scheduler
                    // admission. Cancel waits for the real Auto owner to settle
                    // its Stop and guarded original-server return.
                    using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    if (!overviewLifecycleService.MatchesOwnedMapScanSession(proofSession))
                        throw new InvalidDataException("Exact owner retired: cannot safely alter a successor's Auto or game.");
                    MapAutoScanSnapshot current = await mapAutoScanService.GetSnapshotAsync(cleanupBudget.Token);
                    if (current.Config.Enabled)
                        await mapAutoScanService.UpdateConfigAsync(
                            current.Config with { Enabled = false }, cleanupBudget.Token);
                    if (mapAutoScanService.IsCycleBusy)
                        await mapAutoScanService.CancelAsync(cleanupBudget.Token);
                    for (int i = 0; i < 450 &&
                        overviewLifecycleService.GetLiveServerId() != originalServer; i++)
                    {
                        if (!overviewLifecycleService.MatchesOwnedMapScanSession(proofSession))
                            throw new InvalidDataException("Exact owner was replaced during 2212 restoration.");
                        await Task.Delay(100, cleanupBudget.Token);
                    }
                    if (overviewLifecycleService.GetLiveServerId() == targetServer &&
                        !map317CommandService.IsScanActive &&
                        !mapAutoScanService.IsCycleBusy &&
                        overviewLifecycleService.MatchesOwnedMapScanSession(proofSession))
                    {
                        // If an interrupted native Auto return failed after
                        // its own Stop settled, use the production source-backed
                        // server-only return (never a coordinate/city Jump).
                        object? returned = await map317CommandService.InvokeAsync(
                            "server_jump",
                            JsonSerializer.SerializeToElement(new { serverId = originalServer },
                                JsonOptions.Default), cleanupBudget.Token);
                        JsonElement acknowledgement = JsonSerializer.SerializeToElement(
                            returned, JsonOptions.Default);
                        if (ReadStatusInt32(acknowledgement, "serverId") != originalServer)
                            throw new InvalidDataException("MAP016 cleanup's native return did not acknowledge 2212.");
                        for (int i = 0; i < 450 &&
                            overviewLifecycleService.GetLiveServerId() != originalServer; i++)
                        {
                            if (!overviewLifecycleService.MatchesOwnedMapScanSession(proofSession))
                                throw new InvalidDataException("Exact owner was replaced during corrective return.");
                            await Task.Delay(100, cleanupBudget.Token);
                        }
                        events.Add(new { stage = "source_owned_server_return_recovery",
                            acknowledgedServerId = originalServer,
                            observedServerId = overviewLifecycleService.GetLiveServerId() });
                    }
                    current = await mapAutoScanService.GetSnapshotAsync(cleanupBudget.Token);
                    if (current.Config.Enabled || current.Running || mapAutoScanService.IsCycleBusy ||
                        map317CommandService.IsScanActive ||
                        overviewLifecycleService.GetLiveServerId() != originalServer)
                        throw new InvalidDataException("MAP016 interval cleanup did not prove disabled Auto, idle native scan and original 2212.");
                    observedHomeReturn = true;
                    events.Add(new { stage = "cleanup_verified", autoEnabled = current.Config.Enabled,
                        autoRunning = current.Running, mapActive = map317CommandService.IsScanActive,
                        liveServer = overviewLifecycleService.GetLiveServerId(), exactOwner = true });
                }
                catch (Exception error)
                {
                    cleanupError = error;
                    cleanupFailure = error.ToString();
                    events.Add(new { stage = "cleanup_failed", error = error.ToString() });
                }
            }
            if (primaryError is null && cleanupError is null)
                state = "proven";
            else
                state = "failed";
            await SaveProgressAsync();
            if (state == "proven")
                await File.WriteAllTextAsync(outputPath,
                    await File.ReadAllTextAsync(progressPath, CancellationToken.None),
                    CancellationToken.None);
        }
        if (primaryError is not null && cleanupError is not null)
            throw new AggregateException("MAP016 interval proof and exact cleanup failed.",
                primaryError, cleanupError);
        if (cleanupError is not null)
            ExceptionDispatchInfo.Capture(cleanupError).Throw();
        if (primaryError is not null)
            ExceptionDispatchInfo.Capture(primaryError).Throw();
    }
}
