using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeRegistryChecks
{
    internal static JsonElement Run()
    {
        const string Profile = "profile-a";
        const string Instance = "instance-a";
        const string Token = "token-123";
        const long RegisteredAt = 1_000_000;
        long startupDeadline = RegisteredAt + LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds;

        foreach ((string name, string profile, string instance, string token, long deadline) in new[]
        {
            ("empty-profile", "", Instance, Token, startupDeadline),
            ("empty-instance", Profile, "", Token, startupDeadline),
            ("short-token", Profile, Instance, "123456", startupDeadline),
            ("zero-deadline", Profile, Instance, Token, 0L),
        })
        {
            var invalid = new LWBridgeControlPipeRegistry();
            ExpectError("PIPE_REGISTRATION_INVALID", name, () =>
                invalid.Register(profile, instance, token, deadline));
        }

        var expired = new LWBridgeControlPipeRegistry();
        expired.Register(Profile, Instance, Token, startupDeadline);
        Check(!expired.TryAdmit(Profile, Instance, Token, startupDeadline, new object(), out _),
            "an unclaimed registration expires at the exact deadline boundary");

        var registry = new LWBridgeControlPipeRegistry();
        registry.Register(Profile, Instance, Token, startupDeadline);
        Check(registry.PendingCount == 1 && registry.ConnectedCount == 0 && registry.IsPending(Instance),
            "registration enters the retained pending map before handshake");
        Check(!registry.TryAdmit("foreign-profile", Instance, Token, RegisteredAt + 1, new object(), out _),
            "handshake rejects a foreign profile");
        Check(!registry.TryAdmit(Profile, Instance, "wrong-token", RegisteredAt + 1, new object(), out _),
            "handshake rejects a foreign token hash");

        long refreshedDeadline = startupDeadline + 90_000;
        registry.RefreshPending(Instance, refreshedDeadline);
        object firstRoute = new();
        Check(registry.TryAdmit(Profile, Instance, Token, startupDeadline + 1, firstRoute, out ulong firstGeneration),
            "refreshed unclaimed registration admits inside its new deadline");
        Check(firstGeneration == 1, "first admitted connection receives generation 1");
        Check(registry.PendingCount == 1 && registry.ConnectedCount == 1,
            "successful handshake retains the registration and adds one connected route");
        ConnectedRoute? firstResolved = registry.Resolve(Instance);
        Check(firstResolved?.Generation == firstGeneration && ReferenceEquals(firstResolved.Route, firstRoute),
            "direct instance routing resolves the admitted connection");
        Check(registry.Resolve(LWBridgeControlPipeRegistry.DefaultRoute)?.Generation == firstGeneration,
            "default routing resolves the sole connected route");

        ExpectError("PIPE_REGISTRATION_INVALID", "refresh-after-claim", () =>
            registry.RefreshPending(Instance, refreshedDeadline + 1));
        ExpectError("PIPE_INSTANCE_DUPLICATE", "duplicate-after-claim", () =>
            registry.Register(Profile, Instance, Token, refreshedDeadline + 1));

        Check(registry.RemoveConnected(Instance, firstGeneration),
            "ordinary disconnect removes the matching connected generation");
        Check(registry.PendingCount == 1 && registry.ConnectedCount == 0 && registry.IsPending(Instance),
            "ordinary disconnect retains the claimed registration for authenticated reconnect");

        object secondRoute = new();
        Check(registry.TryAdmit(
                Profile,
                Instance,
                Token,
                refreshedDeadline + 10_000_000,
                secondRoute,
                out ulong secondGeneration),
            "claimed registration permits authenticated reconnect after its original deadline");
        Check(secondGeneration == 2 && registry.ConnectedCount == 1,
            "reconnect replaces the route with the next connection generation");
        Check(!registry.RemoveConnected(Instance, firstGeneration),
            "stale disconnect cannot remove a newer reconnect generation");
        Check(registry.Resolve(Instance)?.Generation == secondGeneration,
            "newer route survives stale generation cleanup");

        const string InstanceB = "instance-b";
        registry.Register(Profile, InstanceB, "token-456", refreshedDeadline + 1);
        Check(registry.TryAdmit(Profile, InstanceB, "token-456", RegisteredAt + 2, new object(), out ulong thirdGeneration),
            "second registered instance can connect independently");
        Check(thirdGeneration == 3 && registry.ConnectedCount == 2,
            "connection generation is host-global and monotonic");
        Check(registry.Resolve(LWBridgeControlPipeRegistry.DefaultRoute) is null,
            "default routing refuses ambiguity when more than one route is connected");

        registry.Unregister(Instance);
        Check(!registry.IsPending(Instance) && registry.Resolve(Instance) is null,
            "explicit unregister removes both retained registration and connected route");
        Check(!registry.TryAdmit(Profile, Instance, Token, RegisteredAt + 3, new object(), out _),
            "unregistered instance cannot reconnect");
        registry.Register(Profile, Instance, Token, refreshedDeadline + 2);
        Check(registry.IsPending(Instance), "instance key may be registered again after explicit unregister");

        var missing = new LWBridgeControlPipeRegistry();
        ExpectError("PIPE_INSTANCE_MISSING", "refresh-missing", () =>
            missing.RefreshPending("missing", startupDeadline));

        var saturating = new LWBridgeControlPipeRegistry(ulong.MaxValue - 1);
        saturating.Register(Profile, "sat", "token-sat", startupDeadline);
        Check(saturating.TryAdmit(Profile, "sat", "token-sat", RegisteredAt, new object(), out ulong maxGeneration) &&
              maxGeneration == ulong.MaxValue,
            "connection generation reaches UInt64 max without wrapping");
        Check(saturating.RemoveConnected("sat", maxGeneration), "max-generation route can disconnect normally");
        Check(saturating.TryAdmit(Profile, "sat", "token-sat", startupDeadline + 1, new object(), out ulong saturatedAgain) &&
              saturatedAgain == ulong.MaxValue,
            "connection generation saturates at UInt64 max on reconnect");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-098",
            recovered = new
            {
                registrationKey = "instanceId",
                fields = new[] { "profileId", "sha256(token)", "expiresAt", "claimed" },
                minimumTokenUtf8Bytes = LWBridgeControlPipeRegistry.MinimumTokenUtf8Bytes,
                startupRegistrationLifetimeMilliseconds = LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds,
                handshake = "profile + token hash + pending deadline before first claim",
                reconnect = "claimed registration retained; deadline skipped; generation advances",
                ordinaryDisconnect = "remove connected route only when instanceId + generation match",
                explicitUnregister = "remove pending registration and connected route",
                defaultRoute = "only when exactly one connected route exists",
                generation = "host-global monotonic UInt64 saturated at max",
            },
            boundary = new
            {
                persistentNamedPipeListenerImplemented = false,
                outboundQueueLimitsRecovered = false,
                proxyLaunchEnvironmentBound = false,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
    }

    private static void ExpectError(string code, string name, Action action)
    {
        try
        {
            action();
            throw new InvalidOperationException($"Overview bridge registry check failed: {name} unexpectedly succeeded");
        }
        catch (BridgeCommandException error)
        {
            Check(error.Code == code, $"{name} expected {code}, got {error.Code}");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Overview bridge registry check failed: " + message);
    }
}
