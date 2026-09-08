namespace LWBridge.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string? capturePath = ReadPathOption(args, "--capture");
        string? liveProbePath = ReadPathOption(args, "--live-probe");
        if (capturePath is not null && liveProbePath is not null)
            throw new ArgumentException("--capture and --live-probe cannot be combined.");
        string initialView = ReadValueOption(args, "--view") ?? "overview";
        string? language = ReadValueOption(args, "--language");
        string? theme = ReadValueOption(args, "--theme");
        Application.Run(new LWBridgeWindow(capturePath, liveProbePath, initialView, language, theme));
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
