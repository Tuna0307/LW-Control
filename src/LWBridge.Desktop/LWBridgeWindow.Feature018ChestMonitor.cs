using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Mini Games' chest hint is an independent, persisted 0.3.17 option.
    // It must still run while LastWar is in the background, without bringing
    // the game to the foreground or synthesizing mouse/keyboard input.
    private readonly System.Windows.Forms.Timer feature018ChestTimer = new() { Interval = 1500 };
    private bool feature018ChestPolling;
    private OverviewMapScanSession? feature018ChestOwner;
    private Feature018HotkeySession? feature018ChestAccount;
    private bool feature018ChestMarkerOwned;
    private string? feature018ChestMarkerSignature;

    private void StartFeature018ChestMonitoring()
    {
        feature018ChestTimer.Tick += Feature018ChestTick;
        feature018ChestTimer.Start();
    }

    private void StopFeature018ChestMonitoring()
    {
        feature018ChestTimer.Stop();
        feature018ChestTimer.Tick -= Feature018ChestTick;
        feature018ChestTimer.Dispose();
        RetireFeature018ChestMonitor();
    }

    private void RetireFeature018ChestMonitor()
    {
        feature018ChestOwner = null;
        feature018ChestAccount = null;
        feature018ChestMarkerOwned = false;
        feature018ChestMarkerSignature = null;
    }

    internal static bool Feature018ChestReceipt(JsonElement receipt,
        string observation, string markerKey, bool markerValue)
    {
        return receipt.ValueKind == JsonValueKind.Object &&
            receipt.TryGetProperty("observation", out JsonElement observed) &&
            observed.ValueKind == JsonValueKind.String &&
            observed.GetString() == observation &&
            receipt.TryGetProperty(markerKey, out JsonElement marker) &&
            marker.ValueKind == (markerValue ? JsonValueKind.True : JsonValueKind.False);
    }

    internal static string? Feature018ChestSignature(JsonElement receipt)
    {
        if (!Feature018ChestReceipt(receipt, "native_treasure_chest_view",
                "markerEligible", true) ||
            !receipt.TryGetProperty("nativeSwitchState", out JsonElement state) ||
            state.ValueKind != JsonValueKind.String ||
            state.GetString() != "SwitchEnd" ||
            !receipt.TryGetProperty("bestRewardBoxIndex", out JsonElement chest) ||
            chest.ValueKind != JsonValueKind.Number ||
            !chest.TryGetInt32(out int index) || index is < 1 or > 3)
            return null;

        var values = new List<string> { index.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        foreach (string field in new[] { "x", "y", "z" })
        {
            if (!receipt.TryGetProperty(field, out JsonElement coordinate) ||
                coordinate.ValueKind != JsonValueKind.Number ||
                !coordinate.TryGetDouble(out double number) ||
                !double.IsFinite(number))
                return null;
            values.Add(number.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        return string.Join("/", values);
    }

    internal static bool Feature018ChestToggleAllowsMark(HotkeyConfig? config) =>
        config?.TreasureChestHint == true;

    private bool Feature018ChestSameOwner(OverviewLifecycleService lifecycle,
        OverviewMapScanSession owner, Feature018HotkeySession account)
    {
        return !sessionClosed && !IsDisposed &&
            feature018ChestOwner == owner &&
            feature018ChestAccount == account &&
            ReferenceEquals(overviewLifecycleService, lifecycle) &&
            lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == account;
    }

    private async Task<JsonElement?> Feature018ChestCommandAsync(
        Feature018GameCommandService commands,
        string name,
        OverviewLifecycleService lifecycle,
        OverviewMapScanSession owner,
        Feature018HotkeySession account)
    {
        if (!Feature018ChestSameOwner(lifecycle, owner, account))
            return null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(1400));
        using JsonDocument empty = JsonDocument.Parse("{}");
        object? result = await commands.InvokeAsync(
            name, empty.RootElement.Clone(), cancellation.Token);
        if (!Feature018ChestSameOwner(lifecycle, owner, account))
            return null;
        return result is JsonElement value ? value : null;
    }

    private async Task Feature018ChestClearAsync(
        Feature018GameCommandService commands,
        OverviewLifecycleService lifecycle,
        OverviewMapScanSession owner,
        Feature018HotkeySession account)
    {
        if (!feature018ChestMarkerOwned) return;
        try
        {
            JsonElement? response = await Feature018ChestCommandAsync(
                commands, "mini_chest_unmark", lifecycle, owner, account);
            // A failed or missing response does not become fake cleanup proof.
            if (response is { } receipt && Feature018ChestReceipt(
                    receipt, "native_treasure_chest_switch_effect",
                    "markerVisible", false))
            {
                System.Diagnostics.Debug.WriteLine("FEATURE018 chest hint: native marker removed.");
            }
        }
        catch (Exception) { }
        finally
        {
            // Once no marker is requested, never keep polling the same
            // effect with unbounded cleanup commands. The native view's
            // destroy hook and lease-loss cleanup also remove owned effects.
            feature018ChestMarkerOwned = false;
            feature018ChestMarkerSignature = null;
        }
    }

    private async void Feature018ChestTick(object? sender, EventArgs e)
    {
        if (feature018ChestPolling || sessionClosed || IsDisposed) return;
        feature018ChestPolling = true;
        try
        {
            OverviewLifecycleService? lifecycle = overviewLifecycleService;
            OverviewMapScanSession? owner = lifecycle?.GetReadyMapScanSession();
            Feature018HotkeySession? account = lifecycle is not null && owner is not null
                ? ReadFeature018AuthenticatedAccount(lifecycle, owner) : null;
            if (lifecycle is null || owner is null || account is null ||
                hotkeyConfigService is null)
            {
                // Never attempt cleanup on a retired or foreign game owner.
                // The game-side mini module handles cleanup on lease loss.
                RetireFeature018ChestMonitor();
                return;
            }
            if (normalUiLiveMapProofPath is not null &&
                Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_R2_PROOF") == "1" &&
                account != feature018R2ApprovedAccount)
            {
                // The packaged proof approved one exact logged-in account.
                // A server/account switch must never let this background
                // marker start mutating the new account's game view.
                RetireFeature018ChestMonitor();
                return;
            }
            if (feature018ChestOwner != owner || feature018ChestAccount != account)
            {
                RetireFeature018ChestMonitor();
                feature018ChestOwner = owner;
                feature018ChestAccount = account;
            }

            using JsonDocument empty = JsonDocument.Parse("{}");
            object? stored = await hotkeyConfigService.InvokeAsync("hotkey_config_get",
                empty.RootElement.Clone(), CancellationToken.None);
            if (!Feature018ChestSameOwner(lifecycle, owner, account)) return;
            var commands = new Feature018GameCommandService(lifecycle);
            if (stored is not HotkeyConfig preferences || !Feature018ChestToggleAllowsMark(preferences))
            {
                await Feature018ChestClearAsync(commands, lifecycle, owner, account);
                return;
            }
            JsonElement? current = await Feature018ChestCommandAsync(
                commands, "mini_chest_position", lifecycle, owner, account);
            if (current is null) return;
            string? signature = Feature018ChestSignature(current.Value);
            if (signature is null)
            {
                await Feature018ChestClearAsync(commands, lifecycle, owner, account);
                return;
            }
            if (feature018ChestMarkerOwned &&
                feature018ChestMarkerSignature == signature)
                return;
            if (feature018ChestMarkerOwned)
            {
                await Feature018ChestClearAsync(commands, lifecycle, owner, account);
                // Observe the next settled frame before marking a new shuffle.
                return;
            }
            JsonElement? marked = await Feature018ChestCommandAsync(
                commands, "mini_chest_mark", lifecycle, owner, account);
            if (marked is not { } markedReceipt ||
                !Feature018ChestReceipt(markedReceipt,
                    "native_treasure_chest_view", "markerVisible", true) ||
                !markedReceipt.TryGetProperty("markerSource", out var source) ||
                source.ValueKind != JsonValueKind.String ||
                source.GetString() != "UITreasureChestView.ShowSwitchEffect")
                return;
            feature018ChestMarkerOwned = true;
            feature018ChestMarkerSignature = signature;
            System.Diagnostics.Debug.WriteLine("FEATURE018 chest hint: source-native marker observed.");
        }
        catch (Exception)
        {
            // Inactive game views and unavailable helpers are expected; do
            // not claim a successful marker or spin on errors.
        }
        finally { feature018ChestPolling = false; }
    }
}
