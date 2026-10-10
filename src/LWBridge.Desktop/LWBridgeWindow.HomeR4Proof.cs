using System.Text.Json;
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

        async Task SelectAsync(string displayName, string expectedProfileId)
        {
            string name = JsonSerializer.Serialize(displayName, JsonOptions.Default);
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
