namespace LWControl.Core;

public static class RuntimePaths
{
    public static string CanonicalLocalApplicationData => ResolveLocalApplicationData(
        Environment.GetEnvironmentVariable("LOCALAPPDATA"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static string ResolveLocalApplicationData(string? localApplicationData, string? userProfile)
    {
        string configured = string.IsNullOrWhiteSpace(localApplicationData)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : localApplicationData;
        string full = Path.GetFullPath(configured);
        if (string.IsNullOrWhiteSpace(userProfile)) return full;

        string canonical = Path.GetFullPath(Path.Combine(userProfile, "AppData", "Local"));
        string packages = canonical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar + "Packages" + Path.DirectorySeparatorChar;
        if (!full.StartsWith(packages, StringComparison.OrdinalIgnoreCase)) return full;

        string suffix = full[packages.Length..];
        string marker = Path.DirectorySeparatorChar + "LocalCache" + Path.DirectorySeparatorChar + "Local";
        if (suffix.Contains(marker, StringComparison.OrdinalIgnoreCase)
            || suffix.EndsWith("LocalCache" + Path.DirectorySeparatorChar + "Local", StringComparison.OrdinalIgnoreCase))
            return canonical;
        return full;
    }

    public static string LWControlRuntimeDirectory =>
        Path.Combine(CanonicalLocalApplicationData, "LWControl", "runtime");
}
