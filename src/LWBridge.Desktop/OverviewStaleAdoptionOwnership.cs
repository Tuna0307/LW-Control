using System.Globalization;

namespace LWBridge.Desktop;

// Only used inside the exclusive existing-adoption-file write transaction.
// A false ProcessMatches result is insufficient: it also means access denied
// or an unreadable incarnation. Never abandon a potentially live owner.
internal static class OverviewStaleAdoptionOwnership
{
    internal static bool CanReplaceRetiredRecord(
        byte[] existing, string profileId, string executable, string newSession,
        IOwnedProcessApi processApi)
    {
        try
        {
            if (!OverviewAdoptionRecord.TryDeserialize(existing, out var previous) || previous is null ||
                !string.Equals(previous.ProfileId, profileId, StringComparison.Ordinal) ||
                string.Equals(previous.InstanceId, newSession, StringComparison.Ordinal) ||
                !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(previous.GameExecutable)),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(executable)), StringComparison.OrdinalIgnoreCase))
                return false;

            IntPtr handle = processApi.Open(previous.Pid,
                OwnedProcessTermination.ProcessQueryLimitedInformation | OwnedProcessTermination.Synchronize);
            if (handle == IntPtr.Zero)
                return !processApi.PidExists(previous.Pid);
            try
            {
                if (processApi.WaitExited(handle, 0)) return true;
                // A known different creation instant proves the recorded process
                // is gone, even when its PID now belongs to another live process.
                // Equivalent timestamp formatting must not retire a live owner.
                return DateTimeOffset.TryParse(previous.ProcessCreatedAt, CultureInfo.InvariantCulture,
                           DateTimeStyles.RoundtripKind, out var recorded) &&
                       DateTimeOffset.TryParse(processApi.CreationUtc(handle), CultureInfo.InvariantCulture,
                           DateTimeStyles.RoundtripKind, out var observed) &&
                       recorded.UtcDateTime.Ticks != observed.UtcDateTime.Ticks;
            }
            finally { processApi.Close(handle); }
        }
        catch { return false; }
    }
}
