using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class AuthBypassChecks
{
    internal static async Task RunAsync()
    {
        var backend = new LWBridgeBackend(new LocalConfigStore(persistent: false));
        using JsonDocument empty = JsonDocument.Parse("{}");

        JsonElement auth = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync("auth_state", empty.RootElement, CancellationToken.None),
            JsonOptions.Default);
        Require(auth.GetProperty("phase").GetString() == "authorized", "auth bypass phase");
        Require(auth.GetProperty("username").GetString() == "local", "auth bypass username");
        Require(auth.GetProperty("accessRole").GetString() == "normal", "auth bypass role");
        Require(auth.GetProperty("errorCode").ValueKind == JsonValueKind.Null, "auth bypass errorCode");

        JsonElement entitlement = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync("multi_entitlement_get", empty.RootElement, CancellationToken.None),
            JsonOptions.Default);
        Require(entitlement.GetProperty("phase").GetString() == "single", "entitlement bypass phase");
        Require(entitlement.GetProperty("maxProfiles").GetInt32() == 1, "entitlement bypass capacity");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
            throw new InvalidDataException("R8-120 production auth bypass check failed: " + name);
    }
}
