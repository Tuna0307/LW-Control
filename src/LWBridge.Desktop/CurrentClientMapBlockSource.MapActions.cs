using System.Globalization;
using System.Text.Json;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop;

internal sealed record CurrentClientTruckQuickRobResult(
    int ServerId,
    long MarchUuid,
    long TrainUuid,
    bool BattleWon,
    int RewardCount,
    JsonElement PlunderRewards,
    bool RewardNormalizationComplete,
    int? DailyRobCount);

internal sealed record CurrentClientDispatchPlunderResult(
    int ServerId,
    string TaskUuid,
    bool Succeeded,
    string? ErrorCode,
    bool RequestSent);

internal sealed record CurrentClientMapPlunderServerDayResult(
    long ServerTime,
    long ServerDayStartAt);

internal sealed record CurrentClientDispatchPlunderPendingHandle(
    Map317.DispatchPlunderArmJob Job,
    string RequestId,
    OverviewMapScanSession Session);

internal sealed record CurrentClientDispatchPlunderArmOutcome(
    Map317.DispatchPlunderArmResult Result,
    CurrentClientDispatchPlunderPendingHandle? Pending,
    Map317.DispatchPlunderResultEvent? ImmediateResult);

internal sealed record CurrentClientTruckPlunderPendingHandle(
    Map317.TruckPlunderArmJob Job,
    string RequestId,
    OverviewMapScanSession Session);

internal sealed record CurrentClientTruckPlunderArmOutcome(
    Map317.TruckPlunderArmResult Result,
    CurrentClientTruckPlunderPendingHandle? Pending,
    Map317.TruckPlunderResultEvent? ImmediateResult);

internal sealed partial class CurrentClientMapBlockSource
{
    internal async Task<CurrentClientMapPlunderServerDayResult> GetMapPlunderServerDayStartAsync(
        CancellationToken cancellationToken)
    {
        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        string requestId = "mapday" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "map-plunder-server-day.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "map-plunder-server-day-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);

        DateTimeOffset deadline = Now() + TimeSpan.FromSeconds(5);
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                if (!MatchesInt(root.Value, "schemaVersion", 1) ||
                    !MatchesString(root.Value, "bridgeVersion", OverviewBridgeVersion) ||
                    !MatchesString(root.Value, "profileId", session.ProfileId) ||
                    !MatchesString(root.Value, "sessionId", session.SessionId) ||
                    !MatchesString(root.Value, "challenge", session.Challenge) ||
                    !MatchesInt(root.Value, "gamePid", session.GamePid))
                {
                    throw new InvalidDataException(
                        "Map plunder server-day result did not match the active owned game session.");
                }

                string state = ReadOptionalString(root.Value, "state") ?? string.Empty;
                if (state == "proven")
                {
                    if (!root.Value.TryGetProperty("serverTime", out JsonElement serverTimeValue) ||
                        !serverTimeValue.TryGetInt64(out long serverTime) || serverTime <= 0 ||
                        !root.Value.TryGetProperty("serverDayStartAt", out JsonElement serverDayValue) ||
                        !serverDayValue.TryGetInt64(out long serverDayStartAt) || serverDayStartAt <= 0 ||
                        serverDayStartAt > serverTime || serverTime - serverDayStartAt >= 86_400_000L)
                    {
                        throw new InvalidDataException("Map plunder server-day result is invalid.");
                    }
                    RequireSameSession(session);
                    return new CurrentClientMapPlunderServerDayResult(serverTime, serverDayStartAt);
                }

