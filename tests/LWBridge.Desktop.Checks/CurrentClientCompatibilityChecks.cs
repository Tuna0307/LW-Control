using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class CurrentClientCompatibilityChecks
{
    private const string PackageA = "070f30914c671fc7c7469c0e0c11007be87d287ff9cdb4ece98fe8eec8f4c837";
    private const string PackageB = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal static void Run()
    {
        AcceptsCompatibleDynamicPackage();
        RejectsCriticalAnchorChange();
        ValidatesExactRestoreIdentity();
    }

    private static void AcceptsCompatibleDynamicPackage()
    {
        JsonElement current = CompatibleCurrent(PackageA, 17);
        string package = CurrentClientCompatibility.ValidateCurrentClient(current);
        Check(package == PackageA, "current dynamic package hash accepted");

        JsonElement futureCompatible = CompatibleCurrent(PackageB, 18);
        string futurePackage = CurrentClientCompatibility.ValidateCurrentClient(futureCompatible);
        Check(futurePackage == PackageB, "future content-only package hash accepted without pinning");
    }

    private static void RejectsCriticalAnchorChange()
    {
        JsonElement current = CompatibleCurrent(PackageA, 18, changedLuaEntry: true);
        ExpectInvalid(() => CurrentClientCompatibility.ValidateCurrentClient(current), "critical Lua entry change fails closed");
    }
    private static void ValidatesExactRestoreIdentity()
    {
        JsonElement restore = JsonSerializer.SerializeToElement(new
        {
            packageSha256 = PackageA,
            originalFiles = new { data = new { sha256 = PackageA } },
        });
        CurrentClientCompatibility.ValidateRestore(restore);

        JsonElement mismatch = JsonSerializer.SerializeToElement(new
        {
            packageSha256 = PackageB,
            originalFiles = new { data = new { sha256 = PackageA } },
        });
        ExpectInvalid(() => CurrentClientCompatibility.ValidateRestore(mismatch), "restore mismatch fails closed");
    }

    private static JsonElement CompatibleCurrent(string packageSha, int contentVersion, bool changedLuaEntry = false) =>
        JsonSerializer.SerializeToElement(new
        {
            compatibilityPolicy = CurrentClientCompatibility.Policy,
            packageSha256 = packageSha,
            packageSize = 41293716,
            packageCrc32 = 2113157353u,
            fileVersion = 3,
            contentVersion,
            entryCount = 18734,
            gameSha256 = CurrentClientCompatibility.ExpectedGameSha256,
            xluaSha256 = CurrentClientCompatibility.ExpectedXluaSha256,
            assemblyCSharpSha256 = CurrentClientCompatibility.ExpectedAssemblyCSharpSha256,
            luaEntrySha256 = changedLuaEntry
                ? "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                : CurrentClientCompatibility.ExpectedLuaEntrySha256,
            criticalEntries = new Dictionary<string, string>
            {
                ["DataCenter/Global/LuaEntry.luac"] = changedLuaEntry
                    ? "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    : CurrentClientCompatibility.ExpectedLuaEntrySha256,
                ["Global/ConstDefine.luac"] = "95e6c733b98dc641c330f36efdef044845033b37ea6703068f7c7ed5fb0048fd",
                ["Util/CSharpCallLuaInterface.luac"] = "af1559723afba0fa5773bb815c486f3233fecb3928768abd082a96b51960229e",
                ["UI/LWMainUI/Component/UIMainBottom/UIMainChangeScene.luac"] = "3843dc02869330f060a199b946ef3cd30aa335504914c2f4c60f8a824e871c5b",
            },
        });

    private static void ExpectInvalid(Action action, string name)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Current-client compatibility check failed: " + name);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException("Current-client compatibility check failed: " + name);
    }
}
