using System.Diagnostics;
using System.Collections.Concurrent;
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
/// Direct observation from the actual owned WinForms 30ms key recognizer.
/// This carries an independent per-capture sequence; it cannot prove which
/// physical device generated a key, or that game RPC succeeded.
/// </summary>
internal sealed record Feature018CapturedIntent(
    string CaptureId, long Sequence, Feature018HotkeyKind Kind, int Slot,
    int GamePid, long ForegroundHwnd, DateTimeOffset ObservedAtUtc);

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

        // The original unmodified matcher only counted keys whose *binding*
        // agreed with Alt's presence: Q plus held D1 (without Alt) produced a
        // single Q candidate and incorrectly dispatched. The owned SendInput
        // witness already requires one exact primary, with only Win32 Alt
        // aliases permitted for equipment chords. Enforce that same rule for
        // real observed key edges, including mixed physical/synthetic states.
        bool singlePrimary = pressed.Count(key =>
            key != Keys.Menu && key != Keys.LMenu && key != Keys.RMenu) == 1;
        if (!control && !shift && !windows && singlePrimary)
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
    // The real Win32 APIs are behind this narrow seam so a changing foreground
    // root or keyboard focus can be reproduced without an OS key or game launch.
    internal interface IPlatform
    {
        nint ForegroundWindow();
        bool IsExactOwnedRoot(nint hwnd, int pid, string path, DateTime startedUtc);
        bool HasOwnedKeyboardFocus(nint hwnd, int pid);
        IReadOnlySet<Keys> WatchedKeysDown();
    }

    private sealed class WindowsPlatform : IPlatform
    {
        internal static readonly WindowsPlatform Instance = new();
        public nint ForegroundWindow() => GetForegroundWindow();
        public bool IsExactOwnedRoot(nint hwnd, int pid, string path, DateTime startedUtc)
        {
            if (!OperatingSystem.IsWindows() || hwnd == 0 ||
                !IsWindowVisible(hwnd) || GetAncestor(hwnd, 2) != hwnd)
                return false;
            _ = GetWindowThreadProcessId(hwnd, out uint windowPid);
            if (windowPid != (uint)pid) return false;
            try
            {
                using Process owner = Process.GetProcessById(pid);
                if (owner.HasExited || owner.StartTime.ToUniversalTime() != startedUtc)
                    return false;
                string? actual = owner.MainModule?.FileName;
                return actual is not null && string.Equals(
                    Path.GetFullPath(actual), path, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        public bool HasOwnedKeyboardFocus(nint hwnd, int pid) =>
            Feature018OwnedWindowKeys.HasOwnedKeyboardFocus(hwnd, pid);
        public IReadOnlySet<Keys> WatchedKeysDown() =>
            Feature018HotkeyRecognizer.Watched.Where(key =>
                (GetAsyncKeyState((int)key) & 0x8000) != 0).ToHashSet();
    }

    private readonly System.Windows.Forms.Timer timer = new() { Interval = 30 };
    private readonly Feature018HotkeyRecognizer recognizer = new();
    private readonly int gamePid;
    private readonly string gamePath;
    private readonly DateTime startTimeUtc;
    private readonly Func<bool> authenticatedOwnerAndTextInputClear;
    private readonly Action<Feature018HotkeyIntent> onIntent;
    private readonly IPlatform platform;
    private readonly string captureId = Guid.NewGuid().ToString("N");
    private readonly ConcurrentQueue<Feature018CapturedIntent> observed = new();
    private long observedSequence;
    private bool disposed;

    internal string CaptureId => captureId;
    internal long LastObservedSequence => Interlocked.Read(ref observedSequence);
    internal Feature018CapturedIntent[] SnapshotCapturedIntents() => observed.ToArray();

    internal Feature018HotkeyCapture(
        int gamePid,
        string gamePath,
        DateTime startTimeUtc,
        Func<bool> authenticatedOwnerAndTextInputClear,
        Action<Feature018HotkeyIntent> onIntent)
        : this(gamePid, gamePath, startTimeUtc, authenticatedOwnerAndTextInputClear,
            onIntent, WindowsPlatform.Instance) { }

    internal Feature018HotkeyCapture(
        int gamePid,
        string gamePath,
        DateTime startTimeUtc,
        Func<bool> authenticatedOwnerAndTextInputClear,
        Action<Feature018HotkeyIntent> onIntent, IPlatform platform)
    {
        if (gamePid <= 0) throw new ArgumentOutOfRangeException(nameof(gamePid));
        if (string.IsNullOrWhiteSpace(gamePath)) throw new ArgumentException("Game path required", nameof(gamePath));
        this.gamePid = gamePid;
        this.gamePath = Path.GetFullPath(gamePath);
        this.startTimeUtc = startTimeUtc.ToUniversalTime();
        this.authenticatedOwnerAndTextInputClear = authenticatedOwnerAndTextInputClear ??
            throw new ArgumentNullException(nameof(authenticatedOwnerAndTextInputClear));
        this.onIntent = onIntent ?? throw new ArgumentNullException(nameof(onIntent));
        this.platform = platform ?? throw new ArgumentNullException(nameof(platform));
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
        nint exactRoot = platform.ForegroundWindow();
        if (!OwnerAndInputAdmitted(exactRoot))
        {
            recognizer.Disarm();
            return;
        }
        IReadOnlySet<Keys> down = platform.WatchedKeysDown();
        // Native text-input focus may switch during the key-state query,
        // and a game process can foreground a different owned sibling HWND.
        // Neither is proof that these keys came from this exact original root.
        if (!OwnerAndInputAdmitted(exactRoot))
        {
            recognizer.Disarm();
            return;
        }
        Feature018HotkeyIntent? intent = recognizer.Observe(down, true, true);
        if (intent is not null)
        {
            // Record the previously verified *same* root, not a new unchecked
            // GetForegroundWindow result. This is an observed capture callback
            // only; it does not prove input device origin or Unity-game ACK.
            long sequence = Interlocked.Increment(ref observedSequence);
            observed.Enqueue(new(captureId, sequence, intent.Value.Kind, intent.Value.Slot,
                gamePid, exactRoot.ToInt64(), DateTimeOffset.UtcNow));
            while (observed.Count > 128) observed.TryDequeue(out _);
            onIntent(intent.Value);
        }
    }

    internal void PollOnceForChecks() => Tick(null, EventArgs.Empty);

    private bool OwnerAndInputAdmitted(nint expectedRoot)
    {
        if (expectedRoot == 0 || platform.ForegroundWindow() != expectedRoot ||
            !platform.IsExactOwnedRoot(expectedRoot, gamePid, gamePath, startTimeUtc) ||
            !platform.HasOwnedKeyboardFocus(expectedRoot, gamePid))
            return false;
        try { return authenticatedOwnerAndTextInputClear(); }
        catch { return false; }
    }

    internal bool IsExactForegroundOwner()
    {
        nint hwnd = platform.ForegroundWindow();
        // This public predicate is also the asynchronous action broker's
        // final focus gate, after the recognizer Tick has returned. The
        // foreground game root can remain unchanged while GUI-thread keyboard
        // focus moves to another window; do not dispatch based on root alone.
        return hwnd != 0 &&
            platform.IsExactOwnedRoot(hwnd, gamePid, gamePath, startTimeUtc) &&
            platform.ForegroundWindow() == hwnd &&
            platform.HasOwnedKeyboardFocus(hwnd, gamePid) &&
            platform.ForegroundWindow() == hwnd;
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
