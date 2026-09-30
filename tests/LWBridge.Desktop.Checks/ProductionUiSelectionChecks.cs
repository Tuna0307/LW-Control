using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ProductionUiSelectionChecks
{
    internal static void Run()
    {
        string repo = FindRepoRoot();
        string desktopOutput = Path.Combine(
            repo, "src", "LWBridge.Desktop", "bin", "Release", "net10.0-windows10.0.17763.0");

        DesktopUiSelection canonical = DesktopUiContentRoot.Select(
            desktopOutput,
            useLegacyUi: false,
            explicitUiRoot: null,
            allowProofOverride: false);
        Check(canonical.Mode == "canonical-production", "normal Desktop selection must be canonical production mode");
        Check(Path.GetFileName(canonical.RootPath) == DesktopUiContentRoot.CanonicalDirectoryName,
            "normal Desktop selection must resolve ProductionUi");
        Check(canonical.BuildIdentity is not null &&
              canonical.BuildIdentity.Project == DesktopUiContentRoot.CanonicalProject &&
              canonical.BuildIdentity.CanonicalSource == DesktopUiContentRoot.CanonicalSource,
            "normal Desktop package must carry the reconstructed UI build identity");

        string legacySource = Path.Combine(repo, "src", "LWBridge.Desktop", "WebUi", "index.html");
        Check(File.Exists(legacySource), "legacy WebUi must remain present for recovery/reference");

        string scratch = Path.Combine(Path.GetTempPath(), "lwbridge-ui-selection-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(scratch, DesktopUiContentRoot.LegacyDirectoryName));
            File.WriteAllText(Path.Combine(scratch, DesktopUiContentRoot.LegacyDirectoryName, "index.html"), "<!doctype html>");
            DesktopUiSelection legacy = DesktopUiContentRoot.Select(
                scratch,
                useLegacyUi: true,
                explicitUiRoot: null,
                allowProofOverride: false);
            Check(legacy.Mode == "legacy-recovery" && legacy.BuildIdentity is null,
                "legacy WebUi must require the deliberate recovery selection");

            string proof = Path.Combine(scratch, "proof");
            Directory.CreateDirectory(proof);
            File.WriteAllText(Path.Combine(proof, "index.html"), "<!doctype html>");
            bool arbitraryRootRejected = false;
            try
            {
                DesktopUiContentRoot.Select(scratch, false, proof, allowProofOverride: false);
            }
            catch (ArgumentException)
            {
                arbitraryRootRejected = true;
            }
            Check(arbitraryRootRejected, "arbitrary --ui-root must fail without explicit proof admission");
            Check(DesktopUiContentRoot.Select(scratch, false, proof, allowProofOverride: true).Mode == "proof-override",
                "explicit proof admission may use a controlled UI-root override");

            bool missingCanonicalRejected = false;
            try
            {
                DesktopUiContentRoot.Select(scratch, false, null, allowProofOverride: false);
            }
            catch (InvalidOperationException error)
            {
                missingCanonicalRejected = error.Message.Contains("Canonical production UI root", StringComparison.Ordinal);
            }
            Check(missingCanonicalRejected, "missing canonical UI assets must fail clearly without legacy fallback");

            string tamperedRoot = Path.Combine(scratch, "tampered-production");
            CopyDirectory(canonical.RootPath, tamperedRoot);
            string secondaryAsset = Directory.GetFiles(
                    Path.Combine(tamperedRoot, "assets"),
                    "*",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .First();
            File.AppendAllText(secondaryAsset, "\n/* tampered */");
            bool artifactMismatchRejected = false;
            try
            {
                DesktopUiContentRoot.ReadCanonicalIdentity(tamperedRoot);
            }
            catch (InvalidOperationException error)
            {
                artifactMismatchRejected = error.Message.Contains("artifact fingerprint", StringComparison.Ordinal);
            }
            Check(artifactMismatchRejected, "changed packaged UI assets must fail their build-identity fingerprint");
        }
        finally
        {
            if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
        }

        string hostSource = File.ReadAllText(Path.Combine(repo, "src", "LWBridge.Desktop", "LWBridgeWindow.cs"));
        Check(hostSource.Contains("window.__LWBridgeBootstrap=", StringComparison.Ordinal),
            "Desktop must keep bootstrap injection on the privileged WebView origin");
        string bridgeSource = File.ReadAllText(Path.Combine(repo, "src", "LWBridge.UI-0.3.17", "src", "backendBridge.js"));
        Check(bridgeSource.Contains("host.chrome?.webview", StringComparison.Ordinal) &&
              bridgeSource.Contains("nativeWebView.postMessage", StringComparison.Ordinal),
            "canonical frontend must keep the existing window.chrome.webview transport");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "task.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "src", "LWBridge.UI-0.3.17")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("could not locate LW-Control repository root");
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Production UI selection check failed: " + message);
    }
}
