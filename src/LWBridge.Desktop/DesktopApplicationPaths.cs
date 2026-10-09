namespace LWBridge.Desktop;

internal sealed class DesktopApplicationPaths
{
    private const string ProductDirectoryName = "LWBridgeRebuild";

    private DesktopApplicationPaths(string root, bool explicitIsolation)
    {
        Root = Path.GetFullPath(root);
        ExplicitIsolation = explicitIsolation;
    }

    internal string Root { get; }
    internal bool ExplicitIsolation { get; }

    internal static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductDirectoryName);

    internal static DesktopApplicationPaths Create(string? explicitIsolatedRoot = null)
    {
        if (explicitIsolatedRoot is null)
            return new DesktopApplicationPaths(DefaultRoot, explicitIsolation: false);

        ArgumentException.ThrowIfNullOrWhiteSpace(explicitIsolatedRoot);
        string fullRoot = Path.GetFullPath(explicitIsolatedRoot);
        string fullDefault = Path.GetFullPath(DefaultRoot);
        string? volumeRoot = Path.GetPathRoot(fullRoot);
        if (!string.IsNullOrEmpty(volumeRoot) &&
            string.Equals(
                TrimDirectorySeparators(fullRoot),
                TrimDirectorySeparators(volumeRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "--isolated-root must not be a filesystem root.",
                nameof(explicitIsolatedRoot));
        }

        if (IsSameOrWithin(fullRoot, fullDefault) ||
            IsSameOrWithin(fullDefault, fullRoot))
        {
            throw new ArgumentException(
                "--isolated-root must not overlap the normal LWBridge data root.",
                nameof(explicitIsolatedRoot));
        }

        return new DesktopApplicationPaths(fullRoot, explicitIsolation: true);
    }

    internal string ControllerDatabasePath => Path.Combine(Root, "controller.db");
    internal string OverviewRuntimeRoot => Path.Combine(Root, "overview-bridge");
    internal string OverviewEvidenceRoot => Path.Combine(Root, "overview-evidence");
    internal string OverviewBackupRoot => Path.Combine(Root, "overview-bridge-backups");
    internal string LiveResourceRuntimeRoot => Path.Combine(Root, "live-resource");
    internal string LocaleCacheRoot => Path.Combine(Root, "locales");

    internal string ProfileRoot(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        return Path.Combine(Root, "profiles", profileId);
    }

    internal string ProfileDatabasePath(string profileId) =>
        Path.Combine(ProfileRoot(profileId), "profile.db");

    internal string ProfileRuntimeDirectory(string profileId) =>
        Path.Combine(ProfileRoot(profileId), "runtime");

    internal string RuntimeConfigPath(string profileId) =>
        Path.Combine(ProfileRuntimeDirectory(profileId), "config.json");

    internal string MapDatabasePath(string profileId) =>
        Path.Combine(ProfileRoot(profileId), "map-data", "map-data.db");

    internal string LegacyLiveResourceMapDatabasePath(string profileId) =>
        Path.Combine(ProfileRoot(profileId), "map-data.db");

    internal string WebViewUserDataRoot(string mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        return Path.Combine(Root, mode);
    }

    internal bool Contains(string path) => IsSameOrWithin(Path.GetFullPath(path), Root);

    private static bool IsSameOrWithin(string candidate, string root)
    {
        string relative = Path.GetRelativePath(
            Path.GetFullPath(root),
            Path.GetFullPath(candidate));
        return relative == "." ||
            (!Path.IsPathRooted(relative) &&
             relative != ".." &&
             !relative.StartsWith(
                 ".." + Path.DirectorySeparatorChar,
                 StringComparison.Ordinal) &&
             !relative.StartsWith(
                 ".." + Path.AltDirectorySeparatorChar,
                 StringComparison.Ordinal));
    }

    private static string TrimDirectorySeparators(string value) =>
        value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
