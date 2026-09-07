using System.Globalization;
using System.Text.Json;

namespace LWControl.Desktop;

public static partial class UiText
{
    private static readonly IReadOnlyDictionary<string, string> ReferenceStrings = LoadCatalog("ReferenceUiStrings.json");
    private static readonly IReadOnlyDictionary<string, string> DesktopStrings = LoadCatalog("DesktopUiStrings.json");

    private static Dictionary<string, string> LoadCatalog(string name)
    {
        using var stream = typeof(UiText).Assembly.GetManifestResourceStream($"LWControl.Desktop.{name}")
            ?? throw new InvalidDataException($"Missing UI catalog: {name}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Empty UI catalog: {name}");
    }

    public static bool HasTranslation(string text) => text.Length == 0
        || DesktopStrings.ContainsKey(text) || ReferenceStrings.ContainsKey(text)
        || text.Split('\n').All(line => DesktopStrings.ContainsKey(line.TrimEnd('\r')) || ReferenceStrings.ContainsKey(line.TrimEnd('\r')));

    public static string Translate(UiLanguage language, string text)
    {
        if (language == UiLanguage.English || text.Length == 0) return text;
        if (DesktopStrings.TryGetValue(text, out var local)) return local;
        if (ReferenceStrings.TryGetValue(text, out var recovered)) return recovered;
        if (text.Contains('\n')) return string.Join("\n", text.Split('\n').Select(line => Translate(language, line.TrimEnd('\r'))));
        return text; // Preserve unknown diagnostic codes and user/game data verbatim.
    }

    public static string Format(UiLanguage language, string template, params object?[] args) =>
        string.Format(language == UiLanguage.SimplifiedChinese ? CultureInfo.GetCultureInfo("zh-CN") : CultureInfo.GetCultureInfo("en-US"),
            Translate(language, template), args);

    internal static string Feature(UiLanguage language, string id, string part, string english) =>
        language == UiLanguage.English ? english : ReferenceStrings[$"feature.{id}.{part}.{english}"];

    internal static bool HasFeature(string id, string part, string english) =>
        ReferenceStrings.ContainsKey($"feature.{id}.{part}.{english}");
}
