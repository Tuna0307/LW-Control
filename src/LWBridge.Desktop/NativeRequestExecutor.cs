namespace LWBridge.Desktop;

internal enum NativeRequestExecutionStatus
{
    Success,
    Cancelled,
    Rejected,
}

internal sealed record NativeRequestExecution(NativeRequestExecutionStatus Status, object? Result = null);

internal sealed class NativeRequestExecutor : IDisposable
{
    private readonly NativeRequestRegistry requests = new();

    public bool IsClosed => requests.IsClosed;
    public int ActiveCount => requests.ActiveCount;

    public bool Cancel(string id) => requests.Cancel(id);

    public async Task<NativeRequestExecution> ExecuteAsync(
        string id,
        Func<CancellationToken, Task<object?>> operation)
    {
        if (!requests.TryStart(id, out CancellationTokenSource? cancellation) || cancellation is null)
            return new(NativeRequestExecutionStatus.Rejected);

        CancellationToken token = cancellation.Token;
        bool ownershipReleased = false;
        try
        {
            object? result = await operation(token).ConfigureAwait(true);
            token.ThrowIfCancellationRequested();
            bool publishable = requests.Complete(id, cancellation);
            ownershipReleased = true;
            return publishable
                ? new(NativeRequestExecutionStatus.Success, result)
                : new(NativeRequestExecutionStatus.Cancelled);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || requests.IsClosed)
        {
            return new(NativeRequestExecutionStatus.Cancelled);
        }
        catch (Exception) when (token.IsCancellationRequested || requests.IsClosed)
        {
            return new(NativeRequestExecutionStatus.Cancelled);
        }
        finally
        {
            if (!ownershipReleased)
                requests.Complete(id, cancellation);
        }
    }

    public void Close() => requests.Close();
    public void Dispose() => requests.Dispose();
}
