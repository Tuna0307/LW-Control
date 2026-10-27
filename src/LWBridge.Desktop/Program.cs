namespace LWBridge.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string? capturePath = ReadPathOption(args, "--capture");
        string? normalUiLiveMapProofPath = ReadPathOption(args, "--normal-ui-live-map-proof");
        string? isolatedRootPath = ReadPathOption(args, "--isolated-root");
        if (isolatedRootPath is not null && capturePath is not null)
            throw new ArgumentException("--isolated-root is restricted to normal production composition.");
        if (isolatedRootPath is not null && normalUiLiveMapProofPath is not null)
        {
            string relative = Path.GetRelativePath(isolatedRootPath, normalUiLiveMapProofPath);
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException("Normal production proof output must remain inside its explicitly isolated root.");
        }
        if (normalUiLiveMapProofPath is not null && capturePath is not null)
            throw new ArgumentException("--normal-ui-live-map-proof cannot be combined with other probe/capture modes.");
        string initialView = ReadValueOption(args, "--view") ?? "overview";
        if (capturePath is null)
            StartupMapScanCleanup.Clear(DesktopApplicationPaths.Create(isolatedRootPath).Root);
        string? language = ReadValueOption(args, "--language");
        string? theme = ReadValueOption(args, "--theme");
        var window = new LWBridgeWindow(
            capturePath, initialView, language, theme, normalUiLiveMapProofPath, isolatedRootPath);
        Application.Run(window);
        window.FinalizeIsolatedProbeStorage();
    }

    private static string? ReadPathOption(string[] args, string name)
    {
        string? value = ReadValueOption(args, name);
        return value is null ? null : Path.GetFullPath(value);
    }

    private static string? ReadValueOption(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0) return null;
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{name} requires a value.");
        return args[index + 1];
    }
}
