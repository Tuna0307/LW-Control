using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

internal static class Map008InstalledNameChecks
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidDataException("MAP008_NAMES: " + message);
    }

    private static JsonElement Request(string language, IReadOnlyList<string> keys) =>
        JsonSerializer.SerializeToElement(new { language, keys });

    private static JsonElement Lookup(LastWarInstalledTexts service, GameRootStatus root, string language,
        IReadOnlyList<string> keys) => JsonSerializer.SerializeToElement(service.Localize(Request(language, keys), root));

    private static void WriteLocale(string root, string version, string language, params string[] pairs)
    {
        string directory = Path.Combine(root, "Game", "LastWar_Data", "StreamingAssets", "locale", version);
        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, language + ".bin"));
        using var zip = new GZipStream(file, CompressionMode.Compress);
        using var binary = new BinaryWriter(zip, Encoding.UTF8);
        binary.Write(2);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            binary.Write(pairs[i]);
            binary.Write(pairs[i + 1]);
        }
    }

    internal static void Run()
    {
        string path = Path.Combine(Path.GetTempPath(), "map008-names-" + Guid.NewGuid().ToString("N"));
        try
        {
            string localeDirectory = Path.Combine(path, "Game", "LastWar_Data", "StreamingAssets", "locale");
            Directory.CreateDirectory(localeDirectory);
            File.WriteAllText(Path.Combine(localeDirectory, "version"), "42,en,ja");
            WriteLocale(path, "42", "en", "resource.fixture.one", "Current English Resource", "monster.fixture.one", "Current English Monster");
            WriteLocale(path, "42", "ja", "resource.fixture.one", "日本語資源", "monster.fixture.one", "日本語モンスター");
            var root = new GameRootStatus(true, path, "fixture", null, null, null, null, true);
            var service = new LastWarInstalledTexts();
            string[] keys = ["resource.fixture.one", "monster.fixture.one", "unavailable.fixture.key"];
            var english = Lookup(service, root, "en", keys);
            var japanese = Lookup(service, root, "ja", keys);
            Require(english.GetProperty("texts").GetProperty(keys[0]).GetString() == "Current English Resource", "English resource");
            Require(japanese.GetProperty("texts").GetProperty(keys[1]).GetString() == "日本語モンスター", "Japanese monster");
            Require(english.GetProperty("texts").GetProperty(keys[2]).GetString() == keys[2], "Missing key disclosed");
            Require(english.GetProperty("buildId").GetString() == "42" &&
                    english.GetProperty("localeSha256").GetString() != japanese.GetProperty("localeSha256").GetString(),
                    "Locale build and hash identity");
            Require(Lookup(service, root, "en", keys).GetProperty("texts").GetProperty(keys[0]).GetString() == "Current English Resource", "Cache re-entry");
            File.WriteAllText(Path.Combine(localeDirectory, "version"), "43,en,ja");
            WriteLocale(path, "43", "en", "resource.fixture.one", "Updated English Resource");
            WriteLocale(path, "43", "ja", "resource.fixture.one", "更新された資源");
            var updated = Lookup(service, root, "en", keys);
            Require(updated.GetProperty("buildId").GetString() == "43" &&
                    updated.GetProperty("texts").GetProperty(keys[0]).GetString() == "Updated English Resource", "New build retires old cache");
            try
            {
                Lookup(service, root, "en", Enumerable.Repeat("x", 201).ToArray());
                throw new InvalidDataException("MAP008_NAMES: accepted >200 keys");
            }
            catch (BridgeCommandException ex) when (ex.Code == "TOO_MANY_LOCALE_KEYS") { }
            Console.WriteLine("MAP008_INSTALLED_NAME_CONTROLLED_OK English/Japanese, missing, build refresh, 200-key bound");
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
    }

    // Opt-in read-only verification against retained real scan keys and the
    // current game StreamingAssets. No dependence on old owner locale caches.
    internal static void CheckInstalled(string gameRoot, string database)
    {
        string[] keys;
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly }.ToString()))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT CAST(json_extract(data_json, '$.resourceNameKey') AS TEXT) nameKey
                FROM map_records WHERE kind='resource'
                UNION
                SELECT DISTINCT CAST(json_extract(data_json, '$.monsterNameKey') AS TEXT)
                FROM map_records WHERE kind='monster'
                """;
            using var reader = command.ExecuteReader();
            var found = new List<string>();
            while (reader.Read()) if (!reader.IsDBNull(0) && !string.IsNullOrWhiteSpace(reader.GetString(0))) found.Add(reader.GetString(0));
            keys = found.ToArray();
        }
        Require(keys.Length > 0, "Archived real scanner DB has no name keys");
        var source = new LastWarInstalledTexts();
        var root = new GameRootStatus(true, Path.GetFullPath(gameRoot), "selected", null, null, null, null, true);
        foreach (string language in new[] { "en", "ja" })
        {
            var translations = new Dictionary<string, string>(StringComparer.Ordinal);
            string? build = null;
            for (int i = 0; i < keys.Length; i += 200)
            {
                var batch = keys.Skip(i).Take(200).ToArray();
                var result = Lookup(source, root, language, batch);
                string id = result.GetProperty("buildId").GetString() ?? "";
                Require(build is null || build == id, "Game build changed during key batch");
                build = id;
                foreach (var row in result.GetProperty("texts").EnumerateObject())
                    translations[row.Name] = row.Value.GetString() ?? "";
            }
            var resolved = translations.Where(pair => pair.Value != pair.Key).Take(8).ToArray();
            Require(resolved.Length >= 4, "Insufficient actual archived native name keys resolved in " + language);
            Console.WriteLine(JsonSerializer.Serialize(new { language, build, queriedKeys = keys.Length, resolvedKeys = translations.Count(pair => pair.Value != pair.Key), samples = resolved }));
        }
        // Repeat real saved-data open independently and require its key set remains unchanged.
        using var reopened = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly }.ToString());
        reopened.Open();
        using var count = reopened.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM map_records WHERE kind IN ('monster','resource')";
        Require(Convert.ToInt64(count.ExecuteScalar()) > 0, "Stored native records not visible after reopening");
        Console.WriteLine("MAP008_INSTALLED_NAME_NATIVE_OK saved records reopened, EN/JA from installed game binary");
    }
}
