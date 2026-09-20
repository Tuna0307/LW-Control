using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeCallRegistryChecks
{
    internal static async Task<JsonElement> RunAsync()
    {
        JsonElement args =
            JsonSerializer.SerializeToElement(new { refresh = true });

        using var registry =
            new LWBridgeControlPipeCallRegistry(
                initialCommandCounter: 6);

        LWBridgePendingCall first = registry.BeginCall(
            "profile-a",
            "instance-a",
            "getStatus",
            args,
            createdAt: 100);
        Check(first.Id == "cmd_7",
            "injected counter seed 6 must emit monotonic cmd_7");
        Check(registry.PendingCount == 1,
            "begin adds one authoritative outstanding Lua call");

        var success = new LWBridgeCallResult(
            1,
            LWBridgeControlPipeProtocol.ResultType,
            "profile-a",
            "instance-a",
            "outer-unasserted",
            JsonSerializer.SerializeToElement(101),
            first.Id,
            true,
            JsonSerializer.SerializeToElement(new { state = "ready" }),
            null);

        Check(!registry.TryCompleteResult(
                "profile-a",
                "foreign-instance",
                success),
            "authenticated foreign route cannot consume another route's pending call");
        Check(registry.PendingCount == 1,
            "route mismatch leaves pending call intact");

        Check(registry.TryCompleteResult(
                "profile-a",
                "instance-a",
                success),
            "matching payload.id completes pending call");
        JsonElement? successValue = await first.Completion;
        Check(
            successValue?.GetProperty("state").GetString() == "ready" &&
            registry.PendingCount == 0,
            "success result returns cloned JSON and removes pending entry");

        LWBridgePendingCall failure = registry.BeginCall(
            "profile-a",
            "instance-a",
            "getStatus",
            JsonSerializer.SerializeToElement(new { }),
            createdAt: 200);
        Check(failure.Id == "cmd_8" &&
              registry.PendingCount == 1,
            "second command ID advances monotonically");

        var failedResult = new LWBridgeCallResult(
            1,
            LWBridgeControlPipeProtocol.ResultType,
            "profile-a",
            "instance-a",
            failure.Id,
            JsonSerializer.SerializeToElement(201),
            failure.Id,
            false,
            null,
            "lua exploded");
        Check(registry.TryCompleteResult(
                "profile-a",
                "instance-a",
                failedResult),
            "failure result correlates by payload.id");
        await ExpectBridgeErrorAsync(
            "LUA_CALL_FAILED",
            "proxy Lua failure mapping",
            async () => _ = await failure.Completion);
        Check(registry.PendingCount == 0,
            "Lua failure removes correlated pending entry");

        LWBridgePendingCall timeout = registry.BeginCall(
            "profile-a",
            "instance-a",
            "getStatus",
            JsonSerializer.SerializeToElement(new { }),
            createdAt: 300);
        var stopwatch = Stopwatch.StartNew();
        await ExpectBridgeErrorAsync(
            "LUA_CALL_TIMEOUT",
            "recovered 200 ms Lua-call timeout",
            async () => _ = await timeout.Completion.WaitAsync(
                TimeSpan.FromSeconds(2)));
        stopwatch.Stop();
        Check(registry.PendingCount == 0,
            "timeout removes pending entry before completion");
        Check(
            stopwatch.Elapsed >= TimeSpan.FromMilliseconds(150) &&
            stopwatch.Elapsed < TimeSpan.FromSeconds(1.5),
            "runtime timeout remains consistent with recovered 200 ms deadline");

        var lateResult = new LWBridgeCallResult(
            1,
            LWBridgeControlPipeProtocol.ResultType,
            "profile-a",
            "instance-a",
            timeout.Id,
            JsonSerializer.SerializeToElement(500),
            timeout.Id,
            true,
            JsonSerializer.SerializeToElement(new { late = true }),
            null);
        Check(!registry.TryCompleteResult(
                "profile-a",
                "instance-a",
                lateResult),
            "late result after timeout is uncorrelated/ignored");

        LWBridgePendingCall shutdownA = registry.BeginCall(
            "profile-a",
            "instance-a",
            "getStatus",
            JsonSerializer.SerializeToElement(new { a = 1 }),
            createdAt: 400);
        LWBridgePendingCall shutdownB = registry.BeginCall(
            "profile-a",
            "instance-a",
            "getStatus",
            JsonSerializer.SerializeToElement(new { b = 2 }),
            createdAt: 401);
        Check(registry.PendingCount == 2,
            "status pending count equals exact outstanding collection length");

        registry.Stop();
        Check(registry.PendingCount == 0,
            "shutdown atomically drains outstanding collection");
        await ExpectBridgeErrorAsync(
            "APP_SHUTTING_DOWN",
            "shutdown pending A",
            async () => _ = await shutdownA.Completion);
        await ExpectBridgeErrorAsync(
            "APP_SHUTTING_DOWN",
            "shutdown pending B",
            async () => _ = await shutdownB.Completion);

        ExpectBridgeError(
            "BRIDGE_STOPPED",
            "new call after shutdown",
            () => registry.BeginCall(
                "profile-a",
                "instance-a",
                "getStatus",
                JsonSerializer.SerializeToElement(new { }),
                createdAt: 500));

        Check(
            LWBridgeControlPipeCallRegistry.CallTimeoutMilliseconds == 200 &&
            LWBridgeControlPipeCallRegistry.CallTimeoutSeconds == 0 &&
            LWBridgeControlPipeCallRegistry.CallTimeoutNanoseconds ==
                200_000_000 &&
            LWBridgeControlPipeCallRegistry.RustNanosecondsPerSecond ==
                1_000_000_000,
            "source-backed Rust Duration timeout constants remain pinned");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-119",
            recovered = new
            {
                timeoutMilliseconds =
                    LWBridgeControlPipeCallRegistry.CallTimeoutMilliseconds,
                duration = new
                {
                    seconds =
                        LWBridgeControlPipeCallRegistry.CallTimeoutSeconds,
                    nanoseconds =
                        LWBridgeControlPipeCallRegistry
                            .CallTimeoutNanoseconds,
                },
                timeoutFutureRva =
                    $"0x{LWBridgeControlPipeCallRegistry.CallTimeoutFutureRva:X}",
                pending = "exact outstanding bridge-to-Lua call/result collection length",
                id = "monotonic cmd_<n>; initial process seed still intentionally external",
                correlation = "payload.id",
                outerRequestIdEqualityAsserted = false,
                luaFailureCode = "LUA_CALL_FAILED",
                timeoutCode = "LUA_CALL_TIMEOUT",
                shutdownCode = "APP_SHUTTING_DOWN",
                shutdownMessage = "application is shutting down",
            },
            proof = new
            {
                routeMismatchRejected = true,
                successCorrelated = true,
                failureCorrelated = true,
                timeoutObservedMilliseconds =
                    stopwatch.Elapsed.TotalMilliseconds,
                lateResultIgnored = true,
                shutdownDrainedTwo = true,
            },
            boundary = new
            {
                pipeSessionTransportImplemented = false,
                productionPendingExposed = false,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
    }

    private static async Task ExpectBridgeErrorAsync(
        string expectedCode,
        string name,
        Func<Task> action)
    {
        try
        {
            await action();
            throw new InvalidOperationException(
                $"Overview bridge call-registry check failed: {name} unexpectedly succeeded");
        }
        catch (BridgeCommandException error)
        {
            Check(
                error.Code == expectedCode,
                $"{name} expected {expectedCode}, got {error.Code}");
        }
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
                $"Overview bridge call-registry check failed: {name} unexpectedly succeeded");
        }
        catch (BridgeCommandException error)
        {
            Check(
                error.Code == expectedCode,
                $"{name} expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "Overview bridge call-registry check failed: " + message);
        }
    }
}
