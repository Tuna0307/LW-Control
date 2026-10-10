using System.Security.Cryptography;
using System.Text;

namespace LWBridge.Desktop;

// LWB-R7-098: protocol-independent reconstruction of the original bridge-store
// instance registry. This deliberately does not create a named-pipe listener or
// own transport queues. R7-099 separately recovers numeric queue/byte/time limits;
// the live listener lifecycle and pipe options remain blocked.
internal sealed class LWBridgeControlPipeRegistry
{
    public const string DefaultRoute = "default";
    public const int MinimumTokenUtf8Bytes = 7;
    public const long StartupRegistrationLifetimeMilliseconds = 90_000;

    private readonly object gate = new();
    private readonly Dictionary<string, PendingRegistration> pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConnectedRoute> connected = new(StringComparer.Ordinal);
    private ulong connectionGeneration;
    private ulong registrationSerial;

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

    // Returns the monotonic serial of the new registration. A retiring owner can
    // later unregister exactly that registration, never a successor's.
    public ulong Register(
        string profileId,
        string instanceId,
        string token,
        long expiresAtMilliseconds,
        string? expectedCanonicalClientPath = null)
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

            ulong serial = ++registrationSerial;
            pending.Add(instanceId, new PendingRegistration(
                profileId,
                tokenHash,
                expiresAtMilliseconds,
                Claimed: false,
                Serial: serial,
                ExpectedCanonicalClientPath: expectedCanonicalClientPath));
            return serial;
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

    internal long? GetPendingExpiration(string instanceId)
    {
        lock (gate)
            return pending.TryGetValue(instanceId, out PendingRegistration? value)
                ? value.ExpiresAtMilliseconds : null;
    }

    public bool IsPending(string instanceId)
    {
        lock (gate) return pending.ContainsKey(instanceId);
    }

    // Only an exact token-bearing pending registration can override the
    // listener's bootstrap image path. Binding the image to this session
    // prevents a second profile's installation from widening A's identity
    // check. No token contents or path are returned for mismatching callers.
    internal string? ExpectedClientPathForAuthenticatedHello(
        string profileId, string instanceId, string token, long nowMilliseconds)
    {
        byte[] candidateHash = HashToken(token ?? string.Empty);
        lock (gate)
        {
            if (!pending.TryGetValue(instanceId, out PendingRegistration? entry) ||
                !string.Equals(entry.ProfileId, profileId, StringComparison.Ordinal) ||
                (!entry.Claimed && entry.ExpiresAtMilliseconds <= nowMilliseconds) ||
                !CryptographicOperations.FixedTimeEquals(entry.TokenHash, candidateHash))
                return null;
            return entry.ExpectedCanonicalClientPath;
        }
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
            connected[instanceId] = new ConnectedRoute(instanceId, generation, route, registration.Serial);
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

    // Registration-scoped unregister: removes the pending record and any route
    // admitted through that exact registration only. A stale owner whose
    // registration was already replaced is a no-op.
    public bool Unregister(string instanceId, ulong registrationSerial)
    {
        lock (gate)
        {
            bool removed = false;
            if (pending.TryGetValue(instanceId, out PendingRegistration? registration) &&
                registration.Serial == registrationSerial)
            {
                pending.Remove(instanceId);
                removed = true;
            }
            if (connected.TryGetValue(instanceId, out ConnectedRoute? route) &&
                route.RegistrationSerial == registrationSerial)
            {
                connected.Remove(instanceId);
                removed = true;
            }
            return removed;
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
        bool Claimed,
        ulong Serial,
        string? ExpectedCanonicalClientPath);
}

internal sealed record ConnectedRoute(
    string InstanceId,
    ulong Generation,
    object Route,
    ulong RegistrationSerial = 0);
