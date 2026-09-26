namespace LWBridge.Desktop;

internal sealed class NativeSubscriptionRegistry
{
    private readonly HashSet<string> allowlist;
    private readonly HashSet<string> subscriptions = new(StringComparer.Ordinal);
    private bool closed;

    public NativeSubscriptionRegistry(IEnumerable<string> allowlist)
    {
        this.allowlist = new HashSet<string>(allowlist, StringComparer.Ordinal);
    }

    public int Count => subscriptions.Count;

    public bool Contains(string eventName) => subscriptions.Contains(eventName);

    public bool Listen(string eventName)
    {
        if (closed || !allowlist.Contains(eventName)) return false;
        return subscriptions.Add(eventName);
    }

    public bool Unlisten(string eventName)
    {
        if (closed) return false;
        return subscriptions.Remove(eventName);
    }

    public void Close()
    {
        closed = true;
        subscriptions.Clear();
    }
}
