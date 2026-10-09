using System.Diagnostics;

namespace LWBridge.Desktop;

// LWB-R7-117: recovered startup registration + environment binding object.
// Creating this object is side-effect free; registration remains owned by the
// shared host state, and the four variables are applied only to an explicitly
// supplied child ProcessStartInfo.
internal sealed record LWBridgeControlPipeLaunchBinding(
    string ProfileId,
    string InstanceId,
    string PipeToken,
    string BuildId,
    long ExpiresAtMilliseconds,
    IReadOnlyDictionary<string, string> Environment)
{
    internal void ApplyTo(ProcessStartInfo start)
    {
        ArgumentNullException.ThrowIfNull(start);
        foreach ((string name, string value) in Environment)
            start.Environment[name] = value;
    }
}
