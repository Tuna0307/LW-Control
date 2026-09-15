using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class CompositeAsyncCommandService(params INativeAsyncCommandService[] services) : INativeAsyncCommandService
{
    private readonly INativeAsyncCommandService[] services = services ?? throw new ArgumentNullException(nameof(services));

    public bool CanHandle(string command) => services.Any(service => service.CanHandle(command));

    public Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        foreach (INativeAsyncCommandService service in services)
        {
            if (service.CanHandle(command))
                return service.InvokeAsync(command, payload, cancellationToken);
        }
        throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED", $"No native service handles '{command}'.");
    }
}
