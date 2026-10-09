using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop;

/// <summary>
/// Completes the high-level Map317 provider boundary with the asynchronous run
/// lifetime that begins only after the durable scan has been accepted.
/// </summary>
internal interface IMap317RunScopedProvider : Map317.IMapProvider, IDisposable
{
    event Action<string>? RunTerminated;

    void ActivateAcceptedRun(Map317.MapControlPlane control, string scanRunId);
}
