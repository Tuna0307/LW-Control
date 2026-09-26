using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
    private static readonly TimeSpan AssetCacheMaintenanceInterval = TimeSpan.FromSeconds(60);
    private const long AssetCacheMaxBytes = 256L * 1024L * 1024L;
    private readonly SemaphoreSlim assetImageGate = new(1, 1);
    private long lastAssetCacheMaintenanceAt;

    public async Task<CurrentClientAssetImageResult> GetAssetImageAsync(
        string? assetPath,
        string? spriteName,
        CancellationToken cancellationToken)
    {
        (string sourceMode, string sourceValue) = NormalizeAssetImageSource(assetPath, spriteName);

        await assetImageGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? cachePath = GetAssetCachePath(sourceMode, sourceValue);
            if (cachePath is not null && TryReadCachedAsset(cachePath) is byte[] cachedPng)
                return BuildAssetImageResult(cachedPng, sourceMode, sourceValue);

            CurrentClientAssetImageResult result;
            try
            {
                OverviewMapScanSession session = RequireReadySession();
                if (waitForHealthySession is { } waitForHealthy)
                {
                    await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
                    RequireSameSession(session);
                }

                result = await ProbeAssetImageAsync(
                        session,
                        sourceMode,
                        sourceValue,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (BridgeCommandException error) when (
                error.Code == "GAME_CONNECTION_UNAVAILABLE")
            {
                throw new BridgeCommandException(
                    "GAME_DISCONNECTED",
                    "game disconnected",
                    error.Details);
            }

            if (cachePath is not null)
                TryWriteCachedAsset(cachePath, DecodeDataUrl(result.DataUrl));
            return result;
        }
        finally
        {
            TryMaintainAssetCache();
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

        ValidatePngAsset(png);
        return BuildAssetImageResult(png, sourceMode, sourceValue);
    }

    private static (string Mode, string Value) NormalizeAssetImageSource(
        string? assetPath,
        string? spriteName)
    {
        string asset = assetPath?.Trim() ?? string.Empty;
        string sprite = spriteName?.Trim() ?? string.Empty;
        if ((asset.Length == 0) == (sprite.Length == 0))
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "exactly one image source is required");

        return asset.Length > 0 ? ("assetPath", asset) : ("spriteName", sprite);
    }

    private static void ValidatePngAsset(byte[] png)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (png.Length < signature.Length ||
            !png.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            throw new BridgeCommandException("INVALID_ASSET", "invalid PNG asset");
        }
    }

    private static CurrentClientAssetImageResult BuildAssetImageResult(
        byte[] png,
        string sourceMode,
        string sourceValue)
    {
        ValidatePngAsset(png);
        (int width, int height) = ReadPngDimensionsIfPresent(png);
        return new CurrentClientAssetImageResult(
            "data:image/png;base64," + Convert.ToBase64String(png),
            width,
            height,
            sourceMode,
            sourceValue);
    }

    private static (int Width, int Height) ReadPngDimensionsIfPresent(byte[] png)
    {
        if (png.Length < 24 || !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            return (0, 0);

        uint width = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4));
        uint height = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4));
        return width is > 0 and <= int.MaxValue && height is > 0 and <= int.MaxValue
            ? ((int)width, (int)height)
            : (0, 0);
    }

    private static byte[] DecodeDataUrl(string dataUrl)
    {
        const string Prefix = "data:image/png;base64,";
        if (!dataUrl.StartsWith(Prefix, StringComparison.Ordinal))
            throw new BridgeCommandException("INVALID_ASSET", "invalid PNG asset");
        try
        {
            byte[] png = Convert.FromBase64String(dataUrl[Prefix.Length..]);
            ValidatePngAsset(png);
            return png;
        }
        catch (FormatException error)
        {
            throw new BridgeCommandException("INVALID_ASSET", "invalid PNG asset", error.Message);
        }
    }

    private string? GetAssetCachePath(string sourceMode, string sourceValue)
    {
        if (assetCacheRoot is null) return null;

        // Native uses a SHA-256-derived lowercase-hex PNG filename. The exact
        // preimage bytes are not yet byte-closed, so the retained rebuild uses a
        // collision-safe canonical source identity while preserving cache behavior.
        byte[] keyBytes = Encoding.UTF8.GetBytes(sourceMode + ":" + sourceValue);
        string digest = Convert.ToHexString(SHA256.HashData(keyBytes)).ToLowerInvariant();
        return Path.Combine(assetCacheRoot, digest + ".png");
    }

    private static byte[]? TryReadCachedAsset(string cachePath)
    {
        try
        {
            if (!File.Exists(cachePath)) return null;
            byte[] png = File.ReadAllBytes(cachePath);
            ValidatePngAsset(png);
            return png;
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryWriteCachedAsset(string cachePath, byte[] png)
    {
        try
        {
            string directory = Path.GetDirectoryName(cachePath)!;
            Directory.CreateDirectory(directory);
            string temporary = cachePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary, png);
                File.Move(temporary, cachePath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch
                {
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void TryMaintainAssetCache()
    {
        if (assetCacheRoot is null) return;

        long now = Now().ToUnixTimeMilliseconds();
        if (lastAssetCacheMaintenanceAt > 0 &&
            now - lastAssetCacheMaintenanceAt < AssetCacheMaintenanceInterval.TotalMilliseconds)
            return;
        lastAssetCacheMaintenanceAt = now;

        try
        {
            if (!Directory.Exists(assetCacheRoot)) return;
            FileInfo[] entries = new DirectoryInfo(assetCacheRoot)
                .EnumerateFiles("*.png", SearchOption.TopDirectoryOnly)
                .ToArray();
            long totalBytes = 0;
            foreach (FileInfo entry in entries)
                totalBytes = totalBytes > long.MaxValue - entry.Length
                    ? long.MaxValue
                    : totalBytes + entry.Length;
            if (totalBytes <= AssetCacheMaxBytes) return;

            foreach (FileInfo entry in entries
                         .OrderBy(entry => entry.LastWriteTimeUtc)
                         .ThenBy(entry => entry.Name, StringComparer.Ordinal))
            {
                if (totalBytes <= AssetCacheMaxBytes) break;
                long length = entry.Length;
                try
                {
                    entry.Delete();
                    totalBytes = Math.Max(0, totalBytes - length);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
