using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewStatusContractChecks
{
    internal static async Task<JsonElement> RunAsync()
    {
        string repo = FindRepoRoot();
        string assetRoot = Path.Combine(repo, "src", "LWBridge.Desktop", "WebUi", "assets");
        string indexPath = Directory.GetFiles(assetRoot, "index-*.js").Single();
        string index = File.ReadAllText(indexPath);
        string apiPath = Directory.GetFiles(assetRoot, "api-*.js").Single();
        string api = File.ReadAllText(apiPath);

        Check(index.Contains("?.pending??0", StringComparison.Ordinal),
            "recovered status card must display host get_status.pending with zero only as the frontend nullish fallback");
        const string RefreshSequence = "Rt(await C()),ne(await we()),await zt(`getStatus`,`getStatus`)";
        Check(index.Contains(RefreshSequence, StringComparison.Ordinal),
            "recovered Refresh Status must remain get_status -> proxy_status -> call_lua(getStatus)");
        Check(api.Contains("get_status", StringComparison.Ordinal) &&
              api.Contains("call_lua", StringComparison.Ordinal) &&
              api.Contains("fnName", StringComparison.Ordinal),
            "recovered API adapter must retain distinct host get_status and generic call_lua surfaces");

        var config = new LocalConfigStore(persistent: false);
        var backend = new LWBridgeBackend(config);
        JsonElement payload = JsonSerializer.SerializeToElement(new { profileId = backend.ProfileId });
        JsonElement status = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync("get_status", payload, CancellationToken.None), JsonOptions.Default);
        Check(status.TryGetProperty("pending", out JsonElement pending) && pending.ValueKind == JsonValueKind.Null,
            "rebuild get_status.pending must stay unknown/null until the original bridge-store pending-call registry has an authentic implementation");

        string callLuaError = "UNEXPECTED_SUCCESS";
        try
        {
            await backend.InvokeAsync(
                "call_lua",
                JsonSerializer.SerializeToElement(new
                {
                    profileId = backend.ProfileId,
                    fnName = "getStatus",
                    args = new { },
                }, JsonOptions.Default),
                CancellationToken.None);
        }
        catch (BridgeCommandException error)
        {
            callLuaError = error.Code;
        }
        Check(callLuaError == "COMMAND_NOT_IMPLEMENTED",
            "generic call_lua must remain fail-closed until outbound host command/result serialization is recovered");

        string protocolPath = Path.Combine(repo, "src", "LWBridge.Desktop", "LWBridgeControlPipeProtocol.cs");
        string protocol = File.ReadAllText(protocolPath);
        Check(protocol.Contains(
                  "This class intentionally stops before hello.ack or\n// command serialization because those outbound host contracts are still\n// unrecovered.",
                  StringComparison.Ordinal) &&
              !protocol.Contains("EncodeCommand", StringComparison.Ordinal) &&
              !protocol.Contains("ParseCommandResult", StringComparison.Ordinal),
            "control-pipe implementation must continue to disclose that outbound RPC framing/result contracts are unrecovered");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            acceptanceCase = "A11",
            recoveredContract = new
            {
                visiblePendingSource = "host get_status.pending",
                frontendFallbackOnly = 0,
                refreshOrder = new[] { "get_status", "proxy_status", "call_lua:getStatus" },
            },
            currentImplementation = new
            {
                pending = (int?)null,
                callLuaGetStatusError = callLuaError,
                outboundControlPipeCommandSerializationRecovered = false,
            },
        }, JsonOptions.Default);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "task.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "src", "LWBridge.Desktop", "WebUi")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("could not locate LW-Control repository root");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Overview status contract check failed: " + message);
    }
}
