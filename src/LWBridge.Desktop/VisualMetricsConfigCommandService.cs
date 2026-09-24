using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class VisualMetricsConfigCommandService : INativeAsyncCommandService
{
    private readonly ProfileRuntimeConfigStore store;

    internal VisualMetricsConfigCommandService(string runtimeConfigPath) =>
        store = new ProfileRuntimeConfigStore(runtimeConfigPath);

    internal VisualMetricsConfigCommandService(ProfileRuntimeConfigStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public bool CanHandle(string command) =>
        command is "visual_metrics_config_get" or "visual_metrics_config_save";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object result = command switch
        {
            "visual_metrics_config_get" => store.ReadVisualMetrics(),
            "visual_metrics_config_save" =>
                store.SaveVisualMetrics(ParseSavePayload(payload)),
            _ => throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Visual Metrics config service does not handle '{command}'."),
        };

        return Task.FromResult<object?>(result);
    }

    private static VisualMetricsConfig ParseSavePayload(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("showFps", out JsonElement showFps) ||
            showFps.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !payload.TryGetProperty("showPing", out JsonElement showPing) ||
            showPing.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "invalid visual metrics config");
        }

        return new VisualMetricsConfig(
            ShowFps: showFps.GetBoolean(),
            ShowPing: showPing.GetBoolean());
    }
}
