namespace LWBridge.Desktop;

// LWB-R7-114: client-process image normalization recovered from the original
// control-pipe hello verifier. Used by the live handshake and by lifecycle code
// to build the expected <root>\Game\LastWar.exe path.
internal static class LWBridgeControlPipeClientPathContract
{
    public const uint DesiredAccess = 0;
    public const uint ShareMode = 0x00000007;
    public const uint OpenExisting = 3;
    public const uint FileFlagBackupSemantics = 0x02000000;
    public const uint GetFinalPathNameFlags = 0;

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
