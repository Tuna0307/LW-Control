using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace LWBridge.Desktop;

internal static class LWBridgeControlPipeContract
{
    public const string FullPathPrefix = @"\\.\pipe\lwbridge-control-v1-";

    public static string GetCurrentUserFullPath()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string? sid = identity.User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
            throw new InvalidOperationException("Current Windows user SID is unavailable.");
        return GetFullPathForSid(sid);
    }

    internal static string GetFullPathForSid(string sid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(sid));
        string suffix = Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();
        return FullPathPrefix + suffix;
    }
}
