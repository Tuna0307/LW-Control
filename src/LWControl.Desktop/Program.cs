namespace LWControl.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--smoke-test"))
        {
            var directory = Path.Combine(Path.GetTempPath(), $"lwcontrol-smoke-{Guid.NewGuid():N}");
            try
            {
                using var form = new MainForm(Path.Combine(directory, "settings.json"));
                form.Show();
                Application.DoEvents();
                form.RunSmokeCheck();
                int outputIndex = Array.IndexOf(args, "--smoke-output");
                if (outputIndex >= 0 && outputIndex + 1 >= args.Length)
                    throw new ArgumentException("--smoke-output requires a directory.");
                form.RunAppearanceSmokeCheck(outputIndex >= 0 ? Path.GetFullPath(args[outputIndex + 1]) : null);
                form.RunLocalizationSmokeCheck(outputIndex >= 0 ? Path.GetFullPath(args[outputIndex + 1]) : null);
                Console.WriteLine("PASS desktop smoke checks.");
                form.Close();
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
            return;
        }
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWControlRebuild", "settings.json");
        Application.Run(new MainForm(path));
    }
}
