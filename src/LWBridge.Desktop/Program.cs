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
        string? firstLiveResultPath = ReadPathOption(args, "--first-live-result");
        string? liveResourceProofPath = ReadPathOption(args, "--live-resource-proof");
        string? liveCityProofPath = ReadPathOption(args, "--live-city-proof");
        string? normalUiLiveResourceProofPath = ReadPathOption(args, "--normal-ui-live-resource-proof");
        string? ownerEvidencePath = ReadPathOption(args, "--owner-evidence");
        string? cityReopenProofPath = ReadPathOption(args, "--city-reopen-proof");
        if (cityReopenProofPath is not null)
        {
            if (new[] { capturePath, liveProbePath, hostProbePath, firstLiveResultPath, liveResourceProofPath, liveCityProofPath, normalUiLiveResourceProofPath, ownerEvidencePath }.Any(path => path is not null))
                throw new ArgumentException("--city-reopen-proof cannot be combined with other probe/capture modes.");
            LiveResourceProofRunner.RunCityReopenAsync(cityReopenProofPath).GetAwaiter().GetResult();
            return;
        }
        if (liveResourceProofPath is not null || liveCityProofPath is not null)
        {
            if (liveResourceProofPath is not null && liveCityProofPath is not null)
                throw new ArgumentException("--live-resource-proof and --live-city-proof are mutually exclusive.");
            if (capturePath is not null || liveProbePath is not null || hostProbePath is not null || firstLiveResultPath is not null || normalUiLiveResourceProofPath is not null || ownerEvidencePath is not null)
                throw new ArgumentException("Live map proof mode cannot be combined with other probe/capture modes.");
            string proofPath = liveCityProofPath ?? liveResourceProofPath!;
            LiveResourceProofRunner.RunTwiceAsync(proofPath, liveCityProofPath is not null ? "city" : "resource").GetAwaiter().GetResult();
            return;
        }
        if (normalUiLiveResourceProofPath is not null &&
            (capturePath is not null || liveProbePath is not null || hostProbePath is not null || firstLiveResultPath is not null || ownerEvidencePath is not null))
        {
            throw new ArgumentException("--normal-ui-live-resource-proof cannot be combined with other probe/capture modes.");
        }
        if (new[] { capturePath, liveProbePath, hostProbePath }.Count(path => path is not null) > 1)
            throw new ArgumentException("--capture, --live-probe and --host-probe are mutually exclusive.");
        if (firstLiveResultPath is not null && (liveProbePath is not null || hostProbePath is not null))
            throw new ArgumentException("--first-live-result cannot be combined with --live-probe or --host-probe.");
        if (ownerEvidencePath is not null && (capturePath is not null || liveProbePath is not null || hostProbePath is not null || firstLiveResultPath is not null))
            throw new ArgumentException("--owner-evidence is passive normal-app instrumentation and cannot be combined with capture/probe/replay modes.");
        string initialView = ReadValueOption(args, "--view") ?? "overview";
        string? language = ReadValueOption(args, "--language");
        string? theme = ReadValueOption(args, "--theme");
        var window = new LWBridgeWindow(
            capturePath, liveProbePath, hostProbePath, initialView, language, theme, firstLiveResultPath,
            normalUiLiveResourceProofPath, ownerEvidencePath);
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
