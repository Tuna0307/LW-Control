using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Explicit isolated packaged instrumentation only. Every scan starts through
    // the mounted production checkbox picker and Start/Run now button.
    private async Task RunMap017UiProofAsync(CoreWebView2 core, string outputPath,
        string directory, OverviewMapScanSession owner, CancellationToken token)
    {
        string? mode = Environment.GetEnvironmentVariable("LWBRIDGE_MAP017_UI_MODE");
        if (productionPaths?.ExplicitIsolation != true || map317CommandService is null ||
            overviewLifecycleService is null || mapAutoScanService is null ||
            mode is not ("boss-first" or "boss-after-resource" or "picker-empty" or "once-two-server"))
            throw new InvalidOperationException("MAP017 requires its explicit isolated production owner and supported mode.");
        void Owner()
        {
            token.ThrowIfCancellationRequested();
            if (!overviewLifecycleService.MatchesOwnedMapScanSession(owner))
                throw new InvalidDataException("MAP017 proof lost its exact owned game session.");
        }
        async Task<bool> Dom(string script) => await core.ExecuteScriptAsync(script) == "true";
        async Task Wait(Func<Task<bool>> condition, string stage, int polls = 450)
        {
            for (int n = 0; n < polls; n++)
            {
                Owner();
                if (await condition()) return;
                await Task.Delay(100, token);
            }
            throw new TimeoutException("MAP017 packaged UI did not settle: " + stage);
        }
        async Task Screenshot(string name)
        {
            Owner();
            await using var file = File.Create(Path.Combine(directory, name));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        }
        const string manualBoxes = ".panel.map-panel .map-header .map-category-menu input[type=checkbox]";
        const string autoBoxes = ".map-auto-scan-card .map-category-menu input[type=checkbox]";
        async Task Select(string selector, int index)
        {
            // Set the wanted category first, then observe each React commit.
            // This also works against the historical last-item restriction.
            foreach (int i in new[] { index }.Concat(Enumerable.Range(0, 10).Where(i => i != index)))
            {
                bool wanted = i == index;
                if (!await Dom($"(()=>{{const b=document.querySelectorAll('{selector}')[{i}];if(!b)return false;if(b.checked!=={wanted.ToString().ToLowerInvariant()})b.click();return true;}})()"))
                    throw new InvalidDataException("MAP017 picker checkbox missing: " + i);
                await Wait(() => Dom($"document.querySelectorAll('{selector}')[{i}]?.checked==={wanted.ToString().ToLowerInvariant()}"), "checkbox " + i);
            }
        }
        async Task<object> ManualScan(int index, string kind, string label)
        {
            Owner();
            await Select(manualBoxes, index);
            await Dom("(()=>{const d=document.querySelector('.map-header .map-category-picker');if(d&&!d.open)d.querySelector('summary')?.click();return true;})()");
            await Screenshot("map017-" + label + "-selected.png");
            await Dom("(()=>{const d=document.querySelector('.map-header .map-category-picker');if(d?.open)d.querySelector('summary')?.click();return true;})()");
            JsonElement initial = JsonSerializer.SerializeToElement(map317CommandService.CreateStatus(), JsonOptions.Default);
            string? previous = ReadStatusString(initial, "scanRunId");
            await Wait(() => Dom("!!document.querySelector('.map-header .map-actions button.primary:not(:disabled)')"), "Manual Start enabled");
            if (!await Dom("(()=>{const b=document.querySelector('.map-header .map-actions button.primary:not(:disabled)');if(!b)return false;b.click();return true;})()"))
                throw new InvalidDataException("MAP017 could not click actual Manual Start.");
            JsonElement completed = default;
            await Wait(() =>
            {
                JsonElement status = JsonSerializer.SerializeToElement(map317CommandService.CreateStatus(), JsonOptions.Default);
                string? error = ReadStatusString(status, "lastError");
                if (!string.IsNullOrWhiteSpace(error)) throw new InvalidDataException("MAP017 scan failed: " + error);
                string? run = ReadStatusString(status, "scanRunId");
                bool done = ReadStatusString(status, "phase") == "completed" && !string.IsNullOrEmpty(run) && run != previous;
                if (done) completed = status;
                return Task.FromResult(done);
            }, "completed " + label, 4200);
            string[] selected = ReadNormalUiProofSelectedTypes(completed);
            if (!selected.SequenceEqual([kind], StringComparer.Ordinal) || ReadStatusInt32(completed, "totalBlocks") != 2500 ||
                ReadStatusInt32(completed, "readBlocks") != 2500 || ReadStatusInt32(completed, "failedBlocks") != 0)
                throw new InvalidDataException("MAP017 scan did not complete exact selected full-world coverage.");
            string runId = ReadStatusString(completed, "scanRunId")!;
            var persisted = map317CommandService.ReadPersistedScanRunForProof(runId)
                ?? throw new InvalidDataException("MAP017 completed run missing from SQLite.");
            if (persisted.Status != "completed" || persisted.ServerId != 2212 || persisted.Error is not null ||
                !persisted.SelectedTypes.SequenceEqual([kind]) || persisted.CompletedBlocks != 2500 || persisted.FailedBlocks != 0)
                throw new InvalidDataException("MAP017 persisted run does not match completed selected source.");
            var retainedNativeFiles = new List<string>();
            var rawNativeFiles = new List<string>();
            var nativeFileAttribution = new List<object>();
            foreach (string name in new[] { "camera-free-scan-result.json", "map-fullworld-probe-metrics.json",
                "map-engine-stage-metrics.json", "fast-truck-scan-result.json" })
            {
                string source = Path.Combine(overviewLifecycleService.LiveResourceRuntimeRoot, name);
                if (!File.Exists(source)) continue;
                string saved = Path.Combine(directory, "map017-" + label + "-" + name);
                byte[] bytes = await File.ReadAllBytesAsync(source, token);
                await using (var copy = new FileStream(saved, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await copy.WriteAsync(bytes, token);
                string? declaredRunId = null, parseError = null;
                int declaredServerId = 0, ownerFieldsPresent = 0;
                bool ownerFieldsMatch = true;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(bytes);
                    JsonElement receipt = document.RootElement;
                    declaredRunId = ReadStatusString(receipt, "scanRunId") ?? ReadStatusString(receipt, "runId");
                    declaredServerId = ReadStatusInt32(receipt, "serverId") ?? 0;
                    if (receipt.TryGetProperty("identity", out JsonElement identity) && identity.ValueKind == JsonValueKind.Object)
                    {
                        declaredRunId ??= ReadStatusString(identity, "RunId");
                        if (declaredServerId == 0) declaredServerId = ReadStatusInt32(identity, "ServerId") ?? 0;
                    }
                    foreach (var field in new[] { ("challenge", owner.Challenge), ("profileId", owner.ProfileId), ("launchSessionId", owner.SessionId) })
                    {
                        if (!receipt.TryGetProperty(field.Item1, out JsonElement value)) continue;
                        ownerFieldsPresent++;
                        ownerFieldsMatch &= value.ValueKind == JsonValueKind.String && value.GetString() == field.Item2;
                    }
                    if (receipt.TryGetProperty("gamePid", out JsonElement pid))
                    {
                        ownerFieldsPresent++;
                        ownerFieldsMatch &= pid.TryGetInt32(out int actualPid) && actualPid == owner.GamePid;
                    }
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException) { parseError = error.Message; }
                bool correlated = parseError is null && declaredRunId == runId &&
                    declaredServerId == persisted.ServerId && ownerFieldsMatch;
                string attribution = !correlated ? "uncorrelated_raw" : ownerFieldsPresent == 4 ? "run_and_native_owner" : "run_identity";
                if (correlated) retainedNativeFiles.Add(saved);
                else rawNativeFiles.Add(saved);
                nativeFileAttribution.Add(new { name, path = saved, correlated, attribution, declaredRunId,
                    declaredServerId, ownerFieldsPresent, ownerFieldsMatch,
                    sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), parseError });
            }
            long sequence = GetNormalUiProofSearchSequence();
            if (!await Dom($"(()=>{{const b=document.querySelectorAll('.map-tabs button')[{index}];if(!b)return false;b.click();return true;}})()"))
                throw new InvalidDataException("MAP017 actual results tab missing.");
            NormalUiResourceProofSearchObservation? observation = null;
            async Task<bool> SearchReady()
            {
                var seen = ReadNormalUiProofSearchAfter(sequence);
                if (seen is not null && MapDataQueryContract.NormalizeSearch(seen.Payload) is { } query &&
                    query.Kind == kind && query.ServerId == 2212 && query.Page == 1 &&
                    await Dom($"!!document.querySelector('.map-table--{kind}[aria-busy=false]')")) observation = seen;
                return observation is not null;
            }
            // Changing active tabs normally searches; an already-selected tab
            // needs the same real Search action a user can press.
            await Task.Delay(500, token);
            if (!await SearchReady())
                await Dom("(()=>{const b=document.querySelector('.map-searchbar > button');if(!b||b.disabled)return false;b.click();return true;})()");
            await Wait(SearchReady, "current native saved search " + kind);
            int total = observation!.Result.GetProperty("total").GetInt32();
            JsonElement renderedSnapshot = default;
            await Wait(async () =>
            {
                renderedSnapshot = JsonSerializer.Deserialize<JsonElement>(await core.ExecuteScriptAsync($$"""
                    (() => {
                      const table = document.querySelector('.map-table--{{kind}}');
                      const tabs = [...document.querySelectorAll('.map-tabs button')];
                      const active = tabs.find(b => b.getAttribute('aria-selected') === 'true');
                      const resultCountText = document.querySelector('.map-result-count')?.innerText?.trim() ?? '';
                      const digits = resultCountText.replace(/[^0-9]/g, '');
                      const rows = [...(table?.querySelectorAll('tbody tr') ?? [])];
                      return {
                        tablePresent: !!table, busy: table?.getAttribute('aria-busy') !== 'false',
                        displayedRows: rows.filter(row => !row.querySelector('td.map-empty')).length,
                        emptyRows: rows.filter(row => !!row.querySelector('td.map-empty')).length,
                        activeTabIndex: tabs.indexOf(active), activeTabLabel: active?.querySelector('.map-tab-label')?.innerText?.trim() ?? null,
                        activeTabText: active?.innerText?.trim() ?? null,
                        activeTabCountText: active?.querySelector('.map-tab-count')?.innerText?.trim() ?? null,
                        resultCountText, resultCountValue: digits ? Number(digits) : null,
                        pageInfoText: document.querySelector('.map-pagination span')?.innerText?.trim() ?? null
                      };
                    })()
                    """));
                return renderedSnapshot.GetProperty("tablePresent").ValueKind == JsonValueKind.True &&
                    renderedSnapshot.GetProperty("busy").ValueKind == JsonValueKind.False &&
                    renderedSnapshot.GetProperty("activeTabIndex").GetInt32() == index &&
                    renderedSnapshot.GetProperty("displayedRows").GetInt32() == Math.Min(total, 50) &&
                    renderedSnapshot.GetProperty("resultCountValue").ValueKind == JsonValueKind.Number &&
                    renderedSnapshot.GetProperty("resultCountValue").TryGetInt32(out int displayedTotal) && displayedTotal == total;
            }, "rendered current saved " + kind + " row count");
            await Screenshot("map017-" + label + "-results.png");
            // A genuine zero reproduction is retained as an observation, never
            // promoted to a positive acceptance receipt.
            return new { label, kind, completedStatus = completed, persisted, selectedResponse = observation,
                total, sourceIdentifiedPositiveAvailable = total > 0, selectedTypes = selected, renderedSnapshot,
                retainedNativeFiles, rawNativeFiles, nativeFileAttribution };
        }
        Owner();
        await Wait(() => Task.FromResult(overviewLifecycleService.GetLiveServerId() == 2212), "home 2212");
        var stages = new List<object>();
        async Task Save() => await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, state = "observed", proof = "actual_packaged_map017_ui",
            mode, genuineGame = true, controlledProvider = false, owner.ProfileId, owner.SessionId,
            owner.GamePid, stages, generatedAt = DateTimeOffset.UtcNow,
        }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
        async Task AutoTab()
        {
            if (!await Dom("(()=>{const b=document.querySelectorAll('.map-scan-tabs button')[1];if(!b)return false;b.click();return true;})()"))
                throw new InvalidDataException("MAP017 Auto tab missing.");
            await Wait(() => Dom("!!document.querySelector('.map-auto-scan-card')"), "Auto tab mounted");
        }
        async Task<JsonElement> PickerSnapshot(string selector, string button, string hint)
        {
            string state = await core.ExecuteScriptAsync($"(()=>{{const a=[...document.querySelectorAll('{selector}')];const b=document.querySelector('{button}');return {{selectedIndices:a.flatMap((x,i)=>x.checked?[i]:[]),disabledIndices:a.flatMap((x,i)=>x.disabled?[i]:[]),actionDisabled:b?.disabled,hint:document.querySelector('{hint}')?.textContent??null}};}})()");
            return JsonSerializer.Deserialize<JsonElement>(state);
        }
        if (mode is "boss-first" or "boss-after-resource")
        {
            if (mode == "boss-after-resource")
            {
                stages.Add(await ManualScan(1, "resource", "resource-first"));
                await Save();
            }
            stages.Add(await ManualScan(9, "zombie_boss", "boss"));
        }
        else if (mode == "picker-empty")
        {
            await Select(manualBoxes, 9);
            await Dom("(()=>{const d=document.querySelector('.map-header .map-category-menu')?.closest('details');if(d)d.open=true;return true;})()");
            await Screenshot("map017-manual-last-selected.png");
            bool manualLastCheckboxDisabledAtClick = await Dom($"document.querySelectorAll('{manualBoxes}')[9]?.disabled===true");
            await Dom($"(()=>{{const b=document.querySelectorAll('{manualBoxes}')[9];if(!b)return false;b.click();return true;}})()");
            if (!manualLastCheckboxDisabledAtClick)
                await Wait(() => Dom($"[...document.querySelectorAll('{manualBoxes}')].every(b=>!b.checked)&&document.querySelector('.map-header .map-actions button.primary')?.disabled===true"),
                    "Manual empty picker render");
            // Cross ordinary status refreshes as well as the immediate render;
            // a transient empty local state is not persistent picker behavior.
            await Task.Delay(2500, token);
            JsonElement manual = await PickerSnapshot(manualBoxes, ".map-header .map-actions button.primary", "[data-map-empty-selection=manual]");
            await Screenshot("map017-manual-after-untick-last.png");
            stages.Add(new { stage = "manual-last-untick", snapshot = manual, lastCheckboxDisabledAtClick = manualLastCheckboxDisabledAtClick,
                passed = manual.GetProperty("selectedIndices").GetArrayLength() == 0 &&
                    manual.GetProperty("actionDisabled").ValueKind == JsonValueKind.True &&
                    manual.GetProperty("hint").ValueKind == JsonValueKind.String });
            await Save();
            await AutoTab();
            await Select(autoBoxes, 4);
            await Wait(async () =>
            {
                var acknowledged = await mapAutoScanService.GetSnapshotAsync(token);
                return acknowledged.Config.SelectedTypes?.SequenceEqual(["railway"]) == true &&
                    await Dom($"[...document.querySelectorAll('{autoBoxes}')].every((b,i)=>b.checked===(i===4))");
            }, "native and rendered Auto last selected category");
            await Dom("(()=>{const d=document.querySelector('.map-auto-scan-card .map-category-menu')?.closest('details');if(d)d.open=true;return true;})()");
            await Screenshot("map017-auto-last-selected.png");
            bool autoLastCheckboxDisabledAtClick = await Dom($"document.querySelectorAll('{autoBoxes}')[4]?.disabled===true");
            await Dom($"(()=>{{const b=document.querySelectorAll('{autoBoxes}')[4];if(!b)return false;b.click();return true;}})()");
            if (!autoLastCheckboxDisabledAtClick)
                await Wait(async () =>
                {
                    var acknowledged = await mapAutoScanService.GetSnapshotAsync(token);
                    return acknowledged.Config.SelectedTypes is { Count: 0 } &&
                        await Dom($"[...document.querySelectorAll('{autoBoxes}')].every(b=>!b.checked)&&document.querySelector('.map-auto-scan-options button.primary')?.disabled===true");
                }, "native and rendered Auto empty categories");
            JsonElement auto = await PickerSnapshot(autoBoxes, ".map-auto-scan-options button.primary", "[data-map-empty-selection=auto]");
            var native = await mapAutoScanService.GetSnapshotAsync(token);
            await Screenshot("map017-auto-after-untick-last.png");
            stages.Add(new { stage = "auto-last-untick", snapshot = auto, native, lastCheckboxDisabledAtClick = autoLastCheckboxDisabledAtClick,
                passed = auto.GetProperty("selectedIndices").GetArrayLength() == 0 &&
                    auto.GetProperty("actionDisabled").ValueKind == JsonValueKind.True &&
                    auto.GetProperty("hint").ValueKind == JsonValueKind.String &&
                    native.Config.SelectedTypes is { Count: 0 } });
        }
        else if (mode == "once-two-server")
        {
            var initial = await mapAutoScanService.GetSnapshotAsync(token);
            if (initial.Config.Enabled || initial.Running || initial.Config.ServerIds is { Count: > 0 })
                throw new InvalidDataException("MAP017 one-off requires fresh disabled idle Auto.");
            await AutoTab();
            foreach (int server in new[] { 2211, 2213 })
            {
                if (!await Dom($"(()=>{{const i=document.querySelector('.map-auto-scan-server-input input');if(!i)return false;Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(i,'{server}');i.dispatchEvent(new Event('input',{{bubbles:true}}));return true;}})()"))
                    throw new InvalidDataException("MAP017 target-server input missing.");
                await Wait(() => Dom("!!document.querySelector('.map-auto-scan-server-input button:not(:disabled)')"), "target Add enabled");
                await Dom("(()=>{const b=document.querySelector('.map-auto-scan-server-input button:not(:disabled)');if(!b)return false;b.click();return true;})()");
                await Wait(async () => (await mapAutoScanService.GetSnapshotAsync(token)).Config.ServerIds?.Contains(server) == true, "persist target " + server);
            }
            await Select(autoBoxes, 4);
            await Dom("(()=>{const b=document.querySelector('.map-auto-return input[type=checkbox]');if(!b)return false;if(!b.checked)b.click();return true;})()");
            await Wait(async () =>
            {
                var n = await mapAutoScanService.GetSnapshotAsync(token);
                return !n.Config.Enabled && n.Config.NextRunAt == 0 && n.Config.ReturnToOriginalServer &&
                    n.Config.ServerIds?.SequenceEqual([2211, 2213]) == true &&
                    n.Config.SelectedTypes?.SequenceEqual(["railway"]) == true;
            }, "schedule-off native two-server Railway configuration");
            var configured = await mapAutoScanService.GetSnapshotAsync(token);
            JsonElement before = await PickerSnapshot(autoBoxes, ".map-auto-scan-options button.primary", "[data-map-empty-selection=auto]");
            await Screenshot("map017-once-schedule-off-before.png");
            stages.Add(new { stage = "schedule-off-configured", configured, snapshot = before });
            await Save();
            if (before.GetProperty("actionDisabled").ValueKind == JsonValueKind.True)
            {
                // Baseline reproduction: do not force a disabled UI command.
                stages.Add(new { stage = "one-off-unavailable", passed = false,
                    reason = "Actual Run now is disabled while schedule is off." });
            }
            else
            {
                var runIds = new HashSet<string>(StringComparer.Ordinal);
                var completedRuns = new Dictionary<int, LWBridge.Map317.MapScanRun>();
                var serversSeen = new HashSet<int>();
                DateTimeOffset triggered = DateTimeOffset.UtcNow;
                if (!await Dom("(()=>{const b=document.querySelector('.map-auto-scan-options button.primary:not(:disabled)');if(!b)return false;b.click();return true;})()"))
                    throw new InvalidDataException("MAP017 actual Run now button unavailable.");
                bool sawRunning = false;
                MapAutoScanSnapshot cycle = configured;
                try
                {
                    await Wait(async () =>
                    {
                        cycle = await mapAutoScanService.GetSnapshotAsync(token);
                        if (cycle.Config.Enabled || cycle.Config.NextRunAt != 0)
                            throw new InvalidDataException("MAP017 one-off unexpectedly enabled the schedule.");
                        if (cycle.Running) sawRunning = true;
                        if (overviewLifecycleService.GetLiveServerId() is int server) serversSeen.Add(server);
                        JsonElement current = JsonSerializer.SerializeToElement(map317CommandService.CreateStatus(), JsonOptions.Default);
                        string? runId = ReadStatusString(current, "scanRunId");
                        if (!string.IsNullOrEmpty(runId)) runIds.Add(runId);
                        foreach (string id in runIds)
                        {
                            var persisted = map317CommandService.ReadPersistedScanRunForProof(id);
                            if (persisted?.Status == "completed") completedRuns[persisted.ServerId] = persisted;
                        }
                        return sawRunning && !cycle.Running && cycle.LastCycle is not null &&
                            cycle.LastCycle.StartedAt >= triggered.ToUnixTimeMilliseconds() - 2000;
                    }, "real two-server one-off completion", 6500);
                    if (cycle.LastCycle?.Outcome != "completed" || cycle.LastCycle.LastError is not null ||
                        cycle.LastCycle.OriginalServerId != 2212 ||
                        !cycle.LastCycle.TargetServerIds.SequenceEqual([2211, 2213]) ||
                        !cycle.LastCycle.CompletedServerIds.SequenceEqual([2211, 2213]) ||
                        !serversSeen.Contains(2211) || !serversSeen.Contains(2213))
                        throw new InvalidDataException("MAP017 one-off did not complete both actual target servers.");
                    await Wait(() => Task.FromResult(overviewLifecycleService.GetLiveServerId() == 2212), "return home 2212");
                    foreach (int server in new[] { 2211, 2213 })
                    {
                        if (!completedRuns.TryGetValue(server, out var run) || run.Error is not null ||
                            run.CompletedBlocks != 2500 || run.FailedBlocks != 0 || !run.SelectedTypes.SequenceEqual(["railway"]))
                            throw new InvalidDataException("MAP017 target lacks its exact completed Railway SQLite run: " + server);
                    }
                    await Screenshot("map017-once-completed-returned-home.png");
                    stages.Add(new { stage = "one-off-completed", passed = true, runNowClicks = 1,
                        originalServerId = 2212, restoredServerId = overviewLifecycleService.GetLiveServerId(),
                        cycle, serversSeen, completedRuns, triggered });
                    await Save();
                    // Visit each saved target through ordinary UI after native
                    // home restoration; this never navigates the game again.
                    await Dom("(()=>{const b=document.querySelectorAll('.map-scan-tabs button')[0];if(!b)return false;b.click();return true;})()");
                    foreach (int server in new[] { 2211, 2213 })
                    {
                        Owner();
                        JsonElement nativeQuery = JsonSerializer.SerializeToElement(new
                        {
                            kind = "railway", query = new { serverId = server, page = 1, pageSize = 50 },
                        }, JsonOptions.Default);
                        JsonElement nativeResponse = JsonSerializer.SerializeToElement(
                            await map317CommandService.InvokeAsync("map_search", nativeQuery, token), JsonOptions.Default);
                        if (!nativeResponse.TryGetProperty("rows", out JsonElement nativeRows) ||
                            nativeRows.ValueKind != JsonValueKind.Array ||
                            !nativeResponse.TryGetProperty("total", out JsonElement nativeTotal) ||
                            !nativeTotal.TryGetInt32(out int targetTotal) || targetTotal < 0)
                            throw new InvalidDataException("MAP017 completed target has malformed retained Railway results: " + server);
                        JsonElement nativeSaved = JsonSerializer.SerializeToElement(
                            await map317CommandService.InvokeAsync("map_saved_servers",
                                JsonSerializer.SerializeToElement(new { }, JsonOptions.Default), token), JsonOptions.Default);
                        if (!nativeSaved.TryGetProperty("servers", out JsonElement publishedServers) ||
                            publishedServers.ValueKind != JsonValueKind.Array)
                            throw new InvalidDataException("MAP017 completed target has malformed saved-server results.");
                        bool retainedServer = publishedServers.EnumerateArray().Any(value => value.GetInt32() == server);
                        // Saved servers describe retained records, not completed
                        // scan history. An empty Railway target with no other
                        // saved records correctly has no dropdown option.
                        if (targetTotal == 0 && !retainedServer)
                        {
                            if (nativeRows.GetArrayLength() != 0)
                                throw new InvalidDataException("MAP017 zero Railway total returned nonempty rows.");
                            await Task.Delay(500, token);
                            await Wait(() => Dom($"![...document.querySelectorAll('select[data-map-saved-server] option')].some(o=>o.value==='{server}')"),
                                "valid empty target absent from Saved " + server);
                            JsonElement savedServerSnapshot = JsonSerializer.Deserialize<JsonElement>(
                                await core.ExecuteScriptAsync("(()=>{const s=document.querySelector('select[data-map-saved-server]');return {selectPresent:!!s,selectedValue:s?.value??null,optionValues:[...document.querySelectorAll('select[data-map-saved-server] option')].map(o=>o.value)};})()"));
                            string screenshot = "map017-once-empty-" + server + "-no-saved-phantom.png";
                            await Screenshot(screenshot);
                            stages.Add(new { stage = "empty-target-results", passed = true, serverId = server,
                                nativeQuery, nativeResponse, nativeSaved, total = targetTotal,
                                savedServerSnapshot, screenshot, source = "native_read_after_current_completed_cycle" });
                            await Save();
                            continue;
                        }
                        await Wait(() => Dom($"[...document.querySelectorAll('select[data-map-saved-server] option')].some(o=>o.value==='{server}')"), "saved target " + server);
                        await Dom($"(()=>{{const s=document.querySelector('select[data-map-saved-server]');if(!s)return false;s.value='{server}';s.dispatchEvent(new Event('change',{{bubbles:true}}));return true;}})()");
                        long seq = GetNormalUiProofSearchSequence();
                        await Dom("(()=>{document.querySelectorAll('.map-tabs button')[4]?.click();return true;})()");
                        await Task.Delay(400, token);
                        await Dom("(()=>{document.querySelector('.map-searchbar > button')?.click();return true;})()");
                        NormalUiResourceProofSearchObservation? observation = null;
                        await Wait(() =>
                        {
                            var seen = ReadNormalUiProofSearchAfter(seq);
                            if (seen is not null && MapDataQueryContract.NormalizeSearch(seen.Payload) is { } q &&
                                q.Kind == "railway" && q.ServerId == server) observation = seen;
                            return Task.FromResult(observation is not null);
                        }, "mounted saved target Railway query " + server);
                        await Wait(() => Dom("!!document.querySelector('.map-table--railway[aria-busy=false]')"), "saved target table rendered " + server);
                        await Screenshot("map017-once-saved-" + server + "-railway.png");
                        stages.Add(new { stage = "saved-target-results", serverId = server, selectedResponse = observation,
                            total = observation!.Result.GetProperty("total").GetInt32() });
                    }
                }
                finally
                {
                    if (overviewLifecycleService.MatchesOwnedMapScanSession(owner) && mapAutoScanService.IsCycleBusy)
                        await mapAutoScanService.CancelAsync(CancellationToken.None);
                }
            }
        }
        await Save();
    }
}
