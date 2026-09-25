using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop;

internal sealed record ProfileSettingsRecord(
    long Revision,
    JsonElement Value);

internal sealed class ProfileSettingsStore : IDisposable
{
    private readonly object gate = new();
    private readonly SqliteConnection connection;
    private bool disposed;

    internal ProfileSettingsStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException(
                "Profile database path is required.",
                nameof(databasePath));

        string fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
        }.ToString());
        try
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS settings (
                    id INTEGER PRIMARY KEY CHECK(id = 1),
                    revision INTEGER NOT NULL,
                    value_json TEXT NOT NULL
                );
                INSERT OR IGNORE INTO settings(id, revision, value_json)
                VALUES (1, 0, '{}');
                """;
            command.ExecuteNonQuery();
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal ProfileSettingsRecord Save(
        long expectedRevision,
        JsonElement value)
    {
        if (expectedRevision < 0 ||
            value.ValueKind != JsonValueKind.Object)
            throw InvalidSettings();

        string valueJson = value.GetRawText();
        lock (gate)
        {
            ThrowIfDisposed();
            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using SqliteCommand update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE settings
                SET value_json = $value, revision = revision + 1
                WHERE id = 1 AND revision = $revision
                """;
            update.Parameters.AddWithValue("$value", valueJson);
            update.Parameters.AddWithValue(
                "$revision",
                expectedRevision);

            if (update.ExecuteNonQuery() != 1)
                throw RevisionConflict();

            transaction.Commit();
            return Read();
        }
    }

    internal ProfileSettingsRecord Read()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            using SqliteCommand command =
                connection.CreateCommand();
            command.CommandText = """
                SELECT revision, value_json
                FROM settings
                WHERE id = 1
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
                throw InvalidSettings();

            long revision = reader.GetInt64(0);
            string raw = reader.GetString(1);
            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(raw);
                return new ProfileSettingsRecord(
                    revision,
                    document.RootElement.Clone());
            }
            catch (JsonException)
            {
                throw new BridgeCommandException(
                    "PROFILE_DATA_INVALID",
                    "PROFILE_DATA_INVALID");
            }
        }
    }

    private static BridgeCommandException InvalidSettings() =>
        new(
            "INVALID_PROFILE_SETTINGS",
            "INVALID_PROFILE_SETTINGS");
    private static BridgeCommandException RevisionConflict() =>
        new(
            "PROFILE_REVISION_CONFLICT",
            "PROFILE_REVISION_CONFLICT");

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            connection.Dispose();
        }
    }
}
