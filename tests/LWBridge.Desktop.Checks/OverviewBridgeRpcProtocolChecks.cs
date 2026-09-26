using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeRpcProtocolChecks
{
    internal static JsonElement Run()
    {
        byte[] helloBytes = System.Text.Encoding.UTF8.GetBytes(
            "{\"version\":1,\"type\":\"hello\",\"profileId\":\"profile-a\",\"instanceId\":\"instance-b\",\"requestId\":\"\",\"timestamp\":100,\"payload\":{\"token\":\"token-c\",\"pid\":4321,\"buildId\":\"build-d\"}}");
        LWBridgeProxyHello hello = LWBridgeControlPipeProtocol.ParseProxyHello(helloBytes);

        byte[] ackBytes = LWBridgeControlPipeProtocol.EncodeHelloAck(hello, 101);
        using JsonDocument ackDocument = JsonDocument.Parse(ackBytes);
        JsonElement ack = ackDocument.RootElement;
        CheckEnvelope(ack, "hello.ack", "profile-a", "instance-b", "", 101);
        Check(ack.GetProperty("payload").ValueKind == JsonValueKind.Object &&
              !ack.GetProperty("payload").EnumerateObject().Any(),
            "hello.ack payload must be the recovered empty object");

        JsonElement args = JsonSerializer.SerializeToElement(new { probe = "value", count = 2 });
        byte[] commandBytes = LWBridgeControlPipeProtocol.EncodeCallCommand(
            "profile-a", "instance-b", "cmd_7", "getStatus", args, 200, 199);
        using JsonDocument commandDocument = JsonDocument.Parse(commandBytes);
        JsonElement command = commandDocument.RootElement;
        CheckEnvelope(command, "command", "profile-a", "instance-b", "cmd_7", 200);
        JsonElement commandPayload = command.GetProperty("payload");
        Check(commandPayload.GetProperty("id").GetString() == "cmd_7",
            "outbound command payload id must equal the generated outer requestId");
        Check(commandPayload.GetProperty("kind").GetString() == "call",
            "outbound generic Lua RPC kind must be call");
        Check(commandPayload.GetProperty("fn").GetString() == "getStatus",
            "outbound generic Lua RPC preserves the function name");
        Check(commandPayload.GetProperty("args").GetProperty("probe").GetString() == "value" &&
              commandPayload.GetProperty("args").GetProperty("count").GetInt32() == 2,
            "outbound generic Lua RPC preserves JSON args");
        Check(commandPayload.GetProperty("createdAt").GetInt64() == 199,
            "outbound generic Lua RPC preserves recovered createdAt timestamp");

        byte[] successBytes = System.Text.Encoding.UTF8.GetBytes(
            "{\"version\":1,\"type\":\"result\",\"profileId\":\"profile-a\",\"instanceId\":\"instance-b\",\"requestId\":\"cmd_7\",\"timestamp\":201,\"payload\":{\"id\":\"cmd_7\",\"ok\":true,\"result\":{\"state\":\"ready\"}}}");
        LWBridgeCallResult success = LWBridgeControlPipeProtocol.ParseCallResult(successBytes);
        Check(success.Type == "result" && success.Id == "cmd_7" && success.Ok && success.Error is null &&
              success.Result is JsonElement successResult &&
              successResult.GetProperty("state").GetString() == "ready",
            "result parser preserves correlated success payload");

        byte[] failureBytes = System.Text.Encoding.UTF8.GetBytes(
            "{\"version\":1,\"type\":\"result\",\"profileId\":\"profile-a\",\"instanceId\":\"instance-b\",\"requestId\":\"cmd_8\",\"timestamp\":202,\"payload\":{\"id\":\"cmd_8\",\"ok\":false,\"error\":\"lua failed\"}}");
        LWBridgeCallResult failure = LWBridgeControlPipeProtocol.ParseCallResult(failureBytes);
        Check(failure.Id == "cmd_8" && !failure.Ok && failure.Result is null && failure.Error == "lua failed",
            "result parser preserves recovered failure error string");

        bool rejectedWrongType = false;
        try
        {
            LWBridgeControlPipeProtocol.ParseCallResult(System.Text.Encoding.UTF8.GetBytes(
                "{\"version\":1,\"type\":\"heartbeat\",\"profileId\":\"profile-a\",\"instanceId\":\"instance-b\",\"requestId\":\"cmd_9\",\"timestamp\":203,\"payload\":{\"id\":\"cmd_9\",\"ok\":true}}"));
        }
        catch (InvalidDataException)
        {
            rejectedWrongType = true;
        }
        Check(rejectedWrongType, "call-result parser rejects a non-result message type");

        // R7-097 intentionally does not assert inbound outer requestId == payload.id:
        // outbound equality is source-backed, while an explicit original inbound
        // equality check has not yet been recovered.
        byte[] independentIds = System.Text.Encoding.UTF8.GetBytes(
            "{\"version\":1,\"type\":\"result\",\"profileId\":\"profile-a\",\"instanceId\":\"instance-b\",\"requestId\":\"outer-id\",\"timestamp\":204,\"payload\":{\"id\":\"payload-id\",\"ok\":true,\"result\":null}}");
        LWBridgeCallResult independent = LWBridgeControlPipeProtocol.ParseCallResult(independentIds);
        Check(independent.RequestId == "outer-id" && independent.Id == "payload-id",
            "parser preserves outer and payload result ids independently until equality validation is recovered");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            recovered = new
            {
                helloAck = new[] { "version", "type", "profileId", "instanceId", "requestId", "timestamp", "payload:{}" },
                commandEnvelope = new[] { "version", "type=command", "profileId", "instanceId", "requestId", "timestamp", "payload" },
                callPayload = new[] { "id=requestId", "kind=call", "fn", "args", "createdAt" },
                resultPayload = new[] { "id", "ok", "result|error" },
            },
            boundary = new
            {
                persistentPipeHostImplemented = false,
                inboundResultOuterIdEqualityRecovered = false,
            },
        }, JsonOptions.Default);
    }

    private static void CheckEnvelope(
        JsonElement root,
        string type,
        string profileId,
        string instanceId,
        string requestId,
        long timestamp)
    {
        Check(root.GetProperty("version").GetInt32() == 1, "envelope version must be 1");
        Check(root.GetProperty("type").GetString() == type, "envelope type mismatch");
        Check(root.GetProperty("profileId").GetString() == profileId, "envelope profileId mismatch");
        Check(root.GetProperty("instanceId").GetString() == instanceId, "envelope instanceId mismatch");
        Check(root.GetProperty("requestId").GetString() == requestId, "envelope requestId mismatch");
        Check(root.GetProperty("timestamp").GetInt64() == timestamp, "envelope timestamp mismatch");
        Check(root.GetProperty("payload").ValueKind == JsonValueKind.Object, "envelope payload must be an object");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Overview bridge RPC protocol check failed: " + message);
    }
}
