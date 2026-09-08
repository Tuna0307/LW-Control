using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record LWBridgeLocalConfig
{
    public const int CurrentSchemaVersion = 1;
    public const string CurrentOwner = "LWBridgeRebuild";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string Owner { get; init; } = CurrentOwner;
    public string ProfileId { get; init; } = string.Empty;
    public string? GameRoot { get; init; }
    public bool AutoLaunchGame { get; init; } = true;
    public bool AutoReconnect { get; init; }
    public IReadOnlyList<int> ServerJumpHistory { get; init; } = Array.Empty<int>();

    public static LWBridgeLocalConfig CreateDefault() => new()
    {
        ProfileId = "local-" + Guid.NewGuid().ToString("N"),
    };
}

internal sealed class LocalConfigStoreException : Exception
{
    public string Code { get; }

    public LocalConfigStoreException(string code, string message, Exception? innerException = null)
        : base(message, innerException) => Code = code;
}

internal sealed class LocalConfigStore
{
    private static readonly ConcurrentDictionary<string, object> StorageGates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan StorageLockTimeout = TimeSpan.FromSeconds(5);

    private readonly string? path;
    private readonly string? backupPath;
    private readonly string? lockPath;
    private readonly object? storageGate;
    private readonly object gate = new();
    private LWBridgeLocalConfig current;

    public LocalConfigStore(string? root = null, bool persistent = true)
    {
        if (!persistent)
        {
            current = LWBridgeLocalConfig.CreateDefault();
            return;
        }

        root ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild");
        root = Path.GetFullPath(root);

        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalConfigStoreException(
                "CONFIG_ROOT_UNAVAILABLE",
                "LWBridge configuration storage is not writable.",
                ex);
        }

