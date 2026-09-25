using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ClaimDelayConfigChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-claim-delay-" + Guid.NewGuid().ToString("N"));
        string configPath = Path.Combine(root, "runtime", "config.json");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(
                configPath,
                """
                {
                  "futureRoot": { "keep": 7 },
                  "scheduler": {
                    "requestTimeoutSeconds": 15,
                    "redPacketClaimDelaySeconds": [0, 0],
                    "treasureClaimDelaySeconds": [0, 0]
                  },
                  "chat_automation": {
                    "red_packet": {
                      "enabled": true,
                      "replyEnabled": false,
                      "claimDelaySeconds": [0, 0]
                    },
                    "treasure": {
                      "enabled": true,
                      "dispatchRetrySeconds": 30,
                      "claimDelaySeconds": [0, 0]
                    },
                    "fireworks": {
                      "enabled": false,
                      "claimDelaySeconds": [0, 0]
                    }
                  },
                  "tasks": {
                    "construction": { "enabled": true, "maxBuilders": 1 }
                  }
                }
                """);
            var store = new ProfileRuntimeConfigStore(configPath);
            var service = new ClaimDelayConfigCommandService(store);

            JsonElement redPacket = await Invoke(
                service,
                "red_packet_delay_configure",
                new { minSeconds = 1.5, maxSeconds = 60.0 });
            AssertSuccessRange(redPacket, 1.5, 60.0);

            JsonObject afterRedPacket = ReadConfig(configPath);
            AssertRange(
                afterRedPacket["scheduler"]!["redPacketClaimDelaySeconds"]!,
                1.5,
                60.0,
                "scheduler red packet mirror");
            AssertRange(
                afterRedPacket["chat_automation"]!["red_packet"]!["claimDelaySeconds"]!,
                1.5,
                60.0,
                "chat red packet mirror");
            AssertRange(
                afterRedPacket["scheduler"]!["treasureClaimDelaySeconds"]!,
                0.0,
                0.0,
                "red packet save preserves treasure scheduler range");
            Require(
                afterRedPacket["chat_automation"]!["red_packet"]!["enabled"]!
                    .GetValue<bool>(),
                "red packet save preserves chat sibling fields");
            Require(
                afterRedPacket["futureRoot"]!["keep"]!.GetValue<int>() == 7,
                "red packet save preserves unknown root fields");

            JsonElement treasure = await Invoke(
                service,
                "treasure_delay_configure",
                new { minSeconds = 0, maxSeconds = 600 });
            AssertSuccessRange(treasure, 0.0, 600.0);

            JsonObject afterTreasure = ReadConfig(configPath);
            AssertRange(
                afterTreasure["scheduler"]!["treasureClaimDelaySeconds"]!,
                0.0,
                600.0,
                "scheduler treasure mirror");
            AssertRange(
                afterTreasure["chat_automation"]!["treasure"]!["claimDelaySeconds"]!,
                0.0,
                600.0,
                "chat treasure mirror");
            Require(
                afterTreasure["chat_automation"]!["treasure"]!["dispatchRetrySeconds"]!
                    .GetValue<int>() == 30,
                "treasure save preserves chat sibling fields");
            await ExpectInvalid(
                service,
                "red_packet_delay_configure",
                new { minSeconds = -0.01, maxSeconds = 1 },
                "red packet delay must be a valid range from 0 to 60 seconds");
            await ExpectInvalid(
                service,
                "red_packet_delay_configure",
                new { minSeconds = 2, maxSeconds = 1 },
                "red packet delay must be a valid range from 0 to 60 seconds");
            await ExpectInvalid(
                service,
                "red_packet_delay_configure",
                new { minSeconds = 0, maxSeconds = 60.01 },
                "red packet delay must be a valid range from 0 to 60 seconds");
            await ExpectInvalid(
                service,
                "red_packet_delay_configure",
                new { minSeconds = 0 },
                "red packet delay must be a valid range from 0 to 60 seconds");
            await ExpectInvalid(
                service,
                "treasure_delay_configure",
                new { minSeconds = 0, maxSeconds = 600.01 },
                "treasure delay must be a valid range from 0 to 600 seconds");
            await ExpectInvalid(
                service,
                "treasure_delay_configure",
                new { minSeconds = "0", maxSeconds = 1 },
                "treasure delay must be a valid range from 0 to 600 seconds");

            var backend = new LWBridgeBackend(
                new LocalConfigStore(persistent: false),
                asyncCommands: service);

            JsonElement backendResult =
                JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "red_packet_delay_configure",
                        JsonSerializer.SerializeToElement(
                            new { minSeconds = 2, maxSeconds = 3 },
                            JsonOptions.Default),
                        CancellationToken.None),
                    JsonOptions.Default);
            AssertSuccessRange(backendResult, 2.0, 3.0);

            JsonObject afterBackend = ReadConfig(configPath);
            AssertRange(
                afterBackend["scheduler"]!["redPacketClaimDelaySeconds"]!,
                2.0,
                3.0,
                "backend routes red packet delay");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }
    private static async Task<JsonElement> Invoke(
        ClaimDelayConfigCommandService service,
        string command,
        object payload)
    {
        object? result = await service.InvokeAsync(
            command,
            JsonSerializer.SerializeToElement(
                payload,
                JsonOptions.Default),
            CancellationToken.None);
        return JsonSerializer.SerializeToElement(
            result,
            JsonOptions.Default);
    }

    private static async Task ExpectInvalid(
        ClaimDelayConfigCommandService service,
        string command,
        object payload,
        string expectedMessage)
    {
        try
        {
            _ = await Invoke(service, command, payload);
            throw new InvalidOperationException(
                $"Expected {command} to fail.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == "INVALID_REQUEST",
                $"{command} uses INVALID_REQUEST");
            Require(
                error.Message == expectedMessage,
                $"{command} preserves native validation detail");
        }
    }

    private static JsonObject ReadConfig(string path) =>
        JsonNode.Parse(File.ReadAllText(path)) as JsonObject
        ?? throw new InvalidOperationException("runtime config is not object");
    private static void AssertSuccessRange(
        JsonElement result,
        double expectedMin,
        double expectedMax)
    {
        Require(
            result.GetProperty("ok").GetBoolean(),
            "delay configure result ok=true");
        JsonElement range = result.GetProperty("range");
        Require(
            range.ValueKind == JsonValueKind.Array &&
            range.GetArrayLength() == 2,
            "delay configure result has two-element range");
        Require(
            range[0].GetDouble() == expectedMin &&
            range[1].GetDouble() == expectedMax,
            "delay configure result preserves requested range");
    }

    private static void AssertRange(
        JsonNode node,
        double expectedMin,
        double expectedMax,
        string message)
    {
        if (node is not JsonArray array || array.Count != 2)
            throw new InvalidOperationException(message);

        Require(
            array[0]!.GetValue<double>() == expectedMin &&
            array[1]!.GetValue<double>() == expectedMax,
            message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
