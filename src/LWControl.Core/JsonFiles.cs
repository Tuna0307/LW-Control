using System.Text.Json;
using System.Text.Json.Serialization;

namespace LWControl.Core;

public static class JsonFiles
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter<ClaimKind>(allowIntegerValues: false),
            new JsonStringEnumConverter<CurrentTaskState>(allowIntegerValues: false)
        }
    };

    public static T Read<T>(string path) where T : class
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 2 * 1024 * 1024)
            throw new InvalidDataException("JSON input exceeds the 2 MiB limit.");
        return JsonSerializer.Deserialize<T>(stream, Options)
            ?? throw new InvalidDataException("JSON input is empty or null.");
    }

    public static void Write<T>(string path, T value)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".lwcontrol-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, value, Options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
