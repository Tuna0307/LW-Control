using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed class ClaimDelayConfigCommandService :
    INativeAsyncCommandService
{
    private readonly ProfileRuntimeConfigStore store;

    internal ClaimDelayConfigCommandService(
        ProfileRuntimeConfigStore store)
    {
        this.store = store ??
            throw new ArgumentNullException(nameof(store));
    }

    public bool CanHandle(string command) =>
        command is "red_packet_delay_configure" or
            "treasure_delay_configure";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanHandle(command))
        {
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Claim delay service does not handle '{command}'.");
        }
        string label;
        string chatKind;
        string schedulerKey;
        double maximumSeconds;

        if (command == "treasure_delay_configure")
        {
            label = "treasure";
            chatKind = "treasure";
            schedulerKey = "treasureClaimDelaySeconds";
            maximumSeconds = 600d;
        }
        else
        {
            label = "red packet";
            chatKind = "red_packet";
            schedulerKey = "redPacketClaimDelaySeconds";
            maximumSeconds = 60d;
        }

        double minSeconds = ReadNumber(payload, "minSeconds");
        double maxSeconds = ReadNumber(payload, "maxSeconds");

        if (!double.IsFinite(minSeconds) ||
            !double.IsFinite(maxSeconds) ||
            minSeconds < 0d ||
            minSeconds > maxSeconds ||
            maxSeconds > maximumSeconds)
        {
            string limit =
                maximumSeconds.ToString("0", CultureInfo.InvariantCulture);
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                $"{label} delay must be a valid range from 0 to {limit} seconds");
        }
        JsonArray range = store.SaveClaimDelayRange(
            chatKind,
            schedulerKey,
            minSeconds,
            maxSeconds);

        return Task.FromResult<object?>(
            new JsonObject
            {
                ["ok"] = true,
                ["range"] = range,
            });
    }

    private static double ReadNumber(
        JsonElement payload,
        string name)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out double parsed))
        {
            return double.NaN;
        }

        return parsed;
    }
}
