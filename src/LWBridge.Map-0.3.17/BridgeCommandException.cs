namespace LWBridge.Map317;

public sealed class BridgeCommandException : Exception
{
    public BridgeCommandException(string code, string message, object? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }
    public object? Details { get; }
}
