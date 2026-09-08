namespace LWBridge.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string? capturePath = ReadPathOption(args, "--capture");
        string? liveProbePath = ReadPathOption(args, "--live-probe");
        string? hostProbePath = ReadPathOption(args, "--host-probe");
        if (new[] { capturePath, liveProbePath, hostProbePath }.Count(path => path is not null) > 1)
            throw new ArgumentException("--capture, --live-probe and --host-probe are mutually exclusive.");
        string initialView = ReadValueOption(args, "--view") ?? "overview";
        string? language = ReadValueOption(args, "--language");
        string? theme = ReadValueOption(args, "--theme");
        var window = new LWBridgeWindow(capturePath, liveProbePath, hostProbePath, initialView, language, theme);
        if (hostProbePath is null)
        {
            Application.Run(window);
            return;
        }

        // IMPLEMENTATION POLICY: the isolated host probe deliberately closes its
        // real window before releasing a late backend completion. Keep the UI
        // thread alive just long enough to persist that post-close evidence.
        using var context = new ApplicationContext();
        window.HostProbeFinished += (_, _) => context.ExitThread();
        window.Show();
        Application.Run(context);
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
