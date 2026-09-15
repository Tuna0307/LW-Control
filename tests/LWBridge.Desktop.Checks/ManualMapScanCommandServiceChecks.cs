using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ManualMapScanCommandServiceChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        await NormalStartOwnsOneRunAndStopCancels();
        await FastUsesRecoveredConcurrencyAndPublishes();
        await ContextFailureLeavesTruthfulError();
        await MixedTypesFailClosed();
    }

    private static JsonElement Payload(string mode, params string[] types) =>
        JsonSerializer.SerializeToElement(new
        {
            profileId = "default",
            scanMode = mode,
            selectedTypes = types,
        }, JsonOptions.Default);

    private static CurrentClientMapContext Context(int width = 20, int height = 20) =>
        new(2212, 0, width, height);

    private static async Task NormalStartOwnsOneRunAndStopCancels()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new BlockingSource();
        int contextCalls = 0;
        var service = new ManualMapScanCommandService(
            store,
            _ =>
            {
                contextCalls++;
                return Task.FromResult(Context());
            },
            source);

        _ = await service.InvokeAsync(
            "map_scan_start",
            Payload("normal", "resource"),
            CancellationToken.None);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        JsonElement status = Status(service);
        Check(Bool(status, "isReading"), "normal Start should own an active scan");
        Check(String(status, "phase") == "scanning", "normal Start should enter scanning phase");
        Check(Int(status, "concurrency") == 8, "normal Start must preserve recovered concurrency 8");
        Check(Int(status, "totalBlocks") == 1 && Int(status, "unreadBlocks") == 0,
            "one inflight 20x20 block should use truthful scheduler counters");

        try
        {
            _ = await service.InvokeAsync(
                "map_scan_start",
                Payload("normal", "resource"),
                CancellationToken.None);
            throw new InvalidOperationException("expected duplicate Start rejection");
        }
        catch (BridgeCommandException error) when (
            error.Code == MapScanStartOwnership.ActiveScanErrorCode &&
            error.Message == MapScanStartOwnership.ActiveScanErrorMessage)
        {
        }

        _ = await service.InvokeAsync(
            "map_scan_stop",
            Payload("normal", "resource"),
            CancellationToken.None);
        WaitForPhase(service, "idle");
        status = Status(service);
        Check(!Bool(status, "isReading") && Int(status, "inflightBlocks") == 0,
            "Stop should release scan ownership and clear inflight work");
        Check(contextCalls == 1, "duplicate Start must not reacquire live context");
        service.Close();
    }

    private static async Task FastUsesRecoveredConcurrencyAndPublishes()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context(width: 40, height: 20)),
            source);

        object? start = await service.InvokeAsync(
            "map_scan_start",
            Payload("fast", "resource"),
            CancellationToken.None);
        JsonElement status = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
        if (Bool(status, "isReading"))
            WaitForPhase(service, "completed");
        status = Status(service);

        Check(String(status, "phase") == "completed" && !Bool(status, "isReading"),
            "fast scan should publish after all blocks succeed");
        Check(Int(status, "concurrency") == 20, "fast Start must preserve recovered concurrency 20");
        Check(Int(status, "totalBlocks") == 2 && Int(status, "readBlocks") == 2,
            "40x20 map should traverse two recovered 20-tile blocks");
        Check(Double(status, "progressPercent") == 100.0,
            "completed fast scan should expose recovered 100 percent state");
        Check(source.Calls == 2, "fast scan must use the same block source for each traversal block");
        string runId = String(status, "scanRunId");
        Check(store.ReadScanBlockCheckpointsForTest(runId).Count == 0,
            "successful publication should clean block staging");
        service.Close();
    }

    private static async Task ContextFailureLeavesTruthfulError()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var service = new ManualMapScanCommandService(
            store,
            _ => throw new BridgeCommandException(
                MapScanStartOwnership.ServerUnavailableCode,
                MapScanStartOwnership.ServerUnavailableMessage),
            new ImmediateSource());
        try
        {
            _ = await service.InvokeAsync(
                "map_scan_start",
                Payload("normal", "city"),
                CancellationToken.None);
            throw new InvalidOperationException("expected live-context failure");
        }
        catch (BridgeCommandException error) when (
            error.Code == MapScanStartOwnership.ServerUnavailableCode)
        {
        }
        JsonElement status = Status(service);
        Check(!Bool(status, "isReading") && String(status, "phase") == "error",
            "failed Start should release ownership and expose an error phase");
        Check(String(status, "lastError") == MapScanStartOwnership.ServerUnavailableMessage,
            "failed Start should preserve the live-context error message");
        service.Close();
    }

    private static async Task MixedTypesFailClosed()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        int contextCalls = 0;
        var service = new ManualMapScanCommandService(
            store,
            _ =>
            {
                contextCalls++;
                return Task.FromResult(Context());
            },
            new ImmediateSource());
        try
        {
            _ = await service.InvokeAsync(
                "map_scan_start",
                Payload("normal", "city", "resource"),
                CancellationToken.None);
            throw new InvalidOperationException("expected unsupported mixed-type rejection");
        }
        catch (BridgeCommandException error) when (error.Code == "LIVE_BLOCK_TYPES_UNSUPPORTED")
        {
        }
        Check(contextCalls == 0, "unsupported mixed scan must fail before live-context acquisition");
        service.Close();
    }

    private static void WaitForPhase(
        ManualMapScanCommandService service,
        string expected)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (String(Status(service), "phase") == expected) return;
            Thread.Sleep(10);
        }
        throw new TimeoutException($"manual scan status did not reach '{expected}'");
    }

    private static JsonElement Status(ManualMapScanCommandService service) =>
        JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);

    private static string String(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int Int(JsonElement root, string name) =>
        root.GetProperty(name).GetInt32();

    private static double Double(JsonElement root, string name) =>
        root.GetProperty(name).GetDouble();

    private static bool Bool(JsonElement root, string name) =>
        root.GetProperty(name).GetBoolean();

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ImmediateSource : IMapScanBlockSource
    {
        public int Calls { get; private set; }

        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new MapScanBlockCapture(
                request.ServerId,
                request.WorldId,
                block.BlockIndex,
                "{}",
                []));
        }
    }

    private sealed class BlockingSource : IMapScanBlockSource
    {
        private readonly TaskCompletionSource<MapScanBlockCapture> completion = new();
        public TaskCompletionSource<bool> Entered { get; } = new();

        public async Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult(true);
            using CancellationTokenRegistration registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            return await completion.Task.ConfigureAwait(false);
        }
    }
}
