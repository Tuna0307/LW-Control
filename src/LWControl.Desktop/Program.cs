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
