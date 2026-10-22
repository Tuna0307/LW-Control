using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Opt-in task-owned packaged WebView proof. No OS input, focus, window operations,
    // owner application access, synthetic game state, or replay of an uncertain send.
    private async Task RunFeature018R30AutomationProofAsync(CoreWebView2 core, string outputPath,
        string directory, OverviewMapScanSession owner, string uid, int serverId, string kind, CancellationToken token)
    {
        bool observeOnly = kind.StartsWith("observe-", StringComparison.Ordinal);
        if (observeOnly) kind = kind[8..];
        if (!AutomationTaskConfigCommandService.Kinds.Contains(kind, StringComparer.Ordinal))
            throw new InvalidDataException("R30 proof service is unavailable.");
        var lifecycle = overviewLifecycleService!;
        var approved = new Feature018HotkeySession(owner.ProfileId, owner.SessionId, uid, serverId);
        var game = new Feature018GameCommandService(new Feature018OwnedGameExchange(lifecycle), equipmentConfigService,
            new Feature018AuthenticatedGameAccount(uid, serverId), profileRuntimeConfigStore);
        using var empty = JsonDocument.Parse("{}");
        var ownerGrace = new Feature018ProofOwnerGrace(TimeSpan.FromSeconds(15));
        int statusBlips = 0;
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            // Same 15 s tolerance for a momentarily unavailable account read as the R29 proofs.
            if (!ownerGrace.Check(Feature018IsApprovedAccount(uid, serverId),
                    lifecycle.OwnsExactProcess(owner.GamePid) && lifecycle.MatchesOwnedMapScanSession(owner),
                    ReadFeature018AuthenticatedAccount(lifecycle, owner), approved))
                throw new InvalidDataException("R30 exact proof game UID/session changed; no action permitted.");
        }
        async Task WaitDom(string expression)
        {
            for (int i = 0; i < 120; i++)
            {
                Owned();if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R30 WebView control was unavailable: " + expression);
        }
        async Task<JsonElement> Status()
        {
            // Status is read-only: a brief native account blip (e.g. while the game refreshes player
            // data after a reward) is retried for up to 15 s; the exact owner check still applies.
            for (int attempt = 0; ; attempt++)
            {
                Owned();
                try
                {
                    var value = await game.InvokeAsync("feature018_automation_status", empty.RootElement, token);Owned();
                    return value is JsonElement element ? element.Clone() : throw new InvalidDataException("R30 game status unavailable.");
                }
                catch (BridgeCommandException error) when (error.Code is "GAME_ACCOUNT_CHANGED" or "GAME_CONNECTION_UNAVAILABLE" && attempt < 15)
                {
                    statusBlips++;await Task.Delay(1000, token);
                }
            }
        }
        string selector = "[data-native-automation=" + kind + "]";
        var observations = new List<JsonElement>();
        JsonElement? terminal = null;
        bool stopped = false;
        bool uiConfirmed = false;
        bool waitingFull = false;
        bool cleanupConfirmed = true;
        try
        {
            Owned();
            await core.ExecuteScriptAsync("document.querySelectorAll('.side-nav button')[1]?.click()");
            await WaitDom("!!document.querySelector('[data-automation-category=alliance]')");
            string category = kind switch
            {
                "treatment" or "dispatch" or "dispatchAssist" => "daily",
                "resourceGather" => "resourceGather",
                "tradeStation" => "trade",
                _ => "alliance",
            };
            // Per-kind setup through the real controls (each click saves through the native store).
            string[] setup = kind switch
            {
                "resourceGather" => [
                    "document.querySelector('[data-native-automation=resourceGather] .automation-config-trigger[aria-expanded=false]')?.click()",
                    "(()=>{const g=document.querySelectorAll('[data-native-automation=resourceGather] .automation-resource-gather-squad')[0];const c=g.querySelector('input[type=checkbox]');if(!c.checked)c.click();})()",
                    "(()=>{const g=document.querySelectorAll('[data-native-automation=resourceGather] .automation-resource-gather-squad')[0];const s=g.querySelectorAll('select');s[0].value='" + (Environment.GetEnvironmentVariable("LWBRIDGE_R30_GATHER_RESOURCE") ?? "food") + "';s[0].dispatchEvent(new Event('change',{bubbles:true}));})()",
                    "(()=>{const g=document.querySelectorAll('[data-native-automation=resourceGather] .automation-resource-gather-squad')[0];const s=g.querySelectorAll('select');s[1].value='" + (Environment.GetEnvironmentVariable("LWBRIDGE_R30_GATHER_LEVEL") ?? "1") + "';s[1].dispatchEvent(new Event('change',{bubbles:true}));})()",
                    "(()=>{const c=document.querySelector('[data-native-automation=resourceGather] .automation-form-grid input[type=checkbox]');if(!c.checked)c.click();})()",
                ],
                "dispatchAssist" => [
                    "document.querySelectorAll('[data-native-mission-fields=dispatchAssist] .automation-squad-choices input').forEach(c=>{if(!c.checked)c.click();})",
                ],
                _ => [],
            };
            await core.ExecuteScriptAsync("document.querySelector('[data-automation-category=" + category + "]').click()");
            if (setup.Length > 0)
                await WaitDom(kind == "dispatchAssist"
                    ? "!!document.querySelector('[data-native-mission-fields=dispatchAssist] .automation-inline-checkbox input:not(:disabled)')"
                    : "!!document.querySelector('" + selector + " button[role=switch]:not(:disabled)')");
            foreach (string step in setup)
            {
                Owned();await core.ExecuteScriptAsync(step);await Task.Delay(900, token);
            }
            if (kind == "resourceGather")
                await WaitDom("document.querySelectorAll('[data-native-automation=resourceGather] .automation-resource-gather-squad input[type=checkbox]')[0]?.checked===true");
            if (kind == "officialPosition")
            {
                // Owner rule 2026-10-21: the product supports all six positions, but live tests
                // may never apply for Vice President, Strategy or Defense (10002-10004).
                string position = Environment.GetEnvironmentVariable("LWBRIDGE_R30_OFFICIAL_POSITION") ?? "10005";
                if (position is not ("10005" or "10006" or "10007"))
                    throw new InvalidDataException("R30 live tests may only apply for positions 10005, 10006 or 10007.");
                await WaitDom("!!document.querySelector('" + selector + " select:not(:disabled)')");
                await core.ExecuteScriptAsync("(()=>{const s=document.querySelector('" + selector + " select');s.value='" + position + "';s.dispatchEvent(new Event('change',{bubbles:true}));})()");
                await WaitDom("document.querySelector('" + selector + " select').value==='" + position + "'");
            }
            await WaitDom(kind == "dispatchAssist" || observeOnly ? "!!document.querySelector('" + selector + "')" : "!!document.querySelector('" + selector + " button[role=switch]:not(:disabled)')");
            observations.Add(await Status());
            if (observeOnly)
            {
                for (int i = 0; i < 40; i++)
                {
                    Owned();
                    object? value = await game.InvokeAsync("feature018_automation_observe", JsonSerializer.SerializeToElement(new { kind }), token);
                    if (value is JsonElement observed)
                    {
                        observations.Add(observed.Clone());
                        if (observed.TryGetProperty("readOnly", out var readOnly) && readOnly.GetBoolean() &&
                            !(observed.TryGetProperty("waiting", out var waiting) && waiting.GetBoolean()) &&
                            !(observed.TryGetProperty("awaitingFreshLists", out var awaiting) && awaiting.GetBoolean()))
                        { terminal = observed.Clone();uiConfirmed = true;break; }
                    }
                    await Task.Delay(500, token);
                }
                if (!terminal.HasValue) throw new InvalidDataException("R30 read-only observation did not settle.");
                return;
            }
            // Exercise the real saved-config switch through WebView, not a synthetic fixture.
            if (kind == "dispatchAssist")
            {
                if (await core.ExecuteScriptAsync("(()=>{const c=document.querySelector('[data-native-mission-fields=dispatchAssist] .automation-inline-checkbox input');if(!c||c.checked)return false;c.click();return true;})()") != "true")
                    throw new InvalidDataException("R30 proof requires a previously disabled Dispatch Assist.");
            }
            else if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('" + selector + " button[role=switch]');if(b.getAttribute('aria-checked')!=='false')return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R30 proof requires a previously disabled service.");
            int polls = int.TryParse(Environment.GetEnvironmentVariable("LWBRIDGE_R30_WATCH_SECONDS"), out int watch) && watch is > 0 and <= 3600 ? watch * 2 : 80;
            string[] after = kind switch
            {
                "dispatch" => ["(()=>{const c=document.querySelectorAll('[data-native-mission-fields=dispatch] .automation-inline-checkbox input')[1];if(c&&!c.checked)c.click();})()"],
                _ => [],
            };
            foreach (string step in after)
            {
                await Task.Delay(900, token);Owned();await core.ExecuteScriptAsync(step);
            }
            for (int i = 0; i < polls; i++)
            {
                var status = await Status();observations.Add(status);
                if (status.TryGetProperty("services", out var services) && services.TryGetProperty(kind, out var row) &&
                    row.TryGetProperty("state", out var state) && state.GetString() is "success" or "nothing_available" or "error" or "unconfirmed")
                { terminal = row.Clone();break; }
                await Task.Delay(500, token);
            }
            // A full official list is a valid, still-retrying outcome: record it as observed.
            if (!terminal.HasValue && observations.Count > 0 && observations[^1].TryGetProperty("services", out var lastServices) &&
                lastServices.TryGetProperty(kind, out var lastRow) && lastRow.TryGetProperty("before", out var lastBefore) &&
                lastBefore.ValueKind == JsonValueKind.Object && lastBefore.TryGetProperty("full", out var full) && full.ValueKind == JsonValueKind.True)
            {
                terminal = lastRow.Clone();waitingFull = true;
            }
            if (terminal.HasValue && terminal.Value.GetProperty("state").GetString() is "success" or "nothing_available")
            {
                if (terminal.Value.GetProperty("state").GetString() == "success" && terminal.Value.TryGetProperty("confirmed", out var count) && count.GetInt32() > 0)
                    await WaitDom("Number(document.querySelector('" + selector + "')?.dataset.automationConfirmed)>=" + count.GetInt32());
                else
                    await WaitDom("document.querySelector('" + selector + "')?.dataset.automationState==='" + terminal.Value.GetProperty("state").GetString() + "'");
                uiConfirmed = true;
            }
            await core.ExecuteScriptAsync("document.querySelector('" + selector + "')?.scrollIntoView({block:'center'})");
            await using var capture = File.Create(Path.Combine(directory, "r30-" + kind + ".png"));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, capture);
        }
        finally
        {
            Owned();
            await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('" + selector + " button[role=switch]');if(b?.getAttribute('aria-checked')==='true')b.click();})()");
            // Let a switch-off cleanup (gathering recall) be sent and confirmed before the explicit stop.
            if (kind == "resourceGather") await Task.Delay(TimeSpan.FromSeconds(20), token);
            // Cleanup is an explicit stop, never a resend of the gameplay request.
            await game.InvokeAsync("feature018_automation_stop", JsonSerializer.SerializeToElement(new { kind }), token);
            var store = new AutomationTaskConfigCommandService(profileRuntimeConfigStore!);
            var saved = (profileRuntimeConfigStore!.ReadTasksSnapshot()[kind]?.DeepClone() ?? AutomationTaskConfigCommandService.Default(kind)).AsObject();
            saved["enabled"] = false;
            foreach (string sub in new[] { "autoExecute", "collectRewards", "autoHelp", "autoStartOwn", "autoJoinAlliance", "autoClaimRewards" })
                if (saved.ContainsKey(sub)) saved[sub] = false;
            await store.InvokeAsync("feature018_automation_config_save", JsonSerializer.SerializeToElement(new { kind, config = saved }), token);
            var final = await Status();observations.Add(final);
            var finalRow = final.GetProperty("services").GetProperty(kind);
            if (kind == "resourceGather" && terminal.HasValue && terminal.Value.TryGetProperty("confirmed", out var gathered) && gathered.GetInt32() > 0)
                cleanupConfirmed = finalRow.TryGetProperty("cleanupSubmitted", out var cleanupCount) && cleanupCount.GetInt32() == gathered.GetInt32() &&
                    finalRow.TryGetProperty("cleanupConfirmed", out var confirmedCleanup) && confirmedCleanup.GetInt32() == cleanupCount.GetInt32();
            string uiState = await core.ExecuteScriptAsync("(()=>{const c=document.querySelector('" + selector + "');return {language:document.querySelector('.language-select select')?.value,state:c?.dataset.automationState,text:c?.innerText};})()");
            stopped = !final.GetProperty("services").GetProperty(kind).GetProperty("enabled").GetBoolean();
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { kind, observeOnly, native = true, fixture = false,
                terminal, stopped, waitingFull, statusBlips, observations, uiState, uiConfirmed, cleanupConfirmed, success = stopped && cleanupConfirmed && terminal.HasValue &&
                    (waitingFull || uiConfirmed && (observeOnly || terminal.Value.GetProperty("state").GetString() is "success" or "nothing_available")) },
                new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
        }
        if (!stopped || !cleanupConfirmed || !terminal.HasValue || !waitingFull && terminal.Value.GetProperty("state").GetString() is not ("success" or "nothing_available"))
            throw new InvalidDataException("R30 live result was not confirmed; preserve its receipt.");
    }
}
