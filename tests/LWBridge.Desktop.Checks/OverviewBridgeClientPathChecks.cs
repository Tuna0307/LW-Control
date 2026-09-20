using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeClientPathChecks
{
    internal static JsonElement Run()
    {
        Check(LWBridgeControlPipeClientPathContract.Utf16ToUtf8LossyRva == 0x029810 &&
              LWBridgeControlPipeClientPathContract.Utf8LossyViewRva == 0x0291F0,
            "lossy UTF conversion helpers must remain pinned");

        Check(LWBridgeControlPipeClientPathContract.GetFullPathNameCallRva == 0x5BC6A6 &&
              LWBridgeControlPipeClientPathContract.GetFinalPathNameCallRva == 0x5B7258 &&
              LWBridgeControlPipeClientPathContract.CreateFileCallRva == 0x5B868A,
            "Windows canonicalization API callsites must remain pinned");

        Check(LWBridgeControlPipeClientPathContract.DesiredAccess == 0 &&
              LWBridgeControlPipeClientPathContract.ShareMode == 7 &&
              LWBridgeControlPipeClientPathContract.OpenExisting == 3 &&
              LWBridgeControlPipeClientPathContract.FileFlagBackupSemantics == 0x02000000 &&
              LWBridgeControlPipeClientPathContract.GetFinalPathNameFlags == 0,
            "final-path handle open policy must remain exact");

        Check(LWBridgeControlPipeClientPathContract.VerbatimDosPrefix == @"\\?\" &&
              LWBridgeControlPipeClientPathContract.VerbatimUncPrefix == @"\\?\UNC\",
            "verbatim DOS and UNC prefixes must remain exact");

        byte[] upper = Encoding.UTF8.GetBytes(@"C:\Games\LastWar\xLua.dll");
        byte[] lower = Encoding.UTF8.GetBytes(@"c:\games\lastwar\xlua.dll");
        Check(LWBridgeControlPipeClientPathContract.AsciiCaseInsensitiveUtf8PathEquals(upper, lower),
            "final comparison folds ASCII A-Z");

        byte[] nonAsciiA = Encoding.UTF8.GetBytes(@"C:\路径\xLua.dll");
        byte[] nonAsciiB = Encoding.UTF8.GetBytes(@"c:\路径\xlua.dll");
        Check(LWBridgeControlPipeClientPathContract.AsciiCaseInsensitiveUtf8PathEquals(nonAsciiA, nonAsciiB),
            "non-ASCII UTF-8 bytes remain byte-equal while ASCII path bytes fold");

        byte[] changedUnicode = Encoding.UTF8.GetBytes(@"C:\路徑\xLua.dll");
        Check(!LWBridgeControlPipeClientPathContract.AsciiCaseInsensitiveUtf8PathEquals(nonAsciiA, changedUnicode),
            "non-ASCII bytes are not culture-folded");

        Check(!LWBridgeControlPipeClientPathContract.AsciiCaseInsensitiveUtf8PathEquals(
                Encoding.UTF8.GetBytes("abc"),
                Encoding.UTF8.GetBytes("abcd")),
            "path length mismatch rejects before byte folding");

        Check(LWBridgeControlPipeHandshakeContract.ExactClientImageNormalizationRecovered,
            "handshake contract marks exact client-image normalization recovered only after R7-114");

        Check(
            LWBridgeControlPipeClientPathContract.ExpectedGameExecutableBuilderRva == 0x43365C &&
            LWBridgeControlPipeClientPathContract.GameComponentLiteralRefRva == 0x433687 &&
            LWBridgeControlPipeClientPathContract.LastWarExecutableLiteralRefRva == 0x4336C4 &&
            LWBridgeControlPipeClientPathContract.HostConstructorCallRva == 0x4100B5 &&
            LWBridgeControlPipeClientPathContract.HostExpectedClientCanonicalizationCallRva == 0x3C3654,
            "R7-122 game executable builder and host constructor callsites remain pinned");
        Check(
            LWBridgeControlPipeClientPathContract.BuildExpectedGameExecutablePath(
                @"C:\Games\LastWarRoot") ==
                @"C:\Games\LastWarRoot\Game\LastWar.exe",
            "R7-122 expected pipe-client image is <gameRoot>\\Game\\LastWar.exe");

        string repo = FindRepoRoot();
        string hostSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeControlPipeHostState.cs"));
        string windowSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeWindow.cs"));
        Check(hostSource.Contains(
                  "CanonicalizeExpectedClientPath",
                  StringComparison.Ordinal),
            "shared host must apply the recovered expected-client-path canonicalization");
        Check(windowSource.Contains(
                  "StartRpcTransport(",
                  StringComparison.Ordinal) &&
              windowSource.Contains(
                  "BuildExpectedGameExecutablePath",
                  StringComparison.Ordinal),
            "normal application composition starts the listener with the recovered expected game executable path");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-114",
            recovered = new
            {
                queryImageOutput = "UTF-16 -> lossy UTF-8",
                pathPreparation = "UTF-8 -> UTF-16 NUL-terminated; embedded NUL rejected",
                lexicalNormalization = "GetFullPathNameW + explicit Win32 verbatim DOS/UNC handling",
                canonicalOpen = new
                {
                    desiredAccess = 0,
                    shareMode = 7,
                    creationDisposition = 3,
                    flagsAndAttributes = "0x02000000",
                },
                filesystemCanonicalization = "GetFinalPathNameByHandleW(flags=0)",
                finalEncoding = "lossy UTF-16 -> UTF-8 / valid UTF-8 lossy view",
                finalEquality = "equal length + ASCII A-Z case-folded byte comparison",
                expectedClientImage =
                    @"<gameRoot>\Game\LastWar.exe",
            },
            boundary = new
            {
                nativeHandshakeImplementedInSharedHost = true,
                sharedHostRunsClientPathGateWhenExplicitlyStarted = true,
                normalWindowStartsSharedHostTransport = true,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "task.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "src", "LWBridge.Desktop")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("could not locate LW-Control repository root");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "Overview bridge client path check failed: " + message);
    }
}
