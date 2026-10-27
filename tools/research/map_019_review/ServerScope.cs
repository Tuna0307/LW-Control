using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

// The owner explicitly supplied these four read-only Map server views.
internal sealed class Map019ServerScope : IAsyncDisposable
{
    private readonly CurrentClientMapBlockSource source;
    private readonly CurrentClientMapContext original;
    private readonly Func<string,CancellationToken,Task<JsonElement>> camera;
    private readonly string root;
    private readonly JsonElement before;
    private Map019ServerScope(CurrentClientMapBlockSource source, CurrentClientMapContext original,
        Func<string,CancellationToken,Task<JsonElement>> camera, string root, JsonElement before)
        => (this.source,this.original,this.camera,this.root,this.before)=(source,original,camera,root,before);

    internal static async Task<Map019ServerScope?> EnterAsync(CurrentClientMapBlockSource source,
        Func<string,CancellationToken,Task<JsonElement>> camera,string root, CancellationToken token)
    {
        string? raw=Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_SERVER");
        if (raw is null) return null;
        if (!int.TryParse(raw,out int target) || target is not (2175 or 2180 or 2198 or 2203))
            throw new ArgumentException("MAP019 target must be an owner-supplied server:2175,2180,2198,2203");
        CurrentClientMapContext initial=await source.GetCurrentContextAsync(token);
        // Establish a known task-owned home view before measuring the server
        // transition; PlayerTile is not claimed to be an arbitrary viewed tile.
        if (initial.PlayerTileX is int hx && initial.PlayerTileY is int hy)
            await source.JumpToCoordinateAsync(initial.ServerId,hx,hy,token);
        await Task.Delay(1000,token);
        JsonElement snapshot=await camera("remote-original",token);
        var scope=new Map019ServerScope(source,initial,camera,root,snapshot);
        try
        {
            var timer=Stopwatch.StartNew();
            var jump=await source.JumpToServerAsync(target,token);
            CurrentClientMapContext current=await source.GetCurrentContextAsync(token);
            if (current.ServerId!=target) throw new InvalidDataException("Remote view did not enter requested server");
            File.WriteAllText(Path.Combine(root,"map019-server-enter.json"),JsonSerializer.Serialize(new {
                target,originalServer=initial.ServerId,seconds=timer.Elapsed.TotalSeconds,jump,
                confirmedServer=current.ServerId,readOnlyView=true,gameActions=0}));
            return scope;
        }
        catch
        {
            await scope.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        using var restore=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var timer=Stopwatch.StartNew();
        var returned=await source.JumpToServerAsync(original.ServerId,restore.Token);
        if (original.PlayerTileX is int x && original.PlayerTileY is int y)
            await source.JumpToCoordinateAsync(original.ServerId,x,y,restore.Token);
        await Task.Delay(1000,restore.Token);
        var current=await source.GetCurrentContextAsync(restore.Token);
        var after=await camera("remote-return",restore.Token);
        bool exactGeometry=JsonElement.DeepEquals(before.GetProperty("cameraSnapshot"),after.GetProperty("cameraSnapshot"));
        File.WriteAllText(Path.Combine(root,"map019-server-return.json"),JsonSerializer.Serialize(new {
            originalServer=original.ServerId,confirmedServer=current.ServerId,returned,
            seconds=timer.Elapsed.TotalSeconds,serverRestored=current.ServerId==original.ServerId,
            originalPlayerTile= new {original.PlayerTileX,original.PlayerTileY},
            cameraGeometryExactlyRestored=exactGeometry,
            cameraLimit="Task-owned home view was established before the transition; geometry equality is reported independently.",
            gameActions=0}));
        if (current.ServerId!=original.ServerId) throw new InvalidDataException("Original Map server was not restored");
    }
}
