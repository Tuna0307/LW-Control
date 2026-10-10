using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class HomeR4TransportChecks
{
    internal static void Run()
    {
        const long now = 10_000;
        string temporaryRoot = Path.Combine(Path.GetTempPath(),
            "lwbridge-home004-r4-transport-" + Guid.NewGuid().ToString("N"));
        string imageA = Path.Combine(temporaryRoot, "A", "Game", "LastWar.exe");
        string imageB = Path.Combine(temporaryRoot, "B", "Game", "LastWar.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(imageA)!);
        Directory.CreateDirectory(Path.GetDirectoryName(imageB)!);
        File.WriteAllBytes(imageA, new byte[] { 0x4D, 0x5A });
        File.WriteAllBytes(imageB, new byte[] { 0x4D, 0x5A });
        try
        {
        var registry = new LWBridgeControlPipeRegistry();
        int entropyGeneration = 0;
        using var host = new LWBridgeControlPipeHostState(
            pipePath: "ignored-no-listener",
            registry: registry,
            pipeTokenEntropyFactory: () =>
                Enumerable.Repeat((byte)(0x3A + ++entropyGeneration), 32).ToArray());

        LWBridgeControlPipeLaunchBinding a =
            host.PrepareLaunchBinding("profile-A", "session-A", "bridge-0.3.17", now, imageA);
        LWBridgeControlPipeLaunchBinding b =
            host.PrepareLaunchBinding("profile-B", "session-B", "bridge-0.3.17", now, imageB);

        string canonicalA =
            LWBridgeControlPipeIsolatedHandshake.CanonicalizeExpectedClientPath(imageA);
        string canonicalB =
            LWBridgeControlPipeIsolatedHandshake.CanonicalizeExpectedClientPath(imageB);
        Require(canonicalA != canonicalB, "distinct installations have distinct canonical image identities");
        Require(registry.ExpectedClientPathForAuthenticatedHello(
                "profile-A", "session-A", a.PipeToken, now) == canonicalA &&
            registry.ExpectedClientPathForAuthenticatedHello(
                "profile-B", "session-B", b.PipeToken, now) == canonicalB,
            "each real host launch binding owns its own canonical image path");
        Require(registry.ExpectedClientPathForAuthenticatedHello(
                "profile-A", "session-A", b.PipeToken, now) is null &&
            registry.ExpectedClientPathForAuthenticatedHello(
                "profile-B", "session-A", a.PipeToken, now) is null &&
            registry.ExpectedClientPathForAuthenticatedHello(
                "profile-A", "session-B", a.PipeToken, now) is null,
            "wrong token, profile or instance cannot reuse a sibling's path");
        Require(registry.ExpectedClientPathForAuthenticatedHello(
                "profile-A", "session-A", a.PipeToken, now + 90_000) is null,
            "expired unclaimed session cannot override the handshake image identity");

        object routeA = new();
        object routeB = new();
        Require(registry.TryAdmit("profile-A", "session-A", a.PipeToken, now, routeA,
                out ulong generationA) &&
            registry.TryAdmit("profile-B", "session-B", b.PipeToken, now, routeB,
                out ulong generationB) &&
            generationA != generationB,
            "real host sibling registrations admit independently with unique route generations");
        Require(registry.RemoveConnected("session-A", generationA) &&
            registry.Resolve("session-B")?.Route == routeB,
            "A route retirement does not disturb B's accepted route");
        host.CancelLaunchBinding("session-A");
        Require(registry.ExpectedClientPathForAuthenticatedHello(
                "profile-A", "session-A", a.PipeToken, now) is null &&
            registry.ExpectedClientPathForAuthenticatedHello(
                "profile-B", "session-B", b.PipeToken, now) == canonicalB,
            "exact A unregister removes only A's authorized executable");
        host.CancelLaunchBinding("session-B");
        Require(registry.PendingCount == 0 && registry.ConnectedCount == 0,
            "all controlled route registrations close without leaking a sibling owner");

        Console.WriteLine(
            "HOME004_R4_TOKEN_BOUND_IMAGE_ROUTING_OK per-session images, wrong-token/profile, expiry, generations, exact route teardown; game launches=0");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("HOME004_R4_TOKEN_BOUND_IMAGE_ROUTING_FAILED: " + message);
    }
}
