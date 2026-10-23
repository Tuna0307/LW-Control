using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop;

internal sealed record ProfileStateRecord(
    string ProfileId,
    string Key,
    long Revision,
    JsonElement? Value);

internal sealed class ProfileStateStore : IDisposable
{
    private readonly object gate = new();
    private readonly SqliteConnection connection;
    private bool disposed;

    public ProfileStateStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Profile database path is required.", nameof(databasePath));

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
                CREATE TABLE IF NOT EXISTS profile_state (
                    key TEXT PRIMARY KEY,
                    value_json TEXT NOT NULL,
                    revision INTEGER NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }
        catch (Exception error)
        {
            connection.Dispose();
            throw Unavailable(error);
        }
    }

    internal ProfileStateRecord Read(string profileId, string key)
    {
        ValidateIdentity(profileId, key);
        lock (gate)
        {
            ThrowIfDisposed();
            try
            {
                return ReadLocked(profileId, key);
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception error) { throw Unavailable(error); }
        }
    }

    internal ProfileStateRecord Save(
        string profileId,
        string key,
        long expectedRevision,
        JsonElement value)
    {
        ValidateIdentity(profileId, key);
        if (expectedRevision < 0)
            throw InvalidState();

        string valueJson = value.GetRawText();
        lock (gate)
        {
            ThrowIfDisposed();
            try
            {
                using SqliteTransaction transaction = connection.BeginTransaction();
                long? currentRevision = ReadRevisionLocked(key, transaction);
                if (currentRevision is null)
                {
                    if (expectedRevision != 0)
                        throw RevisionConflict();

                    using SqliteCommand insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT OR IGNORE INTO profile_state(key, value_json, revision)
                        VALUES ($key, $value, 1)
                        """;
                    insert.Parameters.AddWithValue("$key", key);
                    insert.Parameters.AddWithValue("$value", valueJson);
                    if (insert.ExecuteNonQuery() != 1)
                        throw RevisionConflict();
                }
                else
                {
                    // A cleared marker may be recreated from revision 0 (a new draft), and the new draft
                    // still gets a fresh, higher revision than any editor saw before the clear.
                    using SqliteCommand update = connection.CreateCommand();
                    update.Transaction = transaction;
                    update.CommandText = """
                        UPDATE profile_state
                        SET value_json = $value, revision = revision + 1
                        WHERE key = $key AND (revision = $revision OR ($revision = 0 AND value_json = 'null'))
                        """;
                    update.Parameters.AddWithValue("$value", valueJson);
                    update.Parameters.AddWithValue("$key", key);
                    update.Parameters.AddWithValue("$revision", expectedRevision);
                    if (update.ExecuteNonQuery() != 1)
                        throw RevisionConflict();
                }

                transaction.Commit();
                return ReadLocked(profileId, key);
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception error) { throw Unavailable(error); }
        }
    }

    internal ProfileStateRecord Clear(
        string profileId,
        string key,
        long expectedRevision)
    {
        ValidateIdentity(profileId, key);
        if (expectedRevision < 0)
            throw InvalidState();

        lock (gate)
        {
            ThrowIfDisposed();
            try
            {
                // Audit 2026-10-23 W04: keep a cleared marker with the next revision instead of deleting
                // the row, so a recreated draft never reuses an old revision and a stale editor conflicts.
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE profile_state
                    SET value_json = 'null', revision = revision + 1
                    WHERE key = $key AND revision = $revision
                    """;
                command.Parameters.AddWithValue("$key", key);
                command.Parameters.AddWithValue("$revision", expectedRevision);
                if (command.ExecuteNonQuery() != 1)
                    throw RevisionConflict();

                return ReadLocked(profileId, key);
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception error) { throw Unavailable(error); }
        }
    }

    private ProfileStateRecord ReadLocked(string profileId, string key)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT revision, value_json
            FROM profile_state
            WHERE key = $key
            """;
        command.Parameters.AddWithValue("$key", key);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
            return Empty(profileId, key);

        long revision = reader.GetInt64(0);
        string valueJson = reader.GetString(1);
        try
        {
            using JsonDocument document = JsonDocument.Parse(valueJson);
            // A cleared marker reads as "no value" but keeps its revision.
            return new ProfileStateRecord(profileId, key, revision,
                document.RootElement.ValueKind == JsonValueKind.Null ? null : document.RootElement.Clone());
        }
        catch (JsonException error)
        {
            _ = error;
            throw new BridgeCommandException(
                "PROFILE_DATA_INVALID",
                "profile data invalid");
        }
    }

    private long? ReadRevisionLocked(string key, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT revision FROM profile_state WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        object? value = command.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    private static ProfileStateRecord Empty(string profileId, string key) =>
        new(profileId, key, 0, null);

    private static void ValidateIdentity(string profileId, string key)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(key))
            throw InvalidState();
    }

    private static BridgeCommandException InvalidState() =>
        new("INVALID_PROFILE_STATE", "invalid profile state");

    private static BridgeCommandException RevisionConflict() =>
        new("PROFILE_REVISION_CONFLICT", "profile revision conflict");

    private static BridgeCommandException Unavailable(Exception error)
    {
        _ = error;
        return new BridgeCommandException(
            "PROFILE_STATE_UNAVAILABLE",
            "profile state unavailable");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            connection.Dispose();
        }
    }
}
