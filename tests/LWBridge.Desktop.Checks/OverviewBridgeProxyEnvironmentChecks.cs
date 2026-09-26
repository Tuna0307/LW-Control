using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeProxyEnvironmentChecks
{
    internal static JsonElement Run()
    {
        byte[] entropy = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        string token = LWBridgeProxyLaunchEnvironmentContract.EncodePipeToken(entropy);
        Check(token == "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8",
            "32-byte fixture uses URL-safe Base64 without padding");
        Check(token.Length == LWBridgeProxyLaunchEnvironmentContract.PipeTokenEncodedLength,
            "pipe token is exactly 43 characters");
        Check(!token.Contains('=') && !token.Contains('+') && !token.Contains('/'),
            "pipe token contains no padding or standard-Base64-only characters");

        IReadOnlyDictionary<string, string> bindings =
            LWBridgeProxyLaunchEnvironmentContract.CreateBindings(
                "profile-a", "instance-a", token, "build-a");
        Check(bindings.Count == 4, "core recovered launch binding has four identity/build entries");
        Check(bindings[LWBridgeProxyLaunchEnvironmentContract.ProfileIdVariable] == "profile-a",
            "LWBRIDGE_PROFILE_ID carries descriptor profileId");
        Check(bindings[LWBridgeProxyLaunchEnvironmentContract.InstanceIdVariable] == "instance-a",
            "LWBRIDGE_INSTANCE_ID carries descriptor instanceId");
        Check(bindings[LWBridgeProxyLaunchEnvironmentContract.PipeTokenVariable] == token,
            "LWBRIDGE_PIPE_TOKEN carries descriptor pipeToken");
        Check(bindings[LWBridgeProxyLaunchEnvironmentContract.BuildIdVariable] == "build-a",
            "LWBRIDGE_BUILD_ID carries descriptor buildId");

        string lifecycleChallengeFixture = Convert.ToHexString(entropy).ToLowerInvariant();
        Check(lifecycleChallengeFixture.Length == 64 && lifecycleChallengeFixture != token,
            "existing rebuild challenge format is distinct from recovered original pipe-token format");

        Check(
            LWBridgeProxyLaunchEnvironmentContract.PipeTokenPayloadPointerOffset ==
                LWBridgeProxyLaunchEnvironmentContract.PipeTokenLaunchStateOffset + 8 &&
            LWBridgeProxyLaunchEnvironmentContract.PipeTokenPayloadLengthOffset ==
                LWBridgeProxyLaunchEnvironmentContract.PipeTokenLaunchStateOffset + 16,
            "recovered token payload pointer/length remain the exact +8/+16 slice of the launch-state token object");
        Check(
            LWBridgeProxyLaunchEnvironmentContract.PipeTokenProducerCallRva == 0x1D511D &&
            LWBridgeProxyLaunchEnvironmentContract.DirectStartupRegistrationCallRva == 0x1D7C94 &&
            LWBridgeProxyLaunchEnvironmentContract.RegistryWrapperRva == 0x3CCA38 &&
            LWBridgeProxyLaunchEnvironmentContract.RegistryRegisterRva == 0x3C2A69,
            "recovered original pipe-token producer and startup-registration RVAs remain pinned");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-101/R7-108",
            recovered = new
            {
                variables = new[]
                {
                    LWBridgeProxyLaunchEnvironmentContract.ProfileIdVariable,
                    LWBridgeProxyLaunchEnvironmentContract.InstanceIdVariable,
                    LWBridgeProxyLaunchEnvironmentContract.PipeTokenVariable,
                    LWBridgeProxyLaunchEnvironmentContract.BuildIdVariable,
                },
                pipeTokenEntropyBytes = LWBridgeProxyLaunchEnvironmentContract.PipeTokenEntropyBytes,
                pipeTokenEncoding = "Base64URL without padding",
                pipeTokenEncodedLength = LWBridgeProxyLaunchEnvironmentContract.PipeTokenEncodedLength,
                launchStateTokenObjectOffset = LWBridgeProxyLaunchEnvironmentContract.PipeTokenLaunchStateOffset,
                launchStateTokenPayloadPointerOffset = LWBridgeProxyLaunchEnvironmentContract.PipeTokenPayloadPointerOffset,
                launchStateTokenPayloadLengthOffset = LWBridgeProxyLaunchEnvironmentContract.PipeTokenPayloadLengthOffset,
                startupRegistryAlias = "instruction-level proven: +0x3A8/+0x3B0 pointer/length forwarded unchanged through 0x3CCA38 into 0x3C2A69",
                childEnvironment = "inherited because both recovered CreateProcessW paths pass lpEnvironment=NULL",
                challengeRelationship = "distinct contract; do not alias rebuild challenge to original pipeToken",
            },
            boundary = new
            {
                processEnvironmentMutated = false,
                childProcessStarted = false,
                productionNamedPipeListenerImplemented = false,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Overview bridge proxy environment check failed: " + message);
    }
}
