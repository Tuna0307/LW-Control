namespace LWBridge.Desktop;

// LWB-R7-113: hello authentication gates recovered from the original host.
// Used by the live handshake in LWBridgeControlPipeIsolatedHandshake.
internal static class LWBridgeControlPipeHandshakeContract
{
    public const uint ProcessQueryLimitedInformation = 0x1000;

    public static bool IsValidClaimedPid(long pid) =>
        pid >= 1 && pid <= uint.MaxValue;

    public static bool ClientPidMatches(uint actualClientPid, long claimedPid) =>
        IsValidClaimedPid(claimedPid) && actualClientPid == (uint)claimedPid;

    public static bool BuildIdMatches(string expectedBuildId, string suppliedBuildId) =>
        string.Equals(expectedBuildId, suppliedBuildId, StringComparison.Ordinal);
}
