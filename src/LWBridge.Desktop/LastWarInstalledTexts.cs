using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

// Read the current, admitted game's own StreamingAssets locale binaries.
// The old standalone LWBridgeRebuild/locales cache is intentionally not used.
internal sealed class LastWarInstalledTexts
{
    private const int MaxLocaleBytes = 32 * 1024 * 1024;
    private readonly object sync = new();
    private readonly Dictionary<string, LocaleData> entries = new(StringComparer.OrdinalIgnoreCase);
    private sealed record LocaleData(string File, long Length, long LastWriteTicks, string Hash,
        IReadOnlyDictionary<string, string> Texts);

    public object Localize(JsonElement payload, GameRootStatus root)
    {
        (string language, List<string> keys) = LastWarLocaleService.ParseRequest(payload);
        if (keys.Count > LastWarLocaleService.MaxKeys)
            throw new BridgeCommandException("TOO_MANY_LOCALE_KEYS", "at most 200 game text keys per request");
        if (!root.Valid || string.IsNullOrWhiteSpace(root.Path))
            throw new BridgeCommandException("GAME_ROOT_INVALID", "select an installed Last War game to load names");

        string directory = Path.Combine(root.Path, "Game", "LastWar_Data", "StreamingAssets", "locale");
        string versionFile = Path.Combine(directory, "version");
        string version;
        try { version = File.ReadAllText(versionFile, Encoding.UTF8).Split(',')[0].Trim(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new BridgeCommandException("GAME_LOCALE_UNAVAILABLE", "read current game locale version", ex.Message); }
        if (version.Length is < 1 or > 16 || !version.All(char.IsAsciiDigit))
            throw new BridgeCommandException("GAME_LOCALE_INVALID", "invalid current game locale version");

        string localeCode = language switch
        {
            "zh-CN" => "zh_CN", "zh-TW" => "zh_TW",
            "en" or "ja" or "ko" or "vi" or "id" or "ru" or "pt" => language,
            _ => throw new BridgeCommandException("UNSUPPORTED_LOCALE", "unsupported current game language")
        };
        string folder = Path.Combine(directory, version);
        lock (sync)
        {
            LocaleData selected = Load(folder, localeCode);
            LocaleData english = localeCode == "en" ? selected : Load(folder, "en");
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                if (selected.Texts.TryGetValue(key, out string? value) ||
                    english.Texts.TryGetValue(key, out value))
                    texts[key] = value;
                else
                    texts[key] = key;
            }
            return new
            {
                language, buildId = version, gameRoot = root.Path,
                localeSha256 = selected.Hash, englishSha256 = english.Hash,
                texts,
                unresolved = texts.Count(pair => pair.Key == pair.Value)
            };
        }
    }

    private LocaleData Load(string folder, string code)
    {
        string file = Path.Combine(folder, code + ".bin");
        FileInfo info = new(file);
        if (!info.Exists || info.Length is < 1 or > MaxLocaleBytes)
            throw new BridgeCommandException("GAME_LOCALE_UNAVAILABLE", "current game text locale missing: " + code);
        string key = file;
        if (entries.TryGetValue(key, out LocaleData? cached) &&
            cached.Length == info.Length && cached.LastWriteTicks == info.LastWriteTimeUtc.Ticks)
            return cached;
        try
        {
            byte[] compressed = File.ReadAllBytes(file);
            string hash = Convert.ToHexString(SHA256.HashData(compressed));
            using var zip = new GZipStream(new MemoryStream(compressed, writable: false), CompressionMode.Decompress);
            using var raw = new MemoryStream();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = zip.Read(buffer, 0, buffer.Length)) != 0)
            {
                if (raw.Length + count > MaxLocaleBytes)
                    throw new InvalidDataException("Game locale exceeds supported size");
                raw.Write(buffer, 0, count);
            }
            raw.Position = 0;
            using var reader = new BinaryReader(raw, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadInt32() != 2)
                throw new InvalidDataException("Unsupported game locale binary format");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            while (raw.Position < raw.Length)
            {
                string textKey = reader.ReadString();
                string value = reader.ReadString();
                if (string.IsNullOrEmpty(textKey) || !values.TryAdd(textKey, value))
                    throw new InvalidDataException("Invalid duplicate/empty game text key");
            }
            var loaded = new LocaleData(file, compressed.LongLength, info.LastWriteTimeUtc.Ticks, hash, values);
            if (entries.Count >= 4) entries.Clear();
            entries[key] = loaded;
            return loaded;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or
                                    EndOfStreamException or ArgumentException)
        { throw new BridgeCommandException("GAME_LOCALE_INVALID", "read current installed game texts: " + code, ex.Message); }
    }
}
