using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace LWBridge.Desktop;

internal sealed record Feature018OwnedKeyReceipt(
    string Binding, int GamePid, long Hwnd, bool Pressed, string Observation);

/// <summary>
/// Opt-in R2 acceptance input. GetAsyncKeyState does not observe posted WM_KEYDOWN
/// messages, so use standard Win32 keyboard input while the *exact task-owned*
/// game window is proven foreground. Unknown/replaced HWND/PID/account fails closed.
/// Nothing is registered as a global hotkey and no owner's window is activated.
/// </summary>
internal static class Feature018OwnedWindowKeys
{
    private const uint GaRoot = 2;
    private const uint KeyUp = 0x0002;
    private const byte Alt = 0x12;
    private delegate bool EnumerateWindow(nint hwnd, nint value);

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
        CancellationToken token)
    {
        if (!TryKeyPlan(binding, out byte[] keys))
            return new(binding, session.GamePid, 0, false, "unsupported_key_binding");
        // Validate all identifying process fields before even selecting an HWND.
        if (!authenticatedApprovedOwner() || !SameProcess(session))
            return new(binding, session.GamePid, 0, false, "approved_game_identity_unavailable");
        nint hwnd = FindExactWindow(session.GamePid);
        if (hwnd == 0)
            return new(binding, session.GamePid, 0, false, "owned_game_window_unavailable");
        if (!SetForegroundWindow(hwnd))
            return new(binding, session.GamePid, hwnd.ToInt64(), false, "game_foreground_activation_denied");
        // First allow the capture recognizer to observe all keys released;
        // first-focus held keys are deliberately disarmed.
        await Task.Delay(420, token);
        if (!AllowedNow()) return new(binding, session.GamePid, hwnd.ToInt64(),
            false, "owned_game_window_or_account_changed");
        // The native focus monitor is intentionally fail-closed until it gets a
        // fresh game EventSystem reply. Synchronize the test press to that real
        // gate; otherwise timer scheduling can yield a false keyboard negative.
        bool ready = false;
        for (int i = 0; i < 75; i++)
        {
            if (!AllowedNow())
                return new(binding, session.GamePid, hwnd.ToInt64(),
                    false, "owned_game_window_changed_during_input_admission");
            if (gameInputClear()) { ready = true; break; }
            await Task.Delay(25, token);
        }
        if (!ready)
            return new(binding, session.GamePid, hwnd.ToInt64(),
                false, "game_text_input_focus_could_not_be_qualified");

        bool keyDown = false;
        bool modifierDown = false;
        try
        {
            if (keys.Length == 2)
            {
                KeyEvent(keys[0], 0, 0, 0);
                modifierDown = true;
                await Task.Delay(55, token);
                if (!AllowedNow() || !gameInputClear()) return new(binding, session.GamePid, hwnd.ToInt64(),
                    false, "owned_window_changed_before_chord");
            }
            if (!AllowedNow() || !gameInputClear()) return new(binding, session.GamePid, hwnd.ToInt64(),
                false, "owned_window_changed_before_keydown");
            byte primary = keys[^1];
            KeyEvent(primary, 0, 0, 0);
            keyDown = true;
            await Task.Delay(185, token);
            return new(binding, session.GamePid, hwnd.ToInt64(), true,
                "keys_injected_while_exact_task_game_hwnd_foreground");
        }
        finally
        {
            // Release every injected key even on cancellation or focus loss;
            // never leave Alt or a game action hotkey physically held.
            if (keyDown) KeyEvent(keys[^1], 0, KeyUp, 0);
            if (modifierDown) KeyEvent(keys[0], 0, KeyUp, 0);
            await Task.Delay(70, CancellationToken.None);
        }

        bool AllowedNow() => authenticatedApprovedOwner() &&
            SameProcess(session) && GetForegroundWindow() == hwnd &&
            GetAncestor(hwnd, GaRoot) == hwnd && IsWindowVisible(hwnd) &&
            WindowBelongsTo(hwnd, session.GamePid);
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
    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void KeyEvent(byte key, byte scan, uint flags, nuint extra);
}