        path = Path.Combine(root, "config.json");
        backupPath = Path.Combine(root, "config.backup.json");
        lockPath = Path.Combine(root, "config.lock");
        storageGate = StorageGates.GetOrAdd(path, static _ => new object());
        current = WithStorageLock(Load);
    }

    public LWBridgeLocalConfig Snapshot
    {
        get { lock (gate) return current; }
    }

    public LWBridgeLocalConfig Update(Func<LWBridgeLocalConfig, LWBridgeLocalConfig> update)
    {
        lock (gate)
        {
            if (path is null)
            {
                current = Validate(update(current));
                return current;
            }

            return WithStorageLock(() =>
            {
                // Refresh under the cross-instance lock so two desktop/config owners
                // cannot silently overwrite each other's already-committed changes.
                LWBridgeLocalConfig baseline = Load();
                LWBridgeLocalConfig next = Validate(update(baseline));
                Save(next);
                current = next;
                return current;
            });
        }
    }

    private T WithStorageLock<T>(Func<T> action)
    {
        if (path is null || lockPath is null || storageGate is null)
            return action();

        lock (storageGate)
        {
            var stopwatch = Stopwatch.StartNew();
            FileStream? lockStream = null;
            while (lockStream is null)
            {
                try
                {
                    lockStream = new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 1,
                        FileOptions.None);
                }
                catch (IOException) when (stopwatch.Elapsed < StorageLockTimeout)
                {
                    Thread.Sleep(25);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new LocalConfigStoreException(
                        "CONFIG_LOCK_FAILED",
                        "LWBridge could not acquire ownership of its configuration store.",
                        ex);
                }
                catch (IOException ex)
                {
                    throw new LocalConfigStoreException(
                        "CONFIG_LOCK_TIMEOUT",
                        "LWBridge timed out waiting for another configuration writer to finish.",
                        ex);
                }
            }

            using (lockStream)
                return action();
        }
    }

    private LWBridgeLocalConfig Load()
    {
        if (path is null) return LWBridgeLocalConfig.CreateDefault();

        bool primaryExists = GetFilePresence(
            path,
            "CONFIG_READ_FAILED",
            "LWBridge configuration storage could not be inspected. The existing storage was left in place.");
        if (!primaryExists)
        {
            bool backupExists = backupPath is not null && GetFilePresence(
                backupPath,
                "CONFIG_RECOVERY_FAILED",
                "LWBridge configuration recovery storage could not be inspected. The existing storage was left in place.");
            if (backupExists)
            {
                try
                {
                    LWBridgeLocalConfig recovered = Read(backupPath!);
                    RestorePrimaryFromBackup();
                    return recovered;
                }
                catch (LocalConfigStoreException ex) when (IsCompatibilityError(ex))
                {
                    throw;
                }
                catch (Exception backupError) when (IsRecoverableReadError(backupError))
                {
                    throw new LocalConfigStoreException(
                        "CONFIG_RECOVERY_FAILED",
                        "LWBridge primary configuration is missing and its recovery copy could not be read. The recovery copy was left in place.",
                        backupError);
                }
            }

            LWBridgeLocalConfig created = LWBridgeLocalConfig.CreateDefault();
            Save(created);
            return created;
        }

        try
        {
            return Read(path);
        }
        catch (LocalConfigStoreException primaryError) when (IsCompatibilityError(primaryError))
        {
            throw;
        }
        catch (Exception primaryError) when (IsRecoverableReadError(primaryError))
        {
            bool backupExists = backupPath is not null && GetFilePresence(
                backupPath,
                "CONFIG_RECOVERY_FAILED",
                "LWBridge configuration recovery storage could not be inspected. The existing files were left in place.");
            if (backupExists)
            {
                try
                {
                    LWBridgeLocalConfig recovered = Read(backupPath!);
                    PreserveCorruptPrimary();
                    RestorePrimaryFromBackup();
                    return recovered;
                }
                catch (Exception backupError) when (IsRecoverableReadError(backupError) ||
                    backupError is LocalConfigStoreException compatibilityError && IsCompatibilityError(compatibilityError))
                {
                    throw new LocalConfigStoreException(
                        "CONFIG_RECOVERY_FAILED",
                        "LWBridge configuration and its recovery copy could not be read. The files were left in place.",
                        new AggregateException(primaryError, backupError));
                }
            }

            throw new LocalConfigStoreException(
                "CONFIG_READ_FAILED",
                "LWBridge configuration could not be read. The file was left in place instead of resetting the local profile identity.",
                primaryError);
        }
    }

    private static bool GetFilePresence(string filePath, string code, string message)
    {
        try
        {
            _ = File.GetAttributes(filePath);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalConfigStoreException(code, message, ex);
        }
    }

    private static bool IsCompatibilityError(LocalConfigStoreException error) =>
        error.Code is "CONFIG_OWNER_MISMATCH" or "CONFIG_SCHEMA_UNSUPPORTED";

    private static bool IsRecoverableReadError(Exception error) =>
        error is JsonException or IOException or UnauthorizedAccessException ||
        error is LocalConfigStoreException configError && !IsCompatibilityError(configError);

    private static LWBridgeLocalConfig Read(string filePath)
    {
        string json = File.ReadAllText(filePath);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new LocalConfigStoreException("CONFIG_INVALID", "LWBridge configuration must be a JSON object.");

        if (root.TryGetProperty("schemaVersion", out JsonElement schema))
        {
            if (schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version) || version != LWBridgeLocalConfig.CurrentSchemaVersion)
                throw new LocalConfigStoreException("CONFIG_SCHEMA_UNSUPPORTED", "LWBridge configuration schema is not supported by this build.");
        }

        if (root.TryGetProperty("owner", out JsonElement owner))
        {
            if (owner.ValueKind != JsonValueKind.String || !string.Equals(owner.GetString(), LWBridgeLocalConfig.CurrentOwner, StringComparison.Ordinal))
                throw new LocalConfigStoreException("CONFIG_OWNER_MISMATCH", "The configuration file belongs to a different application or format.");
        }

        LWBridgeLocalConfig? parsed = JsonSerializer.Deserialize<LWBridgeLocalConfig>(json, JsonOptions.Default);
        if (parsed is null)
            throw new LocalConfigStoreException("CONFIG_INVALID", "LWBridge configuration could not be decoded.");
        return Validate(parsed with
        {
            SchemaVersion = LWBridgeLocalConfig.CurrentSchemaVersion,
            Owner = LWBridgeLocalConfig.CurrentOwner,
        });
    }

    private static LWBridgeLocalConfig Validate(LWBridgeLocalConfig value)
    {
        if (value.SchemaVersion != LWBridgeLocalConfig.CurrentSchemaVersion)
            throw new LocalConfigStoreException("CONFIG_SCHEMA_UNSUPPORTED", "LWBridge configuration schema is not supported by this build.");
        if (!string.Equals(value.Owner, LWBridgeLocalConfig.CurrentOwner, StringComparison.Ordinal))
            throw new LocalConfigStoreException("CONFIG_OWNER_MISMATCH", "The configuration file belongs to a different application or format.");
        if (string.IsNullOrWhiteSpace(value.ProfileId))
            throw new LocalConfigStoreException("CONFIG_PROFILE_INVALID", "LWBridge configuration is missing its stable local profile identity.");
        return value with { ServerJumpHistory = NormalizeServerJumpHistory(value.ServerJumpHistory) };
    }

    private static IReadOnlyList<int> NormalizeServerJumpHistory(IEnumerable<int>? values)
    {
        if (values is null) return Array.Empty<int>();
        var seen = new HashSet<int>();
        var result = new List<int>(5);
        foreach (int serverId in values)
        {
            if (serverId < 1 || serverId > 99999 || !seen.Add(serverId)) continue;
            result.Add(serverId);
            if (result.Count == 5) break;
        }
        return result;
    }

    private void Save(LWBridgeLocalConfig value)
    {
        if (path is null) return;

        string temp = path + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions.Indented));
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Replace(temp, path, backupPath, ignoreMetadataErrors: true);
            else
                File.Move(temp, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalConfigStoreException(
                "CONFIG_WRITE_FAILED",
                "LWBridge could not save its configuration; the running settings were not changed.",
                ex);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { }
        }
    }

    private void PreserveCorruptPrimary()
    {
        if (path is null || !File.Exists(path)) return;
        string preserved = path + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        File.Copy(path, preserved, overwrite: false);
    }

    private void RestorePrimaryFromBackup()
    {
        if (path is null || backupPath is null) return;
        string temp = path + ".recover." + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(backupPath, temp, overwrite: false);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { }
        }
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
