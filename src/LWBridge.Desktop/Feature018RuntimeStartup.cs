using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed record Feature018StartupOwner(
    OverviewMapScanSession Session, Feature018AuthenticatedGameAccount Account);

// Profile-owned, never page-owned. Only saved background settings are applied;
// scans, equipment changes, city moves and manual mini-game actions stay manual.
internal sealed class Feature018RuntimeStartup
{
    private readonly Func<Feature018StartupOwner?> currentOwner;
    private readonly Func<JsonObject> readTasks;
    private readonly Func<JsonElement> readMetrics;
    private readonly Func<Feature018StartupOwner, string, JsonElement, CancellationToken, Task<object?>> invoke;
    private readonly HashSet<string> applied = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> errors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> desired = new(StringComparer.Ordinal);
    private Feature018StartupOwner? owner;
    private int busy;
    private string status = "{\"ready\":false,\"applied\":[],\"errors\":{}}";
    private string? chat;

    internal Feature018RuntimeStartup(Func<Feature018StartupOwner?> currentOwner,
        Func<JsonObject> readTasks, Func<JsonElement> readMetrics,
        Func<Feature018StartupOwner, string, JsonElement, CancellationToken, Task<object?>> invoke)
    {
        this.currentOwner = currentOwner;
        this.readTasks = readTasks;
        this.readMetrics = readMetrics;
        this.invoke = invoke;
    }

    internal JsonNode Snapshot => owner is not null && currentOwner() != owner
        ? new JsonObject { ["ready"] = false, ["applied"] = new JsonArray(), ["errors"] = new JsonObject() }
        : JsonNode.Parse(Volatile.Read(ref status))!;
    internal JsonNode? ChatSnapshot => currentOwner() == owner && Volatile.Read(ref chat) is { } value ? JsonNode.Parse(value) : null;

    internal static Feature018RuntimeStartup Create(OverviewLifecycleService lifecycle,
        EquipmentConfigCommandService equipment, ProfileRuntimeConfigStore store) => new(
        () =>
        {
            var session = lifecycle.GetReadyMapScanSession();
            if (session is null) return null;
            var account = Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount(
                Path.Combine(lifecycle.RuntimeRoot, "heartbeat.json"), session);
            return account is not null && lifecycle.MatchesOwnedMapScanSession(session)
                ? new(session, account) : null;
        }, store.ReadTasksSnapshot, () =>
        {
            var metrics = store.ReadVisualMetrics();
            return JsonSerializer.SerializeToElement(new { showFps = metrics.ShowFps, showPing = metrics.ShowPing });
        },
        async (expected, command, payload, token) =>
        {
            // Every queued exchange is pinned too: a selection/session change
            // between the coordinator check and mailbox admission cannot retarget it.
            var exchange = new Feature018OwnedGameExchange(
                () => lifecycle.GetReadyMapScanSession() == expected.Session ? expected.Session : null,
                candidate => candidate == expected.Session && lifecycle.MatchesOwnedMapScanSession(candidate),
                Path.Combine(lifecycle.RuntimeRoot, "feature018-request.txt"),
                Path.Combine(lifecycle.RuntimeRoot, "feature018-response.json"),
                currentAccount: candidate => Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount(
                    Path.Combine(lifecycle.RuntimeRoot, "heartbeat.json"), candidate));
            var commands = new Feature018GameCommandService(exchange, equipment, expected.Account, store);
            return await commands.InvokeAsync(command, payload, token).ConfigureAwait(false);
        });

