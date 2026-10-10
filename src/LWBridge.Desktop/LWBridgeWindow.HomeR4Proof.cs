using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

// A mounted, map-free Home proof: production WebView/command routing with two
// isolated inert profile owners. No genuine game, installer, Map scan or updater.
internal sealed partial class LWBridgeWindow
{
    private async Task RunHomeR4ProofAsync(CoreWebView2 core, string outputPath)
    {
        if (isolatedConfigRoot is null || profileRegistryService is null)
            throw new InvalidOperationException("R4 Home proof requires isolated two-profile production composition.");

        string expectedLanguage = language ?? (homeMapCampaignNarrow ? "ja" : "en");
        string expectedTheme = theme ?? (homeMapCampaignNarrow ? "dark" : "light");
        if (expectedLanguage is not ("en" or "ja") || expectedTheme is not ("light" or "dark"))
            throw new InvalidOperationException("R4 mounted Home proof requires EN/JA and light/dark.");

        async Task WaitForUiAsync(string expression, string label, int attempts = 240)
        {
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    if (await core.ExecuteScriptAsync("Boolean(" + expression + ")") == "true")
                        return;
                }
                catch (InvalidOperationException) when (i < attempts - 1) { }
                await Task.Delay(40);
            }
            throw new TimeoutException("R4 mounted Home did not reach " + label);
        }

        async Task ClickAsync(string expression, string label)
        {
            if (await core.ExecuteScriptAsync(expression) != "true")
                throw new InvalidDataException("R4 mounted Home could not click " + label);
        }

        async Task<JsonElement> ReadUiAsync(string expression)
        {
            string json = await core.ExecuteScriptAsync(expression);
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        async Task<JsonElement> InvokeNativeAsync(string command, object payload)
        {
            string requestId = "r4-owner-" + Guid.NewGuid().ToString("N");
            await core.ExecuteScriptAsync($$"""
                (() => {
                  const id = {{JsonSerializer.Serialize(requestId)}};
                  window.__homeR4OwnerResponse = null;
                  const handler = event => {
                    const response = event.data;
                    if (response?.kind !== 'response' || response.id !== id) return;
                    window.chrome.webview.removeEventListener('message', handler);
                    window.__homeR4OwnerResponse = response;
                  };
                  window.chrome.webview.addEventListener('message', handler);
                  window.chrome.webview.postMessage({kind:'invoke',
                    sessionId:window.__LWBridgeBootstrap.sessionId, id,
                    command:{{JsonSerializer.Serialize(command)}},
                    payload:{{JsonSerializer.Serialize(payload, JsonOptions.Default)}}});
                })()
                """);
            for (int i = 0; i < 240; i++)
            {
                JsonElement result = await ReadUiAsync("window.__homeR4OwnerResponse");
                if (result.ValueKind == JsonValueKind.Object) return result;
                await Task.Delay(40);
            }
            throw new TimeoutException("Actual native dispatcher did not respond to " + command);
        }

        async Task SelectAsync(string displayName, string expectedProfileId)
        {
            string name = JsonSerializer.Serialize(displayName, JsonOptions.Default);
            // Native selected-owner changes can be committed before React
            // finishes clearing its brief profile-selection busy gate.
            // Require the real visible target control to be enabled rather
            // than racing an ignored synthetic click; no assertion removed.
            await WaitForUiAsync($$"""
                [...document.querySelectorAll('.profile-compact-item')]
                  .some(item => item.querySelector('strong')?.textContent?.includes({{name}}) && !item.disabled)
                """, "enabled profile " + displayName);
            await ClickAsync($$"""
                (() => {
                  const button = [...document.querySelectorAll('.profile-compact-item')]
                    .find(item => item.querySelector('strong')?.textContent?.includes({{name}}));
                  if (!button || button.disabled) return false;
                  button.click();
                  return true;
                })()
                """, "profile " + displayName);
            for (int i = 0; i < 240 && backend.ProfileId != expectedProfileId; i++)
                await Task.Delay(40);
            if (backend.ProfileId != expectedProfileId)
                throw new InvalidDataException("Native UI selection did not activate " + expectedProfileId);
            await WaitForUiAsync($$"""
                [...document.querySelectorAll('.profile-compact-item, .profile-row')]
                  .some(item => item.classList.contains('active') &&
                    item.querySelector('strong')?.textContent?.includes({{name}}))
                """, "selected profile " + displayName);
        }

        async Task ClickCloseAsync(string label)
        {
            await WaitForUiAsync(
                "document.querySelector('.game-controls > button:not(.primary)')?.disabled === false",
                label + " admission");
            await ClickAsync("""
                (() => {
                  const button = document.querySelector('.game-controls > button:not(.primary)');
                  if (!button || button.disabled) return false;
                  button.click();
                  return true;
                })()
                """, label);
        }

        async Task ClickLaunchAsync(string label)
        {
            await WaitForUiAsync(
                "document.querySelector('.game-controls button.primary')?.disabled === false",
                label + " admission");
            await ClickAsync("""
                (() => {
                  const button = document.querySelector('.game-controls button.primary');
                  if (!button || button.disabled) return false;
                  button.click();
                  return true;
                })()
                """, label);
        }

        async Task WaitForOwnerAsync(
            HomeMapCampaignLifecycleProbe owner, bool alive, int requiredStopCalls, string label)
        {
            for (int i = 0; i < 240; i++)
            {
                if (owner.ProcessAlive == alive && owner.StopCalls >= requiredStopCalls)
                    return;
                await Task.Delay(40);
            }
            throw new InvalidDataException(label +
                $" processAlive={owner.ProcessAlive}, starts={owner.StartCalls}, stops={owner.StopCalls}");
        }

        static JsonElement ProfilePayload(string ownerId) =>
            JsonSerializer.SerializeToElement(new { profileId = ownerId }, JsonOptions.Default);

        async Task<JsonElement> InstanceStatusAsync(string ownerId) =>
            JsonSerializer.SerializeToElement(
                await backend.InvokeAsync("profile_instance_status", ProfilePayload(ownerId),
                    CancellationToken.None), JsonOptions.Default);

        string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        Directory.CreateDirectory(outputDirectory);
        await WaitForUiAsync(
            "!!document.querySelector('.quick-actions-panel') && document.querySelectorAll('.profile-compact-item').length === 2",
            "packaged native Home and two real profile controls");

        string languageJson = JsonSerializer.Serialize(expectedLanguage, JsonOptions.Default);
        await ClickAsync($$"""
            (() => {
              const select = document.querySelector('.language-select select');
              if (!select) return false;
              select.value = {{languageJson}};
              select.dispatchEvent(new Event('change', { bubbles: true }));
              return true;
            })()
            """, "language selector");
        await WaitForUiAsync(
            $"document.documentElement.lang === {languageJson} && document.querySelector('.language-select select')?.value === {languageJson}",
            expectedLanguage + " locale");

        string originalTheme = JsonSerializer.Deserialize<string>(
            await core.ExecuteScriptAsync("document.documentElement.dataset.theme || ''")) ?? "";
        const string clickTheme = """
            (() => {
              const button = document.querySelector('.theme-toggle');
              if (!button || button.disabled) return false;
              button.click();
              return true;
            })()
            """;
        if (originalTheme == expectedTheme)
        {
            await ClickAsync(clickTheme, "theme toggle away");
            await WaitForUiAsync(
                $"document.documentElement.dataset.theme !== '{expectedTheme}'",
                "opposite theme");
            await Task.Delay(300);
        }
        await ClickAsync(clickTheme, "target theme");
        await WaitForUiAsync(
            $"document.documentElement.dataset.theme === '{expectedTheme}' && document.querySelector('.theme-toggle')?.getAttribute('aria-pressed') === '{(expectedTheme == "dark" ? "true" : "false")}'",
            expectedTheme + " theme");

        HomeMapCampaignLifecycleProbe? a = null;
        HomeMapCampaignLifecycleProbe? b = null;
        for (int i = 0; i < 240; i++)
        {
            lock (homeMapCampaignProbes)
            {
                homeMapCampaignProbes.TryGetValue("campaign-A", out var ap);
                homeMapCampaignProbes.TryGetValue("campaign-B", out var bp);
                a = ap.Lifecycle;
                b = bp.Lifecycle;
            }
            if (a is not null && b is not null &&
                a.ProcessAlive && b.ProcessAlive && a.StartCalls > 0 && b.StartCalls > 0)
                break;
            await Task.Delay(40);
        }
        if (a is null || b is null || !a.ProcessAlive || !b.ProcessAlive)
            throw new InvalidDataException(
                $"Production ordered reconcile did not start two retained inert owners: A={a?.StartCalls}, B={b?.StartCalls}.");
        if (backend.ProfileId != "campaign-A")
            throw new InvalidDataException("The R4 mounted proof lost the selected original profile A.");
        JsonElement initialA = await InstanceStatusAsync("campaign-A");
        string aInstanceId = initialA.GetProperty("instanceId").GetString() ??
            throw new InvalidDataException("Initial A instance lacks an exact session identifier.");
        int originalAStops = a.StopCalls;
        int originalBStops = b.StopCalls;
        await WaitForUiAsync(
            "document.querySelector('.status-card.status-online')?.classList.contains('online') === true",
            "initial authenticated inert Connected Home");
        await WaitForUiAsync(
            "document.querySelector('.game-controls button.primary')?.disabled === true && document.querySelector('.game-controls > button:not(.primary)')?.disabled === false",
            "running Home buttons: Launch disabled and Close admitted");
        // Capture after the real 220 ms view-transition has completed: an
        // intermediate frame looks dimmed and is not a useful Home screenshot.
        await Task.Delay(500);

        string connectedScreenshot = Path.Combine(outputDirectory,
            Path.GetFileNameWithoutExtension(outputPath) + "-home-connected.png");
        await using (var picture = File.Create(connectedScreenshot))
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, picture);

        // R9 H-39: profile_note_set is a registry mutation addressed to an
        // explicit profile; its durable acknowledgement must not be mistaken
        // for a selected-Home-view status reply. Delay its native execution,
        // select B through the actual App, then complete A's note write.
        // Before correction the native dispatcher returned
        // PROFILE_GENERATION_RETIRED despite persisting the A note.
        string expectedAIndependentNote = "r9-note-A-during-B-selection";
        Task<HomeMapCampaignDelayedRequest> heldANoteCommand =
            ArmHomeMapCampaignCommandDelay("profile_note_set");
        Task<JsonElement> pendingANote = InvokeNativeAsync("profile_note_set",
            new { profileId = "campaign-A", note = expectedAIndependentNote });
        HomeMapCampaignDelayedRequest heldANote =
            await heldANoteCommand.WaitAsync(TimeSpan.FromSeconds(8));
        if (heldANote.ProfileId != "campaign-A")
            throw new InvalidDataException("R9 held note mutation was not issued from native A.");
        await SelectAsync("Campaign B", "campaign-B");
        ReleaseHomeMapCampaignCommandDelay();
        JsonElement noteReply = await pendingANote.WaitAsync(TimeSpan.FromSeconds(8));
        string? savedANote = profileRegistryService.Snapshot.Profiles
            .Single(profile => profile.Id == "campaign-A").Note;
        if (savedANote != expectedAIndependentNote)
            throw new InvalidDataException("R9 native note mutation was not durably attributed to A.");
        if (!noteReply.GetProperty("ok").GetBoolean() ||
            noteReply.GetProperty("result").GetProperty("selectedProfileId").GetString() != "campaign-B" ||
            backend.ProfileId != "campaign-B")
            throw new InvalidDataException(
                "A profile note was committed but its acknowledgement was retired by unrelated B selection: " + noteReply);
        await SelectAsync("Campaign A", "campaign-A");

        // R9 same global-registry invariant, independent operation family:
        // reorder is committed by the registry even if native selection
        // changes while the command is held. Its acknowledgement must be a
        // successful B-selected snapshot, never PROFILE_GENERATION_RETIRED.
        Task<HomeMapCampaignDelayedRequest> heldReorderCommand =
            ArmHomeMapCampaignCommandDelay("profile_reorder");
        Task<JsonElement> pendingReorder = InvokeNativeAsync("profile_reorder",
            new { profileIds = new[] { "campaign-B", "campaign-A" } });
        HomeMapCampaignDelayedRequest heldReorder =
            await heldReorderCommand.WaitAsync(TimeSpan.FromSeconds(8));
        if (heldReorder.ProfileId != "campaign-A")
            throw new InvalidDataException("R9 pending registry reorder did not begin with selected A.");
        await SelectAsync("Campaign B", "campaign-B");
        ReleaseHomeMapCampaignCommandDelay();
        JsonElement reorderReply = await pendingReorder.WaitAsync(TimeSpan.FromSeconds(8));
        if (!profileRegistryService.Snapshot.Profiles.Select(profile => profile.Id)
                .SequenceEqual(new[] { "campaign-B", "campaign-A" }) ||
            !reorderReply.GetProperty("ok").GetBoolean() ||
            reorderReply.GetProperty("result").GetProperty("selectedProfileId").GetString() != "campaign-B" ||
            backend.ProfileId != "campaign-B")
            throw new InvalidDataException(
                "A committed native profile reorder lost its acknowledgement across B selection: " + reorderReply);
        JsonElement resetOrder = await InvokeNativeAsync("profile_reorder",
            new { profileIds = new[] { "campaign-A", "campaign-B" } });
        if (!resetOrder.GetProperty("ok").GetBoolean() ||
            !profileRegistryService.Snapshot.Profiles.Select(profile => profile.Id)
                .SequenceEqual(new[] { "campaign-A", "campaign-B" }))
            throw new InvalidDataException("R9 test-owned profile display order was not restored.");
        await SelectAsync("Campaign A", "campaign-A");

        // R10 H-39: drive the *actual React profile-sidebar drag/drop*,
        // not a hand-built native command. The persistent B,A reorder can
        // finish after the user selects B. UI revision fencing must not
        // discard the successful registry order and leave stale visible A,B.
        await ClickAsync("""
            (() => { const toggle=document.querySelector('.profile-collapse');
              if (!toggle) return false; toggle.click(); return true; })()
            """, "expand profile rows for concurrent UI reorder");
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-row').length === 2 &&
            document.querySelectorAll('.profile-row .profile-copy strong')[0]?.textContent?.includes('Campaign A')
            """, "original UI A,B profile order before R10 drag");
        Task<HomeMapCampaignDelayedRequest> heldUiReorder =
            ArmHomeMapCampaignCommandDelay("profile_reorder");
        await ClickAsync("""
            (() => {
              const handles=document.querySelectorAll('.profile-row .profile-drag-handle');
              if (handles.length !== 2) return false;
              const transfer=new DataTransfer();
              return handles[1].dispatchEvent(
                new DragEvent('dragstart',{bubbles:true,cancelable:true,dataTransfer:transfer}));
            })()
            """, "start actual B-to-A native sidebar drag");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row')].some(row =>
                row.classList.contains('dragging') &&
                row.querySelector('strong')?.textContent?.includes('Campaign B'))
            """, "B drag revision accepted before drop");
        await ClickAsync("""
            (() => {
              const target=[...document.querySelectorAll('.profile-row')]
                .find(row => row.querySelector('strong')?.textContent?.includes('Campaign A'));
              if (!target) return false;
              // React intentionally calls preventDefault on an accepted
              // drop; dispatchEvent(false) therefore means it was handled,
              // not rejected. The required native command proves delivery.
              target.dispatchEvent(
                new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:new DataTransfer()}));
              return true;
            })()
            """, "drop B ahead of A through real JSX");
        HomeMapCampaignDelayedRequest heldUiReorderRequest =
            await heldUiReorder.WaitAsync(TimeSpan.FromSeconds(8));
        if (heldUiReorderRequest.ProfileId != "campaign-A")
            throw new InvalidDataException("R10 actual sidebar reorder was not issued while A selected.");
        await ClickAsync("""
            (() => {
              const button=[...document.querySelectorAll('.profile-row .profile-item')]
                .find(item => item.querySelector('strong')?.textContent?.includes('Campaign B'));
              if (!button || button.disabled) return false;
              button.click(); return true;
            })()
            """, "select B while UI reorder has not completed");
        for (int i = 0; i < 240 && backend.ProfileId != "campaign-B"; i++)
            await Task.Delay(40);
        if (backend.ProfileId != "campaign-B")
            throw new InvalidDataException("R10 real UI B selection did not progress during reorder.");
        ReleaseHomeMapCampaignCommandDelay();
        for (int i = 0; i < 240 && !profileRegistryService.Snapshot.Profiles
            .Select(profile => profile.Id).SequenceEqual(new[] { "campaign-B", "campaign-A" }); i++)
            await Task.Delay(40);
        if (!profileRegistryService.Snapshot.Profiles.Select(profile => profile.Id)
            .SequenceEqual(new[] { "campaign-B", "campaign-A" }))
            throw new InvalidDataException("R10 actual UI reorder failed to persist B,A.");
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-row .profile-copy strong')[0]
              ?.textContent?.includes('Campaign B') === true &&
            [...document.querySelectorAll('.profile-row')]
              .some(row => row.querySelector('strong')?.textContent?.includes('Campaign B') &&
                row.classList.contains('active'))
            """, "R10 completed registry order visible on still selected B", attempts: 40);
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row')]
              .some(row => row.querySelector('strong')?.textContent?.includes('Campaign A') &&
                row.querySelector('.profile-note')?.textContent?.includes('r9-note-A-during-B-selection')) &&
            [...document.querySelectorAll('.profile-row')]
              .some(row => row.querySelector('strong')?.textContent?.includes('Campaign B') &&
                !row.querySelector('.profile-note')?.textContent?.includes('r9-note-A-during-B-selection'))
            """, "R10 reordered UI preserves exact A note without copying to B");
        JsonElement resetUiOrder = await InvokeNativeAsync("profile_reorder",
            new { profileIds = new[] { "campaign-A", "campaign-B" } });
        if (!resetUiOrder.GetProperty("ok").GetBoolean())
            throw new InvalidDataException("R10 test-owned native order rollback failed.");
        await ClickAsync("""
            (() => {
              const button=[...document.querySelectorAll('.profile-row .profile-item')]
                .find(item => item.querySelector('strong')?.textContent?.includes('Campaign A'));
              if (!button || button.disabled) return false; button.click(); return true;
            })()
            """, "restore selected A after R10 registry order test");
        for (int i = 0; i < 240 && backend.ProfileId != "campaign-A"; i++)
            await Task.Delay(40);
        if (backend.ProfileId != "campaign-A")
            throw new InvalidDataException("R10 A view restoration failed.");
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-row .profile-copy strong')[0]
              ?.textContent?.includes('Campaign A') === true &&
            [...document.querySelectorAll('.profile-row')]
              .some(row => row.querySelector('strong')?.textContent?.includes('Campaign A') &&
                row.classList.contains('active'))
            """, "R10 exact A,B UI ordering and selected A restored");
        await ClickAsync("""
            (() => { const toggle=document.querySelector('.profile-collapse');
              if (!toggle) return false; toggle.click(); return true; })()
            """, "restore compact profile list for existing Home tests");
        await WaitForUiAsync("document.querySelectorAll('.profile-compact-item').length === 2",
            "R10 compact sidebar restored");

        // R11 H-39: R9 repaired native note acknowledgements, but the React
        // note caller still retires a successful note snapshot on a newer
        // selected-view request. Exercise the mounted note-dialog JSX,
        // hold its real native command, and switch B before it completes.
        // The test selects B via an actual DOM handler while the modal is
        // open to force this concurrency; this is a controlled scheduling
        // inverse, not a claim that a person can click through a modal.
        const string r11Note = "r11-note-A-during-selected-B";
        await ClickAsync("""
            (() => {
              const toggle=document.querySelector('.profile-collapse');
              if (!toggle) return false; toggle.click(); return true;
            })()
            """, "expand R11 A profile note editor");
        await WaitForUiAsync("document.querySelectorAll('.profile-row').length === 2",
            "R11 two editable profile rows");
        await ClickAsync("""
            (() => {
              const row=[...document.querySelectorAll('.profile-row')]
                .find(row=>row.querySelector('strong')?.textContent?.includes('Campaign A'));
              const button=row?.querySelector('.profile-note-edit');
              if (!button || button.disabled) return false; button.click(); return true;
            })()
            """, "open real R11 A note dialog");
        await WaitForUiAsync("document.querySelector('dialog[open] .profile-dialog input') !== null",
            "R11 mounted note dialog opened");
        await ClickAsync("""
            (() => {
              const input=document.querySelector('dialog[open] .profile-dialog input');
              if (!input) return false;
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set
                .call(input,'r11-note-A-during-selected-B');
              input.dispatchEvent(new Event('input',{bubbles:true}));
              return input.value==='r11-note-A-during-selected-B';
            })()
            """, "edit A note through actual mounted input");
        await WaitForUiAsync("""
            document.querySelector('dialog[open] .profile-dialog input')
              ?.value==='r11-note-A-during-selected-B'
            """, "R11 note value rendered before submitting");
        Task<HomeMapCampaignDelayedRequest> heldUiNote =
            ArmHomeMapCampaignCommandDelay("profile_note_set");
        await ClickAsync("""
            (() => {
              const submit=document.querySelector('dialog[open] .profile-dialog button[type=submit]');
              if (!submit || submit.disabled) return false; submit.click(); return true;
            })()
            """, "save A note through real R11 JSX form");
        HomeMapCampaignDelayedRequest heldUiNoteRequest =
            await heldUiNote.WaitAsync(TimeSpan.FromSeconds(8));
        if (heldUiNoteRequest.ProfileId != "campaign-A")
            throw new InvalidDataException("R11 note dialog did not issue native A mutation.");
        await ClickAsync("""
            (() => {
              const item=[...document.querySelectorAll('.profile-row .profile-item')]
                .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign B'));
              if (!item || item.disabled) return false; item.click(); return true;
            })()
            """, "R11 controlled selected B change during pending A note dialog");
        for (int i = 0; i < 240 && backend.ProfileId != "campaign-B"; i++) await Task.Delay(40);
        if (backend.ProfileId != "campaign-B")
            throw new InvalidDataException("R11 selected B did not advance past A note mutation.");
        ReleaseHomeMapCampaignCommandDelay();
        for (int i = 0; i < 240 &&
            profileRegistryService.Snapshot.Profiles.Single(profile=>profile.Id=="campaign-A").Note!=r11Note; i++)
            await Task.Delay(40);
        if (profileRegistryService.Snapshot.Profiles.Single(profile=>profile.Id=="campaign-A").Note!=r11Note)
            throw new InvalidDataException("R11 A note was not durably saved.");
        await WaitForUiAsync("document.querySelector('dialog[open]')===null",
            "R11 actual note save acknowledgement closed dialog");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row')]
              .some(row => row.querySelector('strong')?.textContent?.includes('Campaign A') &&
                row.querySelector('.profile-note')?.textContent?.includes('r11-note-A-during-selected-B')) &&
            [...document.querySelectorAll('.profile-row')]
              .some(row => row.querySelector('strong')?.textContent?.includes('Campaign B') &&
                row.classList.contains('active') &&
                !row.querySelector('.profile-note')?.textContent?.includes('r11-note-A-during-selected-B'))
            """, "R11 committed A note visibly owned by A with B still selected", attempts: 40);
        await ClickAsync("""
            (() => {
              const item=[...document.querySelectorAll('.profile-row .profile-item')]
                .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign A'));
              if (!item || item.disabled) return false; item.click(); return true;
            })()
            """, "R11 return to A after delayed note save");
        for (int i=0;i<240 && backend.ProfileId!="campaign-A";i++) await Task.Delay(40);
        if (backend.ProfileId!="campaign-A")
            throw new InvalidDataException("R11 A selected view was not restored.");
        await ClickAsync("""
            (() => {const toggle=document.querySelector('.profile-collapse');
              if (!toggle) return false; toggle.click();return true;})()
            """, "R11 restore compact profile sidebar");
        await WaitForUiAsync("document.querySelectorAll('.profile-compact-item').length===2",
            "R11 compact profile UI restored");

        // R12 H-39/H-45: an independent registry reorder is allowed to finish
        // while the selected B acknowledgement is held. R10 order-only
        // reconciliation may correctly show B,A yet leave the selected
        // profile's busy flag stuck if it shares registry request revisions.
        await ClickAsync("""
            (()=>{const toggle=document.querySelector('.profile-collapse');
              if(!toggle) return false; toggle.click(); return true;})()
            """, "R12 expand profile list");
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-row').length===2 &&
            document.querySelectorAll('.profile-row .profile-copy strong')[0]?.textContent?.includes('Campaign A') &&
            !document.querySelector('.profile-list')?.classList.contains('busy')
            """, "R12 A,B order and idle selection before overlap");
        await ClickAsync("""
            (()=>{const handle=document.querySelectorAll('.profile-row .profile-drag-handle')[1];
              if(!handle) return false;
              return handle.dispatchEvent(new DragEvent('dragstart',
                {bubbles:true,cancelable:true,dataTransfer:new DataTransfer()}));})()
            """, "R12 actual B drag begins before pending B selection");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row')].some(row=>
              row.classList.contains('dragging') &&
              row.querySelector('strong')?.textContent?.includes('Campaign B'))
            """, "R12 actual React B drag admitted");
        // profile_select is not an explicit-owner reply command. Hold the
        // native dispatch before mutation, not an unsupported reply hook.
        Task<HomeMapCampaignDelayedRequest> heldSelectB =
            ArmHomeMapCampaignCommandDelay("profile_select", targetProfileId:"campaign-A");
        await ClickAsync("""
            (()=>{const button=[...document.querySelectorAll('.profile-row .profile-item')]
                .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign B'));
              if(!button||button.disabled)return false;button.click();return true;})()
            """, "R12 select B through actual JSX while B drag is in progress");
        HomeMapCampaignDelayedRequest selectionB =
            await heldSelectB.WaitAsync(TimeSpan.FromSeconds(8));
        if (selectionB.ProfileId != "campaign-A" || backend.ProfileId != "campaign-A")
            throw new InvalidDataException("R12 B selection not delayed before its native mutation.");
        await WaitForUiAsync("document.querySelector('.profile-list')?.classList.contains('busy')===true",
            "R12 actual B selection pending on native dispatch");
        await ClickAsync("""
            (()=>{const target=[...document.querySelectorAll('.profile-row')]
                .find(row=>row.querySelector('strong')?.textContent?.includes('Campaign A'));
              if(!target)return false;
              target.dispatchEvent(new DragEvent('drop',
                {bubbles:true,cancelable:true,dataTransfer:new DataTransfer()}));
              return true;})()
            """, "R12 complete original B-before-A JSX reorder while B selection reply is held");
        for(int i=0;i<240 &&
            !profileRegistryService.Snapshot.Profiles.Select(profile=>profile.Id)
              .SequenceEqual(new[]{"campaign-B","campaign-A"});i++) await Task.Delay(40);
        if (!profileRegistryService.Snapshot.Profiles.Select(profile=>profile.Id)
              .SequenceEqual(new[]{"campaign-B","campaign-A"}))
            throw new InvalidDataException("R12 registry B,A reorder not committed while native selection held.");
        ReleaseHomeMapCampaignCommandDelay();
        for(int i=0;i<240 && backend.ProfileId!="campaign-B";i++)await Task.Delay(40);
        if(backend.ProfileId!="campaign-B")
            throw new InvalidDataException("R12 released native B selection did not apply.");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row')].some(row=>
              row.classList.contains('active') &&
              row.querySelector('strong')?.textContent?.includes('Campaign B')) &&
            document.querySelectorAll('.profile-row .profile-copy strong')[0]?.textContent?.includes('Campaign B')
            """, "R12 exact selected B and persisted B,A order visible");
        await WaitForUiAsync("""
            !document.querySelector('.profile-list')?.classList.contains('busy') &&
            [...document.querySelectorAll('.profile-row .profile-item')]
              .every(button=>!button.disabled)
            """, "R12 completed selected B clears busy after independent reorder", attempts:45);
        JsonElement restoredR12Order = await InvokeNativeAsync("profile_reorder",
            new { profileIds=new[]{"campaign-A","campaign-B"} });
        if(!restoredR12Order.GetProperty("ok").GetBoolean())
            throw new InvalidDataException("R12 exact test profile order restore failed.");
        if (!profileRegistryService.Snapshot.Profiles.Select(profile=>profile.Id)
              .SequenceEqual(new[]{"campaign-A","campaign-B"}))
            throw new InvalidDataException("R12 native profile order reset failed.");
        await ClickAsync("""
            (()=>{const button=[...document.querySelectorAll('.profile-row .profile-item')]
               .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign A'));
              if(!button||button.disabled)return false;button.click();return true;})()
            """, "R12 exact A selection restored");
        for(int i=0;i<240&&backend.ProfileId!="campaign-A";i++)await Task.Delay(40);
        if(backend.ProfileId!="campaign-A")
            throw new InvalidDataException("R12 failed to restore selected A.");
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-row .profile-copy strong')[0]?.textContent?.includes('Campaign A') &&
            [...document.querySelectorAll('.profile-row')].some(row=>
              row.classList.contains('active') &&
              row.querySelector('strong')?.textContent?.includes('Campaign A'))
            """, "R12 native A,B rollback reflected after actual A selection");
        await ClickAsync("""
            (()=>{const toggle=document.querySelector('.profile-collapse');
              if(!toggle)return false;toggle.click();return true;})()
            """, "R12 restore compact sidebar");
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-compact-item').length===2 &&
            [...document.querySelectorAll('.profile-compact-item')].some(button=>
              button.classList.contains('active') &&
              button.querySelector('strong')?.textContent?.includes('Campaign A'))
            """, "R12 exact A selected, compact sidebar restored");

        // R13 H-39/H-45: the inverse R12 ordering of command completions.
        // Native B selection commits while a reply is held AFTER dispatch,
        // so its snapshot still contains A,B. A real JSX reorder then commits
        // B,A in SQLite and visible React while B's stale selection ack waits.
        // Releasing that older full selection snapshot must not erase B,A.
        await ClickAsync("""
            (()=>{const toggle=document.querySelector('.profile-collapse');
              if(!toggle)return false;toggle.click();return true;})()
            """, "R13 expand original B-before-A sidebar");
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-row .profile-copy strong')[0]
              ?.textContent?.includes('Campaign A')===true &&
            document.querySelectorAll('.profile-row').length===2
            """, "R13 fixture A,B before post-native selection delay");
        await ClickAsync("""
            (()=>{const drag=[...document.querySelectorAll('.profile-row .profile-drag-handle')][1];
              if(!drag)return false;
              drag.dispatchEvent(new DragEvent('dragstart',
                {bubbles:true,cancelable:true,dataTransfer:new DataTransfer()}));
              return true;})()
            """, "R13 actual JSX drag B ahead of A");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row')].some(row=>
              row.classList.contains('dragging') &&
              row.querySelector('strong')?.textContent?.includes('Campaign B'))
            """, "R13 B drag captured before real selection");
        Task<HomeMapCampaignDelayedRequest> heldBSelectionAck =
            ArmHomeMapCampaignCommandDelay("profile_select",
                targetProfileId:"campaign-B", onReply:true);
        await ClickAsync("""
            (()=>{const button=[...document.querySelectorAll('.profile-row .profile-item')]
              .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign B'));
              if(!button||button.disabled)return false;
              button.click();return true;})()
            """, "R13 real JSX select B with delayed completed native reply");
        HomeMapCampaignDelayedRequest heldBReply =
            await heldBSelectionAck.WaitAsync(TimeSpan.FromSeconds(8));
        if(heldBReply.ProfileId!="campaign-B" || backend.ProfileId!="campaign-B")
            throw new InvalidDataException(
                "R13 native B selection must already have completed before its reply delay.");
        await WaitForUiAsync("document.querySelector('.profile-list')?.classList.contains('busy')===true",
            "R13 B owner native but React selection ack not yet delivered");
        await ClickAsync("""
            (()=>{const row=[...document.querySelectorAll('.profile-row')]
              .find(row=>row.querySelector('strong')?.textContent?.includes('Campaign A'));
              if(!row)return false;
              row.dispatchEvent(new DragEvent('drop',
                {bubbles:true,cancelable:true,dataTransfer:new DataTransfer()}));
              return true;})()
            """, "R13 drop B ahead of A after B selected natively");
        for(int i=0;i<240&&!profileRegistryService.Snapshot.Profiles
            .Select(profile=>profile.Id).SequenceEqual(new[]{"campaign-B","campaign-A"});i++)
            await Task.Delay(40);
        if(!profileRegistryService.Snapshot.Profiles.Select(profile=>profile.Id)
            .SequenceEqual(new[]{"campaign-B","campaign-A"}))
            throw new InvalidDataException("R13 native B,A persisted reorder was not committed.");
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-row .profile-copy strong')[0]
              ?.textContent?.includes('Campaign B')===true
            """, "R13 committed JSX B,A rendered before stale B selection ack");
        ReleaseHomeMapCampaignCommandDelay();
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-row .profile-copy strong')[0]
              ?.textContent?.includes('Campaign B')===true &&
            [...document.querySelectorAll('.profile-row')].some(row=>
              row.classList.contains('active') &&
              row.querySelector('strong')?.textContent?.includes('Campaign B')) &&
            !document.querySelector('.profile-list')?.classList.contains('busy')
            """, "R13 stale B selection snapshot cannot erase newer B,A registry order");
        JsonElement r13RestoredOrder = await InvokeNativeAsync("profile_reorder",
            new {profileIds=new[]{"campaign-A","campaign-B"}});
        if(!r13RestoredOrder.GetProperty("ok").GetBoolean() ||
          !profileRegistryService.Snapshot.Profiles.Select(profile=>profile.Id)
              .SequenceEqual(new[]{"campaign-A","campaign-B"}))
            throw new InvalidDataException("R13 exact test A,B registry rollback failed.");
        await ClickAsync("""
            (()=>{const button=[...document.querySelectorAll('.profile-row .profile-item')]
              .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign A'));
              if(!button||button.disabled)return false;button.click();return true;})()
            """, "R13 selected A refreshes reset A,B profile order");
        for(int i=0;i<240 && backend.ProfileId!="campaign-A";i++) await Task.Delay(40);
        if(backend.ProfileId!="campaign-A")
            throw new InvalidDataException("R13 exact A selection restore failed.");
        await WaitForUiAsync("""
            document.querySelectorAll('.profile-row .profile-copy strong')[0]
              ?.textContent?.includes('Campaign A')===true &&
            [...document.querySelectorAll('.profile-row')].some(row=>
              row.classList.contains('active') &&
              row.querySelector('strong')?.textContent?.includes('Campaign A'))
            """, "R13 exact A,B order + selected A restored after late snapshot inverse");
        await ClickAsync("""
            (()=>{const toggle=document.querySelector('.profile-collapse');
              if(!toggle)return false;toggle.click();return true;})()
            """, "R13 restore collapsed sidebar before remaining Home checks");
        await WaitForUiAsync("document.querySelectorAll('.profile-compact-item').length===2",
            "R13 compact sidebar restored");

        // R13 H-39: native first-note dispatch is held before execution.
        // Submitting a later exact-A note must not overtake it in SQLite;
        // otherwise the JSX projects the latest Y while the durable registry
        // is rolled back to older X once the first command finally runs.
        await ClickAsync("""
            (()=>{const toggle=document.querySelector('.profile-collapse');
              if(!toggle)return false;toggle.click();return true;})()
            """, "R13 expand exact A note editor for competing writes");
        await WaitForUiAsync("document.querySelectorAll('.profile-row').length===2",
            "R13 both profile rows available for competing notes");
        await ClickAsync("""
            (()=>{const row=[...document.querySelectorAll('.profile-row')]
              .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign A'));
              const edit=row?.querySelector('.profile-note-edit');
              if(!edit||edit.disabled)return false;edit.click();return true;})()
            """, "R13 open actual A note JSX dialog");
        await WaitForUiAsync("document.querySelector('dialog[open] .profile-dialog input')!==null",
            "R13 live editable A note dialog opened");
        await ClickAsync("""
            (()=>{const input=document.querySelector('dialog[open] .profile-dialog input');
              if(!input)return false;
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set
                .call(input,'r13-first-A-note');
              input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()
            """, "R13 input first A note");
        await WaitForUiAsync("""
            document.querySelector('dialog[open] .profile-dialog input')?.value==='r13-first-A-note'
            """, "R13 first note entered");
        Task<HomeMapCampaignDelayedRequest> heldFirstANote =
            ArmHomeMapCampaignCommandDelay("profile_note_set", targetProfileId:"campaign-A");
        await ClickAsync("""
            (()=>{const save=document.querySelector('dialog[open] .profile-dialog button[type=submit]');
              if(!save||save.disabled)return false;save.click();return true;})()
            """, "R13 first A note Save held in real native dispatcher");
        HomeMapCampaignDelayedRequest heldFirst =
            await heldFirstANote.WaitAsync(TimeSpan.FromSeconds(8));
        if(heldFirst.ProfileId!="campaign-A")
            throw new InvalidDataException("R13 first submitted note did not originate from selected A.");
        await ClickAsync("""
            (()=>{const input=document.querySelector('dialog[open] .profile-dialog input');
              if(!input)return false;
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set
                .call(input,'r13-second-A-note');
              input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()
            """, "R13 change same A note while first Save pending");
        await WaitForUiAsync("""
            document.querySelector('dialog[open] .profile-dialog input')?.value==='r13-second-A-note'
            """, "R13 second note entered");
        await ClickAsync("""
            (()=>{const save=document.querySelector('dialog[open] .profile-dialog button[type=submit]');
              if(!save||save.disabled)return false;save.click();return true;})()
            """, "R13 submit newer note while old A native dispatch remains held");
        await Task.Delay(300);
        ReleaseHomeMapCampaignCommandDelay();
        await WaitForUiAsync("document.querySelector('dialog[open]')===null",
            "R13 both successful note callbacks closed actual dialog");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row')].some(row=>
              row.querySelector('strong')?.textContent?.includes('Campaign A') &&
              row.querySelector('.profile-note')?.textContent?.includes('r13-second-A-note'))
            """, "R13 latest A note visibly saved");
        await Task.Delay(350);
        if(profileRegistryService.Snapshot.Profiles
            .Single(profile=>profile.Id=="campaign-A").Note!="r13-second-A-note" ||
          profileRegistryService.Snapshot.Profiles.Single(profile=>profile.Id=="campaign-B")
            .Note=="r13-second-A-note")
            throw new InvalidDataException(
                "R13 older concurrent note overwrote newer exact A persistent note.");

        // R14 H-39/H-45: the converse case to two successful rapid Saves.
        // First A note succeeds while a newer queued A note fails. The first
        // committed value must not be discarded just because a later revision
        // was requested: both SQLite and visible React must converge to X.
        await ClickAsync("""
            (()=>{const row=[...document.querySelectorAll('.profile-row')]
              .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign A'));
              const edit=row?.querySelector('.profile-note-edit');
              if(!edit||edit.disabled)return false;edit.click();return true;})()
            """, "R14 open real A note dialog before failed second submit");
        await WaitForUiAsync("document.querySelector('dialog[open] .profile-dialog input')!==null",
            "R14 real A note modal ready");
        await ClickAsync("""
            (()=>{const input=document.querySelector('dialog[open] .profile-dialog input');
              if(!input)return false;
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set
                .call(input,'r14-committed-A-note');
              input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()
            """, "R14 first exact A note entered");
        await WaitForUiAsync(
            "document.querySelector('dialog[open] .profile-dialog input')?.value==='r14-committed-A-note'",
            "R14 first A draft reflected in React");
        Task<HomeMapCampaignDelayedRequest> heldR14First =
            ArmHomeMapCampaignCommandDelay("profile_note_set", targetProfileId:"campaign-A");
        await ClickAsync("""
            (()=>{const save=document.querySelector('dialog[open] .profile-dialog button[type=submit]');
              if(!save||save.disabled)return false;save.click();return true;})()
            """, "R14 submit first A note through real JSX");
        HomeMapCampaignDelayedRequest firstR14 =
            await heldR14First.WaitAsync(TimeSpan.FromSeconds(8));
        if(firstR14.ProfileId!="campaign-A")
            throw new InvalidDataException("R14 queued first note did not retain A owner.");
        await ClickAsync("""
            (()=>{const input=document.querySelector('dialog[open] .profile-dialog input');
              if(!input)return false;
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set
                .call(input,'r14-rejected-A-note');
              input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()
            """, "R14 input newer A note before pending first success");
        await WaitForUiAsync(
            "document.querySelector('dialog[open] .profile-dialog input')?.value==='r14-rejected-A-note'",
            "R14 newer A note visible in modal");
        await ClickAsync("""
            (()=>{const save=document.querySelector('dialog[open] .profile-dialog button[type=submit]');
              if(!save||save.disabled)return false;save.click();return true;})()
            """, "R14 submit later A note set to fail in isolated native producer");
        // The first native execution is held; skip its success and reject
        // exactly the subsequent same-owner command, without changing SQLite.
        RejectNextHomeMapCampaignCommand("profile_note_set", successfulCallsBeforeRejection:1);
        ReleaseHomeMapCampaignCommandDelay();
        for(int i=0;i<240 && profileRegistryService.Snapshot.Profiles
            .Single(profile=>profile.Id=="campaign-A").Note!="r14-committed-A-note";i++)
            await Task.Delay(40);
        if(profileRegistryService.Snapshot.Profiles
            .Single(profile=>profile.Id=="campaign-A").Note!="r14-committed-A-note")
            throw new InvalidDataException("R14 first A note did not commit successfully.");
        await WaitForUiAsync("document.querySelector('.profile-error')!==null",
            "R14 newer failed note error surfaced in real sidebar");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row')].some(row=>
              row.querySelector('strong')?.textContent?.includes('Campaign A') &&
              row.querySelector('.profile-note')?.textContent?.includes('r14-committed-A-note'))
            """, "R14 durable successful first A note visible after rejected later Save", attempts:50);
        if(profileRegistryService.Snapshot.Profiles
            .Single(profile=>profile.Id=="campaign-B").Note=="r14-committed-A-note")
            throw new InvalidDataException("R14 failed A retry contaminated B native note.");
        // A failed Save intentionally expands the sidebar error pane even
        // when the collapse switch is set. Clear that error through a real
        // successful same-value retry before restoring the compact fixture.
        await ClickAsync("""
            (()=>{const row=[...document.querySelectorAll('.profile-row')]
              .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign A'));
              const edit=row?.querySelector('.profile-note-edit');
              if(!edit||edit.disabled)return false;edit.click();return true;})()
            """, "R14 reopen A note dialog for successful fixture error clear");
        await WaitForUiAsync("document.querySelector('dialog[open] .profile-dialog input')!==null",
            "R14 A note retry dialog opened");
        await ClickAsync("""
            (()=>{const input=document.querySelector('dialog[open] .profile-dialog input');
              if(!input)return false;
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set
                .call(input,'r14-committed-A-note');
              input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()
            """, "R14 retry exact durable A note through JSX input");
        await WaitForUiAsync(
            "document.querySelector('dialog[open] .profile-dialog input')?.value==='r14-committed-A-note'",
            "R14 retry A note reflected in JSX field");
        await ClickAsync("""
            (()=>{const save=document.querySelector('dialog[open] .profile-dialog button[type=submit]');
              if(!save||save.disabled)return false;save.click();return true;})()
            """, "R14 successful same-note retry clears isolated rejection error");
        await WaitForUiAsync("document.querySelector('dialog[open]')===null && document.querySelector('.profile-error')===null",
            "R14 failure panel cleared after successful native A note retry");

        // R14 inverse failure ordering: failed predecessor X must not poison
        // the exact-A promise chain or block newer Y from committing; B and
        // the selected Home owner remain untouched. Submit through real JSX.
        await ClickAsync("""
            (()=>{const row=[...document.querySelectorAll('.profile-row')]
              .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign A'));
              const edit=row?.querySelector('.profile-note-edit');
              if(!edit||edit.disabled)return false;edit.click();return true;})()
            """, "R14 open A note editor for failed-predecessor retry");
        await WaitForUiAsync("document.querySelector('dialog[open] .profile-dialog input')!==null",
            "R14 failed-predecessor note dialog opened");
        await ClickAsync("""
            (()=>{const input=document.querySelector('dialog[open] .profile-dialog input');
              if(!input)return false;
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set
                .call(input,'r14-rejected-first-A-note');
              input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()
            """, "R14 enter failing first A note");
        await WaitForUiAsync(
            "document.querySelector('dialog[open] .profile-dialog input')?.value==='r14-rejected-first-A-note'",
            "R14 first failing A draft visible");
        Task<HomeMapCampaignDelayedRequest> heldR14RejectedFirst =
            ArmHomeMapCampaignCommandDelay("profile_note_set", targetProfileId:"campaign-A");
        await ClickAsync("""
            (()=>{const save=document.querySelector('dialog[open] .profile-dialog button[type=submit]');
              if(!save||save.disabled)return false;save.click();return true;})()
            """, "R14 first A native request held before intentional rejection");
        HomeMapCampaignDelayedRequest firstRejected =
            await heldR14RejectedFirst.WaitAsync(TimeSpan.FromSeconds(8));
        if(firstRejected.ProfileId!="campaign-A")
            throw new InvalidDataException("R14 failing predecessor did not retain exact A.");
        await ClickAsync("""
            (()=>{const input=document.querySelector('dialog[open] .profile-dialog input');
              if(!input)return false;
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set
                .call(input,'r14-success-after-failure-A');
              input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()
            """, "R14 input successful later A note before failed X settles");
        await WaitForUiAsync(
            "document.querySelector('dialog[open] .profile-dialog input')?.value==='r14-success-after-failure-A'",
            "R14 later A retry value committed to JSX input");
        await ClickAsync("""
            (()=>{const save=document.querySelector('dialog[open] .profile-dialog button[type=submit]');
              if(!save||save.disabled)return false;save.click();return true;})()
            """, "R14 submit newer Y after held failing X");
        RejectNextHomeMapCampaignCommand("profile_note_set");
        ReleaseHomeMapCampaignCommandDelay();
        await WaitForUiAsync("document.querySelector('dialog[open]')===null",
            "R14 newer A note success closes original note dialog");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row')].some(row=>
              row.querySelector('strong')?.textContent?.includes('Campaign A') &&
              row.querySelector('.profile-note')?.textContent?.includes('r14-success-after-failure-A'))
            """, "R14 failed older native note did not block newer successful A note");
        if(profileRegistryService.Snapshot.Profiles.Single(profile=>profile.Id=="campaign-A")
              .Note!="r14-success-after-failure-A" ||
           profileRegistryService.Snapshot.Profiles.Single(profile=>profile.Id=="campaign-B")
              .Note=="r14-success-after-failure-A")
            throw new InvalidDataException("R14 rejected first A note blocked later durable Y or contaminated B.");
        // A rejected asynchronous Save may leave a visible error after newer
        // successful Save. A real same-value retry clears it before returning
        // to the compact sidebar used by subsequent exact-owner tests.
        if(await core.ExecuteScriptAsync("document.querySelector('.profile-error')!==null") == "true")
        {
            await ClickAsync("""
                (()=>{const row=[...document.querySelectorAll('.profile-row')]
                  .find(item=>item.querySelector('strong')?.textContent?.includes('Campaign A'));
                  const edit=row?.querySelector('.profile-note-edit');
                  if(!edit||edit.disabled)return false;edit.click();return true;})()
                """, "R14 reopen saved Y note for exact error reset");
            await WaitForUiAsync("document.querySelector('dialog[open] .profile-dialog input')!==null",
                "R14 successful Y retry editor opened");
            await ClickAsync("""
                (()=>{const save=document.querySelector('dialog[open] .profile-dialog button[type=submit]');
                  if(!save||save.disabled)return false;save.click();return true;})()
                """, "R14 save already-durable Y through native JSX to clear failure panel");
            await WaitForUiAsync("document.querySelector('dialog[open]')===null && document.querySelector('.profile-error')===null",
                "R14 newer-success note error panel cleared");
        }
        await ClickAsync("""
            (()=>{const toggle=document.querySelector('.profile-collapse');
              if(!toggle)return false;toggle.click();return true;})()
            """, "R13 restore compact profile sidebar after same-A note ordering");
        await WaitForUiAsync("document.querySelectorAll('.profile-compact-item').length===2",
            "R13 same-owner note fixture compacted");

        // Hold an actual A Home status request at the native command boundary;
        // selecting B must not retire A or render A's late response into B.
        long oldAGeneration = Volatile.Read(ref profileRuntimeGeneration);
        Task<HomeMapCampaignDelayedRequest> heldAStatus =
            ArmHomeMapCampaignCommandDelay("get_status");
        await ClickAsync("""
            (() => {
              const button = [...document.querySelectorAll('.top-actions button.top-action.secondary')]
                .at(-1);
              if (!button) return false;
              button.click();
              return true;
            })()
            """, "A Home status refresh before selection");
        HomeMapCampaignDelayedRequest oldAStatus =
            await heldAStatus.WaitAsync(TimeSpan.FromSeconds(8));
        if (oldAStatus.ProfileId != "campaign-A" ||
            oldAStatus.ProfileGeneration != oldAGeneration)
            throw new InvalidDataException("Delayed Home status was not issued by original owner A.");

        await SelectAsync("Campaign B", "campaign-B");
        ReleaseHomeMapCampaignCommandDelay();
        await Task.Delay(250);
        if (backend.ProfileId != "campaign-B")
            throw new InvalidDataException("Late A Home status switched selected B back to A.");
        if (!a.ProcessAlive || a.StopCalls != originalAStops || !b.ProcessAlive)
            throw new InvalidDataException("Selecting B retired a running profile A or lost B's own process.");
        JsonElement bBeforeClose = await InstanceStatusAsync("campaign-B");
        string bInstanceId = bBeforeClose.GetProperty("instanceId").GetString() ??
            throw new InvalidDataException("Selected B has no independently retained session.");
        if (aInstanceId == bInstanceId)
            throw new InvalidDataException("A/B reused a single instance owner identifier.");

        // The actual Home Close button must stop only selected B.
        await ClickCloseAsync("selected B Close");
        await WaitForOwnerAsync(b, alive: false, originalBStops + 1, "B exact Close");
        if (!a.ProcessAlive || a.StopCalls != originalAStops)
            throw new InvalidDataException("B's Close touched unselected A's process or restore owner.");

        // Automatic-reconnect changes are routed through B's mounted control
        // while A remains alive; A's persisted setting cannot change.
        bool aReconnectBefore = retainedProfileRuntimes["campaign-A"].Config.Snapshot.AutoReconnect;
        bool bReconnectBefore = retainedProfileRuntimes["campaign-B"].Config.Snapshot.AutoReconnect;
        await ClickAsync("""
            (() => {
              const toggle = document.querySelectorAll('.quick-actions-panel .toggle-row')[1];
              if (!toggle || toggle.disabled) return false;
              toggle.click();
              return true;
            })()
            """, "selected B Automatic Reconnection");
        bool bReconnectChanged = false;
        for (int i = 0; i < 240; i++)
        {
            if (retainedProfileRuntimes["campaign-B"].Config.Snapshot.AutoReconnect != bReconnectBefore)
            {
                bReconnectChanged = true;
                break;
            }
            await Task.Delay(40);
        }
        if (!bReconnectChanged ||
            retainedProfileRuntimes["campaign-A"].Config.Snapshot.AutoReconnect != aReconnectBefore)
            throw new InvalidDataException("B Automatic Reconnection control changed the wrong persisted owner.");

        await SelectAsync("Campaign A", "campaign-A");
        JsonElement returnedA = await InstanceStatusAsync("campaign-A");
        if (returnedA.GetProperty("instanceId").GetString() != aInstanceId ||
            !a.ProcessAlive || !ReferenceEquals(homeMapCampaignLifecycleProbe, a))
            throw new InvalidDataException("Returning to A created a replacement instead of selecting retained A.");
        await WaitForUiAsync(
            "document.querySelector('.status-card.status-online')?.classList.contains('online') === true",
            "A remained Connected across active B selection and B Close");

        // H-41: persist the next valid installation while A still owns the
        // previous exact path. This uses the same production backend as the UI
        // picker; the native picker/dialog is covered by GameRootSelectChecks.
        string oldRoot = activeProfileConfig.Snapshot.GameRoot ??
            throw new InvalidDataException("A's initial installation was not configured.");
        string newRoot = Path.Combine(isolatedConfigRoot, "r4-next-installation");
        Directory.CreateDirectory(Path.Combine(newRoot, "Game", "LastWar_Data", "Plugins", "x86_64"));
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        File.Copy(Path.Combine(systemDirectory, "cmd.exe"),
            Path.Combine(newRoot, "LastWarLauncher.exe"), overwrite: true);
        File.Copy(Path.Combine(systemDirectory, "cmd.exe"),
            Path.Combine(newRoot, "Game", "LastWar.exe"), overwrite: true);
        File.Copy(Path.Combine(systemDirectory, "kernel32.dll"),
            Path.Combine(newRoot, "Game", "LastWar_Data", "Plugins", "x86_64", "xlua.dll"),
            overwrite: true);
        NativeGameRootSelectionResult changed = backend.SaveNativeGameRootSelection(newRoot);
        if (!changed.Valid ||
            !string.Equals(Path.GetFullPath(activeProfileConfig.Snapshot.GameRoot!), Path.GetFullPath(newRoot),
                StringComparison.OrdinalIgnoreCase) ||
            !a.ProcessAlive)
            throw new InvalidDataException("Running A's configured folder selection was rejected or disturbed A.");

        int aStopsBeforeRootClose = a.StopCalls;
        await ClickCloseAsync("A Close from previous installation");
        await WaitForOwnerAsync(a, alive: false, aStopsBeforeRootClose + 1,
            "A old installation exact restoration");
        a.SetSelectedGameRoot(newRoot);
        int aStartsBeforeRootLaunch = a.StartCalls;
        await ClickLaunchAsync("A next Start from newly selected folder");
        for (int i = 0; i < 240 && (a.StartCalls != aStartsBeforeRootLaunch + 1 || !a.ProcessAlive); i++)
            await Task.Delay(40);
        if (!a.ProcessAlive || a.StartCalls != aStartsBeforeRootLaunch + 1 ||
            (await InstanceStatusAsync("campaign-A")).GetProperty("phase").GetString() != "running")
            throw new InvalidDataException("The next A Start did not consume the selected replacement installation.");

        int aStopsBeforeRepair = a.StopCalls;
        await ClickCloseAsync("A Close before repair");
        await WaitForOwnerAsync(a, alive: false, aStopsBeforeRepair + 1,
            "A restoration before Update-and-Launch");
        a.ArmRepairJournal();
        await ClickAsync("""
            (() => {
              const refresh = [...document.querySelectorAll('.top-actions button.top-action.secondary')].at(-1);
              if (!refresh || refresh.disabled) return false;
              refresh.click();
              return true;
            })()
            """, "repair-required Refresh status");
        await WaitForUiAsync(
            "!document.querySelector('.game-controls button.primary') && document.querySelector('.game-controls > button')?.disabled === false && !!document.querySelector('.game-controls .muted')",
            "original Update-and-Launch repair control");
        int repairStartsBefore = a.StartCalls;
        int repairStopsBefore = a.StopCalls;
        await ClickAsync("""
            (() => {
              const button = document.querySelector('.game-controls > button');
              if (!button || button.disabled) return false;
              button.click();
              return true;
            })()
            """, "Update-and-Launch");
        for (int i = 0; i < 240 && (
            a.StartCalls != repairStartsBefore + 1 || a.StopCalls != repairStopsBefore + 1 ||
            a.RepairRequired || !a.ProcessAlive); i++)
            await Task.Delay(40);
        if (a.StartCalls != repairStartsBefore + 1 ||
            a.StopCalls != repairStopsBefore + 1 || a.RepairRequired || !a.ProcessAlive)
            throw new InvalidDataException("Mounted Update-and-Launch skipped its exact repair/launch producer.");
        await WaitForUiAsync(
            "document.querySelector('.status-card.status-online')?.classList.contains('online') === true",
            "repaired A Connected");

        int aStopsBeforeFinalClose = a.StopCalls;
        await ClickCloseAsync("A final Close");
        await WaitForOwnerAsync(a, alive: false, aStopsBeforeFinalClose + 1,
            "A final exact restoration");
        if (b.ProcessAlive || b.StopCalls != originalBStops + 1 ||
            homeMapCampaignScanStartCount != 0 || homeMapCampaignScanStopCount != 0)
            throw new InvalidDataException("Home-only campaign left B active or exercised unrelated Map scans.");

        // R4 lead inverse 02: exercise the actual sidebar and production
        // WebView dispatcher while A stays selected. No `profile_select`
        // occurs during these native per-owner controls.
        await ClickAsync("""
            (() => { const button=document.querySelector('.profile-collapse');
              if (!button) return false; button.click(); return true; })()
            """, "expand native profile instance controls");
        await WaitForUiAsync("document.querySelectorAll('.profile-row').length === 2",
            "expanded native profile rows");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row .profile-run')].length === 2 &&
            [...document.querySelectorAll('.profile-row .profile-run')].every(button =>
              !button.disabled && !button.classList.contains('is-running'))
            """, "two polled stopped native sidebar owners");
        int aStartsBeforeSidebar = a.StartCalls;
        int aStopsBeforeSidebar = a.StopCalls;
        int bStartsBeforeSidebar = b.StartCalls;
        int bStopsBeforeSidebar = b.StopCalls;
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "unselected B sidebar Start");
        for (int i = 0; i < 240 && (b.StartCalls != bStartsBeforeSidebar + 1 || !b.ProcessAlive); i++)
            await Task.Delay(40);
        if (b.StartCalls != bStartsBeforeSidebar + 1 || !b.ProcessAlive ||
            backend.ProfileId != "campaign-A" || a.StartCalls != aStartsBeforeSidebar ||
            a.StopCalls != aStopsBeforeSidebar || a.ProcessAlive)
            throw new InvalidDataException("Unselected B sidebar Start did not address only retained B.");
        string activeBInstanceId = (await InstanceStatusForHostAsync()).GetProperty("instanceId").GetString()!;
        async Task<JsonElement> InstanceStatusForHostAsync()
        {
            JsonElement response = await InvokeNativeAsync("profile_instance_status",
                new { profileId = "campaign-B" });
            if (!response.GetProperty("ok").GetBoolean())
                throw new InvalidDataException("Host failed explicit B status while A was selected: " + response);
            return response.GetProperty("result");
        }
        if (string.IsNullOrWhiteSpace(activeBInstanceId) || backend.ProfileId != "campaign-A")
            throw new InvalidDataException("Unselected B status did not return exact B instance.");
        foreach ((object payload, string expectedCode) in new (object, string)[]
        {
            (new { }, "PROFILE_ID_REQUIRED"),
            (new { profileId = "bad/id" }, "INVALID_PROFILE_ID"),
            (new { profileId = "missing-profile" }, "PROFILE_NOT_FOUND"),
        })
        {
            JsonElement invalid = await InvokeNativeAsync("profile_instance_status", payload);
            if (invalid.GetProperty("ok").GetBoolean() ||
                invalid.GetProperty("error").GetProperty("code").GetString() != expectedCode)
                throw new InvalidDataException("Native profile status input was not rejected with " + expectedCode + ": " + invalid);
        }
        JsonElement mismatch = await InvokeNativeAsync("profile_instance_stop",
            new { profileId = "campaign-B", instanceId = "stale-previous-owner" });
        if (mismatch.GetProperty("ok").GetBoolean() ||
            mismatch.GetProperty("error").GetProperty("code").GetString() != "INSTANCE_MISMATCH" ||
            !b.ProcessAlive || b.StopCalls != bStopsBeforeSidebar ||
            backend.ProfileId != "campaign-A")
            throw new InvalidDataException("Stale explicit B Stop crossed exact retained-owner boundary: " + mismatch);
        // A targeted B read may complete after A -> B -> A selection. It still
        // belongs to B and may update B's sidebar row; it cannot replace the
        // selected Home owner or mutate A's visible state.
        Task<HomeMapCampaignDelayedRequest> delayedBStatus =
            ArmHomeMapCampaignCommandDelay("profile_instance_status", targetProfileId: "campaign-B");
        Task<JsonElement> pendingBStatus = InvokeNativeAsync("profile_instance_status",
            new { profileId = "campaign-B" });
        HomeMapCampaignDelayedRequest delayedBOwner =
            await delayedBStatus.WaitAsync(TimeSpan.FromSeconds(8));
        if (delayedBOwner.ProfileId != "campaign-B")
            throw new InvalidDataException("The delayed status request did not target native owner B.");
        await ClickAsync("""
            (() => {const button=document.querySelector('.profile-collapse');
              if (!button) return false; button.click(); return true;})()
            """, "collapse profile rows before native A/B/A selection");
        await SelectAsync("Campaign B", "campaign-B");
        await SelectAsync("Campaign A", "campaign-A");
        ReleaseHomeMapCampaignCommandDelay();
        JsonElement completedBStatus = await pendingBStatus.WaitAsync(TimeSpan.FromSeconds(8));
        if (!completedBStatus.GetProperty("ok").GetBoolean() ||
            completedBStatus.GetProperty("result").GetProperty("instanceId").GetString() != activeBInstanceId ||
            backend.ProfileId != "campaign-A" || a.ProcessAlive || !b.ProcessAlive)
            throw new InvalidDataException("Delayed explicit B status response lost B ownership or replaced A: " + completedBStatus);
        await ClickAsync("""
            (() => {const button=document.querySelector('.profile-collapse');
              if (!button) return false; button.click(); return true;})()
            """, "restore expanded profile controls after A/B/A");
        await WaitForUiAsync("document.querySelectorAll('.profile-row').length === 2",
            "expanded native profile rows after delayed owner response");
        await WaitForUiAsync(
            "document.querySelectorAll('.profile-row .profile-run')[1]?.classList.contains('is-running') === true",
            "B sidebar independently polled running instance");
        // Production race inverse: the real 3 s sidebar poll reads B as
        // running, but its already-computed native response is held while B
        // is stopped from the sidebar. A late older poll must never revive
        // the old running icon or conceal the exact completed Stop.
        Task<HomeMapCampaignDelayedRequest> staleBRowPoll =
            ArmHomeMapCampaignCommandDelay("profile_instance_status",
                targetProfileId: "campaign-B", onReply: true);
        HomeMapCampaignDelayedRequest staleBRowRequest =
            await staleBRowPoll.WaitAsync(TimeSpan.FromSeconds(10));
        if (staleBRowRequest.ProfileId != "campaign-B")
            throw new InvalidDataException("Sidebar stale poll did not target B.");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "unselected B sidebar Stop");
        await WaitForOwnerAsync(b, alive: false, bStopsBeforeSidebar + 1, "unselected B sidebar exact Stop");
        if (backend.ProfileId != "campaign-A" || a.StartCalls != aStartsBeforeSidebar ||
            a.StopCalls != aStopsBeforeSidebar || a.ProcessAlive)
            throw new InvalidDataException("Unselected B sidebar Stop mutated selected A.");
        await WaitForUiAsync(
            "document.querySelectorAll('.profile-row .profile-run')[1]?.classList.contains('is-running') === false",
            "B sidebar independently polled stopped instance");
        ReleaseHomeMapCampaignCommandDelay();
        await Task.Delay(450);
        if (await core.ExecuteScriptAsync(
            "document.querySelectorAll('.profile-row .profile-run')[1]?.classList.contains('is-running') === false") != "true")
            throw new InvalidDataException("A stale native B sidebar status poll resurrected the stopped instance icon.");

        // The recovered batch controls visit enabled profiles in their list
        // order. Their real buttons must receive provider callbacks even when
        // one native owner fails, then continue with the other exact owner.
        RejectNextHomeMapCampaignCommand("profile_instance_start");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-batch-actions button')[0];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "native sidebar Start All with first owner failure");
        for (int i = 0; i < 240 && !b.ProcessAlive; i++) await Task.Delay(40);
        if (a.ProcessAlive || a.StartCalls != aStartsBeforeSidebar ||
            b.StartCalls != bStartsBeforeSidebar + 2 || !b.ProcessAlive ||
            backend.ProfileId != "campaign-A")
            throw new InvalidDataException("Start All did not continue to B after native A failure.");
        await WaitForUiAsync("!!document.querySelector('.profile-error')",
            "batch per-profile failure surfaced to native sidebar");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-batch-actions button')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "native sidebar Stop All B owner");
        await WaitForOwnerAsync(b, alive: false, bStopsBeforeSidebar + 2, "sidebar batch B Stop");
        await WaitForUiAsync(
            "document.querySelectorAll('.profile-row .profile-run')[1]?.classList.contains('is-running') === false",
            "batch Stop All refreshed B owner");
        if (a.ProcessAlive || backend.ProfileId != "campaign-A" || a.StopCalls != aStopsBeforeSidebar)
            throw new InvalidDataException("Batch Stop All interfered with selected inactive A.");

        // H-19: the original global Update-and-Restart visits distinct owners.
        // Arm an inert repair record for each stopped runtime and invoke the
        // real production WebView command; confirm result attribution and both
        // independent restorations, then Stop All through the same sidebar.
        a.ArmRepairJournal();
        b.ArmRepairJournal();
        int aRepairStartsBefore = a.StartCalls;
        int bRepairStartsBefore = b.StartCalls;
        int aRepairStopsBefore = a.StopCalls;
        int bRepairStopsBefore = b.StopCalls;
        JsonElement globalRestart = await InvokeNativeAsync("profile_instances_update_and_restart", new { });
        if (!globalRestart.GetProperty("ok").GetBoolean())
            throw new InvalidDataException("Global native Update-and-Restart rejected: " + globalRestart);
        JsonElement restartResult = globalRestart.GetProperty("result");
        string[] restartedProfiles = restartResult.GetProperty("restarted").EnumerateArray()
            .Select(value => value.GetString() ?? "").ToArray();
        if (!restartedProfiles.SequenceEqual(new[] { "campaign-A", "campaign-B" }) ||
            restartResult.GetProperty("errors").GetArrayLength() != 0 ||
            a.StartCalls != aRepairStartsBefore + 1 || b.StartCalls != bRepairStartsBefore + 1 ||
            a.StopCalls != aRepairStopsBefore + 1 || b.StopCalls != bRepairStopsBefore + 1 ||
            !a.ProcessAlive || !b.ProcessAlive || backend.ProfileId != "campaign-A")
            throw new InvalidDataException("Global native restart did not repair and relaunch both exact retained owners: " + globalRestart);
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row .profile-run')].length === 2 &&
            [...document.querySelectorAll('.profile-row .profile-run')].every(button =>
              button.classList.contains('is-running'))
            """, "global native restart refreshed both per-profile sidebar states");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-batch-actions button')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "both-owner Stop All after global Update-and-Restart");
        await WaitForOwnerAsync(a, alive: false, aRepairStopsBefore + 2, "A Stop All exact restored owner");
        await WaitForOwnerAsync(b, alive: false, bRepairStopsBefore + 2, "B Stop All exact restored owner");

        // H-18/H-19 distinction from H-23 startup reconcile: original manual
        // Update-and-Launch surfaces the first GLOBAL repair result error,
        // even when it belongs to unselected B. Reconcile alone filters to
        // selected owner. Exercise the native B helper failure after A succeeds
        // and prove the actual Home action/error display; never fabricate a UI reply.
        a.ArmRepairJournal();
        b.ArmRepairJournal();
        b.FailNextRepairStop = true;
        int aStartsBeforeForeignError = a.StartCalls;
        int bStartsBeforeForeignError = b.StartCalls;
        await WaitForUiAsync(
            "!document.querySelector('.game-controls button.primary') && document.querySelector('.game-controls > button')?.disabled === false",
            "selected A repair button before isolated B-only failure");
        await ClickAsync("""
            (() => {const button=document.querySelector('.game-controls > button');
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "selected A Update-and-Launch with B-only helper failure");
        // Global repair walks A then B. A's relaunch can finish BEFORE the
        // B-only injected stop failure is reached; wait for BOTH outcomes,
        // without changing the required exact A/B success/error assertions.
        for (int i = 0; i < 240 &&
            (a.StartCalls != aStartsBeforeForeignError + 1 || b.FailNextRepairStop); i++)
            await Task.Delay(40);
        if (a.StartCalls != aStartsBeforeForeignError + 1 || !a.ProcessAlive ||
            b.StartCalls != bStartsBeforeForeignError || !b.RepairJournalActive ||
            !b.ProcessAlive || b.FailNextRepairStop)
            throw new InvalidDataException(
                "Native B-only failure did not preserve successful A repair." +
                $" A starts={a.StartCalls}/{aStartsBeforeForeignError + 1} alive={a.ProcessAlive}" +
                $" B starts={b.StartCalls}/{bStartsBeforeForeignError} alive={b.ProcessAlive}" +
                $" journal={b.RepairJournalActive} failFlag={b.FailNextRepairStop}");
        await WaitForUiAsync(
            "document.querySelector('.game-controls button.primary') !== null",
            "selected A repaired Home returned to normal controls");
        await WaitForUiAsync(
            "document.querySelector('.quick-actions-panel .game-root-error')?.textContent?.trim()?.length > 0",
            "manual Home global repair correctly surfaces first B-only error");
        JsonElement retryB = await InvokeNativeAsync("profile_instances_update_and_restart", new { });
        if (!retryB.GetProperty("ok").GetBoolean() ||
            !retryB.GetProperty("result").GetProperty("restarted").EnumerateArray()
                .Any(value => value.GetString() == "campaign-B") ||
            retryB.GetProperty("result").GetProperty("errors").GetArrayLength() != 0 ||
            !a.ProcessAlive || !b.ProcessAlive)
            throw new InvalidDataException("B-only retry lost A or failed to repair B: " + retryB);
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row .profile-run')].length === 2 &&
            [...document.querySelectorAll('.profile-row .profile-run')].every(button => button.classList.contains('is-running'))
            """, "A/B owner status before B failure cleanup");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-batch-actions button')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "Stop All after isolated B-only Home repair failure");
        await WaitForOwnerAsync(a, alive: false, aRepairStopsBefore + 4, "A exact Stop after B-only failure");
        await WaitForOwnerAsync(b, alive: false, bRepairStopsBefore + 4, "B exact Stop after repair retry");

        // Validate admission for the selected and unselected owners against
        // the real isolated controller registry, then restore fixture state.
        using (var registry = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(productionApplicationRoot!, "controller.db"),
        }.ConnectionString))
        {
            registry.Open();
            using (var lockProfiles = registry.CreateCommand())
            {
                lockProfiles.CommandText = "UPDATE profiles SET locked_reason='R4_LOCKED_FIXTURE' WHERE id IN ('campaign-A', 'campaign-B')";
                if (lockProfiles.ExecuteNonQuery() != 2)
                    throw new InvalidDataException("Mounted locked-profile fixture did not affect both exact owners.");
            }
            try
            {
                foreach (string id in new[] { "campaign-A", "campaign-B" })
                {
                    JsonElement denied = await InvokeNativeAsync("profile_instance_start", new { profileId = id });
                    if (denied.GetProperty("ok").GetBoolean() ||
                        denied.GetProperty("error").GetProperty("code").GetString() != "PROFILE_LOCKED")
                        throw new InvalidDataException("Start bypassed registry admission for " + id + ": " + denied);
                }
                if (a.ProcessAlive || b.ProcessAlive || backend.ProfileId != "campaign-A")
                    throw new InvalidDataException("Denied profile Starts changed exact process ownership.");
            }
            finally
            {
                using var restore = registry.CreateCommand();
                restore.CommandText = "UPDATE profiles SET locked_reason=NULL WHERE id IN ('campaign-A', 'campaign-B') AND locked_reason='R4_LOCKED_FIXTURE'";
                if (restore.ExecuteNonQuery() != 2)
                    throw new InvalidDataException("Mounted profile fixture locked state could not be restored exactly.");
            }
            using (var disable = registry.CreateCommand())
            {
                disable.CommandText = "UPDATE profiles SET enabled=0 WHERE id='campaign-B' AND enabled=1";
                if (disable.ExecuteNonQuery() != 1)
                    throw new InvalidDataException("Mounted disabled profile fixture could not acquire B.");
            }
            try
            {
                JsonElement disabledStart = await InvokeNativeAsync("profile_instance_start",
                    new { profileId = "campaign-B" });
                if (disabledStart.GetProperty("ok").GetBoolean() ||
                    disabledStart.GetProperty("error").GetProperty("code").GetString() != "PROFILE_LOCKED")
                    throw new InvalidDataException("Disabled profile admission unexpectedly launched B: " + disabledStart);
                int previousBStarts = b.StartCalls;
                JsonElement disabledBatch = await InvokeNativeAsync("profile_instances_update_and_restart", new { });
                if (!disabledBatch.GetProperty("ok").GetBoolean() ||
                    disabledBatch.GetProperty("result").GetProperty("restarted").EnumerateArray()
                        .Any(item => item.GetString() == "campaign-B") ||
                    b.StartCalls != previousBStarts || b.ProcessAlive)
                    throw new InvalidDataException("Global restart visited a disabled native owner B: " + disabledBatch);
            }
            finally
            {
                using var restore = registry.CreateCommand();
                restore.CommandText = "UPDATE profiles SET enabled=1 WHERE id='campaign-B' AND enabled=0";
                if (restore.ExecuteNonQuery() != 1)
                    throw new InvalidDataException("Mounted disabled profile fixture could not restore B exactly.");
            }
        }

        // H-47: two actual mounted controls (Home and the selected-profile
        // sidebar) address the same exact lifecycle. A delayed Home Start must
        // not allow sidebar A Start to bypass its in-flight admission. B is an
        // independent owner and its controls must remain available.
        int aStartsBeforeCrossControl = a.StartCalls;
        Task<HomeMapCampaignDelayedRequest> heldHomeStart =
            ArmHomeMapCampaignCommandDelay("profile_instance_start", targetProfileId: "campaign-A");
        await ClickLaunchAsync("selected A Home Start held for sidebar overlap");
        HomeMapCampaignDelayedRequest heldAStart =
            await heldHomeStart.WaitAsync(TimeSpan.FromSeconds(8));
        if (heldAStart.ProfileId != "campaign-A")
            throw new InvalidDataException("Overlapping Home action did not own selected A.");
        int bStartsWhileAHeld = b.StartCalls;
        int bStopsWhileAHeld = b.StopCalls;
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "independent B sidebar Start while A Home Start is pending");
        await WaitForOwnerAsync(b, alive: true, bStopsWhileAHeld,
            "B independent Start did not touch pending A");
        if (b.StartCalls != bStartsWhileAHeld + 1 || a.StartCalls != aStartsBeforeCrossControl)
            throw new InvalidDataException("Independent B Start crossed held A Home lifecycle.");
        await WaitForUiAsync(
            "document.querySelectorAll('.profile-row .profile-run')[1]?.classList.contains('is-running') === true",
            "B row updated independently while A Home was held");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "independent B Stop while A Home Start is pending");
        await WaitForOwnerAsync(b, alive: false, bStopsWhileAHeld + 1,
            "B independent Stop did not cancel pending A");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[0];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "sidebar A Start while Home A Start is pending");
        await Task.Delay(250);
        if (a.StartCalls != aStartsBeforeCrossControl)
            throw new InvalidDataException("Sidebar bypassed pending same-owner Home Start and launched A twice.");
        ReleaseHomeMapCampaignCommandDelay();
        for (int i = 0; i < 240 && !a.ProcessAlive; i++) await Task.Delay(40);
        if (a.StartCalls != aStartsBeforeCrossControl + 1 || !a.ProcessAlive ||
            b.ProcessAlive || backend.ProfileId != "campaign-A")
            throw new InvalidDataException("Home A Start failed exact-owner overlap admission.");
        await ClickCloseAsync("A Close after pending cross-control Start");
        await WaitForOwnerAsync(a, alive: false, aRepairStopsBefore + 5,
            "A restored after overlapping UI Start");

        // Reverse source: the sidebar may own A's held Start first, and the
        // Home button must not issue a second native Start for that same A.
        int aStartsBeforeReverse = a.StartCalls;
        Task<HomeMapCampaignDelayedRequest> heldSidebarStart =
            ArmHomeMapCampaignCommandDelay("profile_instance_start", targetProfileId: "campaign-A");
        await WaitForUiAsync(
            "document.querySelectorAll('.profile-row .profile-run')[0]?.classList.contains('is-running') === false",
            "A sidebar stopped state before reverse overlap");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[0];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "hold sidebar A Start first");
        HomeMapCampaignDelayedRequest heldSidebarOwner =
            await heldSidebarStart.WaitAsync(TimeSpan.FromSeconds(8));
        if (heldSidebarOwner.ProfileId != "campaign-A")
            throw new InvalidDataException("Reverse overlap did not hold actual native sidebar A.");
        // R7 H-47 UI state: B is allowed to progress while A's native Start is
        // still held, but B's completion must not clear A's busy button. The
        // existing single runBusyId loses A when B completes an independent
        // Start/Stop; the native per-owner guard alone hides this visual race.
        int bStartsBeforeConcurrentSidebar = b.StartCalls;
        int bStopsBeforeConcurrentSidebar = b.StopCalls;
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "B sidebar Start while A sidebar Start is pending");
        await WaitForOwnerAsync(b, alive: true, bStopsBeforeConcurrentSidebar,
            "B concurrent sidebar Start leaves A pending");
        await WaitForUiAsync(
            "document.querySelectorAll('.profile-row .profile-run')[1]?.classList.contains('is-running') === true",
            "B concurrent sidebar status after Start");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "B sidebar Stop while A sidebar Start is pending");
        await WaitForOwnerAsync(b, alive: false, bStopsBeforeConcurrentSidebar + 1,
            "B concurrent sidebar Stop leaves A pending");
        await WaitForUiAsync("""
            (() => {const buttons=document.querySelectorAll('.profile-row .profile-run');
              return buttons.length === 2 && !buttons[1].disabled &&
                !buttons[1].classList.contains('is-running');})()
            """, "B concurrent sidebar Stop result completed");
        if (b.StartCalls != bStartsBeforeConcurrentSidebar + 1 ||
            a.StartCalls != aStartsBeforeReverse)
            throw new InvalidDataException("Concurrent sidebar B changed A's pending native Start.");
        JsonElement aBusyWhileBCompleted = await ReadUiAsync(
            "document.querySelectorAll('.profile-row .profile-run')[0]?.disabled === true");
        if (aBusyWhileBCompleted.ValueKind != JsonValueKind.True)
            throw new InvalidDataException("Completed B sidebar action cleared still-pending A sidebar busy indicator.");
        await ClickLaunchAsync("Home A Start while sidebar A Start is pending");
        await Task.Delay(250);
        if (a.StartCalls != aStartsBeforeReverse || b.ProcessAlive)
            throw new InvalidDataException("Home bypassed pending same-owner sidebar Start.");
        ReleaseHomeMapCampaignCommandDelay();
        for (int i = 0; i < 240 && !a.ProcessAlive; i++) await Task.Delay(40);
        if (a.StartCalls != aStartsBeforeReverse + 1 || !a.ProcessAlive)
            throw new InvalidDataException("Sidebar A Start was lost after reverse overlap.");
        await ClickCloseAsync("A Close after reverse sidebar-first overlap");
        await WaitForOwnerAsync(a, alive: false, aRepairStopsBefore + 6,
            "A exact close after reverse cross-control Start");

        // R8 H-14/H-39/H-47: the original Home Pt closes its captured
        // selected profile after awaiting the instance status. A switch to B
        // while that native read is held must not abandon A's acknowledged
        // user Close or accidentally apply it to selected B.
        int aStartsBeforeSwitchClose = a.StartCalls;
        JsonElement aStartForSwitchClose = await InvokeNativeAsync(
            "profile_instance_start", new { profileId = "campaign-A", closeUnmanaged = true });
        if (!aStartForSwitchClose.GetProperty("ok").GetBoolean())
            throw new InvalidDataException("Could not start owned A for R8 delayed Close.");
        for (int i = 0; i < 240 && !a.ProcessAlive; i++) await Task.Delay(40);
        if (!a.ProcessAlive || a.StartCalls != aStartsBeforeSwitchClose + 1)
            throw new InvalidDataException("R8 selected A was not running before Close race.");
        await WaitForUiAsync(
            "document.querySelector('.game-controls > button:not(.primary)')?.disabled === false",
            "A Home Close enabled before delayed owner status");
        int aStopsBeforeSwitchClose = a.StopCalls;
        Task<HomeMapCampaignDelayedRequest> heldCloseStatus =
            ArmHomeMapCampaignCommandDelay("profile_instance_status",
                targetProfileId: "campaign-A", onReply: true);
        await ClickCloseAsync("A Home Close while A status reply is held");
        HomeMapCampaignDelayedRequest heldClose = await heldCloseStatus.WaitAsync(TimeSpan.FromSeconds(8));
        if (heldClose.ProfileId != "campaign-A" || heldClose.Cancelled)
            throw new InvalidDataException("R8 Home Close did not read A's exact native status.");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-item')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "switch to B while selected A Home Close status is pending");
        for (int i = 0; i < 240 && backend.ProfileId != "campaign-B"; i++) await Task.Delay(40);
        if (backend.ProfileId != "campaign-B")
            throw new InvalidDataException("R8 profile B selection could not proceed past A pending Close.");
        int bStartsBeforeSwitchClose = b.StartCalls;
        int bStopsBeforeSwitchClose = b.StopCalls;
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "independent selected B Start while A Home Close is pending");
        await WaitForOwnerAsync(b, alive: true, bStopsBeforeSwitchClose,
            "B running independently during A pending Close");
        if (b.StartCalls != bStartsBeforeSwitchClose + 1 || !a.ProcessAlive)
            throw new InvalidDataException("R8 B Start interfered with A pending Close.");
        ReleaseHomeMapCampaignCommandDelay();
        for (int i = 0; i < 240 && a.StopCalls == aStopsBeforeSwitchClose; i++)
            await Task.Delay(40);
        if (a.ProcessAlive || a.StopCalls != aStopsBeforeSwitchClose + 1 || !b.ProcessAlive)
            throw new InvalidDataException("A Home Close was abandoned or misrouted after switching to B.");
        await WaitForUiAsync(
            "document.querySelectorAll('.profile-row .profile-run')[1]?.classList.contains('is-running') === true",
            "independent B remained running after A pending Close");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-run')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "B independent exact Stop after A pending Close");
        await WaitForOwnerAsync(b, alive: false, bStopsBeforeSwitchClose + 1,
            "B exact Stop after cross-profile pending A Close");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-item')[0];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "return to A after cross-selection Home Close");
        for (int i = 0; i < 240 && backend.ProfileId != "campaign-A"; i++) await Task.Delay(40);
        if (backend.ProfileId != "campaign-A")
            throw new InvalidDataException("R8 Home A selection was not restored.");

        // Generation ABA: A is selected again before its old Home Close
        // status reply arrives. The captured instance ID still owns the
        // original Close and cannot be replaced by a selected-view generation.
        JsonElement aStartForAbaClose = await InvokeNativeAsync(
            "profile_instance_start", new { profileId = "campaign-A", closeUnmanaged = true });
        if (!aStartForAbaClose.GetProperty("ok").GetBoolean())
            throw new InvalidDataException("Could not start A for R8 ABA Home Close.");
        await WaitForUiAsync(
            "document.querySelector('.game-controls > button:not(.primary)')?.disabled === false",
            "A Home Close enabled before ABA status");
        int aStopsBeforeAba = a.StopCalls;
        Task<HomeMapCampaignDelayedRequest> heldAbaStatus =
            ArmHomeMapCampaignCommandDelay("profile_instance_status",
                targetProfileId: "campaign-A", onReply: true);
        await ClickCloseAsync("A Home Close with pending ABA status reply");
        HomeMapCampaignDelayedRequest heldAba = await heldAbaStatus.WaitAsync(TimeSpan.FromSeconds(8));
        if (heldAba.ProfileId != "campaign-A")
            throw new InvalidDataException("R8 ABA status reply was not exact A.");
        await ClickAsync("""
            (() => {const button=document.querySelectorAll('.profile-row .profile-item')[1];
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "ABA select B before A Close acknowledgement");
        for (int i = 0; i < 240 && backend.ProfileId != "campaign-B"; i++) await Task.Delay(40);
        if (backend.ProfileId != "campaign-B")
            throw new InvalidDataException("R8 ABA intermediate B was not selected.");
        await WaitForUiAsync("""
            [...document.querySelectorAll('.profile-row .profile-item')]
              .some(button => button.querySelector('strong')?.textContent?.includes('Campaign B') &&
                button.closest('.profile-row')?.classList.contains('active')) &&
            [...document.querySelectorAll('.profile-row .profile-item')]
              .some(button => button.querySelector('strong')?.textContent?.includes('Campaign A') &&
                !button.disabled)
            """, "R8 ABA B selected and A re-selection admitted");
        await ClickAsync("""
            (() => {const button=[...document.querySelectorAll('.profile-row .profile-item')]
                .find(item => item.querySelector('strong')?.textContent?.includes('Campaign A'));
              if (!button || button.disabled) return false; button.click(); return true;})()
            """, "ABA select A again before A Close acknowledgement");
        for (int i = 0; i < 240 && backend.ProfileId != "campaign-A"; i++) await Task.Delay(40);
        if (backend.ProfileId != "campaign-A")
            throw new InvalidDataException("R8 ABA final A was not selected.");
        ReleaseHomeMapCampaignCommandDelay();
        await WaitForOwnerAsync(a, alive: false, aStopsBeforeAba + 1,
            "A exact Stop after view generation ABA");
        if (b.ProcessAlive)
            throw new InvalidDataException("R8 ABA A Close disturbed stopped B.");

        JsonElement finalUi = await ReadUiAsync("""
            (() => ({
              uiProject: document.querySelector('.app-shell')?.dataset.uiProject || '',
              language: document.documentElement.lang || '',
              theme: document.documentElement.dataset.theme || '',
              bridgeMode: window.__LWBridgeBootstrap?.mode || '',
              selectedProfile: [...document.querySelectorAll('.profile-compact-item')]
                .find(item => item.classList.contains('active'))?.textContent?.trim() || '',
              connectedCard: document.querySelector('.status-card.status-online')?.classList.contains('online') || false,
              hasHome: !!document.querySelector('.quick-actions-panel')
            }))()
            """);
        if (finalUi.GetProperty("language").GetString() != expectedLanguage ||
            finalUi.GetProperty("theme").GetString() != expectedTheme ||
            finalUi.GetProperty("uiProject").GetString() != "LWBridge.UI-0.3.17" ||
            !finalUi.GetProperty("hasHome").GetBoolean())
            throw new InvalidDataException("Mounted Home final locale, theme, or shell did not converge.");

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            proof = "HOME_004_R4_PRODUCTION_MOUNTED_HOME_TWO_INERT_OWNERS",
            controlled = true,
            genuineGameLaunches = 0,
            mapScanStarts = homeMapCampaignScanStartCount,
            locale = expectedLanguage,
            theme = expectedTheme,
            initialReconcile = new
            {
                owners = 2, aStartCalls = a.StartCalls, bStartCalls = b.StartCalls,
                aInstanceId, bInstanceId,
            },
            selectedWhileARunning = true,
            delayedAHomeStatusFencedOnBSelection = true,
            aSessionRetainedAfterBStop = true,
            bStoppedIndependently = true,
            bAutoReconnectPersistedSeparately = true,
            sidebarProfileCommands = new
            {
                aSelectedThroughout = true,
                bothStatusesPolled = true,
                targetOwnerStatusAndErrorRouting = true,
                foreignStaleInstanceRejected = true,
                delayedBReplyAcrossABA = true,
                lateRunningPollCannotResurrectStoppedB = true,
                selectedAndUnselectedLockedAdmission = true,
                disabledStartAndGlobalRestartAdmission = true,
                manualHomeGlobalFirstRepairErrorVisible = true,
                homeAndSidebarSameOwnerStartDedupe = true,
                independentBStartStopWhileAHeld = true,
                reverseSidebarFirstHomeStartDedupe = true,
                concurrentSidebarBusyOwnerRetained = true,
                homeCloseRetainsCapturedOwnerAcrossProfileSwitch = true,
                independentBWhileHomeAClosePending = true,
                homeCloseRetainsCapturedOwnerAcrossABA = true,
                noteSaveAcknowledgedAcrossSelection = true,
                profileReorderAcknowledgedAcrossSelection = true,
                sidebarReorderVisibleAfterSelection = true,
                sidebarNoteVisibleAfterSelection = true,
                selectionBusyReleasedAfterIndependentReorder = true,
                lateSelectionPreservesNewerProfileOrder = true,
                rapidNoteWritesPreserveLatestDurableOwnerValue = true,
                rejectedNewerNotePreservesEarlierDurableOwnerValue = true,
                failedFirstNoteAllowsNewerDurableRetry = true,
                bStartStopExact = true,
                startAllContinuedAfterAError = true,
                stopAllStoppedB = true,
                aUntouched = true,
            },
            globalProfileUpdateAndRestart = new
            {
                repairedProfiles = restartedProfiles,
                bothOwnersRelaunched = true,
                sidebarStoppedBoth = true,
            },
            runningRootSelectedAndNextStart = new
            {
                oldRoot, newRoot, pickerValid = changed.Valid, aStarts = a.StartCalls,
                aStops = a.StopCalls,
            },
            updateAndLaunch = new
            {
                exactRepairStop = true, relaunched = true, repairedConnected = true,
            },
            final = new
            {
                aStopped = !a.ProcessAlive, bStopped = !b.ProcessAlive,
                finalUi,
                connectedScreenshot,
            },
        }, JsonOptions.Indented));
        Console.WriteLine("HOME_004_R4_MOUNTED_HOME_OK " + expectedLanguage + "/" + expectedTheme);
    }
}
