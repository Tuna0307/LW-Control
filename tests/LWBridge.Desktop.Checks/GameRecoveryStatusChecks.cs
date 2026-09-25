using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class GameRecoveryStatusChecks
{
    internal static async Task RunAsync()
    {
        const string profileId = "profile-r8-041";
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-recovery-status-" + Guid.NewGuid().ToString("N"));
        string runtime = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtime);
        try
        {
            OverviewRecoveryStatus idle = new(
                "idle", null, false, false,
                0, null, 0, null, null, 0, false);
            var service = new GameRecoveryStatusCommandService(
                profileId,
                runtime,
                () => idle);

            using JsonDocument payload = JsonDocument.Parse(
                JsonSerializer.Serialize(new { profileId }));
            JsonElement idleJson = JsonSerializer.SerializeToElement(
                service.Invoke(payload.RootElement.Clone()),
                JsonOptions.Default);

            RequireFields(
                idleJson,
                "state", "reason", "updateDetected", "restarted",
                "startedAt", "completedAt", "attempts", "nextRetryAt",
                "error", "noticeId", "noticeVisible");
            Require(
                idleJson.GetProperty("state").GetString() == "idle" &&
                idleJson.GetProperty("reason").ValueKind == JsonValueKind.Null &&
                !idleJson.GetProperty("updateDetected").GetBoolean() &&
                !idleJson.GetProperty("restarted").GetBoolean() &&
                idleJson.GetProperty("startedAt").GetInt64() == 0 &&
                idleJson.GetProperty("completedAt").ValueKind == JsonValueKind.Null &&
                idleJson.GetProperty("attempts").GetInt32() == 0 &&
                idleJson.GetProperty("nextRetryAt").ValueKind == JsonValueKind.Null &&
                idleJson.GetProperty("error").ValueKind == JsonValueKind.Null &&
                idleJson.GetProperty("noticeId").ValueKind == JsonValueKind.Number &&
                idleJson.GetProperty("noticeId").GetUInt64() == 0 &&
                !idleJson.GetProperty("noticeVisible").GetBoolean(),
                "native idle recovery status uses exact defaults and scalar types");

            OverviewRecoveryStatus succeeded = new(
                "succeeded", "forceUpdate", true, true,
                1000, 2000, 2, null, null, 7, true);
            var succeededService = new GameRecoveryStatusCommandService(
                profileId,
                runtime,
                () => succeeded);
            JsonElement succeededJson = JsonSerializer.SerializeToElement(
                succeededService.Invoke(payload.RootElement.Clone()),
                JsonOptions.Default);
            Require(
                succeededJson.GetProperty("state").GetString() == "succeeded" &&
                succeededJson.GetProperty("reason").GetString() == "forceUpdate" &&
                succeededJson.GetProperty("updateDetected").GetBoolean() &&
                succeededJson.GetProperty("restarted").GetBoolean() &&
                succeededJson.GetProperty("startedAt").GetInt64() == 1000 &&
                succeededJson.GetProperty("completedAt").GetInt64() == 2000 &&
                succeededJson.GetProperty("attempts").GetInt32() == 2 &&
                succeededJson.GetProperty("noticeId").GetUInt64() == 7 &&
                succeededJson.GetProperty("noticeVisible").GetBoolean(),
                "terminal succeeded status preserves native values and visible notice");

            OverviewRecoveryStatus failed = new(
                "failed", "disconnect", false, false,
                3000, 4000, 3, null, "BRIDGE_START_TIMEOUT", 8, true);
            var failedService = new GameRecoveryStatusCommandService(
                profileId,
                runtime,
                () => failed);
            JsonElement failedJson = JsonSerializer.SerializeToElement(
                failedService.Invoke(payload.RootElement.Clone()),
                JsonOptions.Default);
            Require(
                failedJson.GetProperty("state").GetString() == "failed" &&
                failedJson.GetProperty("error").GetString() == "BRIDGE_START_TIMEOUT" &&
                failedJson.GetProperty("noticeId").GetUInt64() == 8 &&
                failedJson.GetProperty("noticeVisible").GetBoolean(),
                "terminal failed status exposes error and visible numeric notice");

            using JsonDocument empty = JsonDocument.Parse("{}");
            ExpectError(
                () => service.Invoke(empty.RootElement.Clone()),
                "PROFILE_ID_REQUIRED",
                null,
                "missing profileId");
            using JsonDocument wrongType = JsonDocument.Parse(
                """{"profileId":123}""");
            ExpectError(
                () => service.Invoke(wrongType.RootElement.Clone()),
                "PROFILE_ID_REQUIRED",
                null,
                "non-string profileId");
            using JsonDocument foreign = JsonDocument.Parse(
                """{"profileId":"foreign"}""");
            ExpectError(
                () => service.Invoke(foreign.RootElement.Clone()),
                "PROFILE_RUNTIME_UNAVAILABLE",
                null,
                "foreign profile runtime");

            var missingRuntime = new GameRecoveryStatusCommandService(
                profileId,
                null,
                () => idle);
            ExpectError(
                () => missingRuntime.Invoke(payload.RootElement.Clone()),
                "PROFILE_RUNTIME_UNAVAILABLE",
                null,
                "missing profile runtime");

            var missingState = new GameRecoveryStatusCommandService(
                profileId,
                runtime,
                () => null);
            ExpectError(
                () => missingState.Invoke(payload.RootElement.Clone()),
                "STATE_UNAVAILABLE",
                "game recovery state is unavailable",
                "missing recovery state");
            var config = new LocalConfigStore(Path.Combine(root, "config"));
            config.Update(c => c with { ProfileId = profileId });
            var backend = new LWBridgeBackend(
                config,
                profileRuntimeDirectory: runtime);
            await ExpectErrorAsync(
                () => backend.InvokeAsync(
                    "game_recovery_status",
                    empty.RootElement.Clone(),
                    CancellationToken.None),
                "PROFILE_ID_REQUIRED",
                "backend preserves native recovery profile validation");

            await ExpectErrorAsync(
                () => backend.InvokeAsync(
                    "game_recovery_status",
                    payload.RootElement.Clone(),
                    CancellationToken.None),
                "STATE_UNAVAILABLE",
                "backend does not synthesize idle when recovery state is absent",
                "game recovery state is unavailable");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static void RequireFields(
        JsonElement value,
        params string[] expected)
    {
        string[] actual = value.EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        Require(
            actual.SequenceEqual(expected),
            $"game_recovery_status fields mismatch: {string.Join(",", actual)}");
    }

    private static void ExpectError(
        Action action,
        string expectedCode,
        string? expectedMessage,
        string label)
    {
        try
        {
            action();
            throw new InvalidOperationException(
                $"{label}: expected {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"{label}: expected {expectedCode}, got {error.Code}");
            if (expectedMessage is not null)
                Require(
                    error.Message == expectedMessage,
                    $"{label}: expected '{expectedMessage}', got '{error.Message}'");
        }
    }

    private static async Task ExpectErrorAsync(
        Func<Task<object?>> action,
        string expectedCode,
        string label,
        string? expectedMessage = null)
    {
        try
        {
            await action();
            throw new InvalidOperationException(
                $"{label}: expected {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"{label}: expected {expectedCode}, got {error.Code}");
            if (expectedMessage is not null)
                Require(
                    error.Message == expectedMessage,
                    $"{label}: expected '{expectedMessage}', got '{error.Message}'");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