                string error = ReadOptionalString(root.Value, "error") ?? "map plunder server day unavailable";
                throw new BridgeCommandException(
                    "GAME_CONNECTION_UNAVAILABLE",
                    "map plunder server day unavailable",
                    error);
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("map plunder server-day request timed out");
    }

    internal async Task<bool> ShareDispatchTaskToAllianceAsync(
        JsonElement row,
        CancellationToken cancellationToken)
    {
        if (row.ValueKind != JsonValueKind.Object ||
            !row.TryGetProperty("uuid", out JsonElement uuidValue) ||
            uuidValue.ValueKind != JsonValueKind.String)
            return false;

        string taskUuid = uuidValue.GetString() ?? string.Empty;
        long serverIdValue = ReadDispatchShareIntegerLike(row, "serverId");
        long xValue = ReadDispatchShareIntegerLike(row, "x");
        long yValue = ReadDispatchShareIntegerLike(row, "y");
        long cfgIdValue = ReadDispatchShareIntegerLike(row, "cfgId");
        if (!IsPositiveDecimalText(taskUuid) ||
            serverIdValue is <= 0 or > int.MaxValue ||
            xValue is <= 0 or > int.MaxValue ||
            yValue is <= 0 or > int.MaxValue ||
            cfgIdValue is <= 0 or > int.MaxValue)
            return false;

        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        int serverId = (int)serverIdValue;
        int x = (int)xValue;
        int y = (int)yValue;
        int cfgId = (int)cfgIdValue;
        string requestId = "dispatchshare" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "dispatch-share.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "dispatch-share-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            $"serverId={serverId.ToString(CultureInfo.InvariantCulture)}",
            $"x={x.ToString(CultureInfo.InvariantCulture)}",
            $"y={y.ToString(CultureInfo.InvariantCulture)}",
            $"cfgId={cfgId.ToString(CultureInfo.InvariantCulture)}",
            $"taskUuid={taskUuid}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);

        // Exact 0.3.17 host behavior gives each protected share call 5 seconds.
        // Success is accepted only after the current client observes the matching
        // ChatHeroDispatchShare response without errorCode.
        DateTimeOffset deadline = Now() + TimeSpan.FromSeconds(5);
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                if (!MatchesInt(root.Value, "schemaVersion", 1) ||
                    !MatchesString(root.Value, "bridgeVersion", OverviewBridgeVersion) ||
                    !MatchesString(root.Value, "profileId", session.ProfileId) ||
                    !MatchesString(root.Value, "sessionId", session.SessionId) ||
                    !MatchesString(root.Value, "challenge", session.Challenge) ||
                    !MatchesInt(root.Value, "gamePid", session.GamePid) ||
                    !MatchesInt(root.Value, "serverId", serverId) ||
                    !MatchesString(root.Value, "taskUuid", taskUuid))
                {
                    throw new InvalidDataException(
                        "Dispatch alliance-share result did not match the active owned session or requested task.");
                }

                string state = ReadOptionalString(root.Value, "state") ?? string.Empty;
                string? error = ReadOptionalString(root.Value, "errorCode");
                if (state == "proven")
                {
                    if (!MatchesBool(root.Value, "shared", true) || error is not null)
                        throw new InvalidDataException("Dispatch alliance-share success was not authoritative.");
                    RequireSameSession(session);
                    return true;
                }
                if (state is "rejected" or "failed" or "timeout")
                {
                    if (!MatchesBool(root.Value, "shared", false) || string.IsNullOrWhiteSpace(error))
                        throw new InvalidDataException("Dispatch alliance-share failure did not contain an authoritative error.");
                    return false;
                }
                throw new InvalidDataException("Dispatch alliance-share result did not contain a supported terminal state.");
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    internal async Task<IReadOnlyList<CurrentClientDispatchPlunderArmOutcome>> ArmDispatchPlunderBatchAsync(
        IReadOnlyList<Map317.DispatchPlunderArmJob> jobs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        if (jobs.Count is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(jobs), "Dispatch plunder arm batch must contain 1 to 200 jobs.");

        foreach (Map317.DispatchPlunderArmJob job in jobs)
        {
            if (job.Kind is not ("dispatch" or "ghost") || job.ServerId is < 1 or > 99999 ||
                job.OwnerServer is < 1 or > 99999 || !IsPositiveDecimalText(job.TaskUuid) || job.ExecuteAt <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(jobs), "Dispatch plunder arm job is invalid.");
            }
        }

        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        string batchId = "dispatchbatch" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "dispatch-plunder.txt");
        var lines = new List<string>(9 + jobs.Count * 6)
        {
            "schema=2",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={batchId}",
            $"count={jobs.Count.ToString(CultureInfo.InvariantCulture)}",
        };

        var handles = new CurrentClientDispatchPlunderPendingHandle[jobs.Count];
        for (int index = 0; index < jobs.Count; index++)
        {
            Map317.DispatchPlunderArmJob job = jobs[index];
            string requestId = "dispatchplunder" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            handles[index] = new CurrentClientDispatchPlunderPendingHandle(job, requestId, session);
            string prefix = "job" + (index + 1).ToString(CultureInfo.InvariantCulture);
            lines.Add($"{prefix}RequestId={requestId}");
            lines.Add($"{prefix}Kind={job.Kind}");
            lines.Add($"{prefix}ServerId={job.ServerId.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}OwnerServer={job.OwnerServer.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}TaskUuid={job.TaskUuid}");
            lines.Add($"{prefix}ExecuteAt={job.ExecuteAt.ToString(CultureInfo.InvariantCulture)}");
        }
        lines.Add(string.Empty);
        await WriteCommandAsync(requestPath, string.Join('\n', lines), cancellationToken).ConfigureAwait(false);

        var outcomes = new CurrentClientDispatchPlunderArmOutcome?[jobs.Count];
        int remaining = jobs.Count;
        DateTimeOffset deadline = Now() + TimeSpan.FromSeconds(5);
        while (remaining > 0 && Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            for (int index = 0; index < handles.Length; index++)
            {
                if (outcomes[index] is not null) continue;
                CurrentClientDispatchPlunderPendingHandle handle = handles[index];
                JsonElement? root = TryReadJson(DispatchPlunderResultPath(handle.RequestId));
                if (root is null || !MatchesString(root.Value, "requestId", handle.RequestId)) continue;
                ValidateDispatchArmEnvelope(root.Value, handle);
                string state = ReadOptionalString(root.Value, "state") ?? string.Empty;
                bool armed = MatchesBool(root.Value, "armed", true) || state == "armed";
                if (state == "armed")
                {
                    outcomes[index] = new CurrentClientDispatchPlunderArmOutcome(
                        new Map317.DispatchPlunderArmResult(
                            handle.Job.Kind, handle.Job.ServerId, handle.Job.TaskUuid, true),
                        handle,
                        null);
                }
                else
                {
                    Map317.DispatchPlunderResultEvent terminal = ParseDispatchPlunderResultEvent(root.Value, handle.Job);
                    outcomes[index] = armed
                        ? new CurrentClientDispatchPlunderArmOutcome(
                            new Map317.DispatchPlunderArmResult(
                                handle.Job.Kind, handle.Job.ServerId, handle.Job.TaskUuid, true),
                            null,
                            terminal)
                        : new CurrentClientDispatchPlunderArmOutcome(
                            new Map317.DispatchPlunderArmResult(
                                handle.Job.Kind, handle.Job.ServerId, handle.Job.TaskUuid, false,
                                terminal.ErrorCode ?? "DISPATCH_PLUNDER_SEND_FAILED"),
                            null,
                            null);
                }
                remaining--;
            }
            if (remaining > 0)
                await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        for (int index = 0; index < outcomes.Length; index++)
        {
            if (outcomes[index] is not null) continue;
            Map317.DispatchPlunderArmJob job = jobs[index];
            outcomes[index] = new CurrentClientDispatchPlunderArmOutcome(
                new Map317.DispatchPlunderArmResult(
                    job.Kind, job.ServerId, job.TaskUuid, false, "DISPATCH_PLUNDER_RESPONSE_TIMEOUT"),
                null,
                null);
        }
        return outcomes.Select(value => value!).ToArray();
    }

    internal Map317.DispatchPlunderResultEvent? TryDrainDispatchPlunderResult(
        CurrentClientDispatchPlunderPendingHandle handle)
    {
        JsonElement? root = TryReadJson(DispatchPlunderResultPath(handle.RequestId));
        if (root is null || !MatchesString(root.Value, "requestId", handle.RequestId)) return null;
        ValidateDispatchArmEnvelope(root.Value, handle);
        string state = ReadOptionalString(root.Value, "state") ?? string.Empty;
        if (state == "armed") return null;
        return ParseDispatchPlunderResultEvent(root.Value, handle.Job);
    }

    internal async Task<CurrentClientTruckPlunderArmOutcome> ArmTruckPlunderAsync(
        Map317.TruckPlunderArmJob job,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.ServerId is < 1 or > 99999 || !IsPositiveDecimalText(job.TrainUuid) ||
            string.IsNullOrWhiteSpace(job.JobId) || job.ExecuteAt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(job), "Truck plunder arm job is invalid.");
        }

        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        string requestId = "truckrob" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var handle = new CurrentClientTruckPlunderPendingHandle(job, requestId, session);
        string requestPath = Path.Combine(overviewRuntimeRoot, "truck-quick-rob.txt");
        var lines = new List<string>
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            $"serverId={job.ServerId.ToString(CultureInfo.InvariantCulture)}",
            $"trainUuid={job.TrainUuid}",
            $"jobId={job.JobId}",
            $"executeAt={job.ExecuteAt.ToString(CultureInfo.InvariantCulture)}",
        };
        if (job.RobTimes is int robTimes) lines.Add($"robTimes={robTimes.ToString(CultureInfo.InvariantCulture)}");
        if (job.MaxLootCount is int maxLootCount) lines.Add($"maxLootCount={maxLootCount.ToString(CultureInfo.InvariantCulture)}");
        lines.Add(string.Empty);
        await WriteCommandAsync(requestPath, string.Join('\n', lines), cancellationToken).ConfigureAwait(false);

        DateTimeOffset deadline = Now() + TimeSpan.FromSeconds(5);
        string resultPath = Path.Combine(overviewRuntimeRoot, "truck-quick-rob-result.json");
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                ValidateTruckArmEnvelope(root.Value, handle);
                string state = ReadOptionalString(root.Value, "state") ?? string.Empty;
                bool armed = MatchesBool(root.Value, "armed", true) || state == "armed";
                if (state == "armed")
                    return new CurrentClientTruckPlunderArmOutcome(new Map317.TruckPlunderArmResult(true), handle, null);

                Map317.TruckPlunderResultEvent terminal = ParseTruckPlunderResultEvent(root.Value, job);
                return armed
                    ? new CurrentClientTruckPlunderArmOutcome(new Map317.TruckPlunderArmResult(true), null, terminal)
                    : new CurrentClientTruckPlunderArmOutcome(
                        new Map317.TruckPlunderArmResult(false, terminal.ErrorCode ?? "server response timeout"),
                        null,
                        null);
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        return new CurrentClientTruckPlunderArmOutcome(
            new Map317.TruckPlunderArmResult(false, "server response timeout"), null, null);
    }

    internal Map317.TruckPlunderResultEvent? TryDrainTruckPlunderResult(
        CurrentClientTruckPlunderPendingHandle handle)
    {
        string resultPath = Path.Combine(overviewRuntimeRoot, "truck-quick-rob-result.json");
        JsonElement? root = TryReadJson(resultPath);
        if (root is null || !MatchesString(root.Value, "requestId", handle.RequestId)) return null;
        ValidateTruckArmEnvelope(root.Value, handle);
        string state = ReadOptionalString(root.Value, "state") ?? string.Empty;
        if (state == "armed") return null;
        return ParseTruckPlunderResultEvent(root.Value, handle.Job);
    }

    internal async Task ClearTruckPlunderPendingAsync(
        CurrentClientTruckPlunderPendingHandle handle,
        CancellationToken cancellationToken)
    {
        string requestId = "truckclear" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "truck-quick-rob-clear.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "truck-quick-rob-clear-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={handle.Session.ProfileId}",
            $"sessionId={handle.Session.SessionId}",
            $"challenge={handle.Session.Challenge}",
            $"gamePid={handle.Session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            $"serverId={handle.Job.ServerId.ToString(CultureInfo.InvariantCulture)}",
            $"trainUuid={handle.Job.TrainUuid}",
            $"jobId={handle.Job.JobId}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = Now() + TimeSpan.FromSeconds(5);
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                if (!MatchesInt(root.Value, "schemaVersion", 1) ||
                    !MatchesString(root.Value, "bridgeVersion", OverviewBridgeVersion) ||
                    !MatchesString(root.Value, "profileId", handle.Session.ProfileId) ||
                    !MatchesString(root.Value, "sessionId", handle.Session.SessionId) ||
                    !MatchesString(root.Value, "challenge", handle.Session.Challenge) ||
                    !MatchesInt(root.Value, "gamePid", handle.Session.GamePid))
                    throw new InvalidDataException("Truck pending-clear acknowledgement did not match the owned game session.");
                return;
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("truck pending clear acknowledgement timed out");
    }

    private string DispatchPlunderResultPath(string requestId) =>
        Path.Combine(overviewRuntimeRoot, "dispatch-plunder-result-" + requestId + ".json");

    private static void ValidateDispatchArmEnvelope(
        JsonElement root,
        CurrentClientDispatchPlunderPendingHandle handle)
    {
        ValidateDispatchPlunderEnvelope(
            root, handle.Session, handle.Job.ServerId, handle.Job.TaskUuid, handle.Job.ExecuteAt);
        if (!MatchesString(root, "kind", handle.Job.Kind) ||
            !MatchesInt(root, "ownerServer", handle.Job.OwnerServer))
            throw new InvalidDataException("Dispatch plunder result did not match the requested kind/owner server.");
    }

    private static Map317.DispatchPlunderResultEvent ParseDispatchPlunderResultEvent(
        JsonElement root,
        Map317.DispatchPlunderArmJob job)
    {
        string state = ReadOptionalString(root, "state") ?? string.Empty;
        string? rawError = ReadOptionalString(root, "errorCode") ?? ReadOptionalString(root, "error");
        if (state == "proven")
        {
            if (!MatchesBool(root, "requestSent", true) || !MatchesBool(root, "success", true) || rawError is not null)
                throw new InvalidDataException("Dispatch plunder terminal success was not authoritative.");
            return new Map317.DispatchPlunderResultEvent(
                job.Kind, job.ServerId, job.TaskUuid, true, OwnerServer: job.OwnerServer);
        }
        if (state == "rejected")
        {
            if (!MatchesBool(root, "requestSent", true) || string.IsNullOrWhiteSpace(rawError))
                throw new InvalidDataException("Dispatch plunder rejection did not include a sent request and error.");
            return new Map317.DispatchPlunderResultEvent(
                job.Kind, job.ServerId, job.TaskUuid, false,
                NormalizeDispatchPlunderError(rawError), OwnerServer: job.OwnerServer);
        }
        if (state is "failed" or "ambiguous")
        {
            string normalized = NormalizeDispatchPlunderError(
                rawError ?? (state == "ambiguous" ? "server response timeout" : "DISPATCH_PLUNDER_SEND_FAILED"));
            return new Map317.DispatchPlunderResultEvent(
                job.Kind, job.ServerId, job.TaskUuid, false, normalized, OwnerServer: job.OwnerServer);
        }
        throw new InvalidDataException("Dispatch plunder result did not contain a supported terminal state.");
    }

    private static void ValidateTruckArmEnvelope(
        JsonElement root,
        CurrentClientTruckPlunderPendingHandle handle)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "bridgeVersion", OverviewBridgeVersion) ||
            !MatchesString(root, "profileId", handle.Session.ProfileId) ||
            !MatchesString(root, "sessionId", handle.Session.SessionId) ||
            !MatchesString(root, "challenge", handle.Session.Challenge) ||
            !MatchesInt(root, "gamePid", handle.Session.GamePid) ||
            !MatchesInt(root, "serverId", handle.Job.ServerId) ||
            !MatchesString(root, "trainUuid", handle.Job.TrainUuid) ||
            !MatchesString(root, "jobId", handle.Job.JobId) ||
            !MatchesLong(root, "executeAt", handle.Job.ExecuteAt))
        {
            throw new InvalidDataException("Truck plunder result did not match the owned session or scheduled job.");
        }
    }

    private static Map317.TruckPlunderResultEvent ParseTruckPlunderResultEvent(
        JsonElement root,
        Map317.TruckPlunderArmJob job)
    {
        string state = ReadOptionalString(root, "state") ?? string.Empty;
        string? error = ReadOptionalString(root, "error");
        if (state == "proven")
        {
            if (!MatchesBool(root, "requestSent", true) ||
                !root.TryGetProperty("battleWon", out JsonElement battleWon) ||
                battleWon.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException("Truck plunder success did not contain an authoritative battle outcome.");

            int? dailyRobCount = null;
            if (root.TryGetProperty("dailyRobCount", out JsonElement daily) &&
                daily.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                if (!daily.TryGetInt32(out int parsed) || parsed < 0)
                    throw new InvalidDataException("Truck plunder dailyRobCount is invalid.");
                dailyRobCount = parsed;
            }

            // The current-v21 donor can observe raw reward entries, but its
            // reward:<type>:<item> key is synthetic and is not an exact 0.3.17
            // Map key producer. Do not persist that normalization as authoritative.
            return new Map317.TruckPlunderResultEvent(
                job.ServerId, job.TrainUuid, job.JobId, true,
                BattleWon: battleWon.GetBoolean(),
                PlunderRewards: null,
                DailyRobCount: dailyRobCount);
        }

        if (state is "rejected" or "failed" or "ambiguous")
        {
            string normalized = string.Equals(error, "server_response_timeout", StringComparison.Ordinal)
                ? "server response timeout"
                : error ?? (state == "ambiguous" ? "server response timeout" : "truck plunder failed");
            return new Map317.TruckPlunderResultEvent(
                job.ServerId, job.TrainUuid, job.JobId, false, normalized);
        }
        throw new InvalidDataException("Truck plunder result did not contain a supported terminal state.");
    }

    private static bool IsPositiveDecimalText(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(ch => ch is >= '0' and <= '9') &&
        value.Any(ch => ch != '0');

    private static long ReadDispatchShareIntegerLike(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value)) return 0;
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt64(out long integer)) return integer;
            if (value.TryGetDouble(out double floating) && double.IsFinite(floating))
                return floating >= long.MaxValue ? long.MaxValue :
                    floating <= long.MinValue ? long.MinValue : (long)floating;
        }
        if (value.ValueKind == JsonValueKind.String && long.TryParse(
                value.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed))
            return parsed;
        return 0;
    }

    internal async Task<CurrentClientTruckQuickRobResult> ExecuteTruckQuickRobAsync(
        int requestedServerId,
        long trainUuid,
        CancellationToken cancellationToken)
    {
        if (requestedServerId is < 1 or > 99999 || trainUuid <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedServerId), "Truck quick-rob target identity is invalid.");

        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        string requestId = "truckrob" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "truck-quick-rob.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "truck-quick-rob-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            $"serverId={requestedServerId.ToString(CultureInfo.InvariantCulture)}",
            $"trainUuid={trainUuid.ToString(CultureInfo.InvariantCulture)}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);

        // CURRENT-v21 ADAPTER: the source-proven Truck bridge has a 15 s pre-send
        // setup window and 30 s post-send response window. Keep those inside a
        // host envelope. A host-envelope timeout is state-unknown and MUST NOT
        // be treated as retryable because train.attack may already have reached
        // the server.
        DateTimeOffset deadline = Now() + TimeSpan.FromSeconds(50);
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                CurrentClientTruckQuickRobResult result = ValidateTruckQuickRobResult(
                    root.Value, session, requestedServerId, trainUuid);
                RequireSameSession(session);
                return result;
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new BridgeCommandException(
            "TRUCK_PLUNDER_STATE_UNKNOWN",
            "truck plunder execution state is unknown",
            new
            {
                ambiguous = true,
                serverId = requestedServerId,
                trainUuid = trainUuid.ToString(CultureInfo.InvariantCulture),
            });
    }

    private static CurrentClientTruckQuickRobResult ValidateTruckQuickRobResult(
        JsonElement root,
        OverviewMapScanSession session,
        int requestedServerId,
        long trainUuid)
    {
        string trainText = trainUuid.ToString(CultureInfo.InvariantCulture);
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "bridgeVersion", OverviewBridgeVersion) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "sessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesInt(root, "serverId", requestedServerId) ||
            !MatchesString(root, "trainUuid", trainText))
        {
            throw new InvalidDataException(
                "Truck quick-rob result did not match the active owned game session or requested target.");
        }

        string state = ReadOptionalString(root, "state") ?? string.Empty;
        bool requestSent = MatchesBool(root, "requestSent", true);
        if (state == "proven")
        {
            int currentServerId = RequirePositiveInt(root, "currentServerId");
            if (currentServerId != requestedServerId || !requestSent)
                throw new InvalidDataException("Truck quick-rob did not prove the requested live server and sent request.");
            if (!MatchesString(
                    root,
                    "method",
                    "RailwayUtil.ClickAttackTrain+LWMyStationDataManager.TryAttackTrain"))
            {
                throw new InvalidDataException("Truck quick-rob did not prove the supported current-client route.");
            }

            if (!root.TryGetProperty("battleWon", out JsonElement battleWonValue) ||
                battleWonValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidDataException("Truck quick-rob result did not contain an authoritative battle outcome.");
            }
            if (!root.TryGetProperty("rewardCount", out JsonElement rewardCountValue) ||
                !rewardCountValue.TryGetInt32(out int rewardCount) || rewardCount < 0)
            {
                throw new InvalidDataException("Truck quick-rob result reward count is invalid.");
            }
            if (!root.TryGetProperty("plunderRewards", out JsonElement plunderRewards) ||
                plunderRewards.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Truck quick-rob result plunderRewards must be an array.");
            }
            var rewardKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement reward in plunderRewards.EnumerateArray())
            {
                if (reward.ValueKind != JsonValueKind.Object ||
                    !reward.TryGetProperty("key", out JsonElement keyValue) ||
                    keyValue.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(keyValue.GetString()) ||
                    !rewardKeys.Add(keyValue.GetString()!) ||
                    !reward.TryGetProperty("name", out JsonElement nameValue) ||
                    nameValue.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(nameValue.GetString()) ||
                    !reward.TryGetProperty("iconPath", out JsonElement iconValue) ||
                    iconValue.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(iconValue.GetString()) ||
                    !reward.TryGetProperty("count", out JsonElement countValue) ||
                    !countValue.TryGetDouble(out double count) || !double.IsFinite(count) || count <= 0 ||
                    !reward.TryGetProperty("rewardType", out JsonElement typeValue) ||
                    !typeValue.TryGetInt32(out int rewardType) || rewardType <= 0 ||
                    !reward.TryGetProperty("itemId", out JsonElement itemValue) ||
                    !itemValue.TryGetInt64(out long itemId) || itemId <= 0)
                {
                    throw new InvalidDataException("Truck quick-rob result contains an invalid normalized plunder reward.");
                }
            }
            if (!root.TryGetProperty("rewardNormalizationComplete", out JsonElement normalizationValue) ||
                normalizationValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidDataException("Truck quick-rob result rewardNormalizationComplete must be a boolean.");
            }
            int? dailyRobCount = null;
            if (root.TryGetProperty("dailyRobCount", out JsonElement dailyRobValue) &&
                dailyRobValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                if (!dailyRobValue.TryGetInt32(out int parsedDailyRobCount) || parsedDailyRobCount < 0)
                    throw new InvalidDataException("Truck quick-rob result dailyRobCount is invalid.");
                dailyRobCount = parsedDailyRobCount;
            }

            if (!root.TryGetProperty("marchUuid", out JsonElement marchUuidValue) ||
                !marchUuidValue.TryGetInt64(out long resolvedMarchUuid) || resolvedMarchUuid <= 0)
            {
                throw new InvalidDataException("Truck quick-rob result marchUuid is invalid.");
            }

            return new CurrentClientTruckQuickRobResult(
                currentServerId,
                resolvedMarchUuid,
                trainUuid,
                battleWonValue.GetBoolean(),
                rewardCount,
                plunderRewards.Clone(),
                normalizationValue.GetBoolean(),
                dailyRobCount);
        }

        string error = ReadOptionalString(root, "error") ?? "truck_quick_rob_failed";
        if (state == "ambiguous")
        {
            if (!requestSent)
                throw new InvalidDataException("Ambiguous Truck quick-rob result did not prove that train.attack was sent.");

            object details = new
            {
                ambiguous = true,
                requestSent = true,
                serverId = requestedServerId,
                trainUuid = trainText,
                error,
            };
            if (string.Equals(error, "server_response_timeout", StringComparison.Ordinal))
                throw new BridgeCommandException(
                    "TRUCK_PLUNDER_RESPONSE_TIMEOUT",
                    "server response timeout",
                    details);
            throw new BridgeCommandException(
                "TRUCK_PLUNDER_RESULT_AMBIGUOUS",
                "truck plunder result is ambiguous",
                details);
        }

        if (state == "failed")
        {
            if (requestSent && string.Equals(error, "train_attack_rejected", StringComparison.Ordinal))
            {
                throw new BridgeCommandException(
                    "TRUCK_PLUNDER_SERVER_REJECTED",
                    "train attack rejected by the game",
                    new
                    {
                        requestSent = true,
                        serverId = requestedServerId,
                        trainUuid = trainText,
                    });
            }

            throw new BridgeCommandException(
                "TRUCK_QUICK_ROB_FAILED",
                error,
                new
                {
                    requestSent,
                    serverId = requestedServerId,
                    trainUuid = trainText,
                });
        }

        throw new InvalidDataException("Truck quick-rob result did not contain a supported terminal state.");
    }

    internal async Task<CurrentClientDispatchPlunderResult> ExecuteDispatchPlunderAsync(
        int requestedServerId,
        string taskUuid,
        long executeAt,
        CancellationToken cancellationToken)
    {
        if (requestedServerId is < 1 or > 99999 ||
            string.IsNullOrWhiteSpace(taskUuid) ||
            !taskUuid.All(ch => ch is >= '0' and <= '9') ||
            !long.TryParse(
                taskUuid,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long parsedTaskUuid) ||
            parsedTaskUuid <= 0 ||
            executeAt <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedServerId),
                "Dispatch plunder target identity or executeAt is invalid.");
        }

        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        string requestId =
            "dispatchplunder" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "dispatch-plunder.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "dispatch-plunder-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            $"serverId={requestedServerId.ToString(CultureInfo.InvariantCulture)}",
            $"taskUuid={taskUuid}",
            $"executeAt={executeAt.ToString(CultureInfo.InvariantCulture)}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);

        DateTimeOffset armDeadline = Now() + TimeSpan.FromSeconds(5);
        // The exact 0.3.17 Dispatch worker waits 15 seconds for the terminal
        // result after arming.  The file bridge gets a small outer envelope only
        // so its own 15-second terminal result can be observed without extending
        // the game-side deadline.
        DateTimeOffset hostDeadline = Now() + TimeSpan.FromSeconds(30);
        bool armed = false;
        while (Now() < hostDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                ValidateDispatchPlunderEnvelope(
                    root.Value,
                    session,
                    requestedServerId,
                    taskUuid,
                    executeAt);
                string state = ReadOptionalString(root.Value, "state") ?? string.Empty;
                if (state == "armed")
                {
                    if (MatchesBool(root.Value, "requestSent", true))
                        throw new InvalidDataException(
                            "Armed Dispatch plunder result unexpectedly claimed the request was already sent.");
                    armed = true;
                }
                else
                {
                    CurrentClientDispatchPlunderResult result =
                        ValidateDispatchPlunderTerminal(
                            root.Value,
                            requestedServerId,
                            taskUuid);
                    RequireSameSession(session);
                    return result;
                }
            }

            if (!armed && Now() >= armDeadline)
            {
                throw new BridgeCommandException(
                    "DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
                    "server response timeout",
                    new
                    {
                        ambiguous = false,
                        requestSent = false,
                        phase = "arm",
                        serverId = requestedServerId,
                        taskUuid,
                        executeAt,
                    });
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new BridgeCommandException(
            "DISPATCH_PLUNDER_STATE_UNKNOWN",
            "dispatch plunder execution state is unknown",
            new
            {
                ambiguous = true,
                requestSent = (bool?)null,
                phase = armed ? "result" : "arm",
                serverId = requestedServerId,
                taskUuid,
                executeAt,
            });
    }

    private static void ValidateDispatchPlunderEnvelope(
        JsonElement root,
        OverviewMapScanSession session,
        int requestedServerId,
        string taskUuid,
        long executeAt)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "bridgeVersion", OverviewBridgeVersion) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "sessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesInt(root, "serverId", requestedServerId) ||
            !MatchesString(root, "taskUuid", taskUuid) ||
            !MatchesLong(root, "executeAt", executeAt))
        {
            throw new InvalidDataException(
                "Dispatch plunder result did not match the active owned game session or requested target.");
        }
    }

    private static CurrentClientDispatchPlunderResult ValidateDispatchPlunderTerminal(
        JsonElement root,
        int requestedServerId,
        string taskUuid)
    {
        string state = ReadOptionalString(root, "state") ?? string.Empty;
        bool requestSent = MatchesBool(root, "requestSent", true);
        string? rawError = ReadOptionalString(root, "errorCode") ??
            ReadOptionalString(root, "error");

        if (state == "proven")
        {
            if (!requestSent || !MatchesBool(root, "success", true) || rawError is not null)
                throw new InvalidDataException(
                    "Dispatch plunder success did not prove one sent request without an error.");
            return new CurrentClientDispatchPlunderResult(
                requestedServerId,
                taskUuid,
                Succeeded: true,
                ErrorCode: null,
                RequestSent: true);
        }

        if (state == "rejected")
        {
            if (!requestSent || !MatchesBool(root, "success", false) ||
                string.IsNullOrWhiteSpace(rawError))
            {
                throw new InvalidDataException(
                    "Dispatch plunder rejection did not prove a sent request and authoritative error.");
            }
            return new CurrentClientDispatchPlunderResult(
                requestedServerId,
                taskUuid,
                Succeeded: false,
                ErrorCode: NormalizeDispatchPlunderError(rawError),
                RequestSent: true);
        }

        if (state == "ambiguous")
        {
            if (!requestSent)
                throw new InvalidDataException(
                    "Ambiguous Dispatch plunder result did not prove that DispatchSteal was sent.");
            throw new BridgeCommandException(
                "DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
                "server response timeout",
                new
                {
                    ambiguous = true,
                    requestSent = true,
                    serverId = requestedServerId,
                    taskUuid,
                    error = rawError,
                });
        }

        if (state == "failed")
        {
            string normalized = NormalizeDispatchPlunderError(
                rawError ?? "DISPATCH_PLUNDER_SEND_FAILED");
            throw new BridgeCommandException(
                normalized.StartsWith("DISPATCH_PLUNDER_SERVER_REJECTED:", StringComparison.Ordinal)
                    ? "DISPATCH_PLUNDER_SERVER_REJECTED"
                    : normalized,
                rawError ?? normalized,
                new
                {
                    ambiguous = false,
                    requestSent,
                    serverId = requestedServerId,
                    taskUuid,
                    normalizedError = normalized,
                });
        }

        throw new InvalidDataException(
            "Dispatch plunder result did not contain a supported terminal state.");
    }

    internal static string NormalizeDispatchPlunderError(string error)
    {
        string value = error.Trim();
        if (value.Length == 0)
            return "DISPATCH_PLUNDER_SERVER_REJECTED: empty server error";
        if (value.StartsWith("DISPATCH_PLUNDER_", StringComparison.Ordinal))
            return value;

        return value.ToLowerInvariant() switch
        {
            "invalid dispatch plunder target" or "invalid scheduled target" =>
                "DISPATCH_PLUNDER_INVALID_TARGET",
            "dispatch plunder request already pending" =>
                "DISPATCH_PLUNDER_REQUEST_PENDING",
            "dispatch manager unavailable" =>
                "DISPATCH_PLUNDER_MANAGER_UNAVAILABLE",
            "dispatch steal limit reached" =>
                "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED",
            "cross-server dispatch steal unavailable" =>
                "DISPATCH_PLUNDER_CROSS_SERVER_UNAVAILABLE",
            "server response timeout" =>
                "DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
            "game disconnected" =>
                "DISPATCH_PLUNDER_GAME_DISCONNECTED",
            "dispatch_des040" =>
                "DISPATCH_PLUNDER_TASK_COMPLETED",
            "dispatch_des043" =>
                "DISPATCH_PLUNDER_TASK_DISAPPEARED",
            "task expired" =>
                "DISPATCH_PLUNDER_TASK_EXPIRED",
            "client restarted" =>
                "DISPATCH_PLUNDER_CLIENT_RESTARTED",
            "invalid map plunder schedule" =>
                "DISPATCH_PLUNDER_INVALID_SCHEDULE",
            "map plunder schedule already armed" =>
                "DISPATCH_PLUNDER_ALREADY_ARMED",
            _ => "DISPATCH_PLUNDER_SERVER_REJECTED: " + value,
        };
    }

}
