using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record LWBridgeLocalConfig(
    string ProfileId,
    string? GameRoot,
    bool AutoLaunchGame,
    bool AutoReconnect)
{
    public static LWBridgeLocalConfig CreateDefault() =>
        new("local-" + Guid.NewGuid().ToString("N"), null, true, false);
}

internal sealed class LocalConfigStore
{
    private readonly string path;
    private readonly object gate = new();
    private LWBridgeLocalConfig current;

    public LocalConfigStore()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild");
        Directory.CreateDirectory(root);
        path = Path.Combine(root, "config.json");
        current = Load();
    }

    public LWBridgeLocalConfig Snapshot
    {
        get { lock (gate) return current; }
    }

    public LWBridgeLocalConfig Update(Func<LWBridgeLocalConfig, LWBridgeLocalConfig> update)
    {
        lock (gate)
        {
            current = update(current);
            Save(current);
            return current;
        }
    }

    private LWBridgeLocalConfig Load()
    {
        try
        {
            if (File.Exists(path))
            {
                var parsed = JsonSerializer.Deserialize<LWBridgeLocalConfig>(
                    File.ReadAllText(path), JsonOptions.Default);
                if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.ProfileId))
                    return parsed;
            }
        }
        catch
        {
        }

        var created = LWBridgeLocalConfig.CreateDefault();
        Save(created);
        return created;
    }

    private void Save(LWBridgeLocalConfig value)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonOptions.Indented));
        File.Move(temp, path, true);
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static readonly JsonSerializerOptions Indented = new(Default)
    {
        WriteIndented = true,
    };
}
