using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record CurrentClientAssetImageResult(
    string DataUrl,
    int Width,
    int Height,
    string SourceMode,
    string SourceValue);

internal sealed partial class CurrentClientMapBlockSource
{
    private static readonly TimeSpan AssetImageTimeout = TimeSpan.FromSeconds(15);
    private const int AssetImageCacheEntryLimit = 512;
    private readonly SemaphoreSlim assetImageGate = new(1, 1);
    private readonly Dictionary<string, CurrentClientAssetImageResult> assetImageCache =
        new(StringComparer.Ordinal);
    private readonly Queue<string> assetImageCacheOrder = new();

    public async Task<CurrentClientAssetImageResult> GetAssetImageAsync(
        string? assetPath,
        string? spriteName,
        CancellationToken cancellationToken)
    {
        (string sourceMode, string sourceValue) = NormalizeAssetImageSource(assetPath, spriteName);
        string cacheKey = sourceMode + ":" + sourceValue;

        await assetImageGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (assetImageCache.TryGetValue(cacheKey, out CurrentClientAssetImageResult? cached))
                return cached;

            OverviewMapScanSession session = RequireReadySession();
            if (waitForHealthySession is { } waitForHealthy)
            {
                await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
                RequireSameSession(session);
            }

            Exception? lastError = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    CurrentClientAssetImageResult result = await ProbeAssetImageAsync(
                            session,
                            sourceMode,
                            sourceValue,
                            cancellationToken)
                        .ConfigureAwait(false);
                    AddAssetImageCache(cacheKey, result);
                    return result;
                }
                catch (Exception error) when (
                    error is TimeoutException ||
                    error is BridgeCommandException bridge &&
                    bridge.Code == "GAME_CONNECTION_UNAVAILABLE")
                {
                    lastError = error;
                    if (attempt >= 3) break;
                    if (waitForHealthySession is { } retryHealthy)
                    {
                        await retryHealthy(session, cancellationToken).ConfigureAwait(false);
                        RequireSameSession(session);
                    }
                    await DelayAsync(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
                }
            }

            throw lastError ?? new InvalidOperationException("asset image acquisition failed without an error");
        }
        finally
        {
            assetImageGate.Release();
        }
    }

    private async Task<CurrentClientAssetImageResult> ProbeAssetImageAsync(
        OverviewMapScanSession session,
        string sourceMode,
        string sourceValue,
        CancellationToken cancellationToken)
    {
        string requestId = "asset" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "asset-image.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "asset-image-result.json");
        DateTimeOffset startedAt = Now();
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"launchSessionId={session.SessionId}",
            $"profileId={session.ProfileId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"sourceMode={sourceMode}",
            sourceMode == "assetPath"
                ? $"assetPath={sourceValue}"
                : $"spriteName={sourceValue}",
            string.Empty,
        });

        await WriteCommandAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = startedAt + AssetImageTimeout;
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
                return ValidateAssetImageResult(
                    root.Value,
                    requestId,
                    startedAt,
                    session,
                    sourceMode,
                    sourceValue);

            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The current-client asset image probe did not return a correlated fresh result.");
    }

    private CurrentClientAssetImageResult ValidateAssetImageResult(
        JsonElement root,
        string requestId,
        DateTimeOffset startedAt,
        OverviewMapScanSession session,
        string sourceMode,
        string sourceValue)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "probeVersion", ProbeVersion) ||
            !MatchesString(root, "requestId", requestId) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "launchSessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesString(root, "sourceMode", sourceMode) ||
            !MatchesString(root, "sourceValue", sourceValue))
        {
            throw new InvalidDataException(
                "Asset image result did not match the active owned game session and request.");
        }

        string state = ReadOptionalString(root, "state") ?? string.Empty;
        RequireFreshCaptureTime(root, startedAt);
        if (state == "failed")
        {
            string error = ReadOptionalString(root, "error") ?? "asset_image_failed";
            if (error.StartsWith("overview_", StringComparison.Ordinal) ||
                error.EndsWith("_identity_mismatch", StringComparison.Ordinal))
            {
                throw new BridgeCommandException(
                    "GAME_CONNECTION_UNAVAILABLE",
                    "game connection unavailable",
                    error);
            }
            throw new BridgeCommandException("INVALID_ASSET", "invalid PNG asset", error);
        }
        if (state != "proven")
            throw new InvalidDataException("Asset image result did not contain a supported terminal state.");

        int width = RequirePositiveInt(root, "width");
        int height = RequirePositiveInt(root, "height");
        if (width > 4096 || height > 4096)
            throw new BridgeCommandException("INVALID_ASSET", "invalid PNG asset", "asset image dimensions exceed 4096 pixels");

        string? base64 = ReadOptionalString(root, "base64");
        if (string.IsNullOrWhiteSpace(base64))
            throw new BridgeCommandException("INVALID_ASSET", "invalid PNG asset", "asset image payload is empty");

        byte[] png;
        try
        {
            png = Convert.FromBase64String(base64);
        }
        catch (FormatException error)
        {
            throw new BridgeCommandException("INVALID_ASSET", "invalid PNG asset", error.Message);
        }

        ValidatePngAsset(png, width, height);
        string normalizedBase64 = Convert.ToBase64String(png);
        return new CurrentClientAssetImageResult(
            "data:image/png;base64," + normalizedBase64,
            width,
            height,
            sourceMode,
            sourceValue);
    }

    private static (string Mode, string Value) NormalizeAssetImageSource(
        string? assetPath,
        string? spriteName)
    {
        string asset = assetPath?.Trim() ?? string.Empty;
        string sprite = spriteName?.Trim() ?? string.Empty;
        if ((asset.Length == 0) == (sprite.Length == 0))
            throw new BridgeCommandException(
                "INVALID_ASSET",
                "invalid PNG asset",
                "exactly one of assetPath or spriteName is required");

        string value = asset.Length > 0 ? asset : sprite;
        if (value.Length > 1024 ||
            value.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new BridgeCommandException(
                "INVALID_ASSET",
                "invalid PNG asset",
                "asset source is invalid");
        }

        return asset.Length > 0 ? ("assetPath", asset) : ("spriteName", sprite);
    }

    private static void ValidatePngAsset(byte[] png, int expectedWidth, int expectedHeight)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (png.Length < 24 ||
            !png.AsSpan(0, signature.Length).SequenceEqual(signature) ||
            !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new BridgeCommandException("INVALID_ASSET", "invalid PNG asset");
        }

        int width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)));
        int height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)));
        if (width <= 0 || height <= 0 || width != expectedWidth || height != expectedHeight)
            throw new BridgeCommandException("INVALID_ASSET", "invalid PNG asset");
    }

    private void AddAssetImageCache(string key, CurrentClientAssetImageResult result)
    {
        if (assetImageCache.ContainsKey(key))
        {
            assetImageCache[key] = result;
            return;
        }

        while (assetImageCache.Count >= AssetImageCacheEntryLimit && assetImageCacheOrder.Count > 0)
        {
            string oldest = assetImageCacheOrder.Dequeue();
            assetImageCache.Remove(oldest);
        }

        assetImageCache[key] = result;
        assetImageCacheOrder.Enqueue(key);
    }
}
