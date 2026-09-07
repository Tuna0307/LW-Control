namespace LWControl.Desktop;

// Palette values come from the recovered overlay CSS, not the theme-picker swatches.
internal sealed record DesktopPalette(string Name, string Primary, string Highlight)
{
    public Color PrimaryColor => ColorTranslator.FromHtml(Primary);
    public Color HighlightColor => ColorTranslator.FromHtml(Highlight);

    public static readonly IReadOnlyList<DesktopPalette> All =
    [
        new("Cyan", "#06b6d4", "#67e8f9"),
        new("Gold", "#f59e0b", "#fcd34d"),
        new("Indigo", "#5e6ad2", "#aeb5ff"),
        new("Rose", "#f43f5e", "#fda4af"),
        new("Emerald", "#10b981", "#6ee7b7"),
    ];

    public static DesktopPalette Find(string name) => All.First(item => item.Name == name);
}

internal sealed record DesktopAppearance
{
    public string Accent { get; init; } = "Indigo";
    public string Language { get; init; } = UiText.DetectDefault().ToString();

    public void Validate()
    {
        if (!DesktopPalette.All.Any(item => item.Name == Accent))
            throw new InvalidDataException("Unknown appearance accent.");
        if (Language != nameof(UiLanguage.English) && Language != nameof(UiLanguage.SimplifiedChinese))
            throw new InvalidDataException("Unknown appearance language.");
    }
}
