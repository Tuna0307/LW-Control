namespace LWBridge.Map317.Checks;

internal static class TestAssert
{
    internal static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }

    internal static TException Throws<TException>(Action action, string? code = null)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException error)
        {
            if (code is not null && error is BridgeCommandException bridge && bridge.Code != code)
                throw new InvalidOperationException($"Expected code {code}, got {bridge.Code}", error);
            return error;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}");
    }
}
