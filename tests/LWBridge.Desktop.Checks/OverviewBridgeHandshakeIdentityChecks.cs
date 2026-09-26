using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeHandshakeIdentityChecks
{
    internal static JsonElement Run()
    {
        Check(LWBridgeControlPipeHandshakeContract.EnvelopeValidatorRva == 0x3C40B8,
            "envelope validator RVA must remain pinned");
        Check(LWBridgeControlPipeHandshakeContract.GetNamedPipeClientProcessIdCallRva == 0x3C4283 &&
              LWBridgeControlPipeHandshakeContract.ClientPidCompareRva == 0x3C4291,
            "OS client PID query/equality gate must remain pinned");
        Check(LWBridgeControlPipeHandshakeContract.OpenProcessCallRva == 0x3C42EB &&
              LWBridgeControlPipeHandshakeContract.QueryFullProcessImageNameCallRva == 0x3C432D,
            "client process image verification calls must remain pinned");
        Check(LWBridgeControlPipeHandshakeContract.RegistryAdmissionCallRva == 0x0E41D0 &&
              LWBridgeControlPipeHandshakeContract.RegistryAdmissionRva == 0x3C47AB,
            "hello registry admission call must remain pinned");
        Check(LWBridgeControlPipeHandshakeContract.TokenHashBytes == 32,
            "token admission must remain a 32-byte SHA-256 comparison");

        Check(!LWBridgeControlPipeHandshakeContract.IsValidClaimedPid(0),
            "pid zero is rejected");
        Check(LWBridgeControlPipeHandshakeContract.IsValidClaimedPid(1),
            "pid one is inside the recovered range");
        Check(LWBridgeControlPipeHandshakeContract.IsValidClaimedPid(uint.MaxValue),
            "UInt32 max is inside the recovered range");
        Check(!LWBridgeControlPipeHandshakeContract.IsValidClaimedPid((long)uint.MaxValue + 1),
            "pid above UInt32 max is rejected");

        Check(LWBridgeControlPipeHandshakeContract.ClientPidMatches(4321, 4321),
            "claimed PID must match the OS pipe-client PID");
        Check(!LWBridgeControlPipeHandshakeContract.ClientPidMatches(4321, 4322),
            "PID mismatch is rejected");
        Check(LWBridgeControlPipeHandshakeContract.BuildIdMatches("build-a", "build-a") &&
              !LWBridgeControlPipeHandshakeContract.BuildIdMatches("build-a", "BUILD-A"),
            "buildId comparison remains exact byte/ordinal equality");

        Check(LWBridgeControlPipeHandshakeContract.RequiresClientImagePathVerification,
            "original handshake requires client image verification");
        Check(LWBridgeControlPipeHandshakeContract.ExactClientImageNormalizationRecovered,
            "exact client image normalization is source-backed by R7-114");

        string repo = FindRepoRoot();
        string hostSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeControlPipeHostState.cs"));
        string windowSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeWindow.cs"));
        Check(hostSource.Contains("StartRpcTransport(", StringComparison.Ordinal) &&
              hostSource.Contains("CanonicalizeExpectedClientPath", StringComparison.Ordinal),
            "shared host must retain the now-proven handshake/client-path composition");
        Check(windowSource.Contains("StartRpcTransport(", StringComparison.Ordinal) &&
              windowSource.Contains(
                  "enableBridgeControlPipeLaunchBinding: true",
                  StringComparison.Ordinal),
            "normal application composition starts the recovered listener and enables launch binding");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-113",
            recovered = new
            {
                envelopeFields = new[]
                {
                    "version", "type", "profileId", "instanceId",
                    "requestId", "timestamp", "payload",
                },
                helloFields = new[] { "profileId", "payload.token", "payload.pid", "payload.buildId" },
                helloType = "hello",
                claimedPidRange = "1..UInt32.MaxValue",
                clientPidEqualityRequired = true,
                clientImagePathVerificationRequired = true,
                buildIdExactMatchRequired = true,
                tokenHashBytes = LWBridgeControlPipeHandshakeContract.TokenHashBytes,
                registryAdmission = "instanceId + profileId + first-claim deadline + SHA-256(token)",
            },
            boundary = new
            {
                exactClientImageNormalizationRecovered = true,
                nativeHandshakeImplementedInSharedHost = true,
                normalWindowStartsSharedHostTransport = true,
                productionHostAcceptsAuthenticatedClients = true,
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
                "Overview bridge handshake identity check failed: " + message);
    }
}
