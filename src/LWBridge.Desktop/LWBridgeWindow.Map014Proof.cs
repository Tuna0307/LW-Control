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
            for(int i=0;i<200;i++)
            {
                var seen=ReadNormalUiProofSearchAfter(before);
                if(seen is not null && MapDataQueryContract.NormalizeSearch(seen.Payload).Kind==kind &&
                    await core.ExecuteScriptAsync($"!!document.querySelector('.map-table--{kind}[aria-busy=false]')")=="true") return seen;
                await Task.Delay(100,token);
            }
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
            long resourceBefore=GetNormalUiProofSearchSequence();
            string inputDispatch = await core.ExecuteScriptAsync("(()=>{const inputs=[...document.querySelectorAll('.map-searchbar input[type=number]')];if(inputs.length!==2)return {inputCount:inputs.length};for(const input of inputs){Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'5');input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));}for(const [value,selector] of [['full','full'],['normal','normal']]){const select=[...document.querySelectorAll('.map-searchbar select')].find(s=>[...s.options].some(o=>o.value===selector));if(!select)return {missingSelect:selector};Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(select,value);select.dispatchEvent(new Event('change',{bubbles:true}));}return {dispatched:true};})()");
            await Task.Delay(200,token);
            await core.ExecuteScriptAsync("document.querySelector('.map-searchbar > button').click()");
            NormalUiResourceProofSearchObservation? resourceFiltered=null;
            for(int i=0;i<150;i++)
            {
                var seen=ReadNormalUiProofSearchAfter(resourceBefore);
                if(seen is not null)
                {
                    var q=seen.Payload.GetProperty("query");
                    if(q.TryGetProperty("minLevel",out var low)&&low.GetInt32()==5&&q.TryGetProperty("maxLevel",out var high)&&high.GetInt32()==5&&
                        q.TryGetProperty("resourceAmountState",out var amount)&&amount.GetString()=="full"&&q.TryGetProperty("resourceTileState",out var tile)&&tile.GetString()=="normal") {resourceFiltered=seen;break;}
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
                if(row.GetProperty("level").GetInt32()!=5||!row.GetProperty("blackTileKnown").GetBoolean()||row.GetProperty("isBlackTile").GetBoolean()||
                    !row.GetProperty("resourceDetailKnown").GetBoolean()||row.GetProperty("resourceRemainingAmount").GetInt64()!=row.GetProperty("resourceFullAmount").GetInt64())
                    throw new InvalidDataException("Actual Resource query admitted a nonmatching or unknown row.");
            if(resourceFiltered.Result.GetProperty("total").GetInt32()==0) throw new InvalidDataException("Resource positive combined filter returned no rows.");
            receipts.Add(new{gate="native_resource_combined_filters",resourceFiltered.Payload,resourceFiltered.Result}); await Screenshot("map014-resource-filtered");await Save();

            var truck=await Tab(3,"truck");
            if(!await Wait("[...document.querySelectorAll('img.map-reward-icon')].some(i=>i.complete&&i.naturalWidth>0)",300))
                throw new InvalidDataException("Genuine Truck rewards did not load an actual native PNG in the WebView.");
            string icons=await core.ExecuteScriptAsync("[...document.querySelectorAll('img.map-reward-icon')].filter(i=>i.complete&&i.naturalWidth>0).map(i=>({name:i.alt,width:i.naturalWidth,height:i.naturalHeight,source:i.src.slice(0,22)}))");
            receipts.Add(new{gate="native_truck_icons_rendered",icons=JsonSerializer.Deserialize<JsonElement>(icons),truck.Payload,truck.Result});
            await Screenshot("map014-truck-native-icons"); await Save();

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
            await core.ExecuteScriptAsync("document.querySelector('.map-table--truck .map-coordinate-button:not(:disabled)').click()");
            receipts.Add(new{gate="truck_ui_follow_stop",serverId=followResult.GetProperty("serverId").GetInt32(),marchUuid=followResult.GetProperty("marchUuid").ToString(),state="proven"}); await Save();

            // Two items use ALL semantics; a plain decimal cap means millions.
            long filterBefore=GetNormalUiProofSearchSequence();
            if(await core.ExecuteScriptAsync("(()=>{const buttons=[...document.querySelectorAll('.map-item-filter-menu button')];if(buttons.length<3)return false;buttons[1].click();buttons[2].click();const input=document.querySelector('.map-searchbar input[inputmode=decimal]');if(!input)return false;Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'25');input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));return true;})()")!="true")
                throw new InvalidDataException("Truck multi-item/power controls missing.");
            await Task.Delay(200,token);
            await core.ExecuteScriptAsync("document.querySelector('.map-searchbar > button').click()");
            NormalUiResourceProofSearchObservation? filtered=null;
            for(int i=0;i<150;i++)
            {
                var seen=ReadNormalUiProofSearchAfter(filterBefore);
                if(seen is not null && seen.Payload.GetProperty("query").TryGetProperty("escortPowerMaxMillions",out var power)&&power.GetDouble()==25 &&
                    seen.Payload.GetProperty("query").TryGetProperty("itemKeys",out var keys)&&keys.GetArrayLength()==2) {filtered=seen;break;}
                await Task.Delay(100,token);
            }
            if(filtered is null) throw new InvalidDataException("Truck UI did not send combined filters to production.");
            foreach(var row in filtered.Result.GetProperty("rows").EnumerateArray())
                if(row.TryGetProperty("power",out var power)&&power.ValueKind==JsonValueKind.Number &&power.GetDouble()>25000000)
                    throw new InvalidDataException("Truck power filter admitted over-cap result.");
            receipts.Add(new{gate="genuine_truck_combined_filters",filtered.Payload,filtered.Result}); await Screenshot("map014-truck-filtered"); await Save();

            var dispatch=await Tab(5,"dispatch");
            int knownNames=dispatch.Result.GetProperty("rows").EnumerateArray().Count(r=>r.TryGetProperty("ownerName",out var name)&&name.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(name.GetString()));
            receipts.Add(new{gate="native_dispatch_owner_names",knownNames,dispatch.Payload,dispatch.Result});
            await Screenshot("map014-dispatch-owner-names"); await Save();
        }
        finally { await Save(); }
    }
}
