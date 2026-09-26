using System.Security.Cryptography;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class ProxyStatusTestHooks
{
    public string? ResourceDirectory { get; init; }
    public Func<string, bool>? FileExists { get; init; }
    public Func<string, string?>? ComputeSha256 { get; init; }
    public Func<bool>? GameRunning { get; init; }
    public Func<bool>? RuntimeManaged { get; init; }
}

internal sealed class ProxyStatusCommandService
{
    private const string BundleFileName = "xlua-proxy-bundle.json";
    private const string SecureFileName = "xlua-proxy-secure.dll";
    private const string PlainFileName = "xlua-proxy-plain.dll";

    private readonly string profileId;
    private readonly string? runtimeDirectory;
    private readonly GameInstallationService installation;
    private readonly Func<bool>? runtimeManagedProvider;
    private readonly ProxyStatusTestHooks? testHooks;

    internal ProxyStatusCommandService(
        string profileId,
        string? runtimeDirectory,
        GameInstallationService installation,        Func<bool>? runtimeManagedProvider = null,
        ProxyStatusTestHooks? testHooks = null)
    {
        this.profileId = profileId;
        this.runtimeDirectory = runtimeDirectory;
        this.installation = installation;
        this.runtimeManagedProvider = runtimeManagedProvider;
        this.testHooks = testHooks;
    }

    internal object Invoke(JsonElement payload)
    {
        RequireRuntime(payload);
        return CreateStatus();
    }

    internal object CreateStatus()
    {
        NativeGameRootStatus root = installation.GetNativeStatus();
        ResourceSet? resources = ResolveResourceSet();
        bool resourceAvailable = resources is not null;
        bool runtimeManaged =
            testHooks?.RuntimeManaged?.Invoke() ??
            runtimeManagedProvider?.Invoke() ??
            false;

        if (!root.Valid || string.IsNullOrWhiteSpace(root.Root))
        {
            return CreateReducedStatus(
                resourceAvailable,
                runtimeManaged);
        }

        string pluginDirectory = Path.Combine(
            root.Root, "Game", "LastWar_Data", "Plugins", "x86_64");        string targetPath = Path.Combine(pluginDirectory, "xlua.dll");
        string originalPath = Path.Combine(pluginDirectory, "xlua_.dll");
        bool targetExists = Exists(targetPath);
        bool originalExists = Exists(originalPath);
        string? installedMode = targetExists && resources is not null
            ? ClassifyInstalledMode(targetPath, resources)
            : null;
        bool installed =
            resourceAvailable &&
            targetExists &&
            originalExists &&
            installedMode is not null;

        string state =
            !resourceAvailable ? "resourceMissing" :
            !targetExists ? "targetMissing" :
            !originalExists ? "needsRepair" :
            installed ? "installed" :
            "needsRepair";

        bool gameRunning =
            testHooks?.GameRunning?.Invoke() ??
            installation.GetProcessStatus().GameRunning;
        bool repairRequired =
            !runtimeManaged &&
            gameRunning &&
            string.Equals(state, "needsRepair", StringComparison.Ordinal);

        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["state"] = state,
            ["installed"] = installed,
            ["resourceAvailable"] = resourceAvailable,            ["targetExists"] = targetExists,
            ["originalExists"] = originalExists,
            ["installedMode"] = installedMode,
            ["gameRunning"] = gameRunning,
            ["targetPath"] = targetPath,
            ["runtimeManaged"] = runtimeManaged,
            ["repairRequired"] = repairRequired,
        };
        return result;
    }

    private object CreateReducedStatus(
        bool resourceAvailable,
        bool runtimeManaged)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["state"] = "targetMissing",
            ["installed"] = false,
            ["resourceAvailable"] = resourceAvailable,
            ["targetExists"] = false,
            ["originalExists"] = false,
            ["gameRunning"] = false,
            ["targetPath"] = string.Empty,
            ["runtimeManaged"] = runtimeManaged,
            ["repairRequired"] = false,
        };
        return result;
    }

    private ResourceSet? ResolveResourceSet()
    {
        if (testHooks?.ResourceDirectory is { Length: > 0 } testDirectory)
            return TryCreateResourceSet(testDirectory);        ResourceSet? local = TryCreateResourceSet(AppContext.BaseDirectory);
        if (local is not null) return local;

        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData)) return null;

        string runtimeRoot = Path.Combine(
            localAppData,
            "LastWar-xLua-Bridge",
            "runtime");
        if (!Directory.Exists(runtimeRoot)) return null;

        try
        {
            foreach (string directory in Directory
                         .EnumerateDirectories(runtimeRoot)
                         .OrderByDescending(path => path, StringComparer.Ordinal))
            {
                ResourceSet? set = TryCreateResourceSet(directory);
                if (set is not null) return set;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    private ResourceSet? TryCreateResourceSet(string directory)
    {
        string bundle = Path.Combine(directory, BundleFileName);
        string secure = Path.Combine(directory, SecureFileName);
        string plain = Path.Combine(directory, PlainFileName);        return Exists(bundle) && Exists(secure) && Exists(plain)
            ? new ResourceSet(bundle, secure, plain)
            : null;
    }

    private string? ClassifyInstalledMode(
        string targetPath,
        ResourceSet resources)
    {
        string? targetHash = Hash(targetPath);
        if (targetHash is null) return null;

        string? secureHash = Hash(resources.SecurePath);
        if (secureHash is not null &&
            string.Equals(targetHash, secureHash, StringComparison.OrdinalIgnoreCase))
            return "secure";

        string? plainHash = Hash(resources.PlainPath);
        if (plainHash is not null &&
            string.Equals(targetHash, plainHash, StringComparison.OrdinalIgnoreCase))
            return "plain";

        return null;
    }

    private bool Exists(string path) =>
        testHooks?.FileExists?.Invoke(path) ?? File.Exists(path);

    private string? Hash(string path)
    {
        if (testHooks?.ComputeSha256 is { } compute)
            return compute(path);

        try
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (System.Security.SecurityException) { return null; }
    }

    private void RequireRuntime(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("profileId", out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new BridgeCommandException(
                "PROFILE_ID_REQUIRED",
                "PROFILE_ID_REQUIRED");
        }

        if (!string.Equals(
                property.GetString(),
                profileId,
                StringComparison.Ordinal) ||
            runtimeDirectory is null)
        {
            throw new BridgeCommandException(
                "PROFILE_RUNTIME_UNAVAILABLE",
                "PROFILE_RUNTIME_UNAVAILABLE");
        }
    }

    private sealed record ResourceSet(
        string BundlePath,
        string SecurePath,
        string PlainPath);
}
