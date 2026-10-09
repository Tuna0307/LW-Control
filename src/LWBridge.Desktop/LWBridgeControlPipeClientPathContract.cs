namespace LWBridge.Desktop;

// LWB-R7-114: source-backed client-process image normalization used by the
// original control-pipe hello verifier. This remains a contract/model layer;
// production native handshake I/O is not enabled by this class.
internal static class LWBridgeControlPipeClientPathContract
{
    public const int Utf16ToUtf8LossyRva = 0x029810;
    public const int Utf8LossyViewRva = 0x0291F0;

    public const int CanonicalizationWrapperRva = 0x5B6630;
    public const int Utf8ToUtf16NullTerminatedRva = 0x5BB940;
    public const int FullPathNormalizerRva = 0x5BC4C0;
    public const int GetFullPathNameCallRva = 0x5BC6A6;
    public const int VerbatimDosPrefixRva = 0x5BC7CA;
    public const int VerbatimUncPrefixRva = 0x5BC832;

    public const int FinalPathCanonicalizerRva = 0x5B70D0;
    public const int FixedOpenOptionsLoadRva = 0x5B7119;
    public const int GetFinalPathNamePointerLoadRva = 0x5B719F;
    public const int GetFinalPathNameCallRva = 0x5B7258;
    public const int GetFinalPathNameFlagsLoadRva = 0x5B7255;
    public const int FinalPathUtf16ToUtf8CallRva = 0x5B7334;

    public const int CreateFileOptionsWrapperRva = 0x5B8540;
    public const int CreateFileCallRva = 0x5B868A;
    public const uint DesiredAccess = 0;
    public const uint ShareMode = 0x00000007;
    public const uint OpenExisting = 3;
    public const uint FileFlagBackupSemantics = 0x02000000;
    public const uint GetFinalPathNameFlags = 0;

    public const int FinalLengthCompareRva = 0x3C4538;
    public const int FinalAsciiCaseFoldCompareStartRva = 0x3C4551;
    public const int FinalAsciiCaseFoldCompareEndRva = 0x3C4584;

    public const int ExpectedGameExecutableBuilderRva = 0x43365C;
    public const int GameComponentLiteralRefRva = 0x433687;
    public const int GameComponentAppendCallRva = 0x433697;
    public const int LastWarExecutableLiteralRefRva = 0x4336C4;
    public const int LastWarExecutableAppendCallRva = 0x4336D4;
    public const int HostConstructorCallRva = 0x4100B5;
    public const int HostConstructorRva = 0x3C30BE;
    public const int HostExpectedClientCanonicalizationCallRva = 0x3C3654;

    public const string GameComponent = "Game";
    public const string LastWarExecutableName = "LastWar.exe";
    public const string VerbatimDosPrefix = @"\\?\";
    public const string VerbatimUncPrefix = @"\\?\UNC\";

    public static string BuildExpectedGameExecutablePath(string gameRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        return Path.Combine(gameRoot, GameComponent, LastWarExecutableName);
    }

    // The original final equality loop folds only ASCII A-Z by setting bit 0x20.
    // Non-ASCII UTF-8 bytes are compared byte-for-byte after the lossy UTF-8
    // normalization helpers have run.
    public static bool AsciiCaseInsensitiveUtf8PathEquals(
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
            return false;

        for (int i = 0; i < left.Length; i++)
        {
            byte a = FoldAsciiUpper(left[i]);
            byte b = FoldAsciiUpper(right[i]);
            if (a != b)
                return false;
        }

        return true;
    }

    private static byte FoldAsciiUpper(byte value) =>
        value is >= (byte)'A' and <= (byte)'Z'
            ? (byte)(value | 0x20)
            : value;
}
