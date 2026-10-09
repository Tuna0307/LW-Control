using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record ProductionUiBuildIdentity(
    int SchemaVersion,
    string Project,
    string CanonicalSource,
    string SourceFingerprint,
    string ArtifactFingerprint,
    string Entrypoint);

internal sealed record DesktopUiSelection(
    string Mode,
    string RootPath,
    ProductionUiBuildIdentity? BuildIdentity);

internal static class DesktopUiContentRoot
{
    internal const string CanonicalDirectoryName = "ProductionUi";
    internal const string LegacyDirectoryName = "WebUi";
    internal const string IdentityFileName = "lwbridge-ui-build.json";
    internal const string CanonicalProject = "LWBridge.UI-0.3.17";
    internal const string CanonicalSource = "src/LWBridge.UI-0.3.17";

    internal static DesktopUiSelection Select(
        string baseDirectory,
        bool useLegacyUi,
        string? explicitUiRoot,
        bool allowProofOverride)
    {
        if (useLegacyUi && explicitUiRoot is not null)
            throw new ArgumentException("--legacy-ui cannot be combined with --ui-root.");
        if (explicitUiRoot is not null && !allowProofOverride)
            throw new ArgumentException("--ui-root is restricted to the explicit Map UI integration proof mode.");

        if (explicitUiRoot is not null)
        {
            string proofRoot = RequireIndex(Path.GetFullPath(explicitUiRoot), "Proof UI root");
            return new DesktopUiSelection("proof-override", proofRoot, TryReadCanonicalIdentity(proofRoot));
        }

        if (useLegacyUi)
        {
            string legacyRoot = RequireIndex(Path.Combine(baseDirectory, LegacyDirectoryName), "Legacy recovery UI root");
            return new DesktopUiSelection("legacy-recovery", legacyRoot, null);
        }

        string canonicalRoot = Path.Combine(baseDirectory, CanonicalDirectoryName);
        ProductionUiBuildIdentity identity = ReadCanonicalIdentity(canonicalRoot);
        return new DesktopUiSelection("canonical-production", canonicalRoot, identity);
    }

    internal static ProductionUiBuildIdentity ReadCanonicalIdentity(string rootPath)
    {
        RequireIndex(rootPath, "Canonical production UI root");
        string identityPath = Path.Combine(rootPath, IdentityFileName);
        if (!File.Exists(identityPath))
            throw new InvalidOperationException(
                $"Canonical production UI build identity is missing: {identityPath}. Rebuild LWBridge.Desktop from source.");

        ProductionUiBuildIdentity? identity;
        try
        {
            identity = JsonSerializer.Deserialize<ProductionUiBuildIdentity>(
                File.ReadAllText(identityPath), JsonOptions.Default);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException("Canonical production UI build identity is invalid JSON.", error);
        }

        if (identity is null ||
            identity.SchemaVersion != 1 ||
            !string.Equals(identity.Project, CanonicalProject, StringComparison.Ordinal) ||
            !string.Equals(identity.CanonicalSource, CanonicalSource, StringComparison.Ordinal) ||
            !string.Equals(identity.Entrypoint, "index.html", StringComparison.Ordinal) ||
            !IsSha256(identity.SourceFingerprint) ||
            !IsSha256(identity.ArtifactFingerprint))
        {
            throw new InvalidOperationException(
                "Canonical production UI build identity does not match LWBridge.UI-0.3.17. Rebuild LWBridge.Desktop from source.");
        }

        string artifactFingerprint = ComputeArtifactFingerprint(rootPath);
        if (!string.Equals(identity.ArtifactFingerprint, artifactFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Canonical production UI artifact fingerprint does not match its build identity. Rebuild LWBridge.Desktop from source.");

        return identity;
    }

    private static ProductionUiBuildIdentity? TryReadCanonicalIdentity(string rootPath)
    {
        try { return ReadCanonicalIdentity(rootPath); }
        catch (InvalidOperationException) { return null; }
    }

    private static string RequireIndex(string rootPath, string label)
    {
        string fullRoot = Path.GetFullPath(rootPath);
        string indexPath = Path.Combine(fullRoot, "index.html");
        if (!File.Exists(indexPath))
            throw new InvalidOperationException($"{label} is missing index.html: {fullRoot}");
        return fullRoot;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => Uri.IsHexDigit(character));

    private static string ComputeArtifactFingerprint(string rootPath)
    {
        string fullRoot = Path.GetFullPath(rootPath);
        string[] files = Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), IdentityFileName, StringComparison.Ordinal))
            .OrderBy(path => Path.GetRelativePath(fullRoot, path).Replace('\\', '/'), StringComparer.Ordinal)
            .ToArray();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] separator = [0];
        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(fullRoot, file).Replace('\\', '/');
            hash.AppendData(Encoding.UTF8.GetBytes(relativePath));
            hash.AppendData(separator);
            hash.AppendData(File.ReadAllBytes(file));
            hash.AppendData(separator);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
