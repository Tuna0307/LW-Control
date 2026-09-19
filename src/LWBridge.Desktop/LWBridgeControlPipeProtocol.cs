using System.Buffers.Binary;
using System.Text.Json;

namespace LWBridge.Desktop;

// LWB-R5-007: recovered from the hash-identified LWBridge 0.3.1 secure xLua
// proxy and original host. This class intentionally stops before hello.ack or
// command serialization because those outbound host contracts are still
// unrecovered.
internal static class LWBridgeControlPipeProtocol
{
    public const int ProtocolVersion = 1;
    public const int FramePrefixLength = sizeof(uint);
    public const int MaxFramePayloadLength = 0x800000;
    public const string HelloType = "hello";
    public const string HelloAckType = "hello.ack";

    public static byte[] EncodeFrame(ReadOnlySpan<byte> payload)
    {
        ValidatePayloadLength(payload.Length);

        byte[] frame = new byte[FramePrefixLength + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, checked((uint)payload.Length));
        payload.CopyTo(frame.AsSpan(FramePrefixLength));
        return frame;
    }

    public static bool TryDecodeFrame(
        ReadOnlySpan<byte> bufferedBytes,
        out byte[] payload,
        out int bytesConsumed)
    {
        payload = [];
        bytesConsumed = 0;

        if (bufferedBytes.Length < FramePrefixLength)
            return false;

        uint payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(bufferedBytes);
        if (payloadLength is 0 or > MaxFramePayloadLength)
            throw new InvalidDataException("LWBridge control-pipe frame length is outside the recovered range.");

        int frameLength = checked(FramePrefixLength + (int)payloadLength);
        if (bufferedBytes.Length < frameLength)
            return false;

        payload = bufferedBytes.Slice(FramePrefixLength, (int)payloadLength).ToArray();
        bytesConsumed = frameLength;
        return true;
    }

    public static LWBridgeProxyHello ParseProxyHello(ReadOnlySpan<byte> utf8Json)
    {
        using JsonDocument document = JsonDocument.Parse(utf8Json.ToArray());
        JsonElement root = document.RootElement;

        int version = RequireInt32(root, "version");
        string type = RequireString(root, "type");
        string profileId = RequireString(root, "profileId");
        string instanceId = RequireString(root, "instanceId");
        string requestId = RequireString(root, "requestId");
        JsonElement timestamp = RequireNumber(root, "timestamp");

        if (!root.TryGetProperty("payload", out JsonElement body) || body.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("LWBridge proxy hello payload must be a JSON object.");

        string token = RequireString(body, "token");
        JsonElement pid = RequireNumber(body, "pid");
        string buildId = RequireString(body, "buildId");

        if (version != ProtocolVersion)
            throw new InvalidDataException("LWBridge proxy hello version is unsupported.");
        if (!string.Equals(type, HelloType, StringComparison.Ordinal))
            throw new InvalidDataException("LWBridge proxy hello type is invalid.");
        if (requestId.Length != 0)
            throw new InvalidDataException("LWBridge proxy hello requestId must be empty.");
        return new(version, type, profileId, instanceId, requestId, timestamp.Clone(), token, pid.Clone(), buildId);
    }

    // IMPLEMENTATION POLICY: compare only values whose original handshake
    // validation is source-backed. Timestamp freshness and hello.ack readiness
    // are intentionally excluded until their exact contracts are recovered.
    public static bool MatchesExpectedIdentity(
        LWBridgeProxyHello hello,
        string expectedProfileId,
        string expectedInstanceId,
        string expectedToken,
        string expectedBuildId)
    {
        return string.Equals(hello.ProfileId, expectedProfileId, StringComparison.Ordinal) &&
               string.Equals(hello.InstanceId, expectedInstanceId, StringComparison.Ordinal) &&
               string.Equals(hello.Token, expectedToken, StringComparison.Ordinal) &&
               string.Equals(hello.BuildId, expectedBuildId, StringComparison.Ordinal);
    }

    private static void ValidatePayloadLength(int length)
    {
        if (length <= 0 || length > MaxFramePayloadLength)
            throw new ArgumentOutOfRangeException(nameof(length), "LWBridge control-pipe payload is outside the recovered frame range.");
    }

    private static string RequireString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"LWBridge proxy hello field '{name}' must be a string.");
        return value.GetString()!;
    }

    private static int RequireInt32(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int result))
            throw new InvalidDataException($"LWBridge proxy hello field '{name}' must be an Int32 number.");
        return result;
    }

    private static JsonElement RequireNumber(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException($"LWBridge proxy hello field '{name}' must be a JSON number.");
        return value;
    }
}

internal sealed record LWBridgeProxyHello(
    int Version,
    string Type,
    string ProfileId,
    string InstanceId,
    string RequestId,
    JsonElement Timestamp,
    string Token,
    JsonElement Pid,
    string BuildId);
