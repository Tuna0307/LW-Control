using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Opt-in real Auto -> remote server -> return -> saved-row Jump -> Auto Clear.
    // Uses only isolated production WebView controls, production transport, and
    // the owner's authenticated, exact-owned game session. No fixture rows.
    private async Task RunMap014NativeRemoteUiProofAsync(CoreWebView2 core, string directory, CancellationToken token)
    {
        if (productionPaths?.ExplicitIsolation != true || map317CommandService is null ||
            mapAutoScanService is null || overviewLifecycleService is null)
            throw new InvalidOperationException("Remote native Map proof requires isolated production owners.");

        int homeServer = overviewLifecycleService.GetLiveServerId() ?? 0;
        for (int readiness=0; homeServer<=0 && readiness<450; readiness++)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(100,token);
            homeServer=overviewLifecycleService.GetLiveServerId() ?? 0;
        }
        bool remoteRailway = Environment.GetEnvironmentVariable("LWBRIDGE_MAP016_REMOTE_RAILWAY") == "1";
        int remoteServer = remoteRailway ? 2211 : 2213;
        string? ownedRailwayRunId = null;
        bool reuseGenuineRemote = Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_REMOTE_REUSE") == "1";
        if (homeServer != 2212)
            throw new InvalidDataException("Remote proof requires authenticated game start on 2212; observed "+homeServer);
        OverviewMapScanSession proofSession=overviewLifecycleService.GetReadyMapScanSession()
            ?? throw new InvalidDataException("Remote proof requires an exact ready game session.");
        void RequireProofOwner()
        {
            if(!overviewLifecycleService.MatchesOwnedMapScanSession(proofSession))
                throw new InvalidDataException("Remote proof's original game session was retired or replaced.");
        }
        var results = new List<object>();
        string receipt = Path.Combine(directory, "map014-remote-ui.json");
        async Task Save() => await File.WriteAllTextAsync(receipt,
            JsonSerializer.Serialize(new {genuineGame=true, controlledProvider=false,
                proof=remoteRailway ? "real_native_auto_remote_railway_2211_fast_625_return" :
                    "real_native_auto_remote_resource_2213_jump_clear",
                homeServer,remoteServer,
                proofSession.ProfileId,proofSession.SessionId,proofSession.GamePid,proofSession.GameStartedAtUtc,results},
                new JsonSerializerOptions(JsonOptions.Default){WriteIndented=true}),token);
        async Task RequireUi(Func<Task<bool>> condition, string stage, int attempts=250)
        {
            for (int i=0; i<attempts; i++)
            {
                token.ThrowIfCancellationRequested();
                if (await condition()) return;
                await Task.Delay(100,token);
            }
            throw new TimeoutException("MAP014 real remote UI stage did not settle: " + stage);
        }
        async Task<bool> Dom(string script) => await core.ExecuteScriptAsync(script)=="true";
        async Task<int> ObserveLiveServer(int expected, string stage)
        {
            int? observed = null;
            await RequireUi(() =>
            {
                RequireProofOwner();
                // GetLiveServerId reads an independently refreshed heartbeat.
                // A native return acknowledgement can precede that refresh;
                // incomplete writes yield null, not evidence of a wrong server.
                observed = overviewLifecycleService.GetLiveServerId();
                return Task.FromResult(observed == expected);
            }, stage, 450);
            return observed!.Value;
        }
        async Task<JsonElement> RealRows(int server)
        {
            RequireProofOwner();
            object? value=await map317CommandService.InvokeAsync("map_search",
                JsonSerializer.SerializeToElement(new{kind="resource",query=new{serverId=server,page=1,pageSize=50}},
                    JsonOptions.Default),token);
            JsonElement json=JsonSerializer.SerializeToElement(value,JsonOptions.Default);
            if(!json.TryGetProperty("rows",out var rows) || rows.ValueKind!=JsonValueKind.Array ||
                !json.TryGetProperty("total",out var total) || !total.TryGetInt32(out _))
                throw new InvalidDataException("Production Map search returned malformed persisted data.");
            return json;
        }
        async Task ReturnHome()
        {
            // The lifecycle's cached world summary can lag an individual
            // successful coordinate Jump. Always ask the source-backed
            // server command to confirm home, even if a cached getter says
            // home already. Already-home is an acknowledged no-op.
            for(int wait=0;wait<200;wait++)
            {
                try
                {
                    RequireProofOwner();
                    object? returned=await map317CommandService.InvokeAsync("server_jump",
                        JsonSerializer.SerializeToElement(new{serverId=homeServer},JsonOptions.Default),CancellationToken.None);
                    JsonElement acknowledgement=JsonSerializer.SerializeToElement(returned,JsonOptions.Default);
                    if(acknowledgement.GetProperty("serverId").GetInt32()!=homeServer)
                        throw new InvalidDataException("Server-return native acknowledgement did not confirm original server.");
                    return;
                }
                catch(BridgeCommandException error) when(error.Code=="GAME_OPERATION_IN_PROGRESS")
                {
                    await Task.Delay(100,CancellationToken.None);
                }
            }
            throw new TimeoutException("The preceding native Jump lease did not release before the exact server return.");
        }
        try
        {
            if (remoteRailway)
            {
                // This proof is deliberately scan-only. Remote Resource's later
                // coordinate Jump and saved-server Clear are not Railway actions.
                MapAutoScanSnapshot initialAuto = await mapAutoScanService.GetSnapshotAsync(token);
                RequireProofOwner();
                if (initialAuto.Config.Enabled || initialAuto.Running || mapAutoScanService.IsCycleBusy ||
                    initialAuto.Config.ServerIds is { Count: > 0 })
                    throw new InvalidDataException("Remote Railway requires a fresh idle isolated Auto owner.");
                results.Add(new { stage="railway_original_server",serverId=await ObserveLiveServer(homeServer,
                    "Railway original 2212 heartbeat") });
                await Save();

                if (!await Dom("(()=>{const tab=document.querySelectorAll('.map-scan-tabs button')[1];if(!tab)return false;tab.click();return true;})()"))
                    throw new InvalidDataException("Actual Auto tab is unavailable for Railway.");
                await RequireUi(()=>Dom("!!document.querySelector('.map-auto-scan-card')"),
                    "Railway Auto controls");
                if (!await Dom("(()=>{const input=document.querySelector('.map-auto-scan-server-input input');if(!input)return false;Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'2211');input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()"))
                    throw new InvalidDataException("Railway Auto server input unavailable.");
                await RequireUi(()=>Dom("!!document.querySelector('.map-auto-scan-server-input button:not(:disabled)')"),
                    "Railway target 2211 entry");
                if (!await Dom("(()=>{const b=document.querySelector('.map-auto-scan-server-input button:not(:disabled)');if(!b)return false;b.click();return true;})()"))
                    throw new InvalidDataException("Railway Auto Add server unavailable.");
                await RequireUi(()=>Dom("[...document.querySelectorAll('.map-auto-scan-server-chips > span')].some(x=>x.textContent.includes('2211'))"),
                    "Railway target 2211 persisted");
                // The last selected category is disabled in the live UI:
                // select Railway first so a saved City-only config can be
                // safely narrowed without attempting to click its last box.
                if (!await Dom("(()=>{const b=document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[4];if(!b)return false;if(!b.checked)b.click();return true;})()"))
                    throw new InvalidDataException("Railway Auto category checkbox unavailable.");
                await RequireUi(()=>Dom("document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[4]?.checked===true"),
                    "Railway Auto category selected before deselection");
                for (int index=0;index<10;index++)
                {
                    if(index==4) continue;
                    string select=$"(()=>{{const box=document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[{index}];if(!box)return false;if(box.checked!==({index}===4))box.click();return true;}})()";
                    if (!await Dom(select))
                        throw new InvalidDataException("Railway Auto category checkbox unavailable: "+index);
                    string settled=$"document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[{index}]?.checked===({index}===4)";
                    await RequireUi(()=>Dom(settled),"Railway Auto category selection "+index);
                }
                await RequireUi(()=>Dom("(()=>{const a=[...document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')];return a.length===10&&a.every((b,i)=>b.checked===(i===4));})()"),
                    "Railway only Auto categories");
                if (!await Dom("(()=>{const b=document.querySelector('.map-auto-return input[type=checkbox]');if(!b)return false;if(!b.checked)b.click();return true;})()"))
                    throw new InvalidDataException("Railway Auto return-to-original control unavailable.");
                if (!await Dom("(()=>{const b=document.querySelector('.map-auto-scan-master input[type=checkbox]');if(!b)return false;if(!b.checked)b.click();return true;})()"))
                    throw new InvalidDataException("Railway Auto master enable unavailable.");
                MapAutoScanSnapshot configured = await mapAutoScanService.GetSnapshotAsync(token);
                await RequireUi(async ()=>{
                    configured=await mapAutoScanService.GetSnapshotAsync(token);
                    return configured.Config.Enabled && configured.Config.ReturnToOriginalServer &&
                        configured.Config.ServerIds is {Count:1} && configured.Config.ServerIds[0]==remoteServer &&
                        configured.Config.SelectedTypes is {Count:1} &&
                        configured.Config.SelectedTypes[0]=="railway" && configured.Config.ScanMode=="fast";
                },"exact persisted Railway Auto configuration");
                RequireProofOwner();
                if (mapAutoScanService.IsCycleBusy || configured.Running)
                    throw new InvalidDataException("Railway Auto started without explicit Run Now.");
                results.Add(new {stage="railway_auto_config_persisted",
                    configured.Config.ServerIds, configured.Config.SelectedTypes,
                    configured.Config.ScanMode, configured.Config.ReturnToOriginalServer});
                await Save();

                DateTimeOffset triggeredAt=DateTimeOffset.UtcNow;
                if (!await Dom("(()=>{const b=document.querySelector('.map-auto-scan-options button.primary:not(:disabled)');if(!b)return false;b.click();return true;})()"))
                    throw new InvalidDataException("Explicit Railway Auto Run Now was unavailable.");
                bool sawRunning=false;
                DateTimeOffset? sawRemoteAt=null;
                MapAutoScanSnapshot cycle=configured;
                for(int poll=0;poll<6200;poll++)
                {
                    token.ThrowIfCancellationRequested();
                    RequireProofOwner();
                    cycle=await mapAutoScanService.GetSnapshotAsync(token);
                    if(cycle.Running) sawRunning=true;
                    if(sawRemoteAt is null && overviewLifecycleService.GetLiveServerId()==remoteServer)
                        sawRemoteAt=DateTimeOffset.UtcNow;
                    if(sawRunning && !cycle.Running && cycle.LastCycle is {StartedAt:>0} &&
                       cycle.LastCycle.StartedAt >= triggeredAt.ToUnixTimeMilliseconds()-2000 &&
                       cycle.LastCycle.OriginalServerId==homeServer &&
                       cycle.LastCycle.TargetServerIds.SequenceEqual([remoteServer]) &&
                       cycle.LastCycle.CompletedServerIds.SequenceEqual([remoteServer]))
                        break;
                    await Task.Delay(100,token);
                }
                if(!sawRunning || sawRemoteAt is null || cycle.Running ||
                   cycle.LastCycle?.Outcome!="completed" || cycle.LastCycle.LastError is not null ||
                   cycle.LastCycle.OriginalServerId!=homeServer ||
                   !cycle.LastCycle.TargetServerIds.SequenceEqual([remoteServer]) ||
                   !cycle.LastCycle.CompletedServerIds.SequenceEqual([remoteServer]))
                    throw new InvalidDataException("Source-owned Railway Auto cycle did not complete: "+
                        JsonSerializer.Serialize(cycle.LastCycle,JsonOptions.Default));
                DateTimeOffset observedHomeAt=DateTimeOffset.UtcNow;
                int observedHome=await ObserveLiveServer(homeServer,"Railway native return home 2212");
                observedHomeAt=DateTimeOffset.UtcNow;
                RequireProofOwner();

                string nativePath=Path.Combine(overviewLifecycleService.LiveResourceRuntimeRoot,
                    "fast-truck-scan-result.json");
                using JsonDocument nativeDocument=JsonDocument.Parse(await File.ReadAllTextAsync(nativePath,token));
                JsonElement native=nativeDocument.RootElement;
                string nativeRunId=ReadStatusString(native,"scanRunId") ?? "";
                DateTimeOffset nativeReceiptAt=new(File.GetLastWriteTimeUtc(nativePath));
                if(nativeRunId.Length==0 || ReadStatusString(native,"state")!="proven" ||
                   ReadStatusString(native,"profileId")!=proofSession.ProfileId ||
                   ReadStatusString(native,"launchSessionId")!=proofSession.SessionId ||
                   ReadStatusString(native,"challenge")!=proofSession.Challenge ||
                   ReadStatusInt32(native,"gamePid")!=proofSession.GamePid ||
                   ReadStatusInt32(native,"serverId")!=remoteServer ||
                   ReadStatusInt32(native,"rectanglesCompleted")!=625 ||
                   ReadStatusInt32(native,"initialRequestsSent")!=625 ||
                   ReadStatusInt32(native,"unprovenRectangles")!=0 ||
                   ReadStatusInt32(native,"railwayRecordCount") is null ||
                   !native.TryGetProperty("railwayOnly",out JsonElement railwayOnly) ||
                   railwayOnly.ValueKind!=JsonValueKind.True ||
                   !native.TryGetProperty("railwayIncluded",out JsonElement railwayIncluded) ||
                   railwayIncluded.ValueKind!=JsonValueKind.True ||
                   !native.TryGetProperty("cameraStable",out JsonElement cameraStable) ||
                   cameraStable.ValueKind!=JsonValueKind.True ||
                   !native.TryGetProperty("curViewRestored",out JsonElement nativeRestored) ||
                   nativeRestored.ValueKind!=JsonValueKind.True)
                    throw new InvalidDataException("Railway Auto did not retain its exact 625 native typed rectangles.");
                ownedRailwayRunId=nativeRunId;
                results.Add(new {stage="railway_auto_returned_home",originalServerId=homeServer,
                    targetServerId=remoteServer,liveServerId=observedHome,scanRunId=nativeRunId,
                    nativeRailwayRecords=ReadStatusInt32(native,"railwayRecordCount"),
                    nativeRectangles=625, cycle.LastCycle,
                    triggeredAt,sawRemoteAt,observedHomeAt,
                    nativeReceiptAt,
                    switchObservedMilliseconds=(sawRemoteAt.Value-triggeredAt).TotalMilliseconds,
                    sweepElapsedSeconds=native.TryGetProperty("elapsedSeconds",out JsonElement elapsed) &&
                        elapsed.TryGetDouble(out double elapsedSeconds) ? elapsedSeconds : (double?)null,
                    returnObservedMilliseconds=(observedHomeAt-nativeReceiptAt).TotalMilliseconds});
                await Save();

                // The normal current-status UI is now on 2212; it is allowed to
                // forget the previously completed 2211 run. Require that the
                // exact native run was published durably and keep the current
                // status separately rather than presenting it as the 2211 scan.
                LWBridge.Map317.MapScanRun persistedRun =
                    map317CommandService.ReadPersistedScanRunForProof(nativeRunId)
                    ?? throw new InvalidDataException("Remote Railway completed scan is absent from durable Map storage.");
                if(persistedRun.Id!=nativeRunId || persistedRun.ServerId!=remoteServer ||
                   persistedRun.Status!="completed" ||
                   !persistedRun.SelectedTypes.SequenceEqual(["railway"],StringComparer.Ordinal) ||
                   persistedRun.TotalBlocks!=2500 || persistedRun.CompletedBlocks!=2500 ||
                   persistedRun.FailedBlocks!=0 || persistedRun.Error is not null)
                    throw new InvalidDataException("Remote Railway did not publish its exact completed 2500-block run.");
                var completedStatus=new {
                    source="persisted_completed_scan_run", serverId=persistedRun.ServerId,
                    scanRunId=persistedRun.Id, phase=persistedRun.Status,
                    selectedTypes=persistedRun.SelectedTypes, totalBlocks=persistedRun.TotalBlocks,
                    readBlocks=persistedRun.CompletedBlocks, failedBlocks=persistedRun.FailedBlocks,
                    unreadBlocks=persistedRun.TotalBlocks-persistedRun.CompletedBlocks-persistedRun.FailedBlocks,
                    scanMode="fast"
                };
                JsonElement postReturnLiveStatus=JsonSerializer.SerializeToElement(
                    map317CommandService.CreateStatus(),JsonOptions.Default);
                if(postReturnLiveStatus.TryGetProperty("isReading",out JsonElement postReturnReading) &&
                   postReturnReading.ValueKind==JsonValueKind.True)
                    throw new InvalidDataException("Remote Railway left the live Map scan active after returning home.");
                if(ReadStatusInt32(native,"railwayRecordCount")==0)
                {
                    // A complete empty Railway scan creates no Saved 2211
                    // option. Query that server through the production
                    // Map317 backend and prove the dropdown has no phantom.
                    JsonElement query=JsonSerializer.SerializeToElement(new
                    {kind="railway",query=new {serverId=remoteServer,page=1,pageSize=50}},JsonOptions.Default);
                    object? zeroSearch=await map317CommandService.InvokeAsync(
                        "map_search",query,token);
                    JsonElement emptyResult=JsonSerializer.SerializeToElement(zeroSearch,JsonOptions.Default);
                    if(!emptyResult.TryGetProperty("total",out JsonElement emptyTotal) ||
                       emptyTotal.GetInt32()!=0 ||
                       !emptyResult.TryGetProperty("rows",out JsonElement emptyRows) ||
                       emptyRows.ValueKind!=JsonValueKind.Array || emptyRows.GetArrayLength()!=0)
                        throw new InvalidDataException("Verified empty native Railway disagreed with production Map search.");
                    await RequireUi(()=>Dom("![...document.querySelectorAll('select[data-map-saved-server] option')].some(o=>o.value==='2211')"),
                        "empty Railway has no phantom saved server 2211");
                    if(!await Dom("(()=>{const b=document.querySelectorAll('.map-tabs button')[4];if(!b)return false;b.click();return true;})()"))
                        throw new InvalidDataException("Empty Railway results tab is unavailable.");
                    await RequireUi(()=>Dom("!!document.querySelector('.map-table--railway')"),
                        "empty Railway results table");
                    string emptyScreenshot=Path.Combine(directory,"map016-remote-railway-2211.png");
                    await using(var image=File.Create(emptyScreenshot))
                        await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
                    results.Add(new {stage="railway_remote_ui_saved_result",scanRunId=nativeRunId,
                        serverId=remoteServer,selectedServerId=(int?)null,
                        selectedResponse=new{payload=query,result=emptyResult},
                        completedStatus,postReturnLiveStatus,total=0,
                        mountedDom=new{serverOptionAbsent=true,tablePresent=true,displayedRows=0},
                        screenshot=emptyScreenshot,source="production_map_search_verified_empty"});
                    await Save();
                    return;
                }
                await RequireUi(()=>Dom("[...document.querySelectorAll('select[data-map-saved-server] option')].some(o=>o.value==='2211')"),
                    "Railway published saved server 2211");
                if(!await Dom("(()=>{const s=document.querySelector('select[data-map-saved-server]');if(!s)return false;s.value='2211';s.dispatchEvent(new Event('change',{bubbles:true}));return true;})()"))
                    throw new InvalidDataException("Railway saved server 2211 UI selection unavailable.");
                await RequireUi(()=>Dom("document.querySelector('select[data-map-saved-server]')?.value==='2211'"),
                    "Railway saved server selection settled");
                if(!await Dom("(()=>{const b=document.querySelectorAll('.map-tabs button')[4];if(!b)return false;b.click();return true;})()"))
                    throw new InvalidDataException("Railway results tab unavailable.");
                await RequireUi(()=>Dom("!!document.querySelector('.map-table--railway[aria-busy=false]')"),
                    "Railway saved results table");
                long explicitSearchBefore=GetNormalUiProofSearchSequence();
                if(!await Dom("(()=>{const b=document.querySelector('.map-searchbar > button:not(:disabled)');if(!b)return false;b.click();return true;})()"))
                    throw new InvalidDataException("Railway real UI search unavailable.");
                NormalUiResourceProofSearchObservation? railwaySearch=null;
                for(int poll=0;poll<300;poll++)
                {
                    railwaySearch=ReadNormalUiProofSearchAfter(explicitSearchBefore);
                    if(railwaySearch is not null && MapDataQueryContract.NormalizeSearch(railwaySearch.Payload) is
                       {Kind:"railway",ServerId:2211} && railwaySearch.Result.TryGetProperty("rows",out JsonElement rows) &&
                       rows.ValueKind==JsonValueKind.Array &&
                       railwaySearch.Result.TryGetProperty("total",out JsonElement total) && total.TryGetInt32(out int count) &&
                       count>=0 && await Dom("!!document.querySelector('.map-table--railway[aria-busy=false]')"))
                        break;
                    await Task.Delay(100,token);
                }
                if(railwaySearch is null || MapDataQueryContract.NormalizeSearch(railwaySearch.Payload) is not
                   {Kind:"railway",ServerId:2211})
                    throw new InvalidDataException("Railway Auto returned no same-server real UI search.");
                string screenshot=Path.Combine(directory,"map016-remote-railway-2211.png");
                await using(var image=File.Create(screenshot))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
                string mountedDom=await core.ExecuteScriptAsync(
                    "({tablePresent:!!document.querySelector('.map-table--railway'),displayedRows:document.querySelectorAll('.map-table--railway tbody tr').length})");
                results.Add(new {stage="railway_remote_ui_saved_result",scanRunId=nativeRunId,
                    serverId=remoteServer,selectedServerId=remoteServer,
                    selectedResponse=railwaySearch,completedStatus,postReturnLiveStatus,
                    total=railwaySearch.Result.GetProperty("total").GetInt32(),
                    mountedDom=JsonSerializer.Deserialize<JsonElement>(mountedDom),
                    screenshot,source="real_packaged_webview_map_search"});
                await Save();
                return;
            }
            JsonElement startingRows=await RealRows(homeServer);
            int originalCount=startingRows.GetProperty("total").GetInt32();
            if(originalCount<1) throw new InvalidDataException("Preserved genuine 2212 scan does not contain home Resource rows.");
            results.Add(new{stage="initial_home_persisted_genuine_scan",serverId=homeServer,total=originalCount});
            await Save();

            if(!await Dom("(()=>{const tab=document.querySelectorAll('.map-scan-tabs button')[1];if(!tab)return false;tab.click();return true;})()"))
                throw new InvalidDataException("Actual Auto tab is unavailable.");
            await RequireUi(()=>Dom("!!document.querySelector('.map-auto-scan-card')"),"Auto tab");

            if(!reuseGenuineRemote)
            {
            // Enter one remote destination using the actual public Auto UI.
            if(!await Dom("(()=>{const input=document.querySelector('.map-auto-scan-server-input input');if(!input)return false;Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'2213');input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()"))
                throw new InvalidDataException("Actual Auto target field is missing.");
            await RequireUi(()=>Dom("!!document.querySelector('.map-auto-scan-server-input button:not(:disabled)')"),"remote target entry");
            if(!await Dom("(()=>{const button=document.querySelector('.map-auto-scan-server-input button:not(:disabled)');if(!button)return false;button.click();return true;})()"))
                throw new InvalidDataException("Actual Auto Add button was disabled.");
            await RequireUi(()=>Dom("[...document.querySelectorAll('.map-auto-scan-server-chips > span')].some(x=>x.textContent.includes('2213'))"),"persisted remote target");

            // Auto defaults exclude Resource. Select it first, then disable
            // other kinds after each controlled React state commit.
            if(!await Dom("(()=>{const b=document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[1];if(!b)return false;if(!b.checked)b.click();return true;})()"))
                throw new InvalidDataException("Auto Resource checkbox missing.");
            await RequireUi(()=>Dom("document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[1]?.checked===true"),"Auto Resource selection");
            for(int index=0;index<10;index++)
            {
                if(index==1) continue;
                string predicate=$"(()=>{{const box=document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[{index}];if(!box)return false;if(box.checked)box.click();return true;}})()";
                if(!await Dom(predicate)) throw new InvalidDataException("Auto type checkbox is unavailable: "+index);
                string settled=$"document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[{index}]?.checked===false";
                await RequireUi(()=>Dom(settled),"Auto deselect "+index);
            }
            await RequireUi(()=>Dom("(()=>{const boxes=[...document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')];return boxes.length===10&&boxes.every((box,i)=>box.checked===(i===1));})()"),"resource-only Auto types including unselected Other");
            if(!await Dom("(()=>{const b=document.querySelector('.map-auto-return input[type=checkbox]');if(!b)return false;if(!b.checked)b.click();return true;})()"))
                throw new InvalidDataException("Auto return-to-original checkbox missing.");
            if(!await Dom("(()=>{const b=document.querySelector('.map-auto-scan-master input[type=checkbox]');if(!b)return false;if(!b.checked)b.click();return true;})()"))
                throw new InvalidDataException("Auto master enable control missing.");
            MapAutoScanSnapshot before=await mapAutoScanService.GetSnapshotAsync(token);
            for(int i=0;i<250;i++)
            {
                token.ThrowIfCancellationRequested();
                before=await mapAutoScanService.GetSnapshotAsync(token);
                if(before.Config.Enabled && before.Config.ReturnToOriginalServer &&
                   before.Config.ServerIds is {Count:1} && before.Config.ServerIds[0]==remoteServer &&
                   before.Config.SelectedTypes is {Count:1} && before.Config.SelectedTypes[0]=="resource") break;
                await Task.Delay(100,token);
            }
            if(!before.Config.Enabled || !before.Config.ReturnToOriginalServer ||
               before.Config.ServerIds is not {Count:1} || before.Config.ServerIds[0]!=remoteServer ||
               before.Config.SelectedTypes is not {Count:1} || before.Config.SelectedTypes[0]!="resource")
                throw new InvalidDataException("Real UI Auto config did not persist exactly 2213, Resource, and return home.");
            results.Add(new{stage="ui_auto_config_persisted", before.Config.ServerIds,before.Config.SelectedTypes,before.Config.ReturnToOriginalServer});
            await Save();

            // Enabling Auto may schedule an immediate cycle. If it has not
            // started, exercise Run Now; never create a second concurrent run.
            if(!mapAutoScanService.IsCycleBusy)
                await Dom("(()=>{const b=document.querySelector('.map-auto-scan-options button.primary:not(:disabled)');if(!b)return false;b.click();return true;})()");
            bool observedRunning=mapAutoScanService.IsCycleBusy, observedRemote=false;
            MapAutoScanSnapshot cycle=before;
            for(int i=0;i<6200;i++)
            {
                token.ThrowIfCancellationRequested();
                cycle=await mapAutoScanService.GetSnapshotAsync(token);
                if(cycle.Running) observedRunning=true;
                if(overviewLifecycleService.GetLiveServerId()==remoteServer) observedRemote=true;
                if(observedRunning && !cycle.Running && cycle.LastCycle is {StartedAt:>0} &&
                   cycle.LastCycle.StartedAt>=(before.LastCycle?.StartedAt ?? 0) &&
                   cycle.LastCycle.TargetServerIds.Contains(remoteServer)) break;
                await Task.Delay(100,token);
            }
            if(!observedRunning || !observedRemote || cycle.Running || cycle.LastCycle?.Outcome!="completed" ||
               cycle.LastCycle.CompletedServerIds is null || !cycle.LastCycle.CompletedServerIds.Contains(remoteServer))
                throw new InvalidDataException("Real Auto remote cycle failed: "+JsonSerializer.Serialize(cycle.LastCycle,JsonOptions.Default));
            results.Add(new{stage="auto_cycle_settled_before_heartbeat",cycle.LastCycle,
                observedLiveServerId=overviewLifecycleService.GetLiveServerId()});
            await Save();
            int observedHomeServer = await ObserveLiveServer(homeServer,"Auto returned home heartbeat");
            JsonElement remoteRows=await RealRows(remoteServer);
            int remoteCount=remoteRows.GetProperty("total").GetInt32();
            if(remoteCount<1) throw new InvalidDataException("Actual Auto scan did not publish a positive server-2213 Resource dataset.");
            results.Add(new{stage="remote_published_returned_home",observedRunning,observedRemote,
                liveServerId=observedHomeServer,remoteCount,cycle.LastCycle});
            await Save();
            }
            else
            {
                JsonElement retained=await RealRows(remoteServer);
                int retainedTotal=retained.GetProperty("total").GetInt32();
                if(retainedTotal<1) throw new InvalidDataException("Genuine previously scanned 2213 dataset is missing.");
                await RequireUi(()=>Dom("[...document.querySelectorAll('select[data-map-saved-server] option')].some(o=>o.value==='2213')"),
                    "previously scanned remote server option");
                if(!await Dom("(()=>{const s=document.querySelector('select[data-map-saved-server]');if(!s)return false;s.value='2213';s.dispatchEvent(new Event('change',{bubbles:true}));return true;})()"))
                    throw new InvalidDataException("Real UI saved-server selection failed.");
                results.Add(new{stage="genuine_remote_scan_reused_for_ui_correction",remoteServer,retainedTotal,
                    liveServerId=overviewLifecycleService.GetLiveServerId(),newRemoteScans=0});
                await Save();
            }

            // The server chosen during Auto must remain selected after its
            // return home, without manual reselecting or a summary poll reset.
            await RequireUi(()=>Dom("(()=>{const s=document.querySelector('select[data-map-saved-server]');return s&&s.value==='2213'&&[...s.options].some(o=>o.value==='2212');})()"),"retained remote dataset selection",300);
            if(!await Dom("(()=>{const tab=document.querySelectorAll('.map-tabs button')[1];if(!tab)return false;tab.click();return true;})()"))
                throw new InvalidDataException("Real Resource browsing tab is unavailable.");
            await RequireUi(()=>Dom("!!document.querySelector('.map-tabs button:nth-child(2)[aria-selected=true]')&&!!document.querySelector('.map-table--resource')"),
                "saved remote Resource tab");
            long searchBefore=GetNormalUiProofSearchSequence();
            if(!await Dom("(()=>{const b=[...document.querySelectorAll('.map-searchbar > button')].find(x=>!x.disabled);if(!b)return false;b.click();return true;})()"))
                throw new InvalidDataException("Remote Resource Search button is unavailable.");
            NormalUiResourceProofSearchObservation? searched=null;
            bool searchSettled=false;
            for(int i=0;i<300;i++)
            {
                searched=ReadNormalUiProofSearchAfter(searchBefore);
                if(searched is not null && MapDataQueryContract.NormalizeSearch(searched.Payload) is
                    {Kind:"resource"} && MapDataQueryContract.NormalizeSearch(searched.Payload).ServerId==remoteServer &&
                    searched.Result.TryGetProperty("total",out var count) && count.GetInt32()>0 &&
                    await Dom("!!document.querySelector('.map-table--resource[aria-busy=false]')"))
                { searchSettled=true; break; }
                await Task.Delay(100,token);
            }
            if(!searchSettled || searched is null)
            {
                string rendered=await core.ExecuteScriptAsync("(()=>({selected:document.querySelector('select[data-map-saved-server]')?.value,tab:document.querySelector('.map-tabs [aria-selected=true]')?.textContent,busy:document.querySelector('.map-table')?.getAttribute('aria-busy'),visibleRows:document.querySelectorAll('.map-table tbody tr').length,errors:[...document.querySelectorAll('.map-scan-error')].map(n=>n.textContent)}))()");
                var latest=ReadNormalUiProofSearchAfter(searchBefore);
                results.Add(new{stage="remote_row_search_unsettled",beforeSequence=searchBefore,
                    afterSequence=GetNormalUiProofSearchSequence(),
                    latestKind=latest is null ? null : MapDataQueryContract.NormalizeSearch(latest.Payload).Kind,
                    latestServer=latest is null ? (int?)null : MapDataQueryContract.NormalizeSearch(latest.Payload).ServerId,
                    latestTotal=latest?.Result.GetProperty("total").GetInt32(),
                    dom=JsonSerializer.Deserialize<JsonElement>(rendered)});
                await Save();
                throw new InvalidDataException("Real UI did not browse saved server-2213 Resource rows after returning home.");
            }
            results.Add(new{stage="remote_ui_browse",selectedServerId=remoteServer,
                resultCount=searched.Result.GetProperty("total").GetInt32(),
                nativeQueryServerId=MapDataQueryContract.NormalizeSearch(searched.Payload).ServerId});
            await using(var screenshot=File.Create(Path.Combine(directory,"map014-remote-saved-rows.png")))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,screenshot);
            await Save();

            JsonElement? selected=null;
            foreach(JsonElement row in searched.Result.GetProperty("rows").EnumerateArray())
            {
                if(row.TryGetProperty("x",out var x) && x.TryGetInt32(out int xx) && xx>0 &&
                   row.TryGetProperty("y",out var y) && y.TryGetInt32(out int yy) && yy>0 &&
                   row.TryGetProperty("serverId",out var server) && server.GetInt32()==remoteServer)
                { selected=row.Clone();break; }
            }
            if(selected is null) throw new InvalidDataException("No positive remote stored row supports coordinate Jump.");
            int targetX=selected.Value.GetProperty("x").GetInt32();
            int targetY=selected.Value.GetProperty("y").GetInt32();
            await ObserveLiveServer(homeServer,"home heartbeat before remote stored-row Jump");
            var priorNavigationIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(string file in Directory.EnumerateFiles(productionPaths.Root,"map-navigation-result.json",SearchOption.AllDirectories))
            {
                try
                {
                    using var prior=JsonDocument.Parse(await File.ReadAllTextAsync(file,token));
                    if(prior.RootElement.TryGetProperty("requestId",out var id) && id.ValueKind==JsonValueKind.String &&
                        id.GetString() is {Length:>0} previousId) priorNavigationIds.Add(previousId);
                }
                catch(IOException){} catch(JsonException){}
            }
            string coordinate=JsonSerializer.Serialize($"{targetX},{targetY}");
            if(!await Dom($"(()=>{{const target={coordinate};const buttons=[...document.querySelectorAll('.map-table--resource .map-coordinate-button:not(:disabled)')];const button=buttons.find(b=>b.textContent.includes(target));if(!button)return false;button.click();return true;}})()"))
                throw new InvalidDataException("Real server-2213 row Jump control unavailable.");
            JsonElement navigation=default;
            bool navigated=false;
            for(int i=0;i<450;i++)
            {
                token.ThrowIfCancellationRequested();
                foreach(string file in Directory.EnumerateFiles(productionPaths.Root,"map-navigation-result.json",SearchOption.AllDirectories))
                {
                    try
                    {
                        using var document=JsonDocument.Parse(await File.ReadAllTextAsync(file,token));
                        JsonElement row=document.RootElement;
                        RequireProofOwner();
                        OverviewMapScanSession session=proofSession;
                        if(row.TryGetProperty("requestId",out var id) && id.ValueKind==JsonValueKind.String &&
                           id.GetString() is {Length:>0} requestId && !priorNavigationIds.Contains(requestId) &&
                           row.TryGetProperty("state",out var state) && state.GetString()=="proven" &&
                           row.GetProperty("gamePid").GetInt32()==session.GamePid &&
                           row.GetProperty("profileId").GetString()==session.ProfileId &&
                           row.GetProperty("sessionId").GetString()==session.SessionId &&
                           row.GetProperty("challenge").GetString()==session.Challenge &&
                           row.GetProperty("serverId").GetInt32()==remoteServer &&
                           row.GetProperty("liveCurServerId").GetInt32()==remoteServer &&
                           row.GetProperty("targetX").GetInt32()==targetX &&
                           row.GetProperty("targetY").GetInt32()==targetY)
                        { navigation=row.Clone();navigated=true;break; }
                    }
                    catch(IOException){} catch(JsonException){}
                }
                if(navigated) break;
                await Task.Delay(100,token);
            }
            if(!navigated)
                throw new InvalidDataException("Real UI remote Jump did not prove both 2213 and actual target x/y.");
            results.Add(new{stage="remote_ui_stored_row_native_jump",remoteServer,targetX,targetY,
                nativeRequestId=navigation.GetProperty("requestId").GetString(),
                nativeState=navigation.GetProperty("state").GetString(),
                liveCurServerId=navigation.GetProperty("liveCurServerId").GetInt32()});
            await Save();

            await RequireUi(()=>Dom("!!document.querySelector('.map-table--resource .map-coordinate-button:not(:disabled)')"),
                "native coordinate action settled",200);
            // Clear from Auto while physically back on 2212, with the 2213
            // stored-row selection retained, exercises the explicit saved
            // server deletion path rather than the easier live-server clear.
            await ReturnHome();
            await RequireUi(()=>Dom("document.querySelector('select[data-map-saved-server]')?.value==='2213'"),
                "remote selection after second home return");
            await ObserveLiveServer(homeServer,"home heartbeat before Auto saved-server Clear");
            await Task.Delay(2000,token);
            if(!await Dom("(()=>{const b=[...document.querySelectorAll('.map-auto-scan-options > button')].at(-1);if(!b||b.disabled)return false;b.click();return true;})()"))
                throw new InvalidDataException("Real Auto Clear Map Data control is unavailable.");
            for(int i=0;i<300;i++)
            {
                token.ThrowIfCancellationRequested();
                JsonElement deleted=await RealRows(remoteServer);
                if(deleted.GetProperty("total").GetInt32()==0) break;
                await Task.Delay(100,token);
            }
            JsonElement removed=await RealRows(remoteServer);
            JsonElement preserved=await RealRows(homeServer);
            if(removed.GetProperty("total").GetInt32()!=0 || preserved.GetProperty("total").GetInt32()!=originalCount)
                throw new InvalidDataException("Auto Clear failed server isolation: erased or retained the wrong dataset.");
            results.Add(new{stage="auto_clear_ui_correct_server",clearedServerId=remoteServer,
                clearedTotal=removed.GetProperty("total").GetInt32(),
                preservedServerId=homeServer,preservedTotal=preserved.GetProperty("total").GetInt32()});
            await using(var screenshot=File.Create(Path.Combine(directory,"map014-auto-clear-preserves-home.png")))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,screenshot);
            await Save();
        }
        finally
        {
            if (remoteRailway)
            {
                try
                {
                    MapAutoScanSnapshot current=await mapAutoScanService.GetSnapshotAsync(CancellationToken.None);
                    await mapAutoScanService.UpdateConfigAsync(
                        current.Config with {Enabled=false},CancellationToken.None);
                    if(mapAutoScanService.IsCycleBusy)
                        await mapAutoScanService.CancelAsync(CancellationToken.None);
                    MapAutoScanSnapshot stopped=await mapAutoScanService.GetSnapshotAsync(CancellationToken.None);
                    RequireProofOwner();
                    bool recoveryReturn=false;
                    if(overviewLifecycleService.GetLiveServerId()==remoteServer)
                    {
                        // The Auto cycle normally returns itself. Only try a
                        // bounded exact-owner recovery if its *own* most recent
                        // scan token remains current and fully idle. Never
                        // replace a newer manual scan or unknown target owner.
                        JsonElement currentScan=JsonSerializer.SerializeToElement(
                            map317CommandService.CreateStatus(),JsonOptions.Default);
                        if(!mapAutoScanService.IsCycleBusy && ownedRailwayRunId is {Length:>0} &&
                           ReadStatusString(currentScan,"scanRunId")==ownedRailwayRunId &&
                           ReadStatusString(currentScan,"phase")=="completed" &&
                           currentScan.TryGetProperty("isReading",out JsonElement reading) &&
                           reading.ValueKind==JsonValueKind.False)
                        {
                            await ReturnHome();
                            recoveryReturn=true;
                        }
                    }
                    int liveHome=await ObserveLiveServer(homeServer,"Railway cleanup home 2212");
                    if(stopped.Config.Enabled || stopped.Running || liveHome!=homeServer)
                        throw new InvalidDataException("Railway Auto cleanup did not confirm disabled and home 2212.");
                    results.Add(new{stage="cleanup",homeServer,restored=true,
                        autoEnabled=false,autoRunning=false,liveServerId=liveHome,
                        noAdditionalNavigation=!recoveryReturn});
                }
                catch(Exception error)
                {
                    results.Add(new{stage="cleanup",homeServer,restored=false,
                        noAdditionalNavigation=true,error=error.Message});
                    throw;
                }
                finally { await Save(); }
            }
            else
            {
            Exception? disableError=null;
            try
            {
                MapAutoScanSnapshot current=await mapAutoScanService.GetSnapshotAsync(CancellationToken.None);
                await mapAutoScanService.UpdateConfigAsync(current.Config with {Enabled=false},CancellationToken.None);
            }
            catch(Exception error)
            {
                disableError=error;
                results.Add(new{stage="auto_disable_failed",error=error.Message});
            }
            try
            {
                if(mapAutoScanService.IsCycleBusy)
                    await mapAutoScanService.CancelAsync(CancellationToken.None);
                await ReturnHome();
                MapAutoScanSnapshot stopped=await mapAutoScanService.GetSnapshotAsync(CancellationToken.None);
                if(disableError is not null || stopped.Config.Enabled || stopped.Running)
                    throw new InvalidDataException("Diagnostic Auto cleanup remained enabled or active.");
                results.Add(new{stage="cleanup",homeServer,restored=true,autoEnabled=stopped.Config.Enabled,
                    autoRunning=stopped.Running});
            }
            catch(Exception error)
            {
                results.Add(new{stage="cleanup",homeServer,restored=false,error=error.Message});
                throw;
            }
            finally { await Save(); }
            }
        }
    }
}
