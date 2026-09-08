namespace LWBridge.Desktop;

internal sealed class NativeRequestRegistry : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, CancellationTokenSource> active = new(StringComparer.Ordinal);
    private bool closed;

    public bool IsClosed
    {
        get { lock (gate) return closed; }
    }

    public int ActiveCount
    {
        get { lock (gate) return active.Count; }
    }

    public bool TryStart(string id, out CancellationTokenSource? cancellation)
    {
        cancellation = null;
        lock (gate)
        {
            if (closed || active.ContainsKey(id)) return false;
            var created = new CancellationTokenSource();
            active.Add(id, created);
            cancellation = created;
            return true;
        }
    }

    public bool Cancel(string id)
    {
        CancellationTokenSource? cancellation;
        lock (gate)
        {
            if (!active.TryGetValue(id, out cancellation)) return false;
        }
        try
        {
            cancellation.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public bool Complete(string id, CancellationTokenSource cancellation)
    {
        bool publishable;
        lock (gate)
        {
            publishable = !closed &&
                active.TryGetValue(id, out CancellationTokenSource? current) &&
                ReferenceEquals(current, cancellation);
            if (active.TryGetValue(id, out current) && ReferenceEquals(current, cancellation))
                active.Remove(id);
        }
        cancellation.Dispose();
        return publishable;
    }

    public void Close()
    {
        CancellationTokenSource[] owned;
        lock (gate)
        {
            if (closed) return;
            closed = true;
            owned = active.Values.ToArray();
            active.Clear();
        }
        foreach (CancellationTokenSource cancellation in owned)
        {
            try { cancellation.Cancel(); }
            catch (ObjectDisposedException) { }
            cancellation.Dispose();
        }
    }

    public void Dispose() => Close();
}
