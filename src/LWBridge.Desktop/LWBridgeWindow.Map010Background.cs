using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

// MAP-010: task-isolated native WebView proof. Runs entirely inside our own
// WebView DOM; no global input, window activation/rearrangement, game launch,
// updater, or gameplay command. Proof target must be below --isolated-root.
internal sealed partial class LWBridgeWindow
{
    private async Task RunMap010BackgroundNamesProofAsync(CoreWebView2 core, string outputPath)
    {
        if (productionPaths?.ExplicitIsolation != true || map317CommandService is null ||
            language is not ("en" or "ja") || theme is not ("light" or "dark") ||
            activeProfileConfig.Snapshot.AutoLaunchGame ||
            activeProfileConfig.Snapshot.AutoReconnect ||
            activeProfileConfig.Snapshot.GameDesiredRunning)
            throw new InvalidOperationException(
                "MAP-010 native name proof requires a disabled-Auto isolated normal production app.");

        var records = new List<object>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        JsonElement initial = default;
        for (int attempt = 0; attempt < 180; attempt++)
        {
            cts.Token.ThrowIfCancellationRequested();
            initial = await CaptureStateAsync();
            if (initial.TryGetProperty("tabCount", out JsonElement tabCount) &&
                tabCount.GetInt32() >= 8 &&
                initial.GetProperty("lang").GetString() == language &&
                initial.GetProperty("theme").GetString() == theme)
                break;
            await Task.Delay(125, cts.Token);
        }
        if (initial.ValueKind == JsonValueKind.Undefined ||
            initial.GetProperty("tabCount").GetInt32() < 8 ||
            initial.GetProperty("lang").GetString() != language ||
            initial.GetProperty("theme").GetString() != theme)
            throw new InvalidDataException("Actual native WebView language/theme/Map route did not settle.");

        foreach ((int tabIndex, string kind) in new[] { (1, "resource"), (2, "monster") })
        {
            string clicked = await core.ExecuteScriptAsync(
                $"(() => {{ const button=document.querySelectorAll('.map-tabs [role=tab]')[{tabIndex}];" +
                "if (!button) return false; button.click(); return true; })()");
            if (clicked != "true")
                throw new InvalidDataException("Actual Map native tab was not available: " + kind);
            JsonElement state = default;
            bool settled = false;
            for (int attempt = 0; attempt < 200; attempt++)
            {
                cts.Token.ThrowIfCancellationRequested();
                state = await CaptureStateAsync();
                if (state.GetProperty("activeTabIndex").GetInt32() == tabIndex &&
                    state.GetProperty("selectedTable").GetString() == kind &&
                    state.GetProperty("rowCount").GetInt32() > 0 &&
                    state.GetProperty("nameOptions").GetArrayLength() > 0 &&
                    !state.GetProperty("loading").GetBoolean() &&
                    !state.GetProperty("namesLoading").GetBoolean() &&
                    !state.GetProperty("namesError").GetBoolean())
                {
                    settled = true;
                    break;
                }
                await Task.Delay(150, cts.Token);
            }
            if (!settled)
                throw new InvalidDataException(
                    $"MAP-010 actual {kind} native records/names did not settle: " +
                    JsonSerializer.Serialize(state, JsonOptions.Default));

            List<string> keys = state.GetProperty("nameOptions").EnumerateArray()
                .Select(item => item.GetProperty("key").GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Take(200).Select(item => item!).ToList();
            using JsonDocument request = JsonDocument.Parse(JsonSerializer.Serialize(
                new { language, keys, profileId = activeProfileConfig.Snapshot.ProfileId }));
            JsonElement native = JsonSerializer.SerializeToElement(await backend.InvokeAsync(
                "lastwar_map_localize", request.RootElement.Clone(), cts.Token), JsonOptions.Default);
            if (native.GetProperty("language").GetString() != language ||
                native.GetProperty("gameRoot").GetString() != activeProfileConfig.Snapshot.GameRoot ||
                string.IsNullOrWhiteSpace(native.GetProperty("buildId").GetString()))
                throw new InvalidDataException("Current installed-game locale identity was not admitted.");
            JsonElement strings = native.GetProperty("texts");
            int matchingOptions = 0;
            int distinctLocalized = 0;
            foreach (JsonElement entry in state.GetProperty("nameOptions").EnumerateArray())
            {
                string key = entry.GetProperty("key").GetString()!;
                if (!strings.TryGetProperty(key, out JsonElement value)) continue;
                string localized = value.GetString() ?? "";
                if (localized != key) distinctLocalized++;
                if (entry.GetProperty("label").GetString()?.Contains(localized, StringComparison.Ordinal) == true)
                    matchingOptions++;
            }
            if (distinctLocalized < 1 || matchingOptions < 1)
                throw new InvalidDataException("Actual native name options did not match installed-game strings: " + kind);
            records.Add(new
            {
                kind,
                rowCount = state.GetProperty("rowCount").GetInt32(),
                optionCount = state.GetProperty("nameOptions").GetArrayLength(),
                distinctLocalized,
                matchingOptions,
                nativeBuildId = native.GetProperty("buildId").GetString(),
                localeSha256 = native.GetProperty("localeSha256").GetString(),
                englishSha256 = native.GetProperty("englishSha256").GetString()
            });
            // Only our own WebView content. It may be fully occluded by Chrome;
            // never call Activate/Focus/ShowWindow to change that.
            string screenshot = Path.ChangeExtension(outputPath, "." + kind + ".png");
            await using (var image = File.Create(screenshot))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, task = "LWB317-MAP-BACKGROUND-ACQUISITION-010",
            proof = "actual production WebView, installed-game native locale and archived genuine saved Map store",
            isolatedRoot = productionPaths.Root, profileId = activeProfileConfig.Snapshot.ProfileId,
            language, theme, view = "map-data", source = "Map317 stored native records",
            backgroundCapture = "CoreWebView2.CapturePreviewAsync (no global input/window focus)",
            records,
            gameLaunches = 0, gameActions = 0,
        }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), cts.Token);

        async Task<JsonElement> CaptureStateAsync()
        {
            string json = await core.ExecuteScriptAsync("""
                (() => {
                  const tab = document.querySelector('.map-tabs [role=tab][aria-selected=true]');
                  const table = document.querySelector('.map-table');
                  const options = [...document.querySelectorAll('.map-searchbar select:not([data-map-saved-server]) option')]
                    .filter(option => option.value && option.textContent.trim())
                    .map(option => ({ key: option.value, label: option.textContent.trim() }));
                  return {
                    lang: document.documentElement.lang,
                    theme: document.documentElement.dataset.theme,
                    tabCount: document.querySelectorAll('.map-tabs [role=tab]').length,
                    activeTabIndex: [...document.querySelectorAll('.map-tabs [role=tab]')].indexOf(tab),
                    selectedTable: table?.className?.match(/map-table--(\w+)/)?.[1] || '',
                    rowCount: table?.querySelectorAll('tbody tr:not(:has(.map-empty))').length || 0,
                    loading: table?.getAttribute('aria-busy') === 'true',
                    namesLoading: !!document.querySelector('.muted[role=status]'),
                    namesError: !!document.querySelector('.map-scan-error[role=alert]'),
                    nameOptions: options,
                  };
                })()
                """);
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }
}
