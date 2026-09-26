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
            "an isolated backend without the shared host keeps pending null; production host-backed status is proven separately");

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
        Check(callLuaError == "LUA_CALL_FAILED",
            "exact getStatus is enabled but fails with recovered Lua-call fallback when no shared transport exists");

        string genericCallError = "UNEXPECTED_SUCCESS";
        try
        {
            await backend.InvokeAsync(
                "call_lua",
                JsonSerializer.SerializeToElement(new
                {
                    profileId = backend.ProfileId,
                    fnName = "otherFunction",
                    args = new { },
                }, JsonOptions.Default),
                CancellationToken.None);
        }
        catch (BridgeCommandException error)
        {
            genericCallError = error.Code;
        }
        Check(genericCallError == "COMMAND_NOT_IMPLEMENTED",
            "generic call_lua remains fail-closed outside exact getStatus with empty args");

        string protocolPath = Path.Combine(repo, "src", "LWBridge.Desktop", "LWBridgeControlPipeProtocol.cs");
        string protocol = File.ReadAllText(protocolPath);
        Check(protocol.Contains("EncodeHelloAck", StringComparison.Ordinal) &&
              protocol.Contains("EncodeCallCommand", StringComparison.Ordinal) &&
              protocol.Contains("ParseCallResult", StringComparison.Ordinal) &&
              protocol.Contains("This class remains protocol-only", StringComparison.Ordinal),
            "control-pipe wire contract remains protocol-only while listener/RPC lifecycle lives in separate host classes");

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
                isolatedNoHostPending = (int?)null,
                productionPendingSource = "LWBridgeControlPipeHostState.PendingCallCount",
                callLuaGetStatusWithoutTransportError = callLuaError,
                genericCallLuaError = genericCallError,
                outboundControlPipeWireContractRecovered = true,
                sharedControlPipeHostStateOwned = true,
                persistentControlPipeListenerImplemented = true,
                exactGetStatusCallEnabled = true,
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
