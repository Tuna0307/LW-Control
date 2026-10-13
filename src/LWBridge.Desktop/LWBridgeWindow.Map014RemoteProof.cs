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
        const int remoteServer = 2213;
        bool reuseGenuineRemote = Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_REMOTE_REUSE") == "1";
        if (homeServer != 2212)
            throw new InvalidDataException("Remote proof requires authenticated game start on 2212; observed "+homeServer);
        var results = new List<object>();
        string receipt = Path.Combine(directory, "map014-remote-ui.json");
        async Task Save() => await File.WriteAllTextAsync(receipt,
            JsonSerializer.Serialize(new {genuineGame=true, controlledProvider=false,homeServer,remoteServer,results},
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
        async Task<JsonElement> RealRows(int server)
        {
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
            if(!await Dom("(()=>{const b=document.querySelectorAll('.map-auto-scan-card .map-types input[type=checkbox]')[1];if(!b)return false;if(!b.checked)b.click();return true;})()"))
                throw new InvalidDataException("Auto Resource checkbox missing.");
            await RequireUi(()=>Dom("document.querySelectorAll('.map-auto-scan-card .map-types input[type=checkbox]')[1]?.checked===true"),"Auto Resource selection");
            for(int index=0;index<8;index++)
            {
                if(index==1) continue;
                string predicate=$"(()=>{{const box=document.querySelectorAll('.map-auto-scan-card .map-types input[type=checkbox]')[{index}];if(!box)return false;if(box.checked)box.click();return true;}})()";
                if(!await Dom(predicate)) throw new InvalidDataException("Auto type checkbox is unavailable: "+index);
                string settled=$"document.querySelectorAll('.map-auto-scan-card .map-types input[type=checkbox]')[{index}]?.checked===false";
                await RequireUi(()=>Dom(settled),"Auto deselect "+index);
            }
            await RequireUi(()=>Dom("(()=>{const boxes=[...document.querySelectorAll('.map-auto-scan-card .map-types input[type=checkbox]')];return boxes.length===8&&boxes.every((box,i)=>box.checked===(i===1));})()"),"resource-only Auto types");
            if(!await Dom("(()=>{const b=document.querySelector('.map-auto-scan-options label input[type=checkbox]');if(!b)return false;if(!b.checked)b.click();return true;})()"))
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
            if(overviewLifecycleService.GetLiveServerId()!=homeServer)
                throw new InvalidDataException("Auto did not return to original server 2212.");
            JsonElement remoteRows=await RealRows(remoteServer);
            int remoteCount=remoteRows.GetProperty("total").GetInt32();
            if(remoteCount<1) throw new InvalidDataException("Actual Auto scan did not publish a positive server-2213 Resource dataset.");
            results.Add(new{stage="remote_published_returned_home",observedRunning,observedRemote,
                liveServerId=overviewLifecycleService.GetLiveServerId(),remoteCount,cycle.LastCycle});
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
                    {Kind:"resource",ServerId:remoteServer} &&
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
            if(overviewLifecycleService.GetLiveServerId()!=homeServer)
                throw new InvalidDataException("Remote row Jump did not begin from home server.");
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
                        OverviewMapScanSession? session=overviewLifecycleService.GetReadyMapScanSession();
                        if(session is not null && row.TryGetProperty("state",out var state) && state.GetString()=="proven" &&
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
            if(overviewLifecycleService.GetLiveServerId()!=homeServer)
                throw new InvalidDataException("Could not return home before Auto saved-server Clear.");
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
            try
            {
                if(mapAutoScanService.IsCycleBusy)
                    await mapAutoScanService.CancelAsync(CancellationToken.None);
                await ReturnHome();
                results.Add(new{stage="cleanup",homeServer,restored=true});
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
