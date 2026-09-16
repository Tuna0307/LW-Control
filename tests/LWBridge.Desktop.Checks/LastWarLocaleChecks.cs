using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LastWarLocaleChecks
{
    public static async Task<IReadOnlyList<string>> RunAsync()
    {
        var failures = new List<string>();
        var roots = new List<string>();

        void Check(bool condition, string name)
        {
            if (!condition) failures.Add("LastWar locale: " + name);
        }

        void Expect(string code, string name, Action action)
        {
            try
            {
                action();
                failures.Add("LastWar locale: " + name);
            }
            catch (BridgeCommandException ex)
            {
                Check(ex.Code == code, $"{name} (expected {code}, got {ex.Code})");
            }
        }

        string NewRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "lwbridge-locale-check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            roots.Add(root);
            return root;
        }

        try
        {
            string root = NewRoot();
            WriteValidCache(root);
            var service = new LastWarLocaleService(root);
            using JsonDocument request = JsonDocument.Parse("""
                {"language":"zh-CN","keys":["1","2","3"]}
                """);
            IReadOnlyDictionary<string, string> localized = service.Localize(request.RootElement);
            Check(localized["1"] == "ZH One", "requested language wins");
            Check(localized["2"] == "English Two", "missing translation falls back to English");
            Check(localized["3"] == "3", "missing requested and English value echoes key");

            using JsonDocument defaultRequest = JsonDocument.Parse("""{"keys":["1"]}""");
            IReadOnlyDictionary<string, string> defaultLocalized = service.Localize(defaultRequest.RootElement);
            Check(defaultLocalized["1"] == "English One", "missing language defaults to English");

            var backend = new LWBridgeBackend(lastWarLocales: new LastWarLocaleService(root));
            object? backendResult = await backend.InvokeAsync("lastwar_localize", request.RootElement, CancellationToken.None);
            var backendValues = backendResult as IReadOnlyDictionary<string, string>;
            Check(backendValues is not null && backendValues["1"] == "ZH One", "backend command uses locale service");

            using JsonDocument twoHundred = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                language = "en",
                keys = Enumerable.Range(0, LastWarLocaleService.MaxKeys).Select(i => i.ToString()).ToArray(),
            }));
            Check(service.Localize(twoHundred.RootElement).Count == LastWarLocaleService.MaxKeys,
                "200 keys are accepted");

            using JsonDocument tooMany = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                language = "en",
                keys = Enumerable.Range(0, LastWarLocaleService.MaxKeys + 1).Select(i => i.ToString()).ToArray(),
            }));
            Expect("TOO_MANY_LOCALE_KEYS", "201 keys are rejected", () => service.Localize(tooMany.RootElement));

            using JsonDocument unsupported = JsonDocument.Parse("""{"language":"fr","keys":["1"]}""");
            Expect("UNSUPPORTED_LOCALE", "unsupported language is rejected", () => service.Localize(unsupported.RootElement));

            string missingRoot = NewRoot();
            using JsonDocument englishRequest = JsonDocument.Parse("""{"language":"en","keys":["1"]}""");
            Expect("STATE_UNAVAILABLE", "missing cache fails closed",
                () => new LastWarLocaleService(missingRoot).Localize(englishRequest.RootElement));

            string badManifestRoot = NewRoot();
            File.WriteAllText(Path.Combine(badManifestRoot, "manifest.json"), """{"version":99,"format":"json-gzip","languages":{}}""");
            Expect("INVALID_LOCALE_MANIFEST", "invalid manifest fails closed",
                () => new LastWarLocaleService(badManifestRoot).Localize(englishRequest.RootElement));

            string hashRoot = NewRoot();
            WriteValidCache(hashRoot);
            string enPath = Path.Combine(hashRoot, "en.json.gz");
            byte[] tampered = File.ReadAllBytes(enPath);
            tampered[^1] ^= 0x01;
            File.WriteAllBytes(enPath, tampered);
            Expect("LOCALE_VERIFY_FAILED", "hash mismatch fails closed",
                () => new LastWarLocaleService(hashRoot).Localize(englishRequest.RootElement));

            string gzipRoot = NewRoot();
            LocaleAsset badGzip = WriteRaw(gzipRoot, "en.json.gz", new byte[] { 1, 2, 3, 4, 5 });
            WriteManifest(gzipRoot, badGzip, null);
            Expect("INVALID_LOCALE", "invalid gzip fails closed",
                () => new LastWarLocaleService(gzipRoot).Localize(englishRequest.RootElement));

            string payloadRoot = NewRoot();
            LocaleAsset nonObject = WritePayload(payloadRoot, "en.json.gz", new[] { "not", "an", "object" });
            WriteManifest(payloadRoot, nonObject, null);
            Expect("INVALID_LOCALE", "non-object locale payload fails closed",
                () => new LastWarLocaleService(payloadRoot).Localize(englishRequest.RootElement));
        }
        finally
        {
            foreach (string root in roots)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // Deterministic checks should report behavioral failures, not cleanup races.
                }
            }
        }

        return failures;
    }

    public static async Task RunLocalCacheProofAsync()
    {
        var backend = new LWBridgeBackend();
        string[] keys = ["2000001", "2000003", "2000005"];

        using JsonDocument english = JsonDocument.Parse(JsonSerializer.Serialize(new { language = "en", keys }));
        using JsonDocument chinese = JsonDocument.Parse(JsonSerializer.Serialize(new { language = "zh-CN", keys }));
        object? englishResult = await backend.InvokeAsync("lastwar_localize", english.RootElement, CancellationToken.None);
        object? chineseResult = await backend.InvokeAsync("lastwar_localize", chinese.RootElement, CancellationToken.None);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            english = englishResult,
            simplifiedChinese = chineseResult,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteValidCache(string root)
    {
        LocaleAsset english = WritePayload(root, "en.json.gz", new Dictionary<string, string>
        {
            ["1"] = "English One",
            ["2"] = "English Two",
        });
        LocaleAsset chinese = WritePayload(root, "zh-CN.json.gz", new Dictionary<string, string>
        {
            ["1"] = "ZH One",
        });
        WriteManifest(root, english, chinese);
    }

    private static LocaleAsset WritePayload(string root, string fileName, object payload)
    {
        string path = Path.Combine(root, fileName);
        using (FileStream file = File.Create(path))
        using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
        {
            JsonSerializer.Serialize(gzip, payload);
        }
        return Describe(path, fileName);
    }

    private static LocaleAsset WriteRaw(string root, string fileName, byte[] payload)
    {
        string path = Path.Combine(root, fileName);
        File.WriteAllBytes(path, payload);
        return Describe(path, fileName);
    }

    private static LocaleAsset Describe(string path, string fileName)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return new LocaleAsset(
            fileName,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static void WriteManifest(string root, LocaleAsset english, LocaleAsset? chinese)
    {
        var languages = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["en"] = new { gameCode = "en", file = english.File, size = english.Size, sha256 = english.Sha256 },
        };
        if (chinese is not null)
        {
            languages["zh-CN"] = new
            {
                gameCode = "zh_CN",
                file = chinese.File,
                size = chinese.Size,
                sha256 = chinese.Sha256,
            };
        }

        File.WriteAllText(
            Path.Combine(root, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                version = 1,
                sourceVersion = "test",
                format = "json-gzip",
                keyCount = 2,
                languages,
            }));
    }

    private sealed record LocaleAsset(string File, long Size, string Sha256);
}
