using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class LastWarLocaleService
{
    internal const int MaxKeys = 200;
    private const int ManifestVersion = 1;
    private const string ManifestFormat = "json-gzip";

    private readonly string cacheDirectory;
    private readonly object sync = new();
    private LocaleManifest? manifest;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> loadedLocales =
        new(StringComparer.Ordinal);

    public LastWarLocaleService(string? cacheDirectory = null)
    {
        this.cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild",
            "locales");
    }

    public IReadOnlyDictionary<string, string> Localize(JsonElement payload)
    {
        (string language, List<string> keys) = ParseRequest(payload);
        if (keys.Count > MaxKeys)
            throw new BridgeCommandException(
                "TOO_MANY_LOCALE_KEYS",
                $"too many LastWar locale keys: {keys.Count}");

        if (keys.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        lock (sync)
        {
            LocaleManifest current = manifest ??= LoadManifest();
            IReadOnlyDictionary<string, string> selected = LoadLocale(current, language);
            IReadOnlyDictionary<string, string> english = language == "en"
                ? selected
                : LoadLocale(current, "en");

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                if (selected.TryGetValue(key, out string? value))
                    result[key] = value;
                else if (english.TryGetValue(key, out value))
                    result[key] = value;
                else
                    result[key] = key;
            }
            return result;
        }
    }

    private static (string Language, List<string> Keys) ParseRequest(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw new BridgeCommandException("INVALID_PAYLOAD", "lastwar_localize payload must be an object.");

        string language = "en";
        if (payload.TryGetProperty("language", out JsonElement languageElement))
        {
            if (languageElement.ValueKind != JsonValueKind.String)
                throw new BridgeCommandException("INVALID_PAYLOAD", "lastwar_localize language must be a string.");
            language = languageElement.GetString() ?? "en";
        }

        var keys = new List<string>();
        if (!payload.TryGetProperty("keys", out JsonElement keysElement))
            return (language, keys);
        if (keysElement.ValueKind != JsonValueKind.Array)
            throw new BridgeCommandException("INVALID_PAYLOAD", "lastwar_localize keys must be an array.");

        foreach (JsonElement keyElement in keysElement.EnumerateArray())
        {
            if (keyElement.ValueKind != JsonValueKind.String)
                throw new BridgeCommandException("INVALID_PAYLOAD", "lastwar_localize keys must contain only strings.");
            keys.Add(keyElement.GetString() ?? string.Empty);
        }
        return (language, keys);
    }

    private LocaleManifest LoadManifest()
    {
        string path = Path.Combine(cacheDirectory, "manifest.json");
        if (!File.Exists(path))
            throw new BridgeCommandException("STATE_UNAVAILABLE", "locale cache is unavailable");

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out JsonElement version) ||
                !version.TryGetInt32(out int versionNumber) || versionNumber != ManifestVersion ||
                !root.TryGetProperty("format", out JsonElement format) ||
                format.ValueKind != JsonValueKind.String || format.GetString() != ManifestFormat ||
                !root.TryGetProperty("languages", out JsonElement languages) ||
                languages.ValueKind != JsonValueKind.Object)
            {
                throw InvalidManifest();
            }

            var entries = new Dictionary<string, LocaleManifestEntry>(StringComparer.Ordinal);
            foreach (JsonProperty language in languages.EnumerateObject())
            {
                JsonElement item = language.Value;
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("file", out JsonElement file) || file.ValueKind != JsonValueKind.String ||
                    !item.TryGetProperty("size", out JsonElement size) || !size.TryGetInt64(out long byteSize) || byteSize < 1 ||
                    !item.TryGetProperty("sha256", out JsonElement sha) || sha.ValueKind != JsonValueKind.String)
                {
                    throw InvalidManifest();
                }

                string fileName = file.GetString() ?? string.Empty;
                string digest = sha.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(language.Name) ||
                    string.IsNullOrWhiteSpace(fileName) ||
                    Path.GetFileName(fileName) != fileName ||
                    digest.Length != 64 || !digest.All(Uri.IsHexDigit))
                {
                    throw InvalidManifest();
                }
                entries.Add(language.Name, new LocaleManifestEntry(fileName, byteSize, digest.ToLowerInvariant()));
            }

            if (!entries.ContainsKey("en"))
                throw InvalidManifest();
            return new LocaleManifest(entries);
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            throw new BridgeCommandException("INVALID_LOCALE_MANIFEST", "read locale manifest", ex.Message);
        }
    }

    private IReadOnlyDictionary<string, string> LoadLocale(LocaleManifest current, string language)
    {
        if (loadedLocales.TryGetValue(language, out IReadOnlyDictionary<string, string>? cached))
            return cached;
        if (!current.Languages.TryGetValue(language, out LocaleManifestEntry? entry))
            throw new BridgeCommandException("UNSUPPORTED_LOCALE", $"unsupported LastWar locale: {language}");

        string path = Path.Combine(cacheDirectory, entry.File);
        byte[] compressed;
        try
        {
            compressed = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BridgeCommandException("INVALID_LOCALE", "read locale file", ex.Message);
        }

        if (compressed.LongLength != entry.Size)
            throw VerificationFailure(language, $"size {compressed.LongLength} != {entry.Size}");
        string actualHash = Convert.ToHexString(SHA256.HashData(compressed)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(actualHash),
            Convert.FromHexString(entry.Sha256)))
        {
            throw VerificationFailure(language, $"sha256 {actualHash} != {entry.Sha256}");
        }

        try
        {
            using var input = new MemoryStream(compressed, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using JsonDocument document = JsonDocument.Parse(gzip);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new BridgeCommandException("INVALID_LOCALE", "locale payload must be an object");

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                    throw new BridgeCommandException("INVALID_LOCALE", "locale payload values must be strings");
                values[property.Name] = property.Value.GetString() ?? string.Empty;
            }
            loadedLocales[language] = values;
            return values;
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException)
        {
            throw new BridgeCommandException("INVALID_LOCALE", "decompress locale file", ex.Message);
        }
    }

    private static BridgeCommandException InvalidManifest() =>
        new("INVALID_LOCALE_MANIFEST", "invalid LastWar locale manifest");

    private static BridgeCommandException VerificationFailure(string language, string detail) =>
        new("LOCALE_VERIFY_FAILED", $"LastWar locale verification failed: {language}", detail);

    private sealed record LocaleManifest(IReadOnlyDictionary<string, LocaleManifestEntry> Languages);
    private sealed record LocaleManifestEntry(string File, long Size, string Sha256);
}
