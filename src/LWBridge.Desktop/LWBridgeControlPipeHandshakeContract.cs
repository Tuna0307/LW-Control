namespace LWBridge.Desktop;

// LWB-R7-113: source-backed original hello authentication gates. This file is
// a contract model only. Native framed hello I/O is still intentionally absent
// from the production host until client-image normalization is fully recovered.
internal static class LWBridgeControlPipeHandshakeContract
{
    public const int EnvelopeValidatorRva = 0x3C40B8;
    public const int ConnectionFutureRva = 0x0E2DB5;

    public const int HelloProfileIdLookupRva = 0x0E3B2F;
    public const int HelloTokenLookupRva = 0x0E3B77;
    public const int HelloPidLookupRva = 0x0E3BC0;
    public const int HelloBuildIdLookupRva = 0x0E3BFE;
    public const int HelloTypeLookupRva = 0x0E3C74;
    public const int HelloTypeLiteralCheckRva = 0x0E3CA7;
    public const int HelloPidRangeCheckRva = 0x0E3CED;

    public const int BuildIdLengthCompareRva = 0x0E3F87;
    public const int BuildIdBytesCompareCallRva = 0x0E3F98;

    public const int ClientProcessVerifierRva = 0x3C424B;
    public const int GetNamedPipeClientProcessIdCallRva = 0x3C4283;
    public const int ClientPidCompareRva = 0x3C4291;
    public const int OpenProcessCallRva = 0x3C42EB;
    public const int QueryFullProcessImageNameCallRva = 0x3C432D;
    public const int ClientImageCaseFoldCompareStartRva = 0x3C4538;
    public const int ClientImageCaseFoldCompareEndRva = 0x3C4584;
    public const int ClientProcessVerifierCallRva = 0x0E3FD7;

    public const int RegistryAdmissionRva = 0x3C47AB;
    public const int RegistryAdmissionCallRva = 0x0E41D0;
    public const int TokenHashCompareLoopEndRva = 0x3C485A;
    public const int ClaimedWriteRva = 0x3C48A4;
    public const int GenerationIncrementRva = 0x3C48AA;

    public const uint ProcessQueryLimitedInformation = 0x1000;
    public const int TokenHashBytes = 32;

    public static bool IsValidClaimedPid(long pid) =>
        pid >= 1 && pid <= uint.MaxValue;

    public static bool ClientPidMatches(uint actualClientPid, long claimedPid) =>
        IsValidClaimedPid(claimedPid) && actualClientPid == (uint)claimedPid;

    public static bool BuildIdMatches(string expectedBuildId, string suppliedBuildId) =>
        string.Equals(expectedBuildId, suppliedBuildId, StringComparison.Ordinal);

    public const bool RequiresClientImagePathVerification = true;
    public const bool ExactClientImageNormalizationRecovered = false;
}
