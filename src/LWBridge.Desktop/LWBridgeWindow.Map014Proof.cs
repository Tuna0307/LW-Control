using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Opt-in, explicitly isolated normal-window verification only. All scans,
    // navigation and images use the normal production services and real game.
    private async Task RunMap014NativeUiExtrasAsync(CoreWebView2 core, string directory, CancellationToken token)
    {
        if (productionPaths?.ExplicitIsolation != true || map317CommandService is null || mapAutoScanService is null || overviewLifecycleService is null)
            throw new InvalidOperationException("MAP014 native UI proof requires isolated production services.");
        var receipts = new List<object>();
        async Task Save() => await File.WriteAllTextAsync(Path.Combine(directory,"map014-ui-extras.json"),
            JsonSerializer.Serialize(new { genuineGame=true, controlledProvider=false, receipts },new JsonSerializerOptions(JsonOptions.Default){WriteIndented=true}),token);
        async Task<bool> Wait(string script,int attempts=150)
        {
            for(int i=0;i<attempts;i++) { token.ThrowIfCancellationRequested(); if(await core.ExecuteScriptAsync(script)=="true") return true; await Task.Delay(100,token); }
            return false;
        }
        async Task<NormalUiResourceProofSearchObservation> Tab(int index,string kind)
        {
            long before=GetNormalUiProofSearchSequence();
            await core.ExecuteScriptAsync($"document.querySelectorAll('.map-tabs button')[{index}].click()");
            NormalUiResourceProofSearchObservation? last=null;
            for(int i=0;i<200;i++)
            {
                var seen=ReadNormalUiProofSearchAfter(before);
                if (seen is not null) last=seen;
                if(seen is not null && MapDataQueryContract.NormalizeSearch(seen.Payload).Kind==kind &&
                    await core.ExecuteScriptAsync($"!!document.querySelector('.map-table--{kind}[aria-busy=false]')")=="true") return seen;
                await Task.Delay(100,token);
            }
            // Diagnostic distinction: tab change should itself search. An
            // explicit user Search from the same genuine tab shows whether
            // transport is blocked or the tab's automatic effect was missed.
            long manualBefore=GetNormalUiProofSearchSequence();
            string clicked=await core.ExecuteScriptAsync(
                "(()=>{const b=document.querySelector('.map-searchbar > button');if(!b||b.disabled)return false;b.click();return true;})()");
            NormalUiResourceProofSearchObservation? manual=null;
            if(clicked=="true")
            {
                for(int i=0;i<350;i++)
                {
                    var response=ReadNormalUiProofSearchAfter(manualBefore);
                    if(response is not null && MapDataQueryContract.NormalizeSearch(response.Payload).Kind==kind)
                    { manual=response; break; }
                    await Task.Delay(100,token);
                }
            }
            string state=await core.ExecuteScriptAsync("({activeTab:document.querySelector('.map-tabs [aria-selected=true]')?.textContent,tables:[...document.querySelectorAll('.map-table')].map(t=>({kind:t.className,busy:t.getAttribute('aria-busy'),rows:t.querySelectorAll('tbody tr').length})),errors:[...document.querySelectorAll('.map-scan-error')].map(e=>e.textContent)})");
            receipts.Add(new { gate="native_tab_not_settled",expectedKind=kind,tabIndex=index,dom=JsonSerializer.Deserialize<JsonElement>(state),last,manualSearchClicked=clicked,manualSearch=manual });
            await Save();
            throw new TimeoutException("MAP014 real UI tab did not settle: "+kind);
        }
        async Task Screenshot(string name)
        {
            await using var output=File.Create(Path.Combine(directory,name+".png"));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
        }
        try
        {
            // Native game language remains Chinese. Only the task-owned
            // cloned WebView is set to English so Follow/Stop observation
            // uses the same verified accessibility text as the prior proof.
            await core.ExecuteScriptAsync("(()=>{const select=document.querySelector('.language-select select');Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(select,'en');select.dispatchEvent(new Event('change',{bubbles:true}));})()");
            if(!await Wait("document.querySelector('.language-select select').value==='en' && document.documentElement.lang==='en'"))
                throw new InvalidDataException("Actual clone English selection did not settle.");
            if(Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_EXPIRY_UI")=="1")
            {
                // Bounded passive native expiry, completely separate from
                // already accepted filters/icons/tracking and without a scan.
                // A persisted arrival time is not a live march witness.
                var truckExpiry=await Tab(3,"truck");
                long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var candidates=truckExpiry.Result.GetProperty("rows").EnumerateArray()
                    .Select((row,index)=>new{row,index})
                    .Where(item=>item.row.TryGetProperty("arriveTs",out var deadline) &&
                        deadline.ValueKind==JsonValueKind.Number &&
                        deadline.TryGetInt64(out long target) &&
                        // Source timestamps too close to expiry can retire
                        // Follow *before* its first visible Stop; require a
                        // meaningful setup/camera observation window.
                        target>=now+45000 && target<=now+180000 &&
                        item.row.TryGetProperty("marchUuid",out var uuid) &&
                        uuid.ValueKind==JsonValueKind.String && !string.IsNullOrWhiteSpace(uuid.GetString()))
                    .OrderBy(item=>item.row.GetProperty("arriveTs").GetInt64()).FirstOrDefault();
                if(candidates is null)
                {
                    receipts.Add(new{gate="truck_natural_expiry_availability",qualifies=false,
                        reason="No saved first-page Truck row with a source arrival 45-180 seconds ahead",
                        scannedRows=truckExpiry.Result.GetProperty("rows").GetArrayLength(),newWorldScan=false});
                    await Save();
                    throw new InvalidDataException("No source-backed near-term Truck arrival available for a genuine expiry witness.");
                }
                var picked=candidates.row;
                long arriveTs=picked.GetProperty("arriveTs").GetInt64();
                string expectedUuid=picked.GetProperty("marchUuid").GetString()!;
                int expectedServer=picked.GetProperty("serverId").GetInt32();
                receipts.Add(new{gate="truck_expiry_candidate_preflight",
                    rowIndex=candidates.index,arrivalMillisecondsAhead=arriveTs-now,
                    expectedServer,expectedUuid,source="preserved genuine Truck row; not proof of a currently moving march"});
                await Save();
                try
                {
                    string click=await core.ExecuteScriptAsync($"(()=>{{const b=document.querySelectorAll('.map-table--truck tbody tr')[{candidates.index}]?.querySelector('.map-coordinate-button');if(!b||b.disabled)return false;b.click();return true;}})()");
                    if(click!="true") throw new InvalidDataException("Source-matched Truck expiry row could not start native Follow.");
                    if(!await Wait("[...document.querySelectorAll('.map-table--truck .map-coordinate-button')].some(b=>b.querySelector('strong')?.textContent.trim()==='Stop')",250))
                    {
                        // Distinguish native position success from a UI target
                        // whose saved arrival expired before status/update.
                        string uiState=await core.ExecuteScriptAsync("({error:[...document.querySelectorAll('.map-scan-error')].map(e=>e.textContent),buttons:[...document.querySelectorAll('.map-table--truck .map-coordinate-button')].slice(0,3).map(b=>b.textContent)})");
                        receipts.Add(new{gate="truck_expiry_follow_not_visible",state="unqualified",
                            elapsedMilliseconds=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-now,
                            deadlineRemainingMilliseconds=arriveTs-DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            ui=JsonSerializer.Deserialize<JsonElement>(uiState)});
                        await Save();
                        throw new InvalidDataException("A saved Truck deadline and native Follow do not prove a visible ongoing UI tracking session.");
                    }
                    await Screenshot("map014-expiry-before");
                    var owner=overviewLifecycleService.GetReadyMapScanSession();
                    if(owner is null || owner.GamePid<=0) throw new InvalidDataException("Expiry owner was not ready.");
                    var observed=new HashSet<string>(StringComparer.Ordinal);
                    var worldPositions=new HashSet<(double X,double Z)>();
                    var cameraTiles=new HashSet<(double X,double Y)>();
                    int nativeProven=0;
                    int consecutiveReadyGaps=0;
                    int totalReadyGaps=0;
                    int visibleStopNearArrival=0;
                    long lastUiSample=0;
                    while(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()<arriveTs+5500)
                    {
                        token.ThrowIfCancellationRequested();
                        var currentOwner=overviewLifecycleService.GetReadyMapScanSession();
                        if(currentOwner is null)
                        {
                            totalReadyGaps++;
                            consecutiveReadyGaps++;
                            if(!overviewLifecycleService.MatchesOwnedMapScanSession(owner))
                                throw new InvalidDataException("Natural-expiry observer's exact owned game identity was retired.");
                            if(consecutiveReadyGaps>=12)
                            {
                                receipts.Add(new {gate="truck_natural_expiry_availability",qualifies=false,
                                    reason="Same owned process remained identified, but fresh ready-session attestation was unavailable before source arrival; no arrival attribution",
                                    exactOwnedIdentityStillMatched=true,readinessGaps=totalReadyGaps,
                                    nativeProvenRequests=nativeProven,
                                    deadlineRemainingMilliseconds=arriveTs-DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
                                await Save();
                                throw new InvalidDataException("Natural expiry is unavailable without an attested ready session through arrival.");
                            }
                            await Task.Delay(250,token);
                            continue;
                        }
                        if(currentOwner.SessionId!=owner.SessionId || currentOwner.GamePid!=owner.GamePid ||
                           currentOwner.ProfileId!=owner.ProfileId || currentOwner.Challenge!=owner.Challenge)
                            throw new InvalidDataException("Owned expiry session identity changed.");
                        consecutiveReadyGaps=0;
                        foreach(string file in Directory.EnumerateFiles(productionPaths.Root,"march-follow-result.json",SearchOption.AllDirectories))
                        {
                            try
                            {
                                using var json=JsonDocument.Parse(await File.ReadAllTextAsync(file,token));
                                var r=json.RootElement;
                                if(r.GetProperty("gamePid").GetInt32()!=owner.GamePid ||
                                   r.GetProperty("profileId").GetString()!=owner.ProfileId ||
                                   r.GetProperty("sessionId").GetString()!=owner.SessionId ||
                                   r.GetProperty("challenge").GetString()!=owner.Challenge ||
                                   r.GetProperty("serverId").GetInt32()!=expectedServer ||
                                   r.GetProperty("marchUuid").ToString()!=expectedUuid) continue;
                                if(observed.Add(r.GetProperty("requestId").GetString()!) &&
                                   r.GetProperty("state").GetString()=="proven")
                                {
                                    nativeProven++;
                                    if(r.TryGetProperty("worldX",out var wx) && wx.ValueKind==JsonValueKind.Number &&
                                       r.TryGetProperty("worldZ",out var wz) && wz.ValueKind==JsonValueKind.Number)
                                        worldPositions.Add((wx.GetDouble(),wz.GetDouble()));
                                    if(r.TryGetProperty("cameraTileX",out var cx) && cx.ValueKind==JsonValueKind.Number &&
                                       r.TryGetProperty("cameraTileY",out var cy) && cy.ValueKind==JsonValueKind.Number)
                                        cameraTiles.Add((cx.GetDouble(),cy.GetDouble()));
                                }
                            }
                            catch(IOException){} catch(JsonException){}
                        }
                        long observedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if(observedAt>=arriveTs-10000 && observedAt<arriveTs-1000 &&
                           observedAt-lastUiSample>=1000)
                        {
                            lastUiSample=observedAt;
                            if(await core.ExecuteScriptAsync(
                                "[...document.querySelectorAll('.map-table--truck .map-coordinate-button')].some(b=>b.querySelector('strong')?.textContent.trim()==='Stop')")=="true")
                                visibleStopNearArrival++;
                        }
                        await Task.Delay(250,token);
                    }
                    bool stopAuto=await Wait("[...document.querySelectorAll('.map-table--truck .map-coordinate-button')].every(b=>b.querySelector('strong')?.textContent.trim()!=='Stop')",30);
                    int requestsAtExpiry=observed.Count;
                    await Task.Delay(3600,token);
                    // Read the same exact-owned result once more. It must not
                    // advance after the source arrival + one polling period.
                    foreach(string file in Directory.EnumerateFiles(productionPaths.Root,"march-follow-result.json",SearchOption.AllDirectories))
                    {
                        try
                        {
                            using var json=JsonDocument.Parse(await File.ReadAllTextAsync(file,token));
                            var r=json.RootElement;
                            if(r.GetProperty("sessionId").GetString()==owner.SessionId &&
                               r.GetProperty("marchUuid").ToString()==expectedUuid)
                                observed.Add(r.GetProperty("requestId").GetString()!);
                        }
                        catch(IOException){} catch(JsonException){}
                    }
                    await Screenshot("map014-expiry-after");
                    bool qualifies=stopAuto && totalReadyGaps==0 &&
                        nativeProven>=2 && worldPositions.Count>=2 &&
                        cameraTiles.Count>=1 && visibleStopNearArrival>=1 &&
                        observed.Count==requestsAtExpiry;
                    receipts.Add(new{gate="truck_natural_expiry_native",qualifies,
                        nativeProvenRequests=nativeProven,exactOwner=true,serverId=expectedServer,
                        marchUuid=expectedUuid,arriveTs,stopRetiredWithoutClick=stopAuto,
                        lateFollowIssued=observed.Count!=requestsAtExpiry,
                        distinctWorldPositions=worldPositions.Count,
                        distinctNativeCameraTiles=cameraTiles.Count,
                        stopVisibleNearArrivalSamples=visibleStopNearArrival,
                        readinessGaps=totalReadyGaps,newWorldScan=false});
                    await Save();
                    if(!qualifies) throw new InvalidDataException("Natural Truck expiry lacked complete native Follow/auto-Stop/late-poll proof.");
                    return;
                }
                finally
                {
                    await core.ExecuteScriptAsync("(()=>{const b=[...document.querySelectorAll('.map-table--truck .map-coordinate-button')].find(b=>b.querySelector('strong')?.textContent.trim()==='Stop');if(b)b.click();})()");
                }
            }
            await core.ExecuteScriptAsync("document.querySelector('.server-jump > button').click()");
            if(!await Wait("!!document.querySelector('.server-jump-popover input')")) throw new InvalidDataException("Server popover did not open.");
            await core.ExecuteScriptAsync("window.__map014Popover=document.querySelector('.server-jump-popover');window.__map014ServerInput=window.__map014Popover.querySelector('input');const input=window.__map014ServerInput;Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'2213');input.dispatchEvent(new Event('input',{bubbles:true}));");
            await Task.Delay(250,token);
            var popupSamples=new List<JsonElement>();
            bool hadWarzones=false;
            for(int i=0;i<48;i++)
            {
                string sample=await core.ExecuteScriptAsync("({open:document.querySelector('.server-jump-popover')===window.__map014Popover,inputRetained:document.querySelector('.server-jump-popover input')===window.__map014ServerInput,value:window.__map014ServerInput?.value,warzones:[...document.querySelectorAll('.server-jump-history button')].map(b=>b.textContent)})");
                var value=JsonSerializer.Deserialize<JsonElement>(sample);
                if(hadWarzones && value.GetProperty("warzones").GetArrayLength()==0)
                {
                    receipts.Add(new{gate="native_warzone_options_flicker",samples=popupSamples,sample=value}); await Save();
                    throw new InvalidDataException("Live polling erased the already populated warzone choices.");
                }
                hadWarzones |= value.GetProperty("warzones").GetArrayLength()>0;
                if(!value.GetProperty("open").GetBoolean()||!value.GetProperty("inputRetained").GetBoolean()||value.GetProperty("value").GetString()!="2213")
                {
                    receipts.Add(new{gate="native_warzone_popover_failure",sample=value}); await Save();
                    throw new InvalidDataException("Live header polling destroyed the open server selection.");
                }
                popupSamples.Add(value); await Task.Delay(250,token);
            }
            receipts.Add(new{gate="native_warzone_popover_stable",positiveWarzones=hadWarzones,samples=popupSamples}); await Save();
            await core.ExecuteScriptAsync("document.querySelector('.server-jump > button').click()");

            var resource=await Tab(1,"resource");
            // Derive a positive combined filter from a retained genuine row:
            // fixed guesses (5/full/normal) were observed to return no rows.
            JsonElement? resourceWitness=null;
            foreach(var row in resource.Result.GetProperty("rows").EnumerateArray())
            {
                if(row.TryGetProperty("level",out var level) && level.TryGetInt32(out int sourceLevel) && sourceLevel>0 &&
                   row.TryGetProperty("resourceDetailKnown",out var known) && known.ValueKind==JsonValueKind.True &&
                   row.TryGetProperty("resourceRemainingAmount",out var remain) && remain.TryGetInt64(out long remaining) &&
                   row.TryGetProperty("resourceFullAmount",out var full) && full.TryGetInt64(out long capacity) &&
                   capacity>0 && remaining>=0 && remaining<=capacity &&
                   row.TryGetProperty("blackTileKnown",out var tileKnown) && tileKnown.ValueKind==JsonValueKind.True &&
                   row.TryGetProperty("isBlackTile",out var black) &&
                   black.ValueKind is JsonValueKind.True or JsonValueKind.False)
                { resourceWitness=row.Clone(); break; }
            }
            if(resourceWitness is null) throw new InvalidDataException("No genuine Resource row with positive native level, detail and tile witnesses.");
            var witness=resourceWitness.Value;
            int witnessLevel=witness.GetProperty("level").GetInt32();
            long witnessRemaining=witness.GetProperty("resourceRemainingAmount").GetInt64();
            long witnessCapacity=witness.GetProperty("resourceFullAmount").GetInt64();
            string witnessAmount=witnessRemaining==witnessCapacity?"full":"partial";
            string witnessTerrain=witness.GetProperty("isBlackTile").GetBoolean()?"black":"normal";
            long resourceBefore=GetNormalUiProofSearchSequence();
            string inputDispatch = await core.ExecuteScriptAsync("(()=>{const inputs=[...document.querySelectorAll('.map-searchbar input[type=number]')];if(inputs.length!==2)return {inputCount:inputs.length};for(const input of inputs){Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'WITNESS_LEVEL');input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));}for(const selector of ['WITNESS_AMOUNT','WITNESS_TERRAIN']){const select=[...document.querySelectorAll('.map-searchbar select')].find(s=>[...s.options].some(o=>o.value===selector));if(!select)return {missingSelect:selector};Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(select,selector);select.dispatchEvent(new Event('change',{bubbles:true}));}return {dispatched:true};})()"
                .Replace("WITNESS_LEVEL",witnessLevel.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Replace("WITNESS_AMOUNT",witnessAmount).Replace("WITNESS_TERRAIN",witnessTerrain));
            await Task.Delay(200,token);
            await core.ExecuteScriptAsync("document.querySelector('.map-searchbar > button').click()");
            NormalUiResourceProofSearchObservation? resourceFiltered=null;
            for(int i=0;i<150;i++)
            {
                var seen=ReadNormalUiProofSearchAfter(resourceBefore);
                if(seen is not null)
                {
                    var q=seen.Payload.GetProperty("query");
                    if(q.TryGetProperty("minLevel",out var low)&&low.GetInt32()==witnessLevel&&q.TryGetProperty("maxLevel",out var high)&&high.GetInt32()==witnessLevel&&
                        q.TryGetProperty("resourceAmountState",out var amount)&&amount.GetString()==witnessAmount&&q.TryGetProperty("resourceTileState",out var tile)&&tile.GetString()==witnessTerrain) {resourceFiltered=seen;break;}
                }
                await Task.Delay(100,token);
            }
            if(resourceFiltered is null)
            {
                string dom=await core.ExecuteScriptAsync("[...document.querySelectorAll('.map-searchbar input,.map-searchbar select')].map(e=>({type:e.type,label:e.getAttribute('aria-label'),value:e.value}))");
                var seen=ReadNormalUiProofSearchAfter(resourceBefore);
                receipts.Add(new{gate="resource_filter_diagnostic",inputDispatch,dom,lastSearch=seen}); await Save();
                throw new InvalidDataException("Actual Resource UI failed to send combined level/amount/terrain filters.");
            }
            foreach(var row in resourceFiltered.Result.GetProperty("rows").EnumerateArray())
                if(row.GetProperty("level").GetInt32()!=witnessLevel||!row.GetProperty("blackTileKnown").GetBoolean()||
                    row.GetProperty("isBlackTile").GetBoolean()!=(witnessTerrain=="black")||
                    !row.GetProperty("resourceDetailKnown").GetBoolean()||
                    (row.GetProperty("resourceRemainingAmount").GetInt64()==row.GetProperty("resourceFullAmount").GetInt64())!=(witnessAmount=="full"))
                    throw new InvalidDataException("Actual Resource query admitted a nonmatching or unknown row.");
            if(resourceFiltered.Result.GetProperty("total").GetInt32()==0) throw new InvalidDataException("Resource positive combined filter returned no rows.");
            receipts.Add(new{gate="native_resource_combined_filters",witnessLevel,witnessAmount,witnessTerrain,resourceFiltered.Payload,resourceFiltered.Result}); await Screenshot("map014-resource-filtered");await Save();

            // Resolve owner-name display from the real saved Dispatch population
            // before loading many Truck reward images in the same WebView.
            if(Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_TRACKING_UI")!="1")
            {
                var dispatch=await Tab(5,"dispatch");
                int knownNames=dispatch.Result.GetProperty("rows").EnumerateArray().Count(r=>r.TryGetProperty("ownerName",out var name)&&name.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(name.GetString()));
                if(dispatch.Result.GetProperty("total").GetInt32()==0 || knownNames==0)
                    throw new InvalidDataException("Saved genuine Secret Tasks lacked a positive owner-name witness.");
                receipts.Add(new{gate="native_dispatch_owner_names",knownNames,dispatch.Payload,dispatch.Result});
                await Screenshot("map014-dispatch-owner-names"); await Save();
            }

            var truck=await Tab(3,"truck");
            if(!await Wait("[...document.querySelectorAll('img.map-reward-icon')].some(i=>i.complete&&i.naturalWidth>0)",300))
                throw new InvalidDataException("Genuine Truck rewards did not load an actual native PNG in the WebView.");
            string icons=await core.ExecuteScriptAsync("[...document.querySelectorAll('img.map-reward-icon')].filter(i=>i.complete&&i.naturalWidth>0).map(i=>({name:i.alt,width:i.naturalWidth,height:i.naturalHeight,source:i.src.slice(0,22)}))");
            receipts.Add(new{gate="native_truck_icons_rendered",icons=JsonSerializer.Deserialize<JsonElement>(icons),truck.Payload,truck.Result});
            await Screenshot("map014-truck-native-icons"); await Save();
            await core.ExecuteScriptAsync("document.querySelector('.map-item-filter')?.setAttribute('open','')");
            if(!await Wait("[...document.querySelectorAll('.map-item-filter-menu img.map-item-filter-icon')].some(i=>i.complete&&i.naturalWidth>0)",300))
                throw new InvalidDataException("Genuine reward filters did not load their native item images.");
            receipts.Add(new{gate="native_filter_menu_icons_rendered",
                images=JsonSerializer.Deserialize<JsonElement>(await core.ExecuteScriptAsync(
                    "[...document.querySelectorAll('.map-item-filter-menu img.map-item-filter-icon')].filter(i=>i.complete&&i.naturalWidth>0).map(i=>({name:i.alt,width:i.naturalWidth,source:i.src.slice(0,22)}))"))});
            await core.ExecuteScriptAsync("document.querySelector('.map-item-filter')?.removeAttribute('open')");
            await Save();

            // The UI's own Follow button and its own Stop toggle, never plunder.
            if(!await Wait("!!document.querySelector('.map-table--truck .map-coordinate-button:not(:disabled)')"))
                throw new InvalidDataException("No genuine Truck Follow control available.");
            await core.ExecuteScriptAsync("document.querySelector('.map-table--truck .map-coordinate-button:not(:disabled)').click()");
            bool follow=false; JsonElement followResult=default;
            for(int i=0;i<200;i++)
            {
                var owned=overviewLifecycleService.GetReadyMapScanSession();
                if(owned is null) throw new InvalidDataException("Native owner retired during Follow proof.");
                foreach(string file in Directory.EnumerateFiles(productionPaths.Root,"march-follow-result.json",SearchOption.AllDirectories))
                {
                    try
                    {
                        using var result=JsonDocument.Parse(await File.ReadAllTextAsync(file,token)); var r=result.RootElement;
                        if(r.TryGetProperty("state",out var state)&&state.GetString()=="proven" &&
                            r.GetProperty("gamePid").GetInt32()==owned.GamePid && r.GetProperty("profileId").GetString()==owned.ProfileId &&
                            r.GetProperty("sessionId").GetString()==owned.SessionId && r.GetProperty("challenge").GetString()==owned.Challenge)
                        { follow=true; followResult=r.Clone(); break; }
                    }
                    catch(IOException){} catch(JsonException){}
                }
                if(follow) break; await Task.Delay(100,token);
            }
            if(!follow) throw new InvalidDataException("UI Follow lacked an exact-owned native march result.");
            if(!await Wait("[...document.querySelectorAll('.map-table--truck .map-coordinate-button:not(:disabled)')].some(b=>b.querySelector('strong')?.textContent.trim()==='Stop')",300))
                throw new InvalidDataException("Native Follow returned, but the actual UI never exposed its Stop toggle.");
            bool trackingQualified=true;
            if(Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_TRACKING_UI")=="1")
            {
                // Distinguish source-backed repeated Follow from a first
                // success. Poll only this task's session-bound native result,
                // never inferred positions or the stored scan coordinates.
                string followedUuid=followResult.GetProperty("marchUuid").ToString();
                int followedServer=followResult.GetProperty("serverId").GetInt32();
                var observed=new List<JsonElement>();
                var requestIds=new HashSet<string>(StringComparer.Ordinal);
                var trackedUntil=DateTimeOffset.UtcNow.AddSeconds(24);
                while(DateTimeOffset.UtcNow<trackedUntil)
                {
                    token.ThrowIfCancellationRequested();
                    var owner=overviewLifecycleService.GetReadyMapScanSession();
                    if(owner is null) throw new InvalidDataException("Native tracking owner retired.");
                    foreach(string file in Directory.EnumerateFiles(productionPaths.Root,"march-follow-result.json",SearchOption.AllDirectories))
                    {
                        try
                        {
                            using var json=JsonDocument.Parse(await File.ReadAllTextAsync(file,token));
                            var row=json.RootElement;
                            if(!row.TryGetProperty("requestId",out var request) || request.ValueKind!=JsonValueKind.String ||
                               !row.TryGetProperty("marchUuid",out var uuid) || uuid.ToString()!=followedUuid ||
                               !row.TryGetProperty("serverId",out var server) || server.GetInt32()!=followedServer ||
                               row.GetProperty("gamePid").GetInt32()!=owner.GamePid ||
                               row.GetProperty("profileId").GetString()!=owner.ProfileId ||
                               row.GetProperty("sessionId").GetString()!=owner.SessionId ||
                               row.GetProperty("challenge").GetString()!=owner.Challenge) continue;
                            if(requestIds.Add(request.GetString()!)) observed.Add(row.Clone());
                        }
                        catch(IOException){} catch(JsonException){}
                    }
                    if(observed.Count>=5) break;
                    await Task.Delay(200,token);
                }
                var fresh=observed.Where(row=>row.TryGetProperty("state",out var state) && state.GetString()=="proven" &&
                    row.TryGetProperty("marchSource",out var source) && source.GetString()=="WorldMarchDataManager.GetMarch/GetMarchCurPos" &&
                    row.TryGetProperty("worldX",out var x) && x.ValueKind==JsonValueKind.Number &&
                    row.TryGetProperty("worldZ",out var z) && z.ValueKind==JsonValueKind.Number).ToArray();
                var places=fresh.Select(row=>(row.GetProperty("worldX").GetDouble(),row.GetProperty("worldZ").GetDouble())).Distinct().ToArray();
                var camera=fresh.Where(row=>row.TryGetProperty("cameraTileX",out var x) && x.ValueKind==JsonValueKind.Number &&
                    row.TryGetProperty("cameraTileY",out var y) && y.ValueKind==JsonValueKind.Number)
                    .Select(row=>(row.GetProperty("cameraTileX").GetDouble(),row.GetProperty("cameraTileY").GetDouble())).Distinct().ToArray();
                receipts.Add(new {gate="native_same_uuid_sustained_follow",
                    followedServer,allSameUuid=true,observations=observed.Count,positionQualified=fresh.Length,
                    distinctWorldPositions=places.Length,distinctCameraTiles=camera.Length,
                    results=observed.Select(row=>new{
                        state=row.TryGetProperty("state",out var state)?state.GetString():null,
                        requestId=row.GetProperty("requestId").GetString(),
                        marchSource=row.TryGetProperty("marchSource",out var source)?source.GetString():null,
                        worldX=row.TryGetProperty("worldX",out var x)?x.GetRawText():null,
                        worldZ=row.TryGetProperty("worldZ",out var z)?z.GetRawText():null,
                        cameraTileX=row.TryGetProperty("cameraTileX",out var tx)?tx.GetRawText():null,
                        cameraTileY=row.TryGetProperty("cameraTileY",out var ty)?ty.GetRawText():null,
                        error=row.TryGetProperty("error",out var err)?err.GetString():null
                    })});
                await Save();
                trackingQualified=fresh.Length>=3 && places.Length>=2 && camera.Length>=2;
            }
            if(!await Wait("(()=>{const b=[...document.querySelectorAll('.map-table--truck .map-coordinate-button:not(:disabled)')].find(b=>b.querySelector('strong')?.textContent.trim()==='Stop');if(!b)return false;b.click();return true;})()",1))
                throw new InvalidDataException("Native UI Stop toggle could not be clicked.");
            if(!await Wait("[...document.querySelectorAll('.map-table--truck .map-coordinate-button')].every(b=>b.querySelector('strong')?.textContent.trim()!=='Stop')",100))
                throw new InvalidDataException("Native UI Stop toggle did not retire Follow.");
            receipts.Add(new{gate="truck_ui_follow_stop",serverId=followResult.GetProperty("serverId").GetInt32(),marchUuid=followResult.GetProperty("marchUuid").ToString(),state="proven",stopToggleObserved=true}); await Save();
            if(!trackingQualified)
                throw new InvalidDataException("Moving same-UUID position/camera witness was unavailable; native UI Stop succeeded, see tracking receipt.");

            // Two items use ALL semantics; a plain decimal cap means millions.
            JsonElement? truckWitness=null;
            string[]? requiredKeys=null;
            foreach(var row in truck.Result.GetProperty("rows").EnumerateArray())
            {
                if(!row.TryGetProperty("power",out var nativePower) || !nativePower.TryGetInt64(out long power) ||
                   power>25000000 || power<0 || !row.TryGetProperty("currentGoods",out var goods) || goods.ValueKind!=JsonValueKind.Array)
                    continue;
                var keys=goods.EnumerateArray()
                    .Where(item=>item.TryGetProperty("key",out var key)&&key.ValueKind==JsonValueKind.String)
                    .Select(item=>item.GetProperty("key").GetString()!)
                    .Where(key=>!string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).Take(2).ToArray();
                if(keys.Length!=2) continue;
                truckWitness=row.Clone();requiredKeys=keys;break;
            }
            if(truckWitness is null || requiredKeys is null)
                throw new InvalidDataException("No genuine <=25-million Truck with two distinct reward keys for positive ALL-filter proof.");
            long filterBefore=GetNormalUiProofSearchSequence();
            string selectedKeyJson=JsonSerializer.Serialize(requiredKeys);
            if(await core.ExecuteScriptAsync($"(()=>{{const keys={selectedKeyJson};const buttons=[...document.querySelectorAll('.map-item-filter-menu button')];for(const key of keys){{const button=buttons.find(b=>b.dataset.mapRewardKey===key);if(!button)return false;button.click();}}const input=document.querySelector('.map-searchbar input[inputmode=decimal]');if(!input)return false;Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'25');input.dispatchEvent(new Event('input',{{bubbles:true}}));input.dispatchEvent(new Event('change',{{bubbles:true}}));return true;}})()")!="true")
                throw new InvalidDataException("Truck multi-item/power controls missing.");
            await Task.Delay(200,token);
            await core.ExecuteScriptAsync("document.querySelector('.map-searchbar > button').click()");
            NormalUiResourceProofSearchObservation? filtered=null;
            for(int i=0;i<150;i++)
            {
                var seen=ReadNormalUiProofSearchAfter(filterBefore);
                if(seen is not null && seen.Payload.GetProperty("query").TryGetProperty("escortPowerMaxMillions",out var power)&&power.GetDouble()==25 &&
                    seen.Payload.GetProperty("query").TryGetProperty("itemKeys",out var keys)&&keys.ValueKind==JsonValueKind.Array&&
                    keys.EnumerateArray().Select(key=>key.GetString()).OrderBy(key=>key).SequenceEqual(requiredKeys.OrderBy(key=>key))) {filtered=seen;break;}
                await Task.Delay(100,token);
            }
            if(filtered is null) throw new InvalidDataException("Truck UI did not send combined filters to production.");
            foreach(var row in filtered.Result.GetProperty("rows").EnumerateArray())
            {
                if(row.TryGetProperty("power",out var power)&&power.ValueKind==JsonValueKind.Number &&power.GetDouble()>25000000)
                    throw new InvalidDataException("Truck power filter admitted over-cap result.");
                if(!row.TryGetProperty("currentGoods",out var goods) || goods.ValueKind!=JsonValueKind.Array ||
                   !requiredKeys.All(key=>goods.EnumerateArray().Any(item=>item.TryGetProperty("key",out var itemKey)&&itemKey.GetString()==key)))
                    throw new InvalidDataException("Truck reward filter returned a row without ALL selected rewards.");
            }
            if(filtered.Result.GetProperty("total").GetInt32()==0 ||
               !filtered.Result.GetProperty("rows").EnumerateArray().Any(row=>row.GetProperty("uuid").ToString()==truckWitness.Value.GetProperty("uuid").ToString()))
                throw new InvalidDataException("Genuine qualifying Truck row was omitted from positive combined filters.");
            receipts.Add(new{gate="genuine_truck_combined_filters",witnessKeys=requiredKeys,filtered.Payload,filtered.Result}); await Screenshot("map014-truck-filtered"); await Save();

        }
        finally { await Save(); }
    }
}
