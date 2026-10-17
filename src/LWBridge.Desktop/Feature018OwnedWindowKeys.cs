using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LWBridge.Desktop;

internal sealed record Feature018OwnedKeyReceipt(
    string Binding, int GamePid, long Hwnd, bool Pressed, string Observation)
{
    // These are OS-level observations only. A game recognizer/native outcome
    // must independently correlate a real intent and game state.
    public DateTimeOffset? InputInjectionStartedAtUtc { get; init; }
    public DateTimeOffset? InputBaselineObservedAtUtc { get; init; }
    public DateTimeOffset? KeyDownObservedAtUtc { get; init; }
    public DateTimeOffset? KeyDownLastObservedAtUtc { get; init; }
    public DateTimeOffset? KeyReleaseObservedAtUtc { get; init; }
    public int KeyDownStateSamples { get; init; }
}

/// <summary>
/// Opt-in R2 acceptance input. GetAsyncKeyState does not observe posted WM_KEYDOWN
/// messages, so use SendInput keyboard events while the *exact task-owned*
/// game window is proven foreground. Unknown/replaced HWND/PID/account fails closed.
/// Nothing is registered as a global hotkey and no owner's window is activated.
/// </summary>
internal static class Feature018OwnedWindowKeys
{
    private const uint GaRoot = 2;
    private const uint KeyUp = 0x0002;
    private const byte Alt = 0x12;
    // Feature018HotkeyCapture polls every 30 ms and only arms after a
    // qualified no-key sample. Leave multiple complete polling opportunities
    // for baseline, then observe the held primary across separate intervals.
    private const int BaselineSampleIntervalMilliseconds = 35;
    private const int RequiredBaselineSamples = 3;
    private static readonly int[] PrimaryHoldSampleDelaysMilliseconds = [45, 45, 95];
    private delegate bool EnumerateWindow(nint hwnd, nint value);

    // Makes OS focus denial, input-stream rejection, and asynchronous admission
    // independently testable without sending a key to any real process.
    internal interface IPlatform
    {
        bool SameProcess(OverviewMapScanSession session);
        nint ForegroundWindow();
        nint FindOwnedWindow(int pid);
        bool IsOwnedRootWindow(nint hwnd, int pid);
        bool IsKeyboardFocusInOwnedWindow(nint hwnd, int pid);
        bool ActivateWindow(nint hwnd);
        IReadOnlySet<Keys> WatchedKeysDown();
        bool SendKey(byte key, bool release);
        Task Delay(int milliseconds, CancellationToken token);
    }

    private sealed class WindowsPlatform : IPlatform
    {
        internal static readonly WindowsPlatform Instance = new();
        public bool SameProcess(OverviewMapScanSession session) =>
            Feature018OwnedWindowKeys.SameProcess(session);
        public nint ForegroundWindow() => GetForegroundWindow();
        public nint FindOwnedWindow(int pid) => FindExactWindow(pid);
        public bool IsOwnedRootWindow(nint hwnd, int pid) => hwnd != 0 &&
            IsWindowVisible(hwnd) && GetAncestor(hwnd, GaRoot) == hwnd &&
            WindowBelongsTo(hwnd, pid);
        public bool IsKeyboardFocusInOwnedWindow(nint hwnd, int pid)
        {
            uint thread = GetWindowThreadProcessId(hwnd, out uint windowPid);
            if (thread == 0 || windowPid != (uint)pid) return false;
            var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
            // Ask the foreground game GUI thread directly; an activated root
            // can remain in front while keyboard focus moves to a popup.
            return GetGUIThreadInfo(thread, ref info) &&
                info.Focus != 0 && GetAncestor(info.Focus, GaRoot) == hwnd &&
                WindowBelongsTo(info.Focus, pid);
        }
        public bool ActivateWindow(nint hwnd) => SetForegroundWindow(hwnd);
        public IReadOnlySet<Keys> WatchedKeysDown() =>
            Feature018HotkeyRecognizer.Watched.Where(key =>
                (GetAsyncKeyState((int)key) & 0x8000) != 0).ToHashSet();
        public bool SendKey(byte key, bool release) =>
            SendInput(1, [new Input
            {
                Type = 1, Data = new InputUnion
                {
                    Keyboard = new KeyboardInput { Vk = key, Flags = release ? KeyUp : 0 }
                }
            }], Marshal.SizeOf<Input>()) == 1;
        public Task Delay(int milliseconds, CancellationToken token) =>
            Task.Delay(milliseconds, token);
    }

    internal static int NativeInputSize => Marshal.SizeOf<Input>();
    internal static int NativeGuiThreadInfoSize => Marshal.SizeOf<GuiThreadInfo>();

