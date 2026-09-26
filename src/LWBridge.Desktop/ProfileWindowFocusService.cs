using System.Runtime.InteropServices;

namespace LWBridge.Desktop;

internal sealed class ProfileWindowFocusService
{
    private readonly string currentProfileId;
    private readonly GameInstallationService installation;

    internal ProfileWindowFocusService(
        string currentProfileId,
        GameInstallationService installation)
    {
        if (string.IsNullOrWhiteSpace(currentProfileId))
            throw new ArgumentException(
                "Profile identity is required.",
                nameof(currentProfileId));

        this.currentProfileId = currentProfileId;
        this.installation = installation ??
            throw new ArgumentNullException(nameof(installation));
    }

    internal void TryFocus(string profileId)
    {
        if (!string.Equals(
            profileId,
            currentProfileId,
            StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            GameRootStatus root = installation.GetStatus();
            GameProcessStatus process = installation.GetProcessStatus();
            if (!root.Valid ||
                !process.GameRunning ||
                process.GamePid is not int pid ||
                pid <= 0 ||
                root.GamePath is null ||
                process.GamePath is null ||
                !PathEquals(process.GamePath, root.GamePath))
            {
                return;
            }

            _ = TryFocusProcessWindow(pid);
        }
        catch
        {
            // Original profile_select treats focus as best effort.
        }
    }
    internal static bool TryFocusProcessWindow(int processId)
    {
        if (processId <= 0)
            return false;

        nint targetWindow = 0;
        EnumWindowsProc callback = (window, parameter) =>
        {
            GetWindowThreadProcessId(
                window,
                out uint ownerProcessId);
            if (ownerProcessId != (uint)processId ||
                !IsWindowVisible(window))
            {
                return true;
            }

            targetWindow = window;
            return false;
        };

        _ = EnumWindows(callback, 0);
        GC.KeepAlive(callback);

        if (targetWindow == 0)
            return false;

        _ = ShowWindow(targetWindow, 9);
        return SetForegroundWindow(targetWindow);
    }

    private static bool PathEquals(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private delegate bool EnumWindowsProc(
        nint window,
        nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(
        EnumWindowsProc callback,
        nint parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        nint window,
        out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(
        nint window,
        int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
}
