using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LWBridge.Desktop;

// The original 0.3.17 bindings. These are intentions, not confirmation that a
// current-client game operation is supported or has been executed.
internal enum Feature018HotkeyKind
{
    AttackSquad,
    RecallSquad,
    ShieldRemaining,
    UseShield,
    ApplyEquipmentPreset,
    RandomRelocate,
    AllianceRelocate,
    ReinforceFrontline,
}

internal readonly record struct Feature018HotkeyIntent(
    Feature018HotkeyKind Kind, int Slot = 0);

/// <summary>
/// Pure edge recognizer. Releases must be seen after entering the game window
/// before a press can trigger. Losing focus or opening game text input disarms it.
/// Nothing is registered globally, nor are other windows' key events consumed.
/// </summary>
internal sealed class Feature018HotkeyRecognizer
{
    internal static readonly Keys[] Watched =
    [
        Keys.Q, Keys.W, Keys.E, Keys.R, Keys.A, Keys.S, Keys.D, Keys.F,
        Keys.Space, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10,
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.G, Keys.Menu, Keys.LMenu,
        Keys.RMenu, Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
        Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey, Keys.LWin, Keys.RWin,
    ];

    private readonly HashSet<Keys> previous = [];
    private bool armed;

    internal void Disarm()
    {
        armed = false;
        previous.Clear();
    }

    internal Feature018HotkeyIntent? Observe(
        IReadOnlySet<Keys> pressed,
        bool verifiedGameForeground,
        bool gameTextInputClear)
    {
        if (!verifiedGameForeground || !gameTextInputClear)
        {
            Disarm();
            return null;
        }

        // A held key from another window cannot trigger on focus transfer.
        if (!armed)
        {
            if (pressed.Count == 0) armed = true;
            previous.Clear();
            foreach (Keys key in pressed) previous.Add(key);
            return null;
        }

        bool alt = HasAny(pressed, Keys.Menu, Keys.LMenu, Keys.RMenu);
        bool control = HasAny(pressed, Keys.ControlKey, Keys.LControlKey, Keys.RControlKey);
        bool shift = HasAny(pressed, Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey);
        bool windows = HasAny(pressed, Keys.LWin, Keys.RWin);
        Feature018HotkeyIntent? found = null;
        int candidates = 0;

        if (!control && !shift && !windows)
        {
            foreach ((Keys key, Feature018HotkeyIntent intent, bool needsAlt) in Bindings)
            {
                if (needsAlt != alt || !pressed.Contains(key)) continue;
                // Two keys held in one chord are ambiguous: dispatch neither.
                candidates++;
                if (!previous.Contains(key)) found = intent;
            }
        }

        previous.Clear();
        foreach (Keys key in pressed) previous.Add(key);
        return candidates == 1 ? found : null;
    }

    private static bool HasAny(IReadOnlySet<Keys> keys, params Keys[] options) =>
        options.Any(keys.Contains);

    private static readonly (Keys, Feature018HotkeyIntent, bool)[] Bindings =
    [
        (Keys.Q, new(Feature018HotkeyKind.AttackSquad, 1), false),
        (Keys.W, new(Feature018HotkeyKind.AttackSquad, 2), false),
        (Keys.E, new(Feature018HotkeyKind.AttackSquad, 3), false),
        (Keys.R, new(Feature018HotkeyKind.AttackSquad, 4), false),
        (Keys.A, new(Feature018HotkeyKind.RecallSquad, 1), false),
        (Keys.S, new(Feature018HotkeyKind.RecallSquad, 2), false),
        (Keys.D, new(Feature018HotkeyKind.RecallSquad, 3), false),
        (Keys.F, new(Feature018HotkeyKind.RecallSquad, 4), false),
        (Keys.Space, new(Feature018HotkeyKind.ShieldRemaining), false),
        (Keys.F6, new(Feature018HotkeyKind.UseShield, 8), false),
        (Keys.F7, new(Feature018HotkeyKind.UseShield, 12), false),
        (Keys.F8, new(Feature018HotkeyKind.UseShield, 24), false),
        (Keys.D1, new(Feature018HotkeyKind.ApplyEquipmentPreset, 1), true),
        (Keys.D2, new(Feature018HotkeyKind.ApplyEquipmentPreset, 2), true),
        (Keys.D3, new(Feature018HotkeyKind.ApplyEquipmentPreset, 3), true),
        (Keys.D4, new(Feature018HotkeyKind.ApplyEquipmentPreset, 4), true),
        (Keys.F9, new(Feature018HotkeyKind.RandomRelocate), false),
        (Keys.F10, new(Feature018HotkeyKind.AllianceRelocate), false),
        (Keys.G, new(Feature018HotkeyKind.ReinforceFrontline, 5), false),
    ];
}