    // Matches exactly what the real capture recognizer polls, allowing
    // Windows' generic VK_MENU and left/right Alt aliases to overlap.
    // Input-stream success alone cannot establish a press/intent.
    internal static bool MatchesWatchedKeyState(
        IReadOnlySet<Keys> pressed, byte[] keyPlan, int pressedCount)
    {
        if (pressedCount == 0) return pressed.Count == 0;
        if (pressedCount < 0 || pressedCount > keyPlan.Length || keyPlan.Length is < 1 or > 2)
            return false;
        if (keyPlan.Length == 1)
            return pressedCount == 1 && pressed.Count == 1 &&
                pressed.Contains((Keys)keyPlan[0]);
        if (keyPlan[0] != Alt) return false;
        if (!pressed.Contains(Keys.Menu) && !pressed.Contains(Keys.LMenu) &&
            !pressed.Contains(Keys.RMenu)) return false;
        bool includesPrimary = pressedCount == 2;
        if (includesPrimary != pressed.Contains((Keys)keyPlan[1])) return false;
        return pressed.All(key => key == Keys.Menu || key == Keys.LMenu ||
            key == Keys.RMenu || (includesPrimary && key == (Keys)keyPlan[1]));
    }

    internal static bool TryKeyPlan(string binding, out byte[] keys)
    {
        keys = binding switch
        {
            "Q" => [0x51], "W" => [0x57], "E" => [0x45], "R" => [0x52],
            "A" => [0x41], "S" => [0x53], "D" => [0x44], "F" => [0x46],
            "Space" => [0x20], "F6" => [0x75], "F7" => [0x76],
            "F8" => [0x77], "F9" => [0x78], "F10" => [0x79],
            "G" => [0x47],
            "Alt+1" => [Alt, 0x31], "Alt+2" => [Alt, 0x32],
            "Alt+3" => [Alt, 0x33], "Alt+4" => [Alt, 0x34],
            _ => [],
        };
        return keys.Length is 1 or 2;
    }

    internal static Feature018HotkeyIntent? ExpectedIntent(string binding) => binding switch
    {
        "Q" => new(Feature018HotkeyKind.AttackSquad, 1),
        "W" => new(Feature018HotkeyKind.AttackSquad, 2),
        "E" => new(Feature018HotkeyKind.AttackSquad, 3),
        "R" => new(Feature018HotkeyKind.AttackSquad, 4),
        "A" => new(Feature018HotkeyKind.RecallSquad, 1),
        "S" => new(Feature018HotkeyKind.RecallSquad, 2),
        "D" => new(Feature018HotkeyKind.RecallSquad, 3),
        "F" => new(Feature018HotkeyKind.RecallSquad, 4),
        "Space" => new(Feature018HotkeyKind.ShieldRemaining),
        "F6" => new(Feature018HotkeyKind.UseShield, 8),
        "F7" => new(Feature018HotkeyKind.UseShield, 12),
        "F8" => new(Feature018HotkeyKind.UseShield, 24),
        "Alt+1" => new(Feature018HotkeyKind.ApplyEquipmentPreset, 1),
        "Alt+2" => new(Feature018HotkeyKind.ApplyEquipmentPreset, 2),
        "Alt+3" => new(Feature018HotkeyKind.ApplyEquipmentPreset, 3),
        "Alt+4" => new(Feature018HotkeyKind.ApplyEquipmentPreset, 4),
        "F9" => new(Feature018HotkeyKind.RandomRelocate),
        "F10" => new(Feature018HotkeyKind.AllianceRelocate),
        "G" => new(Feature018HotkeyKind.ReinforceFrontline, 5),
        _ => null,
    };

    internal static async Task<Feature018OwnedKeyReceipt> PressAsync(
        OverviewMapScanSession session, string binding,
        Func<bool> authenticatedApprovedOwner, Func<bool> gameInputClear,
        CancellationToken token) =>
        await PressWithPlatformAsync(session, binding, authenticatedApprovedOwner,
            gameInputClear, WindowsPlatform.Instance, token);

