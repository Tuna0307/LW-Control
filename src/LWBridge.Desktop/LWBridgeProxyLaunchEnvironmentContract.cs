namespace LWBridge.Desktop;

// LWB-R7-101: exact profile-launcher environment binding recovered from the
// embedded lwbridge-profile-launcher. This is intentionally an offline model:
// it does not mutate the current process environment or start a child process.
internal static class LWBridgeProxyLaunchEnvironmentContract
{
    public const string ProfileIdVariable = "LWBRIDGE_PROFILE_ID";
    public const string InstanceIdVariable = "LWBRIDGE_INSTANCE_ID";
    public const string PipeTokenVariable = "LWBRIDGE_PIPE_TOKEN";
    public const string BuildIdVariable = "LWBRIDGE_BUILD_ID";

    public const int PipeTokenEntropyBytes = 32;
    public const int PipeTokenEncodedLength = 43;

    public static string EncodePipeToken(ReadOnlySpan<byte> entropy)
    {
        if (entropy.Length != PipeTokenEntropyBytes)
            throw new ArgumentException(
                $"LWBridge pipe-token entropy must be exactly {PipeTokenEntropyBytes} bytes.",
                nameof(entropy));

        // The original token producer uses the URL-safe Base64 alphabet and
        // disables padding. 32 bytes therefore encode to exactly 43 chars.
        string token = Convert.ToBase64String(entropy)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        if (token.Length != PipeTokenEncodedLength)
            throw new InvalidOperationException("LWBridge pipe-token encoding length changed unexpectedly.");
        return token;
    }

    public static IReadOnlyDictionary<string, string> CreateBindings(
        string profileId,
        string instanceId,
        string pipeToken,
        string buildId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildId);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProfileIdVariable] = profileId,
            [InstanceIdVariable] = instanceId,
            [PipeTokenVariable] = pipeToken,
            [BuildIdVariable] = buildId,
        };
    }
}
