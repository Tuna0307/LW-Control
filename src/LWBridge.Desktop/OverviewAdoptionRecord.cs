using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

// HOME009-R3: supported *current-client* primary-profile adoption journal.
// The original 0x2DC830 serializer provides these ten outer field names, but
// this adapter's challenge and ciphertext payload are NOT an original disk format.
// Only an exact already-owned game can be adopted. No plaintext pipe token is
// written to disk or included in diagnostics.
internal sealed record OverviewAdoptionSnapshot(
    string ProfileId, string InstanceId, string Challenge,
    int Pid, string GameExecutable, string ProcessCreatedAt,
    string BuildId, string PipeToken, long StartedAt, bool LeaseRequired);

internal static class OverviewAdoptionRecord
{
    internal const string FileName = "adoption.json";

    internal static byte[] Serialize(OverviewAdoptionSnapshot identity)
    {
        ValidateIdentity(identity);
        byte[] salt = Entropy(identity);
        byte[] secret = Encoding.UTF8.GetBytes(identity.PipeToken);
        try
        {
            byte[] encrypted = Protect(secret, salt);
            var record = new
            {
                schemaVersion = 1,
                profileId = identity.ProfileId,
                instanceId = identity.InstanceId,
                sessionId = identity.InstanceId, // clone-only exact-ownership alias
                encryptedPipeToken = Convert.ToBase64String(encrypted),
                pid = identity.Pid,
                processCreatedAt = identity.ProcessCreatedAt,
                gameExecutable = identity.GameExecutable,
                buildId = identity.BuildId,
                startedAt = identity.StartedAt,
                leaseRequired = identity.LeaseRequired,
                // Clone-only correlation; original has a separate host lease.
                challenge = identity.Challenge
            };
            return JsonSerializer.SerializeToUtf8Bytes(record);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    internal static bool TryDeserialize(byte[] payload, out OverviewAdoptionSnapshot? record)
    {
        record = null;
        try
        {
            if (payload.Length is < 64 or > 16_384) return false;
            using JsonDocument doc = JsonDocument.Parse(payload);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty item in root.EnumerateObject())
                if (!names.Add(item.Name)) return false;
            if (!root.TryGetProperty("schemaVersion", out JsonElement schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version) || version != 1 ||
                !root.TryGetProperty("pid", out JsonElement pidElement) ||
                !pidElement.TryGetInt32(out int pid) || pid <= 0 ||
                !root.TryGetProperty("startedAt", out JsonElement startedElement) ||
                !startedElement.TryGetInt64(out long started) || started <= 0 ||
                !root.TryGetProperty("leaseRequired", out JsonElement lease) ||
                lease.ValueKind != JsonValueKind.True)
                return false;

            string profile = Required(root, "profileId");
            string session = Required(root, "instanceId");
            if (!string.Equals(Required(root, "sessionId"), session, StringComparison.Ordinal))
                return false;
            string challenge = Required(root, "challenge");
            string processPath = Required(root, "gameExecutable");
            string creation = Required(root, "processCreatedAt");
            string build = Required(root, "buildId");
            string cipher = Required(root, "encryptedPipeToken");
            if (cipher.Length > 8192) return false;
            var descriptor = new OverviewAdoptionSnapshot(
                profile, session, challenge, pid, processPath, creation,
                build, string.Empty, started, true);
            byte[] salt = Entropy(descriptor);
            byte[] decrypted;
            try
            {
                decrypted = Unprotect(Convert.FromBase64String(cipher), salt);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(salt);
            }
            try
            {
                string token = new UTF8Encoding(false, true).GetString(decrypted);
                record = descriptor with { PipeToken = token };
                ValidateIdentity(record);
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(decrypted);
            }
        }
        catch
        {
            record = null;
            return false;
        }
    }

    private static string Required(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException("Invalid adoption record: " + name);
        return value.GetString()!;
    }

    private static void ValidateIdentity(OverviewAdoptionSnapshot record)
    {
        if (string.IsNullOrWhiteSpace(record.ProfileId) ||
            string.IsNullOrWhiteSpace(record.InstanceId) ||
            record.Challenge.Length != 64 || !record.Challenge.All(Uri.IsHexDigit) ||
            record.Pid <= 0 ||
            !Path.IsPathFullyQualified(record.GameExecutable) ||
            !DateTimeOffset.TryParse(record.ProcessCreatedAt, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _) ||
            string.IsNullOrWhiteSpace(record.BuildId) ||
            record.PipeToken.Length != 43 ||
            record.PipeToken.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_') ||
            record.StartedAt <= 0 ||
            !record.LeaseRequired)
            throw new InvalidDataException("The adoption record identity is invalid.");
    }

    private static byte[] Entropy(OverviewAdoptionSnapshot record) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(
            "LWBridge.Home009.R3.v1\0" +
            record.ProfileId + "\0" + record.InstanceId + "\0" +
            record.Challenge + "\0" + record.Pid.ToString(CultureInfo.InvariantCulture) + "\0" +
            Path.GetFullPath(record.GameExecutable).ToUpperInvariant() + "\0" +
            record.ProcessCreatedAt + "\0" + record.BuildId));

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        internal int Length;
        internal IntPtr Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob plain, string? description, ref DataBlob entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DataBlob protectedData);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob protectedData, IntPtr description, ref DataBlob entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DataBlob plain);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);

    private static byte[] Protect(byte[] plaintext, byte[] salt)
    {
        DataBlob input = CopyInput(plaintext);
        DataBlob entropy = CopyInput(salt);
        DataBlob output = default;
        try
        {
            if (!CryptProtectData(ref input, "LWBridge Home session", ref entropy,
                IntPtr.Zero, IntPtr.Zero, 1, out output))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return CopyOutput(output);
        }
        finally
        {
            FreeInput(ref input);
            FreeInput(ref entropy);
            if (output.Data != IntPtr.Zero) _ = LocalFree(output.Data);
        }
    }

    private static byte[] Unprotect(byte[] ciphertext, byte[] salt)
    {
        DataBlob input = CopyInput(ciphertext);
        DataBlob entropy = CopyInput(salt);
        DataBlob output = default;
        try
        {
            if (!CryptUnprotectData(ref input, IntPtr.Zero, ref entropy,
                IntPtr.Zero, IntPtr.Zero, 1, out output))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return CopyOutput(output);
        }
        finally
        {
            FreeInput(ref input);
            FreeInput(ref entropy);
            if (output.Data != IntPtr.Zero) _ = LocalFree(output.Data);
        }
    }

    private static DataBlob CopyInput(byte[] source)
    {
        var blob = new DataBlob { Length = source.Length, Data = Marshal.AllocHGlobal(source.Length) };
        Marshal.Copy(source, 0, blob.Data, source.Length);
        return blob;
    }

    private static byte[] CopyOutput(DataBlob blob)
    {
        if (blob.Data == IntPtr.Zero || blob.Length is <= 0 or > 16_384)
            throw new InvalidDataException("Windows protected-data result was invalid.");
        var bytes = new byte[blob.Length];
        Marshal.Copy(blob.Data, bytes, 0, bytes.Length);
        return bytes;
    }

    private static void FreeInput(ref DataBlob blob)
    {
        if (blob.Data != IntPtr.Zero)
        {
            Span<byte> zeros = stackalloc byte[256];
            zeros.Clear();
            for (int offset = 0; offset < blob.Length; offset += zeros.Length)
                Marshal.Copy(zeros[..Math.Min(zeros.Length, blob.Length - offset)].ToArray(), 0,
                    IntPtr.Add(blob.Data, offset), Math.Min(zeros.Length, blob.Length - offset));
            Marshal.FreeHGlobal(blob.Data);
        }
        blob = default;
    }
}
