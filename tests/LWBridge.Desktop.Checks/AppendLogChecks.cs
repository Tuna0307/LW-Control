using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class AppendLogChecks
{
    internal static async Task RunAsync()
    {
        const string profileId = "append-log-profile";
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-append-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            long now = 1_700_000_000_123;
            var service = new AppendLogCommandService(
                profileId,
                root,
                () => now++);

            object? first = service.Invoke(Payload(new
            {
                profileId,
                message = "alpha\r\nbeta\nend",
            }));
            Require(first is null, "append_log returns null on success");

            string logPath = Path.Combine(
                root,
                "logs",
                "xlua-bridge.log");
            string firstText = File.ReadAllText(
                logPath,
                Encoding.UTF8);
            Require(
                firstText ==
                    "[1700000000123] [bridge-app] alpha  beta end\n",
                "append_log writes exact timestamp/source/message line");

            service.Invoke(Payload(new
            {
                profileId,
                message = string.Empty,
            }));
            string appended = File.ReadAllText(
                logPath,
                Encoding.UTF8);
            Require(
                appended ==
                    "[1700000000123] [bridge-app] alpha  beta end\n" +
                    "[1700000000124] [bridge-app] \n",
                "append_log appends and preserves empty string messages");
            string emojiInput =
                string.Concat(Enumerable.Repeat("😀", 2001));
            string emojiNormalized =
                AppendLogCommandService.NormalizeMessage(emojiInput);
            Require(
                emojiNormalized.EnumerateRunes().Count() == 2000,
                "append_log caps messages at 2000 Unicode scalar values");
            Require(
                emojiNormalized.Length == 4000 &&
                emojiNormalized.EndsWith("😀", StringComparison.Ordinal),
                "append_log truncation never splits a surrogate pair");

            string sanitized =
                AppendLogCommandService.NormalizeMessage("a\rb\nc\r\nd");
            Require(
                sanitized == "a b c  d",
                "append_log replaces every CR and LF scalar with a space");

            ExpectCode(
                () => service.Invoke(Payload(new { message = "x" })),
                "PROFILE_ID_REQUIRED",
                "missing profileId");
            ExpectCode(
                () => service.Invoke(Payload(new
                {
                    profileId = 7,
                    message = "x",
                })),
                "PROFILE_ID_REQUIRED",
                "non-string profileId");
            ExpectCode(
                () => service.Invoke(Payload(new
                {
                    profileId = " ",
                    message = "x",
                })),
                "PROFILE_ID_REQUIRED",
                "blank profileId");
            ExpectCode(
                () => service.Invoke(Payload(new
                {
                    profileId = "other-profile",
                    message = "x",
                })),
                "PROFILE_RUNTIME_UNAVAILABLE",
                "unknown profile runtime");
            ExpectCode(
                () => service.Invoke(Payload(new { profileId })),
                "INVALID_PAYLOAD",
                "missing message is rejected");
            ExpectCode(
                () => service.Invoke(Payload(new
                {
                    profileId,
                    message = 123,
                })),
                "INVALID_PAYLOAD",
                "non-string message is rejected");

            var unavailable = new AppendLogCommandService(
                profileId,
                runtimeDirectory: null,
                () => 1);
            ExpectCode(
                () => unavailable.Invoke(Payload(new
                {
                    profileId,
                    message = "x",
                })),
                "PROFILE_RUNTIME_UNAVAILABLE",
                "missing runtime directory");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }

        string blockedRoot = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-append-log-blocked-" +
            Guid.NewGuid().ToString("N"));
        File.WriteAllText(blockedRoot, "not a directory");
        try
        {
            var bestEffort = new AppendLogCommandService(
                profileId,
                blockedRoot,
                () => 5);
            object? result = bestEffort.Invoke(Payload(new
            {
                profileId,
                message = "write failure is swallowed",
            }));
            Require(
                result is null,
                "append_log keeps native best-effort success on I/O failure");
        }
        finally
        {
            try { File.Delete(blockedRoot); }
            catch { }
        }

        string configRoot = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-append-log-backend-" +
            Guid.NewGuid().ToString("N"));
        string runtimeRoot = Path.Combine(configRoot, "runtime");
        Directory.CreateDirectory(configRoot);
        try
        {
            var config = new LocalConfigStore(configRoot);
            var backend = new LWBridgeBackend(
                config,
                profileRuntimeDirectory: runtimeRoot);
            object? result = await backend.InvokeAsync(
                "append_log",
                Payload(new
                {
                    profileId = backend.ProfileId,
                    message = "backend route",
                }),
                CancellationToken.None);
            Require(result is null, "backend append_log returns null");

            string path = Path.Combine(
                runtimeRoot,
                "logs",
                "xlua-bridge.log");
            string text = File.ReadAllText(path, Encoding.UTF8);
            Require(
                text.Contains(
                    "] [bridge-app] backend route\n",
                    StringComparison.Ordinal),
                "backend routes append_log to profile runtime log");
        }
        finally
        {
            try { Directory.Delete(configRoot, recursive: true); }
            catch { }
        }
    }

    private static JsonElement Payload(object value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static void ExpectCode(
        Action action,
        string expectedCode,
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
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
