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
            await core.ExecuteScriptAsync("(()=>{const select=document.querySelector('.language-select select');Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(select,'en');select.dispatchEvent(new Event('change',{bubbles:true}));})()");
            if(!await Wait("document.querySelector('.language-select select').value==='en' && document.documentElement.lang==='en'"))
                throw new InvalidDataException("Actual clone English selection did not settle.");
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
            var dispatch=await Tab(5,"dispatch");
            int knownNames=dispatch.Result.GetProperty("rows").EnumerateArray().Count(r=>r.TryGetProperty("ownerName",out var name)&&name.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(name.GetString()));
            if(dispatch.Result.GetProperty("total").GetInt32()==0 || knownNames==0)
                throw new InvalidDataException("Saved genuine Secret Tasks lacked a positive owner-name witness.");
            receipts.Add(new{gate="native_dispatch_owner_names",knownNames,dispatch.Payload,dispatch.Result});
            await Screenshot("map014-dispatch-owner-names"); await Save();

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
            if(!await Wait("(()=>{const b=[...document.querySelectorAll('.map-table--truck .map-coordinate-button:not(:disabled)')].find(b=>b.querySelector('strong')?.textContent.trim()==='Stop');if(!b)return false;b.click();return true;})()",1))
                throw new InvalidDataException("Native UI Stop toggle could not be clicked.");
            if(!await Wait("[...document.querySelectorAll('.map-table--truck .map-coordinate-button')].every(b=>b.querySelector('strong')?.textContent.trim()!=='Stop')",100))
                throw new InvalidDataException("Native UI Stop toggle did not retire Follow.");
            receipts.Add(new{gate="truck_ui_follow_stop",serverId=followResult.GetProperty("serverId").GetInt32(),marchUuid=followResult.GetProperty("marchUuid").ToString(),state="proven",stopToggleObserved=true}); await Save();

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
