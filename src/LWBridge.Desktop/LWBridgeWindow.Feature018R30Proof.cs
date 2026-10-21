using System.Text.Json;
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
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (!Feature018IsApprovedAccount(uid, serverId) || !lifecycle.OwnsExactProcess(owner.GamePid) ||
                !lifecycle.MatchesOwnedMapScanSession(owner) || ReadFeature018AuthenticatedAccount(lifecycle, owner) != approved)
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
            Owned();var value = await game.InvokeAsync("feature018_automation_status", empty.RootElement, token);Owned();
            return value is JsonElement element ? element.Clone() : throw new InvalidDataException("R30 game status unavailable.");
        }
        string selector = "[data-native-automation=" + kind + "]";
        var observations = new List<JsonElement>();
        JsonElement? terminal = null;
        bool stopped = false;
        bool uiConfirmed = false;
        try
        {
            Owned();
            await core.ExecuteScriptAsync("document.querySelectorAll('.side-nav button')[1]?.click()");
            await WaitDom("!!document.querySelector('[data-automation-category=alliance]')");
            await core.ExecuteScriptAsync("document.querySelector('[data-automation-category=alliance]').click()");
            if (kind == "officialPosition")
            {
                await WaitDom("!!document.querySelector('" + selector + " select:not(:disabled)')");
                await core.ExecuteScriptAsync("(()=>{const s=document.querySelector('" + selector + " select');s.value='10005';s.dispatchEvent(new Event('change',{bubbles:true}));})()");
            }
            await WaitDom("!!document.querySelector('" + selector + " button[role=switch]:not(:disabled)')");
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
                        if (observed.TryGetProperty("positionId", out _) && observed.TryGetProperty("readOnly", out var readOnly) && readOnly.GetBoolean())
                        { terminal = observed.Clone();uiConfirmed = true;break; }
                    }
                    await Task.Delay(500, token);
                }
                if (!terminal.HasValue) throw new InvalidDataException("R30 read-only observation did not settle.");
                return;
            }
            // Exercise the real saved-config switch through WebView, not a synthetic fixture.
            if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('" + selector + " button[role=switch]');if(b.getAttribute('aria-checked')!=='false')return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R30 proof requires a previously disabled service.");
            for (int i = 0; i < 80; i++)
            {
                var status = await Status();observations.Add(status);
                if (status.TryGetProperty("services", out var services) && services.TryGetProperty(kind, out var row) &&
                    row.TryGetProperty("state", out var state) && state.GetString() is "success" or "nothing_available" or "error" or "unconfirmed")
                { terminal = row.Clone();break; }
                await Task.Delay(500, token);
            }
            if (terminal.HasValue && terminal.Value.GetProperty("state").GetString() is "success" or "nothing_available")
            {
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
            // Cleanup is an explicit stop, never a resend of the gameplay request.
            await game.InvokeAsync("feature018_automation_stop", JsonSerializer.SerializeToElement(new { kind }), token);
            var store = new AutomationTaskConfigCommandService(profileRuntimeConfigStore!);
            var saved = (profileRuntimeConfigStore!.ReadTasksSnapshot()[kind]?.DeepClone() ?? AutomationTaskConfigCommandService.Default(kind)).AsObject();
            saved["enabled"] = false;
            await store.InvokeAsync("feature018_automation_config_save", JsonSerializer.SerializeToElement(new { kind, config = saved }), token);
            var final = await Status();observations.Add(final);
            string uiState = await core.ExecuteScriptAsync("(()=>{const c=document.querySelector('" + selector + "');return {language:document.querySelector('.language-select select')?.value,state:c?.dataset.automationState,text:c?.innerText};})()");
            stopped = !final.GetProperty("services").GetProperty(kind).GetProperty("enabled").GetBoolean();
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { kind, observeOnly, native = true, fixture = false,
                terminal, stopped, observations, uiState, uiConfirmed, success = stopped && uiConfirmed && terminal.HasValue &&
                    (observeOnly || terminal.Value.GetProperty("state").GetString() is "success" or "nothing_available") },
                new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
        }
        if (!stopped || !terminal.HasValue || terminal.Value.GetProperty("state").GetString() is not ("success" or "nothing_available"))
            throw new InvalidDataException("R30 live result was not confirmed; preserve its receipt.");
    }
}