    internal static async Task<Feature018OwnedKeyReceipt> PressWithPlatformAsync(
        OverviewMapScanSession session, string binding,
        Func<bool> authenticatedApprovedOwner, Func<bool> gameInputClear,
        IPlatform platform, CancellationToken token)
    {
        if (!TryKeyPlan(binding, out byte[] keys))
            return new(binding, session.GamePid, 0, false, "unsupported_key_binding");
        // Validate all identifying process fields before even selecting an HWND.
        if (!authenticatedApprovedOwner() || !platform.SameProcess(session))
            return new(binding, session.GamePid, 0, false, "approved_game_identity_unavailable");
        // A game process may own multiple visible top-level HWNDs. Prefer its
        // *already foreground* root instead of activating an arbitrary sibling.
        nint foreground = platform.ForegroundWindow();
        nint hwnd = platform.IsOwnedRootWindow(foreground, session.GamePid)
            ? foreground : platform.FindOwnedWindow(session.GamePid);
        if (hwnd == 0)
            return new(binding, session.GamePid, 0, false, "owned_game_window_unavailable");
        if (!platform.IsOwnedRootWindow(hwnd, session.GamePid))
            return new(binding, session.GamePid, hwnd.ToInt64(), false, "owned_game_window_unavailable");
        // Windows may deny activating a different process's HWND. An already
        // foreground, verified owned HWND requires no new activation privilege.
        if (platform.ForegroundWindow() != hwnd && !platform.ActivateWindow(hwnd))
            return new(binding, session.GamePid, hwnd.ToInt64(), false, "game_foreground_activation_denied");
        // First allow the capture recognizer to observe all keys released;
        // first-focus held keys are deliberately disarmed.
        await platform.Delay(420, token);
        if (!OwnerNow()) return new(binding, session.GamePid, hwnd.ToInt64(),
            false, "owned_game_window_or_account_changed");
        // The native focus monitor is intentionally fail-closed until it gets a
        // fresh game EventSystem reply. Synchronize the test press to that real
        // gate; otherwise timer scheduling can yield a false keyboard negative.
        bool ready = false;
        bool textInputClear = false;
        bool keyboardClear = false;
        bool keyboardFocusClear = false;
        int consecutiveBaselineSamples = 0;
        DateTimeOffset? baselineObservedAt = null;
        for (int i = 0; i < 75; i++)
        {
            if (!OwnerNow())
                return new(binding, session.GamePid, hwnd.ToInt64(),
                    false, "owned_game_window_changed_during_input_admission");
            textInputClear = gameInputClear();
            keyboardClear = MatchesWatchedKeyState(platform.WatchedKeysDown(), keys, 0);
            keyboardFocusClear = platform.IsKeyboardFocusInOwnedWindow(hwnd, session.GamePid);
            if (textInputClear && keyboardClear && keyboardFocusClear)
            {
                consecutiveBaselineSamples++;
                if (consecutiveBaselineSamples == RequiredBaselineSamples)
                {
                    baselineObservedAt = DateTimeOffset.UtcNow;
                    ready = true;
                    break;
                }
            }
            else consecutiveBaselineSamples = 0;
            if (i < 74)
                await platform.Delay(BaselineSampleIntervalMilliseconds, token);
        }
        if (!ready)
            return new(binding, session.GamePid, hwnd.ToInt64(),
                false, !textInputClear ? "game_text_input_focus_could_not_be_qualified" :
                !keyboardClear ? "game_keyboard_baseline_could_not_be_qualified" :
                !keyboardFocusClear ? "game_keyboard_focus_could_not_be_qualified" :
                "game_input_admission_not_stable");

        bool keyDown = false;
        bool modifierDown = false;
        DateTimeOffset? injectedAt = null;
        DateTimeOffset? downAt = null;
        DateTimeOffset? lastDownAt = null;
        DateTimeOffset? releasedAt = null;
        string? rejected = null;
        int downSamples = 0;
        try
        {
            if (keys.Length == 2)
            {
                if (!AllowedNow() || !gameInputClear() ||
                    !MatchesWatchedKeyState(platform.WatchedKeysDown(), keys, 0))
                    return new(binding, session.GamePid, hwnd.ToInt64(), false,
                        "owned_window_changed_before_modifier");
                injectedAt = DateTimeOffset.UtcNow;
                if (!platform.SendKey(keys[0], false))
                    return new(binding, session.GamePid, hwnd.ToInt64(), false,
                        "win32_sendinput_modifier_rejected");
                modifierDown = true;
                await platform.Delay(55, token);
                if (!AllowedNow() || !gameInputClear()) return new(binding, session.GamePid, hwnd.ToInt64(),
                    false, "owned_window_changed_before_chord");
                if (!MatchesWatchedKeyState(platform.WatchedKeysDown(), keys, 1))
                    return new(binding, session.GamePid, hwnd.ToInt64(), false,
                        "win32_modifier_state_not_observed");
            }
            if (!AllowedNow() || !gameInputClear() ||
                !MatchesWatchedKeyState(platform.WatchedKeysDown(), keys, keys.Length - 1))
                return new(binding, session.GamePid, hwnd.ToInt64(),
                false, "owned_window_changed_before_keydown");
            byte primary = keys[^1];
            injectedAt ??= DateTimeOffset.UtcNow;
            if (!platform.SendKey(primary, false))
                return new(binding, session.GamePid, hwnd.ToInt64(), false,
                    "win32_sendinput_keydown_rejected");
            keyDown = true;
            // Three strictly separated, independent reads of the same watched
            // key set. A single late GetAsyncKeyState high bit can reflect an
            // unrelated/late press. This is an OS-state witness; it is not a
            // Feature018HotkeyCapture timer callback or native game receipt.
            foreach (int delay in PrimaryHoldSampleDelaysMilliseconds)
            {
                await platform.Delay(delay, token);
                if (!AllowedNow() || !gameInputClear())
                    return new(binding, session.GamePid, hwnd.ToInt64(), false,
                        "owned_window_changed_during_keypress");
                if (!MatchesWatchedKeyState(platform.WatchedKeysDown(), keys, keys.Length))
                    return new(binding, session.GamePid, hwnd.ToInt64(), false,
                        "win32_keydown_state_not_observed");
                DateTimeOffset observedAt = DateTimeOffset.UtcNow;
                downAt ??= observedAt;
                lastDownAt = observedAt;
                downSamples++;
            }
        }
        finally
        {
            // Release every injected key even on cancellation or focus loss;
            // surface an OS release failure rather than claiming a completed
            // key press with a possibly still-held modifier/action key.
            bool released = true;
            if (keyDown) released = platform.SendKey(keys[^1], true) && released;
            if (modifierDown) released = platform.SendKey(keys[0], true) && released;
            if (keyDown || modifierDown)
                await platform.Delay(70, CancellationToken.None);
            if (!released)
                throw new InvalidOperationException("win32_sendinput_keyup_rejected");
            if (keyDown || modifierDown)
            {
                if (!MatchesWatchedKeyState(platform.WatchedKeysDown(), keys, 0))
                    rejected = "win32_keyup_state_not_observed";
                else if (!OwnerNow() ||
                    !platform.IsKeyboardFocusInOwnedWindow(hwnd, session.GamePid))
                    rejected = "owned_window_changed_during_key_release";
                else releasedAt = DateTimeOffset.UtcNow;
            }
        }

        if (rejected is not null) return new(binding, session.GamePid, hwnd.ToInt64(),
            false, rejected)
        {
            InputInjectionStartedAtUtc = injectedAt, InputBaselineObservedAtUtc = baselineObservedAt,
            KeyDownObservedAtUtc = downAt, KeyDownLastObservedAtUtc = lastDownAt,
            KeyDownStateSamples = downSamples
        };
        return new(binding, session.GamePid, hwnd.ToInt64(), true,
            "win32_key_state_observed_for_owned_game_keyboard_focus")
        {
            InputInjectionStartedAtUtc = injectedAt,
            InputBaselineObservedAtUtc = baselineObservedAt,
            KeyDownObservedAtUtc = downAt, KeyDownLastObservedAtUtc = lastDownAt,
            KeyReleaseObservedAtUtc = releasedAt, KeyDownStateSamples = downSamples
        };

        bool OwnerNow() => authenticatedApprovedOwner() &&
            platform.SameProcess(session) && platform.ForegroundWindow() == hwnd &&
            platform.IsOwnedRootWindow(hwnd, session.GamePid);
        bool AllowedNow() => OwnerNow() &&
            platform.IsKeyboardFocusInOwnedWindow(hwnd, session.GamePid);
    }

