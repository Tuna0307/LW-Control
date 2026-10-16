using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Run only against an isolated extracted Windows package:
    //   LWBRIDGE_MAP016_NO_GAME_UI_PROOF=1
    //   LWBridge.Desktop.exe --view map-data --isolated-root <fresh-root>
    //       --normal-ui-live-map-proof <fresh-root>\map016-no-game-ui.json
    // Preload <fresh-root>/config.json with autoLaunchGame=false,
    // gameDesiredRunning=false, autoReconnect=false and no gameRoot.
    // Preload a COPY of a genuinely scanned Map317 SQLite database into the
    // profile map-data directory. No installed Last War files are opened.
    private bool Map016NoGameUiProofEnabled =>
        normalUiLiveMapProofPath is not null &&
        productionPaths?.ExplicitIsolation == true &&
        Environment.GetEnvironmentVariable("LWBRIDGE_MAP016_NO_GAME_UI_PROOF") == "1";

    private int map016NoGameReconcileCalls;
    private int map016NoGameReconcileCompletions;
    private int map016NoGameBlockedRequests;
    private readonly List<string> map016NoGameBlockedCommands = [];
    private readonly object map016NoGameRequestGate = new();

    // This is deliberately a read-only allowlist at the native dispatch
    // boundary. Even if React accidentally requests Start, Run Now, Jump,
    // profile settings changes, or an old retained action, the test cannot
    // execute it. A real false autoLaunchAll reconcile still passes through
    // the production OrderedProfileReconcileCommandService and lifecycle.
    internal static bool Map016NoGameUiCommandAllowed(string command, JsonElement payload)
    {
        if (command == "profile_instances_reconcile")
            return payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("autoLaunchAll", out JsonElement autoLaunch) &&
                autoLaunch.ValueKind == JsonValueKind.False;
        // The normal Map startup migrates recent server history into a local
        // SQLite setting. This performs no server jump or game operation; keep
        // the real startup path so the observed UI is representative.
        if (command == "server_jump_history_import")
            return payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("history", out JsonElement history) &&
                history.ValueKind == JsonValueKind.Array &&
                history.GetArrayLength() <= 5 &&
                payload.TryGetProperty("profileId", out JsonElement profile) &&
                profile.ValueKind == JsonValueKind.String &&
                profile.GetString() == "map014-native-ui" &&
                history.EnumerateArray().All(item =>
                    item.ValueKind == JsonValueKind.Number &&
                    item.TryGetInt32(out int value) && value >= 1 && value <= 99999);
        return command is
            "profile_list" or "profile_status" or "profiles_get" or
            "profile_instance_status" or "profile_instances_status" or
            "get_status" or "get_proxy_status" or "proxy_status" or
            "update_status" or "lastwar_map_localize" or "get_automation" or
            "game_root_status" or "game_recovery_status" or
            "local_config_get" or "local_map_auto_scan_status" or
            "map_scan_status" or "map_summary" or "map_data_options" or
            "map_saved_servers" or "map_search" or
            "server_jump_history_get" or "map_treasure_claim_status" or
            "map_train_list_coverage" or "game_asset_image" or
            "game_text_lookup" or "game_text_config" or "game_text_get" or
            "game_text_bundle" or "profile_runtime_status";
    }

    private bool Map016NoGameUiProofAllowCommand(string command, JsonElement payload)
    {
        bool allowed = Map016NoGameUiCommandAllowed(command, payload);
        lock (map016NoGameRequestGate)
        {
            if (command == "profile_instances_reconcile")
                map016NoGameReconcileCalls++;
            if (!allowed)
            {
                map016NoGameBlockedRequests++;
                if (map016NoGameBlockedCommands.Count < 32)
                    map016NoGameBlockedCommands.Add(command);
            }
        }
        return allowed;
    }

    private void Map016NoGameUiProofReconcileCompleted()
    {
        lock (map016NoGameRequestGate)
            map016NoGameReconcileCompletions++;
    }

    private async Task RunMap016NoGameUiProofAsync(CoreWebView2 core, string outputPath)
    {
        if (!Map016NoGameUiProofEnabled ||
            productionPaths is null || map317CommandService is null ||
            mapAutoScanService is null || overviewLifecycleService is null ||
            profileRegistryService is null || !string.Equals(initialView, "map-data", StringComparison.Ordinal))
            throw new InvalidOperationException("MAP016 no-game UI proof requires an isolated native Map page.");

        string fullOutput = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
        LWBridgeLocalConfig native = activeProfileConfig.Snapshot;
        if (native.AutoLaunchGame || native.AutoReconnect || native.GameDesiredRunning ||
            native.GameRoot is not null ||
            profileRegistryService.Snapshot.Profiles.Count != 1 ||
            overviewLifecycleService.CreateProfileInstanceStatus() is not null ||
            overviewLifecycleService.RuntimeManaged ||
            map317CommandService.IsScanActive)
            throw new InvalidDataException(
                "No-game UI proof requires one fresh disabled native profile with no game root or running owner.");
        MapAutoScanSnapshot initialAuto = await mapAutoScanService.GetSnapshotAsync();
        if (initialAuto.Config.Enabled || initialAuto.Running || mapAutoScanService.IsCycleBusy)
            throw new InvalidDataException("No-game UI proof requires a disabled, idle native Auto schedule.");

        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        CancellationToken token = budget.Token;
        async Task<JsonElement> ReadDomAsync(string script)
        {
            string raw = await core.ExecuteScriptAsync("JSON.stringify(" + script + ")");
            string json = JsonSerializer.Deserialize<string>(raw) ??
                throw new InvalidDataException("Packaged WebView returned no JSON observation.");
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        async Task<JsonElement> WaitForDomAsync(string script, Func<JsonElement, bool> accept, string stage)
        {
            for (int i = 0; i < 400; i++)
            {
                token.ThrowIfCancellationRequested();
                JsonElement dom = await ReadDomAsync(script);
                if (accept(dom)) return dom;
                await Task.Delay(100, token);
            }
            throw new TimeoutException("MAP016 no-game UI did not settle: " + stage);
        }
        static bool True(JsonElement root, string name) =>
            root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(name, out JsonElement found) &&
            found.ValueKind == JsonValueKind.True;
        static int Int(JsonElement root, string name) =>
            root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int count)
                ? count : throw new InvalidDataException("Missing packaged UI number: " + name);
        static double Double(JsonElement root, string name) =>
            root.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double count)
                ? count : throw new InvalidDataException("Missing packaged UI bound: " + name);

        const string startupScript = """
            (() => ({
              nativeAutoLaunch: window.__LWBridgeBootstrap?.autoLaunchGame,
              initialGlobalAutoLaunch: window.__LWBridgeMap016InitialAutoLaunchGlobal,
              globalAutoLaunch: localStorage.getItem('lwbridge.autoLaunchGame'),
              mapMounted: !!document.querySelector('.map-panel .map-scan-tabs'),
              savedChoices: [...document.querySelectorAll('select[data-map-saved-server] option')].map(x=>x.value),
              width: innerWidth,
              documentWidth: document.documentElement.scrollWidth
            }))()
            """;
        JsonElement startup = await WaitForDomAsync(startupScript,
            root => True(root, "mapMounted"), "native Map page");
        if (startup.GetProperty("nativeAutoLaunch").ValueKind != JsonValueKind.False ||
            startup.GetProperty("initialGlobalAutoLaunch").ValueKind != JsonValueKind.Null ||
            startup.GetProperty("globalAutoLaunch").ValueKind != JsonValueKind.Null ||
            Int(startup, "width") != 1100)
            throw new InvalidDataException(
                "Packaged native no-Auto-Launch bootstrap, absent browser key or exact 1100px viewport failed.");

        // The actual startup React reconciliation must finish with the native
        // false payload. A blocked true reconcile counts as a failure and never
        // reaches the game helper.
        for (int i = 0; i < 400; i++)
        {
            token.ThrowIfCancellationRequested();
            lock (map016NoGameRequestGate)
            {
                if (map016NoGameReconcileCompletions == 1) break;
                if (map016NoGameBlockedRequests != 0)
                    throw new InvalidDataException("Startup attempted a forbidden native lifecycle or Map command.");
            }
            await Task.Delay(100, token);
        }
        lock (map016NoGameRequestGate)
        {
            if (map016NoGameReconcileCalls != 1 || map016NoGameReconcileCompletions != 1 ||
                map016NoGameBlockedRequests != 0)
                throw new InvalidDataException(
                    $"No-game native startup reconcile calls={map016NoGameReconcileCalls}, " +
                    $"completions={map016NoGameReconcileCompletions}, " +
                    $"blocked={map016NoGameBlockedRequests}, " +
                    $"commands=[{string.Join(',', map016NoGameBlockedCommands)}].");
        }
        await Task.Delay(1_000, token);
        if (overviewLifecycleService.CreateProfileInstanceStatus() is not null ||
            overviewLifecycleService.RuntimeManaged || map317CommandService.IsScanActive)
            throw new InvalidDataException("An unrequested native game or Map scan began after startup.");

        // The stored rows are a read-only COPY of previously genuine game data,
        // never synthesized by the test. Require actual Truck rows and actual
        // retained-good options from production SQLite before inspecting UI.
        object? serversObject = await map317CommandService.InvokeAsync(
            "map_saved_servers", JsonSerializer.SerializeToElement(new { }, JsonOptions.Default), token);
        JsonElement servers = JsonSerializer.SerializeToElement(serversObject, JsonOptions.Default);
        if (!servers.TryGetProperty("servers", out JsonElement savedServers) ||
            savedServers.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Genuine saved Map server list was unavailable.");
        int rowServer = 0;
        int savedTruckRows = 0;
        foreach (JsonElement server in savedServers.EnumerateArray())
        {
            int candidate = server.GetInt32();
            object? searchObject = await map317CommandService.InvokeAsync(
                "map_search", JsonSerializer.SerializeToElement(new
                {
                    kind = "truck",
                    query = new { serverId = candidate, page = 1, pageSize = 10 },
                }, JsonOptions.Default), token);
            JsonElement result = JsonSerializer.SerializeToElement(searchObject, JsonOptions.Default);
            if (result.TryGetProperty("total", out JsonElement total) &&
                total.TryGetInt32(out int rows) && rows > 0)
            {
                rowServer = candidate;
                savedTruckRows = rows;
                break;
            }
        }
        if (rowServer <= 0)
            throw new InvalidDataException("Provide a saved genuine Map317 database with positive Truck rows.");

        await WaitForDomAsync(startupScript,
            root => root.GetProperty("savedChoices").EnumerateArray()
                .Any(option => option.GetString() == rowServer.ToString()),
            "genuine saved server listed in mounted Map");
        string selectScript = $$"""
            (() => {
              const select=document.querySelector('select[data-map-saved-server]');
              if(!select) return false;
              const setter=Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set;
              setter.call(select, '{{rowServer}}');
              select.dispatchEvent(new Event('change',{bubbles:true}));
              document.querySelectorAll('.map-tabs button')[3]?.click();
              return true;
            })()
            """;
        if (await core.ExecuteScriptAsync(selectScript) != "true")
            throw new InvalidDataException("Could not choose genuine saved Truck rows in the real Map tab.");
        const string truckDomScript = """
            (() => ({
              server:document.querySelector('select[data-map-saved-server]')?.value,
              selectedTruck:document.querySelectorAll('.map-tabs button')[3]?.getAttribute('aria-selected')==='true',
              table:!!document.querySelector('.map-table--truck[aria-busy=false]'),
              rows:document.querySelectorAll('.map-table--truck tbody tr').length,
              retainedGoods:document.querySelectorAll('.map-item-filter-menu [data-map-reward-key]').length
            }))()
            """;
        JsonElement truck = await WaitForDomAsync(truckDomScript,
            root => True(root, "selectedTruck") && True(root, "table") &&
                Int(root, "rows") > 0 && Int(root, "retainedGoods") > 0 &&
                root.GetProperty("server").GetString() == rowServer.ToString(),
            "saved Truck rows and native retained-goods menu");

        const string openMenuScript = """
            (() => {
              const details=document.querySelector('.map-searchbar details.map-item-filter');
              if(!details || !details.querySelector('.map-item-filter-menu [data-map-reward-key]')) return false;
              details.querySelector('summary').scrollIntoView({block:'center',inline:'nearest'});
              details.open=true;
              return true;
            })()
            """;
        if (await core.ExecuteScriptAsync(openMenuScript) != "true")
            throw new InvalidDataException("Saved Truck retained-goods popup could not be opened.");
        await Task.Delay(150, token);
        const string menuBoundsScript = """
            (() => {
              const details=document.querySelector('.map-searchbar details.map-item-filter');
              const menu=details?.querySelector('.map-item-filter-menu');
              if(!details?.open || !menu) return {open:false};
              const r=menu.getBoundingClientRect();
              const style=getComputedStyle(menu);
              return {open:true,width:innerWidth,height:innerHeight,
                left:r.left,top:r.top,right:r.right,bottom:r.bottom,
                menuWidth:r.width,menuHeight:r.height,
                visible:style.display!=='none'&&style.visibility!=='hidden'&&r.width>0&&r.height>0,
                documentWidth:document.documentElement.scrollWidth,
                documentHeight:document.documentElement.scrollHeight};
            })()
            """;
        JsonElement menu = await ReadDomAsync(menuBoundsScript);
        if (!True(menu, "open") || !True(menu, "visible") ||
            Int(menu, "width") != 1100 || Double(menu, "left") < -0.5 ||
            Double(menu, "top") < -0.5 ||
            Double(menu, "right") > Int(menu, "width") + 0.5 ||
            Double(menu, "bottom") > Int(menu, "height") + 0.5 ||
            Int(menu, "documentWidth") > 1100)
            throw new InvalidDataException(
                "The real 1100px retained-goods menu extends outside the viewport.");

        string screenshotPath = Path.Combine(Path.GetDirectoryName(fullOutput)!,
            "map016-no-game-retained-goods-open-1100.png");
        await using (var screenshot = File.Create(screenshotPath))
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, screenshot);
        await Task.Delay(700, token);
        if (overviewLifecycleService.CreateProfileInstanceStatus() is not null ||
            overviewLifecycleService.RuntimeManaged || map317CommandService.IsScanActive ||
            (await mapAutoScanService.GetSnapshotAsync(token)).Running)
            throw new InvalidDataException("Proof ended with an unexpected game or Map scan owner.");
        int completed;
        int received;
        string[] blocked;
        lock (map016NoGameRequestGate)
        {
            received = map016NoGameReconcileCalls;
            completed = map016NoGameReconcileCompletions;
            blocked = [.. map016NoGameBlockedCommands];
        }
        if (received != 1 || completed != 1 || blocked.Length != 0)
            throw new InvalidDataException("Packaged no-game request barrier did not remain idle.");
        await File.WriteAllTextAsync(fullOutput, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            state = "proven",
            proof = "real_packaged_native_no_auto_launch_and_1100px_saved_truck_popup",
            packagedUi = true,
            packagedExeSha256 = Convert.ToHexString(SHA256.HashData(
                await File.ReadAllBytesAsync(Application.ExecutablePath, token))).ToLowerInvariant(),
            packagedUiIndexSha256 = Convert.ToHexString(SHA256.HashData(
                await File.ReadAllBytesAsync(Path.Combine(uiRootPath, "index.html"), token))).ToLowerInvariant(),
            genuineSavedRows = true,
            genuineLiveGame = false,
            controlledProvider = false,
            nativeAutoLaunchGame = native.AutoLaunchGame,
            nativeGameDesiredRunning = native.GameDesiredRunning,
            nativeAutoReconnect = native.AutoReconnect,
            nativeGameRootAbsent = native.GameRoot is null,
            initialBrowserAutoLaunchAbsent = true,
            nativeReconcileCalls = received,
            nativeReconcileCompletions = completed,
            blockedNativeMutations = blocked,
            gameStarts = 0,
            savedTruckServerId = rowServer,
            savedTruckRows,
            startup,
            truck,
            menuBounds = menu,
            screenshot = Path.GetFileName(screenshotPath),
            capturedAtUtc = DateTimeOffset.UtcNow
        }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
    }
}
