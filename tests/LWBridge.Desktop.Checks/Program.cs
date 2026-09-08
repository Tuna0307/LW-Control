using System.Text.Json;
using LWBridge.Desktop;

var failures = new List<string>();

void Check(bool condition, string name)
{
    if (!condition) failures.Add(name);
}

string detectedRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "FunFly", "Last War-Survival Game");

var store = new LocalConfigStore();
var installation = new GameInstallationService(store);
GameRootStatus installed = installation.Validate(detectedRoot, "self-check");
Check(installed.Valid, "installed root validates");
Check(installed.Is64Bit == true, "installed game/xlua are PE32+");
Check(File.Exists(installed.LauncherPath), "official launcher exists");
Check(File.Exists(installed.GamePath), "game executable exists");
Check(File.Exists(installed.XluaPath), "original xlua exists");

GameRootStatus invalid = installation.Validate(
    Path.Combine(Path.GetTempPath(), "lwbridge-missing-root-" + Guid.NewGuid().ToString("N")),
    "self-check");
Check(!invalid.Valid && invalid.Error == "GAME_ROOT_REQUIRED_FILES_MISSING", "missing root fails closed");

GameProcessStatus process = installation.GetProcessStatus();
var backend = new LWBridgeBackend();
using JsonDocument profilePayload = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = backend.ProfileId }));

try
{
    await backend.InvokeAsync("profile_instance_start", profilePayload.RootElement.Clone(), CancellationToken.None);
    failures.Add("launch is blocked until staged bootstrap is recovered");
}
catch (BridgeCommandException error)
{
    Check(error.Code == "OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED", "launch fails closed with explicit recovery gate");
}

using JsonDocument foreignProfile = JsonDocument.Parse("{\"profileId\":\"foreign-profile\"}");
try
{
    await backend.InvokeAsync("profile_instance_status", foreignProfile.RootElement.Clone(), CancellationToken.None);
    failures.Add("foreign profile is rejected");
}
catch (BridgeCommandException error)
{
    Check(error.Code == "PROFILE_SCOPE_MISMATCH", "foreign profile is rejected");
}

using JsonDocument normalScan = JsonDocument.Parse("{}");
MapScanStartOptions normalOptions = MapScanContract.NormalizeStart(normalScan.RootElement);
Check(normalOptions.ScanMode == "normal" && normalOptions.Concurrency == 8, "normal scan concurrency is recovered as 8");
Check(normalOptions.SelectedTypes.SequenceEqual(MapScanContract.AllTypes), "missing selectedTypes defaults to all eight recovered kinds");

using JsonDocument fastScan = JsonDocument.Parse(
    "{\"scanMode\":\"fast\",\"selectedTypes\":[\"truck\",\"bogus\",\"city\",\"truck\",7,\"treasure\"]}");
MapScanStartOptions fastOptions = MapScanContract.NormalizeStart(fastScan.RootElement);
Check(fastOptions.ScanMode == "fast" && fastOptions.Concurrency == 20, "fast scan concurrency is recovered as 20");
Check(fastOptions.SelectedTypes.SequenceEqual(new[] { "truck", "city", "treasure" }), "scan types filter unknown/non-string values and deduplicate in first-seen order");

using JsonDocument invalidTypes = JsonDocument.Parse("{\"selectedTypes\":[\"unknown\",5]}");
try
{
    MapScanContract.NormalizeStart(invalidTypes.RootElement);
    failures.Add("empty normalized scan type list is rejected");
}
catch (BridgeCommandException error)
{
    Check(error.Code == "INVALID_SCAN_TYPES", "empty normalized scan type list is rejected");
}

using JsonDocument invalidMode = JsonDocument.Parse("{\"scanMode\":\"turbo\"}");
try
{
    MapScanContract.NormalizeStart(invalidMode.RootElement);
    failures.Add("unknown scan mode is rejected");
}
catch (BridgeCommandException error)
{
    Check(error.Code == "INVALID_SCAN_MODE", "unknown scan mode is rejected");
}

var report = new
{
    ok = failures.Count == 0,
    installedRoot = new
    {
        installed.Valid,
        installed.Source,
        installed.Path,
        installed.Error,
        installed.Is64Bit,
    },
    process = new
    {
        process.GameRunning,
        process.LauncherRunning,
        process.GamePid,
        process.LauncherPid,
    },
    failures,
};

Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions.Indented));
return failures.Count == 0 ? 0 : 1;
