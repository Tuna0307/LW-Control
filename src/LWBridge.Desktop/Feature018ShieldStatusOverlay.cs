using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;

namespace LWBridge.Desktop;

/// <summary>
/// Ephemeral, non-activating status display over the exact focused game HWND.
/// This is a read-only display of visible BuildPointInfo shield expiries; never guesses
/// an expiry from inventory or treats a shield ItemUse dispatch as effective.
/// </summary>
internal sealed class Feature018ShieldStatusOverlay : Form
{
    private const int NoActivate = 0x08000000;
    private const int ToolWindow = 0x00000080;
    private readonly Feature018HotkeyCapture capture;
    private readonly Func<Task<JsonElement>> refresh;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 30 };
    private JsonElement snapshot;
    private DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset nextRefresh = DateTimeOffset.UtcNow;
    private bool refreshing;
    private readonly nint ownedGameHwnd;
    private static Feature018ShieldStatusOverlay? current;

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

    internal static bool TryReadCities(JsonElement observation) =>
        observation.ValueKind == JsonValueKind.Object &&
        observation.TryGetProperty("observation", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "native_visible_city_shields" &&
        observation.TryGetProperty("readOnly", out var readOnly) && readOnly.ValueKind == JsonValueKind.True &&
        observation.TryGetProperty("cities", out var cities) && cities.ValueKind == JsonValueKind.Array;

    internal static bool ShowIfCurrentOwner(Feature018HotkeyCapture? capture,
        JsonElement observation, Func<Task<JsonElement>> refresh)
    {
        if (capture?.IsSpaceHeld() != true || !TryReadCities(observation)) return false;
        nint window = GetForegroundWindow();
        if (window == 0) return false;
        current?.Close();
        try
        {
            current = new Feature018ShieldStatusOverlay(window, capture, observation, refresh);
            current.Show();
            return true;
        }
        catch { current = null; return false; }
    }

    private Feature018ShieldStatusOverlay(nint hwnd, Feature018HotkeyCapture capture,
        JsonElement observation, Func<Task<JsonElement>> refresh)
    {
        ownedGameHwnd = hwnd;
        this.capture = capture;
        this.refresh = refresh;
        snapshot = observation.Clone();
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = BackColor;
        DoubleBuffered = true;
        Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold);
        PositionOverClient();
        timer.Tick += Tick;
        FormClosed += (_, _) => { timer.Dispose(); if (ReferenceEquals(current, this)) current = null; };
        timer.Start();
    }

    private void PositionOverClient()
    {
        if (!GetClientRect(ownedGameHwnd, out Rect client)) return;
        var origin = new NativePoint();
        if (!ClientToScreen(ownedGameHwnd, ref origin)) return;
        Bounds = new Rectangle(origin.X, origin.Y, client.Right, client.Bottom);
    }

    private async void Tick(object? sender, EventArgs args)
    {
        if (capture.IsSpaceHeld() != true || GetForegroundWindow() != ownedGameHwnd)
        { Close(); return; }
        PositionOverClient();
        Invalidate();
        if (refreshing || DateTimeOffset.UtcNow < nextRefresh) return;
        refreshing = true;
        nextRefresh = DateTimeOffset.UtcNow.AddMilliseconds(300);
        try
        {
            JsonElement next = await refresh();
            if (IsDisposed) return;
            if (!capture.IsSpaceHeld() || !TryReadCities(next)) { Close(); return; }
            snapshot = next.Clone();
            capturedAt = DateTimeOffset.UtcNow;
            Invalidate();
        }
        catch { if (!IsDisposed) Close(); }
        finally { refreshing = false; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!TryReadCities(snapshot)) return;
        double elapsed = (DateTimeOffset.UtcNow - capturedAt).TotalMilliseconds;
        foreach (JsonElement city in snapshot.GetProperty("cities").EnumerateArray())
        {
            if (!city.TryGetProperty("active", out var active) || active.ValueKind != JsonValueKind.True ||
                !city.TryGetProperty("remainingMilliseconds", out var left) || !left.TryGetDouble(out double remaining) ||
                !city.TryGetProperty("viewportX", out var px) || !px.TryGetDouble(out double x) ||
                !city.TryGetProperty("viewportY", out var py) || !py.TryGetDouble(out double y) ||
                !city.TryGetProperty("viewportZ", out var pz) || !pz.TryGetDouble(out double z) ||
                !double.IsFinite(remaining) || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) ||
                z <= 0 || x < 0 || x > 1 || y < 0 || y > 1 || remaining <= elapsed) continue;
            TimeSpan time = TimeSpan.FromMilliseconds(Math.Min(remaining - elapsed, TimeSpan.MaxValue.TotalMilliseconds));
            string text = $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
            var size = TextRenderer.MeasureText(text, Font);
            var bounds = new Rectangle((int)(x * ClientSize.Width) - size.Width / 2,
                Math.Max(0, (int)((1-y) * ClientSize.Height) - 54), size.Width, size.Height);
            e.Graphics.FillRectangle(Brushes.Black, bounds);
            TextRenderer.DrawText(e.Graphics, text, Font, bounds, Color.White);
        }
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= NoActivate | ToolWindow | 0x00000020;
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
    private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint hwnd, ref NativePoint point);
}