/// <summary>
/// A native app polls only while the exact admitted LastWar window has focus.
/// It never hooks, forwards, synthesizes or suppresses system keyboard events.
/// The account/UI safety gate must be provided by the owned game bridge; unknown
/// account or focused text input means no dispatch.
/// </summary>
internal sealed class Feature018HotkeyCapture : IDisposable
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 30 };
    private readonly Feature018HotkeyRecognizer recognizer = new();
    private readonly int gamePid;
    private readonly string gamePath;
    private readonly DateTime startTimeUtc;
    private readonly Func<bool> authenticatedOwnerAndTextInputClear;
    private readonly Action<Feature018HotkeyIntent> onIntent;
    private bool disposed;

    internal Feature018HotkeyCapture(
        int gamePid,
        string gamePath,
        DateTime startTimeUtc,
        Func<bool> authenticatedOwnerAndTextInputClear,
        Action<Feature018HotkeyIntent> onIntent)
    {
        if (gamePid <= 0) throw new ArgumentOutOfRangeException(nameof(gamePid));
        if (string.IsNullOrWhiteSpace(gamePath)) throw new ArgumentException("Game path required", nameof(gamePath));
        this.gamePid = gamePid;
        this.gamePath = Path.GetFullPath(gamePath);
        this.startTimeUtc = startTimeUtc.ToUniversalTime();
        this.authenticatedOwnerAndTextInputClear = authenticatedOwnerAndTextInputClear ??
            throw new ArgumentNullException(nameof(authenticatedOwnerAndTextInputClear));
        this.onIntent = onIntent ?? throw new ArgumentNullException(nameof(onIntent));
        timer.Tick += Tick;
    }

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        timer.Start();
    }

    internal void Stop()
    {
        timer.Stop();
        recognizer.Disarm();
    }

    private void Tick(object? sender, EventArgs args)
    {
        bool ownedForeground = IsExactForegroundOwner();
        bool admitted = false;
        if (ownedForeground)
        {
            try { admitted = authenticatedOwnerAndTextInputClear(); }
            catch { admitted = false; }
        }
        if (!ownedForeground || !admitted)
        {
            recognizer.Disarm();
            return;
        }

        HashSet<Keys> down = [];
        foreach (Keys key in Feature018HotkeyRecognizer.Watched)
        {
            if ((GetAsyncKeyState((int)key) & 0x8000) != 0) down.Add(key);
        }
        Feature018HotkeyIntent? intent = recognizer.Observe(down, true, true);
        if (intent is not null) onIntent(intent.Value);
    }

    internal bool IsExactForegroundOwner()
    {
        if (!OperatingSystem.IsWindows()) return false;
        nint window = GetForegroundWindow();
        if (window == 0 || !IsWindowVisible(window) || GetAncestor(window, 2) != window)
            return false;
        _ = GetWindowThreadProcessId(window, out uint pid);
        if (pid != (uint)gamePid) return false;
        try
        {
            using Process owner = Process.GetProcessById(gamePid);
            if (owner.HasExited || owner.StartTime.ToUniversalTime() != startTimeUtc)
                return false;
            string? actual = owner.MainModule?.FileName;
            return actual is not null && string.Equals(
                Path.GetFullPath(actual), gamePath,
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Stop();
        timer.Tick -= Tick;
        timer.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