    internal async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref busy, 1) != 0) return;
        try
        {
            var expected = currentOwner();
            if (expected is null)
            {
                Volatile.Write(ref chat, null);
                applied.Remove("visualMetrics");
                Publish(false);
                return;
            }
            if (expected != owner) { owner = expected; applied.Clear(); errors.Clear(); desired.Clear(); Volatile.Write(ref chat, null); }
            var tasks = readTasks();
            var metrics = readMetrics();
            void Changed(string name, string value)
            {
                if (!desired.TryGetValue(name, out var previous) || previous != value)
                { desired[name] = value; applied.Remove(name); }
            }
            // A late profile/config load must not leave a feature permanently
            // marked as disabled just because its first snapshot was empty.
            Changed("afk", tasks["monsterSweep"]?["enabled"]?.ToJsonString() ?? "false");
            Changed("stamina", tasks["staminaPotion"]?["enabled"]?.ToJsonString() ?? "false");
            Changed("allianceDrill", tasks["monsterSweep"]?["allianceDrill"]?["enabled"]?.ToJsonString() ?? "false");
            Changed("visualMetrics", metrics.GetRawText());
            var empty = JsonSerializer.SerializeToElement(new { });
            bool Current() => !cancellationToken.IsCancellationRequested && currentOwner() == expected;
            async Task<bool> Step(string name, Func<CancellationToken, Task> work, bool repeat = false)
            {
                if (!Current()) { Publish(false); return false; }
                if (!repeat && applied.Contains(name)) return true;
                try
                {
                    using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    bounded.CancelAfter(TimeSpan.FromSeconds(3));
                    await work(bounded.Token).ConfigureAwait(false);
                    if (!Current()) { Publish(false); return false; }
                    applied.Add(name); errors.Remove(name);
                }
                catch (Exception error) when (!cancellationToken.IsCancellationRequested)
                {
                    if (!Current()) { Publish(false); return false; }
                    errors[name] = error is BridgeCommandException bridge ? bridge.Code : error.GetType().Name;
                }
                return Current();
            }
            // Status applies the complete current trusted task configuration.
            // It is idempotent and also restores a paused Lua scheduler after a
            // temporary lease gap, without restarting pending game actions.
            if (!await Step("automation", async ct =>
                { await invoke(expected, "feature018_automation_status", empty, ct).ConfigureAwait(false); }, repeat: true)) return;
            if (!await Step("chat", async ct =>
            {
                Volatile.Write(ref chat, null);
                object? observed = await invoke(expected, "feature018_chat_status", empty, ct).ConfigureAwait(false);
                if (Current()) Volatile.Write(ref chat, JsonSerializer.Serialize(observed));
            }, repeat: true)) return;
            if (!await Step("visualMetrics", async ct =>
                { await invoke(expected, "feature018_visual_metrics_configure", metrics, ct).ConfigureAwait(false); })) return;
            if (!await Step("afk", async ct =>
            {
                if (tasks["monsterSweep"]?["enabled"]?.GetValue<bool>() != true) return;
                var observed = JsonSerializer.SerializeToElement(await invoke(expected, "feature018_afk_status", empty, ct).ConfigureAwait(false));
                if (!Current()) return;
                // Idle means this game VM has never started this engine. A
                // running/completed/stopped engine is not reset by startup or a
                // lost acknowledgement; its pending/counter history stays intact.
                if (!observed.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String)
                    throw new BridgeCommandException("GAME_RESULT_INVALID", "Native AFK startup status is unavailable.");
                if (state.GetString() == "idle")
                    await invoke(expected, "feature018_afk_configure", empty, ct).ConfigureAwait(false);
            })) return;
            if (!await Step("stamina", async ct =>
            {
                if (tasks["staminaPotion"]?["enabled"]?.GetValue<bool>() == true)
                    await invoke(expected, "automation_start", JsonSerializer.SerializeToElement(new { taskName = "staminaPotion" }), ct).ConfigureAwait(false);
            })) return;
            if (!await Step("allianceDrill", async ct =>
            {
                if (tasks["monsterSweep"]?["allianceDrill"]?["enabled"]?.GetValue<bool>() == true)
                    await invoke(expected, "automation_start", JsonSerializer.SerializeToElement(new { taskName = "allianceDrill" }), ct).ConfigureAwait(false);
            })) return;
            Publish(Current());
        }
        finally { Interlocked.Exchange(ref busy, 0); }
    }

    private void Publish(bool ready) => Volatile.Write(ref status,
        JsonSerializer.Serialize(new { ready = ready && errors.Count == 0,
            applied = applied.Order(StringComparer.Ordinal).ToArray(), errors }));
}
