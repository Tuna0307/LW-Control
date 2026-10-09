using System.Text.Json;

namespace LWBridge.Desktop;

internal interface INativeAsyncCommandService
{
    bool CanHandle(string command);
    Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken);
}
