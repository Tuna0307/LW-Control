using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class WindowThemeService
{
    internal delegate int SetWindowAttribute(
        nint windowHandle,
        int attribute,
        uint value,
        int valueSize);

    private readonly SetWindowAttribute setWindowAttribute;

    internal WindowThemeService(
        SetWindowAttribute? setWindowAttribute = null)
    {
        this.setWindowAttribute =
            setWindowAttribute ?? ApplyDwmAttribute;
    }

    internal object? Apply(
        nint windowHandle,
        JsonElement payload)
    {
        string? theme = payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("theme", out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        bool dark;
        if (string.Equals(theme, "dark", StringComparison.Ordinal))
            dark = true;
        else if (string.Equals(theme, "light", StringComparison.Ordinal))
            dark = false;
        else
            throw new BridgeCommandException(
                "INVALID_THEME",
                "theme must be light or dark");

        WindowThemeAttribute[] settings =
            dark
                ?
                [
                    new(20, 1),
                    new(35, 0x00282828),
                    new(36, 0x00F7F5F5),
                    new(34, 0x003C3A3A),
                ]
                :
                [
                    new(20, 0),
                    new(35, 0x00FFFFFF),
                    new(36, 0x001F1D1D),
                    new(34, 0x00EAE5E5),
                ];

        foreach (WindowThemeAttribute setting in settings)
        {
            int result = setWindowAttribute(
                windowHandle,
                setting.Attribute,
                setting.Value,
                sizeof(uint));
            if (result < 0)
            {
                throw new BridgeCommandException(
                    "WINDOW_THEME_FAILED",
                    "DwmSetWindowAttribute failed: " +
                    result.ToString(CultureInfo.InvariantCulture));
            }
        }

        return null;
    }

    private static int ApplyDwmAttribute(
        nint windowHandle,
        int attribute,
        uint value,
        int valueSize)
    {
        uint mutableValue = value;
        return DwmSetWindowAttribute(
            windowHandle,
            attribute,
            ref mutableValue,
            valueSize);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref uint attributeValue,
        int attributeSize);

    private readonly record struct WindowThemeAttribute(
        int Attribute,
        uint Value);
}
