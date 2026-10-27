using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Dedicated isolated read-only proof. It has no staged command queue and
    // never enters the sub-account gameplay harness or enables its hotkeys.
    private async Task RunReadOnlyChatCatalogProofAsync(CoreWebView2 core,
        string outputPath, string directory, OverviewMapScanSession owner,
        CancellationToken token)
    {
        _ = core; _ = directory;
        var lifecycle = overviewLifecycleService ?? throw new InvalidDataException("Catalog lifecycle missing.");
        string uid = Environment.GetEnvironmentVariable("LWBRIDGE_CHAT_CATALOG_UID") ?? "";
        if (productionPaths?.ExplicitIsolation != true || uid.Length is < 1 or > 20 ||
            uid[0] == '0' || !uid.All(char.IsAsciiDigit) ||
            !int.TryParse(Environment.GetEnvironmentVariable("LWBRIDGE_CHAT_CATALOG_SERVER"), out int server) || server <= 0 ||
            Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_STAGED_PROOF") == "1")
            throw new InvalidDataException("Catalog requires isolated, explicit read-only identity and no staged queue.");
        var expected = new Feature018HotkeySession(owner.ProfileId, owner.SessionId, uid, server);
        bool SameOwner() => lifecycle.MatchesOwnedMapScanSession(owner) && lifecycle.OwnsExactProcess(owner.GamePid);
        for (int attempt = 0; attempt < 240; attempt++)
        {
            token.ThrowIfCancellationRequested();
            if (!SameOwner()) throw new InvalidDataException("Catalog process/session changed.");
            var account = ReadFeature018AuthenticatedAccount(lifecycle, owner);
            if (account is not null && account != expected)
                throw new InvalidDataException("Read-only catalog account differs from explicit identity.");
            if (account == expected)
            {
                // This proof contains one fixed read-only command. It cannot
                // consume arbitrary commands from a file, browser or caller.
                var game = new Feature018GameCommandService(new Feature018OwnedGameExchange(lifecycle),
                    equipmentConfigService, new Feature018AuthenticatedGameAccount(uid, server), profileRuntimeConfigStore);
                object? result = await game.InvokeAsync("feature018_chat_catalog",
                    JsonSerializer.SerializeToElement(new { profileId = owner.ProfileId }, JsonOptions.Default), token);
                if (!SameOwner() || ReadFeature018AuthenticatedAccount(lifecycle, owner) != expected)
                    throw new InvalidDataException("Catalog owner changed during read.");
                JsonElement catalog = JsonSerializer.SerializeToElement(result, JsonOptions.Default);
                if (!catalog.TryGetProperty("readOnly", out var readOnly) || readOnly.ValueKind != JsonValueKind.True ||
                    !catalog.TryGetProperty("requestDispatched", out var dispatched) || dispatched.ValueKind != JsonValueKind.False)
                    throw new InvalidDataException("Catalog response did not prove read-only configuration inspection.");
                await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
                {
                    schemaVersion = 1, state = "proven", readOnly = true, requestDispatched = false,
                    proof = "native_read_only_chat_catalog_no_gameplay_queue", owner.GamePid,
                    catalog, finishedAtUtc = DateTimeOffset.UtcNow,
                }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
                return;
            }
            await Task.Delay(500, token);
        }
        throw new InvalidDataException("Read-only catalog authenticated account did not become ready.");
    }
}
