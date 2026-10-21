using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Owner direction 2026-10-21 (OWN_DESIGN): screen-only fixes appear without restarting.
    // Watches ONLY this copy's own local ProductionUi folder. A deploy writes the build
    // identity file last; once that identity is readable and its fingerprint changed, the
    // WebView reloads. No network source, no game/bridge code is reloaded by this path.
    private FileSystemWatcher? liveUiWatcher;
    private System.Windows.Forms.Timer? liveUiDebounce;
    private string? liveUiFingerprint;

    private void StartLiveUiReload(CoreWebView2 core)
    {
        if (!liveUiReloadAllowed || liveUiWatcher is not null) return;
        try { liveUiFingerprint = DesktopUiContentRoot.ReadCanonicalIdentity(uiRootPath).ArtifactFingerprint; }
        catch (Exception) { return; }
        liveUiDebounce = new System.Windows.Forms.Timer { Interval = 800 };
        liveUiDebounce.Tick += (_, _) =>
        {
            liveUiDebounce!.Stop();
            string fingerprint;
            try { fingerprint = DesktopUiContentRoot.ReadCanonicalIdentity(uiRootPath).ArtifactFingerprint; }
            catch (Exception) { return; } // Mid-copy or invalid: wait for the next complete write.
            if (fingerprint == liveUiFingerprint) return;
            liveUiFingerprint = fingerprint;
            core.Reload();
        };
        liveUiWatcher = new FileSystemWatcher(uiRootPath, DesktopUiContentRoot.IdentityFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        void Changed(object? sender, FileSystemEventArgs args)
        {
            if (IsDisposed || Disposing) return;
            try { BeginInvoke(new Action(() => { liveUiDebounce?.Stop();liveUiDebounce?.Start(); })); }
            catch (InvalidOperationException) { }
        }
        liveUiWatcher.Changed += Changed;
        liveUiWatcher.Created += Changed;
        liveUiWatcher.Renamed += (sender, args) => Changed(sender, args);
        liveUiWatcher.EnableRaisingEvents = true;
    }

    private void StopLiveUiReload()
    {
        liveUiWatcher?.Dispose();liveUiWatcher = null;
        liveUiDebounce?.Dispose();liveUiDebounce = null;
    }
}
