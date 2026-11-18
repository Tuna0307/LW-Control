using System.Text.Json;

namespace LWBridge.Desktop;

// IMPLEMENTATION POLICY: compatible Lua-content package updates may advance
// without a new hard-coded package hash only while every recovered runtime and
// critical Lua anchor below remains exact. Core/critical changes fail closed.
internal static class CurrentClientCompatibility
{
    internal const string Policy = "lwbridge-current-client-critical-anchors-5";
    internal const string ExpectedGameSha256 = "905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d";
    internal const string ExpectedXluaSha256 = "d22d912f031c60f2649fdaf76d359d695511f7a37b93cd637b557f8346569d45";
    internal const string ExpectedAssemblyCSharpSha256 = "bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e";
    internal const string ExpectedLuaEntrySha256 = "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137";
    private const string V36PackageSha256 = "1f468c90d3a6918df3b3c3f80c613df8ace50f16d12e649a36a249474f70fe13";
    private static readonly (string, string, string) Runtime1106 =
        ("4bd47493eb3fda5d806c0df973839a96c41f8807596505762b54af7391009666",
         "43c33c9311570bb4161d1e61aefd0ff04148709e365332e8f7dd45d39a920a09",
         "dec38a33a63ab2d29e02adbfa2032c4ab3f7f910443cba62a1f8895b809e80d9");

    private static readonly IReadOnlyDictionary<string, string> CriticalEntries =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DataCenter/Global/LuaEntry.luac"] = ExpectedLuaEntrySha256,
            ["Global/ConstDefine.luac"] = "95e6c733b98dc641c330f36efdef044845033b37ea6703068f7c7ed5fb0048fd",
            ["Util/CSharpCallLuaInterface.luac"] = "af1559723afba0fa5773bb815c486f3233fecb3928768abd082a96b51960229e",
            ["UI/LWMainUI/Component/UIMainBottom/UIMainChangeScene.luac"] = "3843dc02869330f060a199b946ef3cd30aa335504914c2f4c60f8a824e871c5b",
        };

    private static readonly IReadOnlyDictionary<string, string> V36CriticalEntries =
        new Dictionary<string, string>(CriticalEntries, StringComparer.Ordinal)
        {
            ["Global/ConstDefine.luac"] = "c733d10341290e52265da1a1266fca8e90b675ac8447e18bd5131158f3ebe314",
            ["Util/CSharpCallLuaInterface.luac"] = "d3d3ad22fa0c6fd3cf09fd2639de99dcbf0bd27b19656651d55d8af22de778c0",
        };

    internal static string ValidateCurrentClient(JsonElement current)
    {
        if (current.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Current-client compatibility evidence is missing.");
        RequireString(current, "compatibilityPolicy", Policy);
        var runtime = (RequiredSha256(current, "gameSha256"),
            RequiredSha256(current, "xluaSha256"), RequiredSha256(current, "assemblyCSharpSha256"));
        if (runtime != (ExpectedGameSha256, ExpectedXluaSha256, ExpectedAssemblyCSharpSha256) &&
            runtime != Runtime1106)
            throw new BridgeCommandException("GAME_UPDATE_UNSUPPORTED",
                "Last War installed a runtime update that needs compatibility qualification.",
                new { gameSha256 = runtime.Item1, xluaSha256 = runtime.Item2, assemblyCSharpSha256 = runtime.Item3 });
        RequireString(current, "luaEntrySha256", ExpectedLuaEntrySha256);
        if (!current.TryGetProperty("fileVersion", out JsonElement fileVersion) ||
            !fileVersion.TryGetInt32(out int parsedFileVersion) || parsedFileVersion != 3)
            throw new InvalidDataException("Current-client LWLF file version is unsupported.");
        if (!current.TryGetProperty("contentVersion", out JsonElement contentVersion) ||
            !contentVersion.TryGetInt32(out int parsedContentVersion) || parsedContentVersion <= 0)
            throw new InvalidDataException("Current-client content version is invalid.");
        if (!current.TryGetProperty("packageSize", out JsonElement packageSize) ||
            !packageSize.TryGetInt64(out long parsedPackageSize) || parsedPackageSize <= 0)
            throw new InvalidDataException("Current-client package size is invalid.");
        if (!current.TryGetProperty("packageCrc32", out JsonElement packageCrc) ||
            !packageCrc.TryGetUInt32(out _))
            throw new InvalidDataException("Current-client package CRC is invalid.");
        if (!current.TryGetProperty("entryCount", out JsonElement entryCount) ||
            !entryCount.TryGetInt32(out int parsedEntryCount) || parsedEntryCount <= 0)
            throw new InvalidDataException("Current-client package entry count is invalid.");

        string packageSha256 = RequiredSha256(current, "packageSha256");
        if (!current.TryGetProperty("criticalEntries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Current-client critical-entry evidence is missing.");
        var expectedEntries = runtime == Runtime1106 && parsedContentVersion == 36 && packageSha256 == V36PackageSha256
            ? V36CriticalEntries : CriticalEntries;
        foreach ((string name, string expected) in expectedEntries)
        {
            RequireString(entries, name, expected);
        }
        return packageSha256;
    }
    internal static void ValidateRestore(JsonElement restore)
    {
        if (restore.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Overview restore evidence is missing.");
        string restoredPackage = RequiredSha256(restore, "packageSha256");
        if (!restore.TryGetProperty("originalFiles", out JsonElement originals) || originals.ValueKind != JsonValueKind.Object ||
            !originals.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Overview restore evidence is missing the exact original package identity.");
        string originalPackage = RequiredSha256(data, "sha256");
        if (!string.Equals(restoredPackage, originalPackage, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Overview restore package does not match the exact backed-up original package.");
    }

    private static string RequiredSha256(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Compatibility field '{name}' is missing.");
        string? text = value.GetString();
        if (text is null || text.Length != 64 || text.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException($"Compatibility field '{name}' is not a SHA-256 value.");
        return text.ToLowerInvariant();
    }

    private static void RequireString(JsonElement root, string name, string expected)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String ||
            !string.Equals(value.GetString(), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Compatibility field '{name}' changed from the recovered supported value.");
    }
}
