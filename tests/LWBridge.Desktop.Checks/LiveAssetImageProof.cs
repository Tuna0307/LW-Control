using System.Buffers.Binary;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveAssetImageProof
{
    internal static async Task RunAsync()
    {
        const string ProfileId = "asset-image-proof";
        string[] assetPaths = (Environment.GetEnvironmentVariable("LWBRIDGE_ASSET_IMAGE_PATHS") ??
            "Assets/Main/Sprites/ItemIcons/item406;" +
            "Assets/Main/Sprites/ItemIcons/item210451.png;" +
            "Assets/Main/Sprites/UI/LWCommon/Sprite/cfm_zhujiemian_tubiao_ziyuan1.png;" +
            "Assets/Main/Sprites/ItemIcons/cfm_icon_baoxiang_putongziyuan")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-asset-image-proof");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot, "asset-image-" + Guid.NewGuid().ToString("N") + ".db");

        using var lifecycle = new OverviewLifecycleService(ProfileId, gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startInstance = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement instanceJson = JsonSerializer.SerializeToElement(startInstance, JsonOptions.Default);
            instanceId = instanceJson.GetProperty("instanceId").GetString();

            using var store = new MapDataStore(databasePath);
            var service = new ManualMapScanCommandService(lifecycle, store);
            try
            {
                if (assetPaths.Length == 0)
                    throw new InvalidOperationException("asset image proof requires at least one asset path");

                string runtimeResultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LWBridgeRebuild", "live-resource", "asset-image-result.json");
                var observations = new List<object>(assetPaths.Length);
                const string Prefix = "data:image/png;base64,";

                foreach (string assetPath in assetPaths)
                {
                    JsonElement payload = JsonSerializer.SerializeToElement(new
                    {
                        profileId = ProfileId,
                        assetPath,
                    }, JsonOptions.Default);

                    object? firstRaw = await service.InvokeAsync(
                        "game_asset_image", payload, operationCts.Token).ConfigureAwait(false);
                    object? secondRaw = await service.InvokeAsync(
                        "game_asset_image", payload, operationCts.Token).ConfigureAwait(false);
                    JsonElement first = JsonSerializer.SerializeToElement(firstRaw, JsonOptions.Default);
                    JsonElement second = JsonSerializer.SerializeToElement(secondRaw, JsonOptions.Default);
                    string firstDataUrl = first.GetProperty("dataUrl").GetString() ?? string.Empty;
                    string secondDataUrl = second.GetProperty("dataUrl").GetString() ?? string.Empty;
                    if (!firstDataUrl.StartsWith(Prefix, StringComparison.Ordinal) ||
                        !string.Equals(firstDataUrl, secondDataUrl, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "public game_asset_image did not return a stable PNG data URL across cache reuse");
                    }

                    byte[] png = Convert.FromBase64String(firstDataUrl[Prefix.Length..]);
                    if (png.Length < 24 ||
                        !png.AsSpan(0, 8).SequenceEqual(
                            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ||
                        !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
                    {
                        throw new InvalidDataException("public game_asset_image returned invalid PNG bytes");
                    }

                    int width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)));
                    int height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)));
                    JsonElement runtime = ReadRuntimeResult(runtimeResultPath);
                    if (runtime.GetProperty("state").GetString() != "proven" ||
                        runtime.GetProperty("sourceMode").GetString() != "assetPath" ||
                        runtime.GetProperty("sourceValue").GetString() != assetPath)
                    {
                        throw new InvalidDataException(
                            "runtime asset-image result did not preserve proven authoritative source identity");
                    }

                    observations.Add(new
                    {
                        assetPath,
                        width,
                        height,
                        pngBytes = png.Length,
                        pngSha256 = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(png)).ToLowerInvariant(),
                        packed = OptionalBool(runtime, "packed"),
                        packingRotation = OptionalString(runtime, "packingRotation"),
                        textureWidth = OptionalInt(runtime, "textureWidth"),
                        textureHeight = OptionalInt(runtime, "textureHeight"),
                        pixelsPerUnit = OptionalDouble(runtime, "pixelsPerUnit"),
                        renderMethod = OptionalString(runtime, "renderMethod"),
                        cacheReuseExact = true,
                    });
                }

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "public_game_asset_image_current_v19",
                    assetCount = observations.Count,
                    observations,
                }, JsonOptions.Default));
            }
            finally
            {
                service.Close();
            }
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop", stopPayload.RootElement, stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("ASSET_IMAGE_PROOF_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }

            foreach (string candidate in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
            {
                try { if (File.Exists(candidate)) File.Delete(candidate); } catch { }
            }
        }
    }

    private static JsonElement ReadRuntimeResult(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private static string? OptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? OptionalInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed
            : null;

    private static double? OptionalDouble(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double parsed)
            ? parsed
            : null;

    private static bool? OptionalBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value)
            ? value.ValueKind == JsonValueKind.True
                ? true
                : value.ValueKind == JsonValueKind.False
                    ? false
                    : null
            : null;
}
