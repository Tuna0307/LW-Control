using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeLaunchBindingChecks
{
    internal static JsonElement Run()
    {
        byte[] entropy = Enumerable.Range(0, 32)
            .Select(value => (byte)value)
            .ToArray();
        const string ExpectedToken =
            "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
        const long PreparedAt = 1_000;
        const long RefreshedAt = 5_000;

        var registry = new LWBridgeControlPipeRegistry();
        using var host = new LWBridgeControlPipeHostState(
            @"\\.\pipe\lwbridge-control-v1-launch-binding-test",
            registry,
            pipeTokenEntropyFactory: () => entropy.ToArray());

        LWBridgeControlPipeLaunchBinding binding = host.PrepareLaunchBinding(
            "profile-a",
            "instance-a",
            "build-a",
            PreparedAt);

        Check(binding.ProfileId == "profile-a" &&
              binding.InstanceId == "instance-a" &&
              binding.PipeToken == ExpectedToken &&
              binding.BuildId == "build-a",
            "prepared launch binding preserves recovered identity/build values");
        Check(binding.ExpiresAtMilliseconds ==
              PreparedAt +
              LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds,
            "prepared registration uses exact 90-second first-claim lifetime");
        Check(host.PendingRegistrationCount == 1 &&
              registry.IsPending("instance-a"),
            "prepare registers the instance before launch");

        Check(binding.Environment.Count == 4 &&
              binding.Environment[
                  LWBridgeProxyLaunchEnvironmentContract.ProfileIdVariable] ==
                  "profile-a" &&
              binding.Environment[
                  LWBridgeProxyLaunchEnvironmentContract.InstanceIdVariable] ==
                  "instance-a" &&
              binding.Environment[
                  LWBridgeProxyLaunchEnvironmentContract.PipeTokenVariable] ==
                  ExpectedToken &&
              binding.Environment[
                  LWBridgeProxyLaunchEnvironmentContract.BuildIdVariable] ==
                  "build-a",
            "prepared environment contains exactly the recovered four LWBRIDGE bindings");

        var start = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
        };
        binding.ApplyTo(start);
        Check(
            start.Environment[
                LWBridgeProxyLaunchEnvironmentContract.ProfileIdVariable] ==
                "profile-a" &&
            start.Environment[
                LWBridgeProxyLaunchEnvironmentContract.InstanceIdVariable] ==
                "instance-a" &&
            start.Environment[
                LWBridgeProxyLaunchEnvironmentContract.PipeTokenVariable] ==
                ExpectedToken &&
            start.Environment[
                LWBridgeProxyLaunchEnvironmentContract.BuildIdVariable] ==
                "build-a",
            "binding applies the four recovered values to the child environment");

        long refreshedExpiry =
            host.RefreshLaunchBinding("instance-a", RefreshedAt);
        Check(refreshedExpiry ==
              RefreshedAt +
              LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds,
            "refresh resets pending first-claim expiry to current clock + 90 seconds");

        object route = new();
        bool admitted = registry.TryAdmit(
            "profile-a",
            "instance-a",
            ExpectedToken,
            nowMilliseconds: refreshedExpiry - 1,
            route,
            out ulong generation);
        Check(admitted && generation == 1 &&
              ReferenceEquals(registry.Resolve("instance-a")?.Route, route),
            "refreshed pending registration admits the exact raw-token identity");
        host.CancelLaunchBinding("instance-a");
        Check(!registry.IsPending("instance-a") &&
              registry.Resolve("instance-a") is null,
            "explicit cancel removes both retained registration and connected route");

        LWBridgeControlPipeLaunchBinding pendingDuringShutdown =
            host.PrepareLaunchBinding(
                "profile-b",
                "instance-b",
                "build-a",
                PreparedAt);
        Check(pendingDuringShutdown.PipeToken == ExpectedToken,
            "deterministic entropy produces the exact recovered token encoding");

        host.Close();
        host.CancelLaunchBinding("instance-b");
        Check(!registry.IsPending("instance-b"),
            "launch-failure cleanup remains legal after host shutdown begins");

        ExpectBridgeError(
            "BRIDGE_STOPPED",
            "prepare after host shutdown",
            () => host.PrepareLaunchBinding(
                "profile-c",
                "instance-c",
                "build-a",
                PreparedAt));
        ExpectBridgeError(
            "BRIDGE_STOPPED",
            "refresh after host shutdown",
            () => host.RefreshLaunchBinding("instance-c", RefreshedAt));

        var duplicateRegistry = new LWBridgeControlPipeRegistry();
        using var duplicateHost = new LWBridgeControlPipeHostState(
            @"\\.\pipe\lwbridge-control-v1-launch-binding-duplicate",
            duplicateRegistry,
            pipeTokenEntropyFactory: () => entropy.ToArray());
        _ = duplicateHost.PrepareLaunchBinding(
            "profile-a",
            "instance-duplicate",
            "build-a",
            PreparedAt);
        ExpectBridgeError(
            "PIPE_INSTANCE_DUPLICATE",
            "duplicate startup registration",
            () => duplicateHost.PrepareLaunchBinding(
                "profile-a",
                "instance-duplicate",
                "build-a",
                PreparedAt));
        duplicateHost.CancelLaunchBinding("instance-duplicate");

        using var badEntropyHost = new LWBridgeControlPipeHostState(
            @"\\.\pipe\lwbridge-control-v1-launch-binding-bad-entropy",
            new LWBridgeControlPipeRegistry(),
            pipeTokenEntropyFactory: () => new byte[31]);
        bool badEntropyRejected = false;
        try
        {
            _ = badEntropyHost.PrepareLaunchBinding(
                "profile-a",
                "instance-bad-entropy",
                "build-a",
                PreparedAt);
        }
        catch (InvalidOperationException)
        {
            badEntropyRejected = true;
        }
        Check(badEntropyRejected &&
              badEntropyHost.PendingRegistrationCount == 0,
            "invalid entropy byte count fails before registry mutation");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-117",
            recovered = new
            {
                tokenEntropyBytes =
                    LWBridgeProxyLaunchEnvironmentContract.PipeTokenEntropyBytes,
                tokenEncoding = "Base64URL without padding",
                token = ExpectedToken,
                registrationLifetimeMilliseconds =
                    LWBridgeControlPipeRegistry
                        .StartupRegistrationLifetimeMilliseconds,
                environmentVariables = binding.Environment.Keys
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                refresh = "current clock + 90000 ms",
                failureCleanup = "explicit unregister remains legal during host shutdown",
            },
            boundary = new
            {
                overviewLifecycleUsesLaunchBinding = true,
                helperProcessEnvironmentMutated = true,
                productionListenerStarted = true,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
    }

    private static void ExpectBridgeError(
        string expectedCode,
        string name,
        Func<object?> action)
    {
        try
        {
            _ = action();
            throw new InvalidOperationException(
                $"Overview bridge launch binding check failed: {name} unexpectedly succeeded");
        }
        catch (BridgeCommandException error)
        {
            Check(error.Code == expectedCode,
                $"{name} expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "Overview bridge launch binding check failed: " + message);
    }
}
