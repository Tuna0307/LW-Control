using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class FeedbackExportContractChecks
{
    internal static async Task RunAsync()
    {
        var backend = new LWBridgeBackend(new LocalConfigStore(persistent: false));

        await ExpectError(
            backend,
            new { profileId = backend.ProfileId },
            "FEEDBACK_EXPORT_FAILED",
            "export ID is required");

        await ExpectError(
            backend,
            new { profileId = backend.ProfileId, exportId = 123 },
            "FEEDBACK_EXPORT_FAILED",
            "export ID is required");

        await ExpectError(
            backend,
            new { profileId = backend.ProfileId, exportId = "" },
            "FEEDBACK_EXPORT_FAILED",
            "export ID is required");

        await ExpectError(
            backend,
            new { profileId = backend.ProfileId, exportId = " \t\r\n\u3000 " },
            "FEEDBACK_EXPORT_FAILED",
            "export ID is required");

        await ExpectError(
            backend,
            new
            {
                profileId = backend.ProfileId,
                exportId = "550e8400-e29b-41d4-a716-446655440000",
            },
            "COMMAND_NOT_IMPLEMENTED",
            "Feedback archive export remains fenced");

        await ExpectError(
            backend,
            new
            {
                profileId = "different-profile",
                exportId = "550e8400-e29b-41d4-a716-446655440000",
            },
            "PROFILE_SCOPE_MISMATCH",
            "different local profile");
    }

    private static async Task ExpectError(
        LWBridgeBackend backend,
        object payload,
        string expectedCode,
        string expectedMessageFragment)
    {
        JsonElement request =
            JsonSerializer.SerializeToElement(payload, JsonOptions.Default);
        try
        {
            _ = await backend.InvokeAsync(
                "feedback_export",
                request,
                CancellationToken.None);
            throw new InvalidOperationException(
                $"Expected feedback_export to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
            Require(
                error.Message.Contains(
                    expectedMessageFragment,
                    StringComparison.Ordinal),
                $"expected message containing '{expectedMessageFragment}', got '{error.Message}'");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
