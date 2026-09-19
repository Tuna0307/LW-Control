namespace LWBridge.Desktop;

internal sealed class BridgeCommandException : Exception
{
    public string Code { get; }
    public object? Details { get; }

    public BridgeCommandException(string code, string message, object? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }
}
