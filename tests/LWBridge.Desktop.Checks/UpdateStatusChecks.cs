using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class UpdateStatusChecks
{
    internal static async Task RunAsync()
    {
        var backend = new LWBridgeBackend(new LocalConfigStore(persistent: false));
        JsonElement empty = JsonSerializer.SerializeToElement(new { });

        object? raw = await backend.InvokeAsync(
            "update_status",
            empty,
            CancellationToken.None);
        JsonElement status =
            JsonSerializer.SerializeToElement(raw, JsonOptions.Default);

        string[] names = status.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expected =
        [
            "currentVersion",
            "downloadDirectory",
            "latestVersion",
            "message",
            "nextManualCheckAt",
            "phase",
            "progress",
            "publishedAt",
            "releaseNotes",
        ];

        Require(names.SequenceEqual(expected, StringComparer.Ordinal),
            "update_status returns the exact nine-field envelope");
        Require(status.GetProperty("phase").GetString() == "idle",
            "idle phase");
        Require(status.GetProperty("currentVersion").GetString() == "0.3.1",
            "reference current version");
        Require(status.GetProperty("latestVersion").ValueKind == JsonValueKind.Null,
            "latestVersion null at idle");
        Require(status.GetProperty("releaseNotes").GetString() == "",
            "releaseNotes empty at idle");
        Require(status.GetProperty("publishedAt").ValueKind == JsonValueKind.Null,
            "publishedAt null at idle");
        Require(status.GetProperty("progress").ValueKind == JsonValueKind.Null,
            "progress null at idle");
        Require(status.GetProperty("message").ValueKind == JsonValueKind.Null,
            "message null at idle");
        Require(status.GetProperty("nextManualCheckAt").ValueKind == JsonValueKind.Null,
            "nextManualCheckAt null at idle");
        Require(status.GetProperty("downloadDirectory").GetString() == "",
            "downloadDirectory empty at idle");

        await ExpectCode(
            backend,
            "update_check",
            "COMMAND_NOT_IMPLEMENTED");
        await ExpectCode(
            backend,
            "update_download_and_open",
            "COMMAND_NOT_IMPLEMENTED");
    }

    private static async Task ExpectCode(
        LWBridgeBackend backend,
        string command,
        string expectedCode)
    {
        JsonElement empty = JsonSerializer.SerializeToElement(new { });
        try
        {
            _ = await backend.InvokeAsync(command, empty, CancellationToken.None);
            throw new InvalidOperationException(
                $"Expected {command} to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == expectedCode,
                $"{command} expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
