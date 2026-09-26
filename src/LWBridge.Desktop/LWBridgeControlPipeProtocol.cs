using System.Buffers.Binary;
using System.Text.Json;

namespace LWBridge.Desktop;

// LWB-R5-007 + LWB-R7-097: framing, hello/hello.ack and the generic call/result
// wire schemas are recovered from the hash-identified LWBridge 0.3.1 proxy and
// original host. This class remains protocol-only: later R7 checkpoints recover
// registry, limits, listener, RPC queues and launch binding in separate owners.
// R7-123 composes those owners into normal startup without moving transport
// lifecycle into this wire-format class.
internal static class LWBridgeControlPipeProtocol
{
    public const int ProtocolVersion = 1;
    public const int FramePrefixLength = sizeof(uint);
    public const int MaxFramePayloadLength = 0x800000;
    public const string HelloType = "hello";
    public const string HelloAckType = "hello.ack";
    public const string CommandType = "command";
    public const string ResultType = "result";
    public const string CallKind = "call";

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
        LWBridgeEnvelope envelope = ParseEnvelope(root);

        if (!string.Equals(envelope.Type, HelloType, StringComparison.Ordinal))
            throw new InvalidDataException("LWBridge proxy hello type is invalid.");
        if (envelope.RequestId.Length != 0)
            throw new InvalidDataException("LWBridge proxy hello requestId must be empty.");

        JsonElement body = envelope.Payload;
        string token = RequireString(body, "token");
        JsonElement pid = RequireNumber(body, "pid").Clone();
        string buildId = RequireString(body, "buildId");

        return new(
            envelope.Version,
            envelope.Type,
            envelope.ProfileId,
            envelope.InstanceId,
            envelope.RequestId,
            envelope.Timestamp,
            token,
            pid,
            buildId);
    }

    public static byte[] EncodeHelloAck(LWBridgeProxyHello hello, long timestamp)
    {
        ArgumentNullException.ThrowIfNull(hello);
        return EncodeEnvelope(
            HelloAckType,
            hello.ProfileId,
            hello.InstanceId,
            hello.RequestId,
            timestamp,
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            });
    }

    public static byte[] EncodeCallCommand(
        string profileId,
        string instanceId,
        string requestId,
        string functionName,
        JsonElement args,
        long timestamp,
        long createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        return EncodeEnvelope(
            CommandType,
            profileId,
            instanceId,
            requestId,
            timestamp,
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("id", requestId);
                writer.WriteString("kind", CallKind);
                writer.WriteString("fn", functionName);
                writer.WritePropertyName("args");
                args.WriteTo(writer);
                writer.WriteNumber("createdAt", createdAt);
                writer.WriteEndObject();
            });
    }

    public static LWBridgeCallResult ParseCallResult(ReadOnlySpan<byte> utf8Json)
    {
        using JsonDocument document = JsonDocument.Parse(utf8Json.ToArray());
        LWBridgeEnvelope envelope = ParseEnvelope(document.RootElement);
        if (!string.Equals(envelope.Type, ResultType, StringComparison.Ordinal))
            throw new InvalidDataException("LWBridge call result envelope type is invalid.");

        JsonElement body = envelope.Payload;
        string id = RequireString(body, "id");
        bool ok = RequireBoolean(body, "ok");
        JsonElement? result = body.TryGetProperty("result", out JsonElement resultValue)
            ? resultValue.Clone()
            : null;
        string? error = body.TryGetProperty("error", out JsonElement errorValue) &&
                        errorValue.ValueKind == JsonValueKind.String
            ? errorValue.GetString()
            : null;

        return new(
            envelope.Version,
            envelope.Type,
            envelope.ProfileId,
            envelope.InstanceId,
            envelope.RequestId,
            envelope.Timestamp,
            id,
            ok,
            result,
            error);
    }

    // IMPLEMENTATION POLICY: compare only values whose original handshake
    // validation is source-backed. Timestamp freshness is deliberately handled
    // by the connection host when that lifecycle is recovered.
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

    internal static LWBridgeEnvelope ParseEnvelope(
        ReadOnlySpan<byte> utf8Json)
    {
        using JsonDocument document =
            JsonDocument.Parse(utf8Json.ToArray());
        return ParseEnvelope(document.RootElement);
    }

    private static LWBridgeEnvelope ParseEnvelope(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("LWBridge control-pipe envelope must be a JSON object.");

        int version = RequireInt32(root, "version");
        string type = RequireString(root, "type");
        string profileId = RequireString(root, "profileId");
        string instanceId = RequireString(root, "instanceId");
        string requestId = RequireString(root, "requestId");
        JsonElement timestamp = RequireNumber(root, "timestamp").Clone();
        if (!root.TryGetProperty("payload", out JsonElement payload) || payload.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("LWBridge control-pipe payload must be a JSON object.");
        if (version != ProtocolVersion)
            throw new InvalidDataException("LWBridge control-pipe version is unsupported.");

        return new(version, type, profileId, instanceId, requestId, timestamp, payload.Clone());
    }

    private static byte[] EncodeEnvelope(
        string type,
        string profileId,
        string instanceId,
        string requestId,
        long timestamp,
        Action<Utf8JsonWriter> writePayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(writePayload);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", ProtocolVersion);
            writer.WriteString("type", type);
            writer.WriteString("profileId", profileId);
            writer.WriteString("instanceId", instanceId);
            writer.WriteString("requestId", requestId);
            writer.WriteNumber("timestamp", timestamp);
            writer.WritePropertyName("payload");
            writePayload(writer);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static void ValidatePayloadLength(int length)
    {
        if (length <= 0 || length > MaxFramePayloadLength)
            throw new ArgumentOutOfRangeException(nameof(length), "LWBridge control-pipe payload is outside the recovered frame range.");
    }

    private static string RequireString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"LWBridge control-pipe field '{name}' must be a string.");
        return value.GetString()!;
    }

    private static int RequireInt32(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int result))
            throw new InvalidDataException($"LWBridge control-pipe field '{name}' must be an Int32 number.");
        return result;
    }

    private static JsonElement RequireNumber(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException($"LWBridge control-pipe field '{name}' must be a JSON number.");
        return value;
    }

    private static bool RequireBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"LWBridge control-pipe field '{name}' must be a boolean.");
        return value.GetBoolean();
    }
}

internal sealed record LWBridgeEnvelope(
    int Version,
    string Type,
    string ProfileId,
    string InstanceId,
    string RequestId,
    JsonElement Timestamp,
    JsonElement Payload);

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

internal sealed record LWBridgeCallResult(
    int Version,
    string Type,
    string ProfileId,
    string InstanceId,
    string RequestId,
    JsonElement Timestamp,
    string Id,
    bool Ok,
    JsonElement? Result,
    string? Error);