    private static bool SameProcess(OverviewMapScanSession session)
    {
        if (!OperatingSystem.IsWindows() ||
            !DateTimeOffset.TryParse(session.GameStartedAtUtc,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out DateTimeOffset started)) return false;
        try
        {
            using Process process = Process.GetProcessById(session.GamePid);
            return !process.HasExited && process.StartTime.ToUniversalTime() == started.UtcDateTime &&
                string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? ""),
                    Path.GetFullPath(session.GamePath), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static nint FindExactWindow(int gamePid)
    {
        nint matched = 0;
        _ = EnumWindows((hwnd, _) =>
        {
            if (matched == 0 && IsWindowVisible(hwnd) &&
                GetAncestor(hwnd, GaRoot) == hwnd && WindowBelongsTo(hwnd, gamePid))
                matched = hwnd;
            return matched == 0;
        }, 0);
        return matched;
    }

    private static bool WindowBelongsTo(nint hwnd, int pid)
    {
        _ = GetWindowThreadProcessId(hwnd, out uint windowPid);
        return windowPid == (uint)pid;
    }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumerateWindow fn, nint user);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk, Scan;
        public uint Flags, Time;
        public nuint ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx, Dy;
        public uint MouseData, Flags, Time;
        public nuint ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint Size, Flags;
        public nint Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public NativeRect CaretRect;
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
