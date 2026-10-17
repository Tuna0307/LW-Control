using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;

namespace LWBridge.Desktop;

/// <summary>
/// Ephemeral, non-activating status display over the exact focused game HWND.
/// This is a read-only display of source-confirmed MAIN_SHIELD; never guesses
/// an expiry from inventory or treats a shield ItemUse dispatch as effective.
/// </summary>
internal sealed class Feature018ShieldStatusOverlay : Form
{
    private const int NoActivate = 0x08000000;
    private const int ToolWindow = 0x00000080;
    private readonly Label status;
    private readonly System.Windows.Forms.Timer timer;
    private readonly DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
    private readonly double originalRemaining;
    private readonly bool active;
    private readonly nint ownedGameHwnd;

    internal static bool TryRead(JsonElement observation, out bool active, out double remaining)
    {
        active = false; remaining = 0;
        if (observation.ValueKind != JsonValueKind.Object ||
            !observation.TryGetProperty("observation", out var kind) ||
            kind.ValueKind != JsonValueKind.String ||
            kind.GetString() != "native_MAIN_SHIELD_effect_status_map" ||
            !observation.TryGetProperty("active", out var state) ||
            state.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !observation.TryGetProperty("remainingMilliseconds", out var remainingValue) ||
            !remainingValue.TryGetDouble(out remaining) ||
            !double.IsFinite(remaining) || remaining < 0 ||
            !observation.TryGetProperty("shieldStatusCount", out var count) ||
            !count.TryGetInt32(out int countValue) || countValue < 0)
            return false;
        active = state.GetBoolean();
        // Multiple simultaneous MAIN_SHIELD entries have different end times.
        // Without one authoritative expiry we must not display a fake timer.
        return active == (countValue == 1) && countValue <= 1 &&
            (active ? remaining > 0 : remaining == 0);
    }

    internal static bool ShowIfCurrentOwner(
        Feature018HotkeyCapture? capture, JsonElement observation)
    {
        if (capture?.IsExactForegroundOwner() != true ||
            !TryRead(observation, out bool active, out double remaining))
            return false;
        nint window = GetForegroundWindow();
        if (window == 0 || !GetWindowRect(window, out Rect bounds))
            return false;
        try
        {
            new Feature018ShieldStatusOverlay(window, bounds, active, remaining).Show();
            return true;
        }
        catch { return false; }
    }

    private Feature018ShieldStatusOverlay(nint hwnd, Rect bounds, bool active, double remaining)
    {
        ownedGameHwnd = hwnd;
        this.active = active;
        originalRemaining = remaining;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(20, 28, 43);
        ForeColor = Color.White;
        Size = new Size(275, 58);
        Location = new Point(
            Math.Max(bounds.Left, bounds.Left + (bounds.Right - bounds.Left - Width) / 2),
            Math.Max(bounds.Top, bounds.Top + 36));
        status = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = BackColor,
        };
        Controls.Add(status);
        timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) =>
        {
            // Never leave a game overlay attached to another foreground app.
            if (GetForegroundWindow() != ownedGameHwnd ||
                DateTimeOffset.UtcNow - capturedAt >= TimeSpan.FromSeconds(9))
            { Close(); return; }
            RefreshText();
        };
        FormClosed += (_, _) => timer.Dispose();
        RefreshText();
        timer.Start();
    }

    private void RefreshText()
    {
        if (!active) { status.Text = "护盾：未激活"; return; }
        double remaining = Math.Max(0,
            originalRemaining - (DateTimeOffset.UtcNow - capturedAt).TotalMilliseconds);
        TimeSpan left = TimeSpan.FromMilliseconds(Math.Min(remaining, TimeSpan.MaxValue.TotalMilliseconds));
        status.Text = $"护盾剩余 {((int)left.TotalHours):00}:{left.Minutes:00}:{left.Seconds:00}";
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= NoActivate | ToolWindow;
            return parameters;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out Rect rect);
}
