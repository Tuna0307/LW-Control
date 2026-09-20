using System.Security.Cryptography;
using System.Text;

namespace LWBridge.Desktop;

// LWB-R7-098: protocol-independent reconstruction of the original bridge-store
// instance registry. This deliberately does not create a named-pipe listener or
// own transport queues; those layers remain blocked until their exact lifecycle
// and limits are recovered.
internal sealed class LWBridgeControlPipeRegistry
{
    public const string DefaultRoute = "default";
    public const int MinimumTokenUtf8Bytes = 7;
    public const long StartupRegistrationLifetimeMilliseconds = 90_000;

    private readonly object gate = new();
    private readonly Dictionary<string, PendingRegistration> pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConnectedRoute> connected = new(StringComparer.Ordinal);
    private ulong connectionGeneration;

    internal LWBridgeControlPipeRegistry(ulong initialGeneration = 0)
    {
        connectionGeneration = initialGeneration;
    }

    public int PendingCount
    {
        get { lock (gate) return pending.Count; }
    }

    public int ConnectedCount
    {
        get { lock (gate) return connected.Count; }
    }

    public void Register(
        string profileId,
        string instanceId,
        string token,
        long expiresAtMilliseconds)
    {
        if (string.IsNullOrEmpty(profileId) ||
            string.IsNullOrEmpty(instanceId) ||
            token is null ||
            Encoding.UTF8.GetByteCount(token) < MinimumTokenUtf8Bytes ||
            expiresAtMilliseconds <= 0)
        {
            throw new BridgeCommandException(
                "PIPE_REGISTRATION_INVALID",
                "named pipe registration is invalid");
        }

        byte[] tokenHash = HashToken(token);
        lock (gate)
        {
            if (pending.ContainsKey(instanceId) || connected.ContainsKey(instanceId))
            {
                throw new BridgeCommandException(
                    "PIPE_INSTANCE_DUPLICATE",
                    "named pipe instance is already registered");
            }

            pending.Add(instanceId, new PendingRegistration(
                profileId,
                tokenHash,
                expiresAtMilliseconds,
                Claimed: false));
        }
    }

    public void RefreshPending(string instanceId, long expiresAtMilliseconds)
    {
        lock (gate)
        {
            if (!pending.TryGetValue(instanceId, out PendingRegistration? registration))
            {
                throw new BridgeCommandException(
                    "PIPE_INSTANCE_MISSING",
                    "named pipe instance is not pending");
            }

            if (expiresAtMilliseconds <= 0 || registration.Claimed)
            {
                throw new BridgeCommandException(
                    "PIPE_REGISTRATION_INVALID",
                    "named pipe registration cannot be refreshed");
            }

            pending[instanceId] = registration with { ExpiresAtMilliseconds = expiresAtMilliseconds };
        }
    }

    public bool IsPending(string instanceId)
    {
        lock (gate) return pending.ContainsKey(instanceId);
    }

    public bool TryAdmit(
        string profileId,
        string instanceId,
        string token,
        long nowMilliseconds,
        object route,
        out ulong generation)
    {
        ArgumentNullException.ThrowIfNull(route);
        generation = 0;

        byte[] candidateHash = HashToken(token ?? string.Empty);
        lock (gate)
        {
            if (!pending.TryGetValue(instanceId, out PendingRegistration? registration) ||
                !string.Equals(registration.ProfileId, profileId, StringComparison.Ordinal) ||
                (!registration.Claimed && registration.ExpiresAtMilliseconds <= nowMilliseconds) ||
                !CryptographicOperations.FixedTimeEquals(registration.TokenHash, candidateHash))
            {
                return false;
            }

            if (!registration.Claimed)
                pending[instanceId] = registration with { Claimed = true };

            generation = NextGeneration();
            connected[instanceId] = new ConnectedRoute(instanceId, generation, route);
            return true;
        }
    }

    public ConnectedRoute? Resolve(string instanceId)
    {
        lock (gate)
        {
            if (string.Equals(instanceId, DefaultRoute, StringComparison.Ordinal))
            {
                if (connected.Count != 1) return null;
                return connected.Values.Single();
            }

            return connected.TryGetValue(instanceId, out ConnectedRoute? route) ? route : null;
        }
    }

    // Ordinary connection teardown is generation-scoped. A stale connection is
    // not allowed to remove a newer route installed by a reconnect.
    public bool RemoveConnected(string instanceId, ulong generation)
    {
        lock (gate)
        {
            if (!connected.TryGetValue(instanceId, out ConnectedRoute? route) ||
                route.Generation != generation)
            {
                return false;
            }

            connected.Remove(instanceId);
            return true;
        }
    }

    // Explicit instance unregister removes both the retained registration and
    // whichever connected route currently owns the same instance key.
    public void Unregister(string instanceId)
    {
        lock (gate)
        {
            pending.Remove(instanceId);
            connected.Remove(instanceId);
        }
    }

    private ulong NextGeneration()
    {
        if (connectionGeneration != ulong.MaxValue)
            connectionGeneration++;
        return connectionGeneration;
    }

    private static byte[] HashToken(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private sealed record PendingRegistration(
        string ProfileId,
        byte[] TokenHash,
        long ExpiresAtMilliseconds,
        bool Claimed);
}

internal sealed record ConnectedRoute(
    string InstanceId,
    ulong Generation,
    object Route);
