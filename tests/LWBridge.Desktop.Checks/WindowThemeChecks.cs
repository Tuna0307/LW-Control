using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class WindowThemeChecks
{
    internal static Task RunAsync()
    {
        const nint windowHandle = 0x1234;

        var calls = new List<(nint Window, int Attribute, uint Value, int Size)>();
        var service = new WindowThemeService(
            (window, attribute, value, size) =>
            {
                calls.Add((window, attribute, value, size));
                return 0;
            });

        object? darkResult = service.Apply(
            windowHandle,
            JsonSerializer.SerializeToElement(
                new { theme = "dark" },
                JsonOptions.Default));
        Require(darkResult is null, "dark theme returns null");
        AssertCalls(
            calls,
            windowHandle,
            [
                (20, 0x00000001u),
                (35, 0x00282828u),
                (36, 0x00F7F5F5u),
                (34, 0x003C3A3Au),
            ],
            "dark");

        calls.Clear();
        object? lightResult = service.Apply(
            windowHandle,
            JsonSerializer.SerializeToElement(
                new { theme = "light" },
                JsonOptions.Default));
        Require(lightResult is null, "light theme returns null");
        AssertCalls(
            calls,
            windowHandle,
            [
                (20, 0x00000000u),
                (35, 0x00FFFFFFu),
                (36, 0x001F1D1Du),
                (34, 0x00EAE5E5u),
            ],
            "light");

        ExpectCode(
            service,
            windowHandle,
            new { theme = "system" },
            "INVALID_THEME",
            "theme must be light or dark");

        calls.Clear();
        int callIndex = 0;
        var failing = new WindowThemeService(
            (window, attribute, value, size) =>
            {
                calls.Add((window, attribute, value, size));
                callIndex++;
                return callIndex == 3 ? unchecked((int)0x80004005) : 0;
            });

        ExpectCode(
            failing,
            windowHandle,
            new { theme = "dark" },
            "WINDOW_THEME_FAILED",
            "DwmSetWindowAttribute failed: -2147467259");
        Require(
            calls.Count == 3 &&
            calls[2].Attribute == 36,
            "theme application stops at first failing DWM attribute");

        return Task.CompletedTask;
    }

    private static void AssertCalls(
        IReadOnlyList<(nint Window, int Attribute, uint Value, int Size)> calls,
        nint expectedWindow,
        IReadOnlyList<(int Attribute, uint Value)> expected,
        string label)
    {
        Require(
            calls.Count == expected.Count,
            $"{label} theme writes four DWM attributes");
        for (int index = 0; index < expected.Count; index++)
        {
            Require(
                calls[index].Window == expectedWindow,
                $"{label} theme preserves window handle");
            Require(
                calls[index].Attribute == expected[index].Attribute,
                $"{label} theme attribute {index} matches native order");
            Require(
                calls[index].Value == expected[index].Value,
                $"{label} theme value {index} matches native value");
            Require(
                calls[index].Size == sizeof(uint),
                $"{label} theme DWM value is four bytes");
        }
    }

    private static void ExpectCode(
        WindowThemeService service,
        nint windowHandle,
        object payload,
        string expectedCode,
        string expectedMessage)
    {
        try
        {
            _ = service.Apply(
                windowHandle,
                JsonSerializer.SerializeToElement(
                    payload,
                    JsonOptions.Default));
            throw new InvalidOperationException(
                $"Expected {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
            Require(
                error.Message == expectedMessage,
                $"{expectedCode} preserves recovered detail");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
