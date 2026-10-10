using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop;

internal sealed record ProfileRegistryEntry(
    string Id,
    string DisplayName,
    string? RoleName,
    string? ServerId,
    string? GameUid,
    string Note,
    long DisplayOrder,
    bool Enabled,
    string? LockedReason,
    bool IsPrimary,
    long? LastLaunchedAt);

internal sealed record ProfileRegistrySnapshot(
    string SelectedProfileId,
    int MaxProfiles,
    IReadOnlyList<ProfileRegistryEntry> Profiles);

internal sealed class ProfileRegistryStore : IDisposable
{
    private readonly object gate = new();
    private readonly SqliteConnection connection;
    private bool disposed;

    internal ProfileRegistryStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException(
                "Controller database path is required.",
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
                CREATE TABLE IF NOT EXISTS controller_state (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS profiles (
                    id TEXT PRIMARY KEY,
                    display_name TEXT NOT NULL,
                    role_name TEXT,
                    server_id TEXT,
                    game_uid TEXT UNIQUE,
                    note TEXT NOT NULL DEFAULT '',
                    display_order INTEGER NOT NULL,
                    enabled INTEGER NOT NULL DEFAULT 1,
                    locked_reason TEXT,
                    is_primary INTEGER NOT NULL DEFAULT 0,
                    created_at INTEGER NOT NULL,
                    updated_at INTEGER NOT NULL,
                    last_launched_at INTEGER
                );
                CREATE UNIQUE INDEX IF NOT EXISTS idx_profiles_primary
                ON profiles(is_primary) WHERE is_primary = 1;
                """;
            command.ExecuteNonQuery();
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal void EnsureLocalProfile(
        string profileId,
        string displayName,
        long nowUnixMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException(
                "Profile identity is required.",
                nameof(profileId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException(
                "Profile display name is required.",
                nameof(displayName));

        lock (gate)
        {
            ThrowIfDisposed();
            using SqliteTransaction transaction =
                connection.BeginTransaction();
            using (SqliteCommand insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT OR IGNORE INTO profiles(
                        id, display_name, role_name, server_id, game_uid,
                        note, display_order, enabled, locked_reason,
                        is_primary, created_at, updated_at, last_launched_at)
                    VALUES (
                        $id, $displayName, NULL, NULL, NULL,
                        '', 0, 1, NULL,
                        1, $now, $now, NULL)
                    """;
                insert.Parameters.AddWithValue("$id", profileId);
                insert.Parameters.AddWithValue("$displayName", displayName);
                insert.Parameters.AddWithValue("$now", nowUnixMilliseconds);
                insert.ExecuteNonQuery();
            }

            using (SqliteCommand selected = connection.CreateCommand())
            {
                selected.Transaction = transaction;
                selected.CommandText = """
                    INSERT OR IGNORE INTO controller_state(key, value)
                    VALUES ('selected_profile_id', $profileId)
                    """;
                selected.Parameters.AddWithValue("$profileId", profileId);
                selected.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    internal void EnsureSecondaryProfile(
        string profileId,
        string displayName,
        int displayOrder,
        long nowUnixMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile identity is required.", nameof(profileId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Profile display name is required.", nameof(displayName));
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder));

        lock (gate)
        {
            ThrowIfDisposed();
            using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT OR IGNORE INTO profiles(
                    id, display_name, role_name, server_id, game_uid,
                    note, display_order, enabled, locked_reason,
                    is_primary, created_at, updated_at, last_launched_at)
                VALUES (
                    $id, $displayName, NULL, NULL, NULL,
                    '', $displayOrder, 1, NULL,
                    0, $now, $now, NULL)
                """;
            insert.Parameters.AddWithValue("$id", profileId);
            insert.Parameters.AddWithValue("$displayName", displayName);
            insert.Parameters.AddWithValue("$displayOrder", displayOrder);
            insert.Parameters.AddWithValue("$now", nowUnixMilliseconds);
            insert.ExecuteNonQuery();
        }
    }

    // OWN_DESIGN F-07: local profiles are not original commercial slots.
    // A creation takes the registry lock for its capacity check and insert;
    // concurrent Create calls cannot each claim the last available slot.
    internal void CreateLocalSecondaryProfile(int maxProfiles, long nowUnixMilliseconds)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            List<ProfileRegistryEntry> existing = ReadProfiles();
            if (maxProfiles < 2 || existing.Count >= maxProfiles)
                throw new BridgeCommandException("PROFILE_LIMIT_REACHED", "Local profile capacity has been reached.");

            string profileId = "local-" + Guid.NewGuid().ToString("N");
            long order = existing.Count == 0 ? 0 : existing.Max(profile => profile.DisplayOrder) + 1;
            using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO profiles(id, display_name, role_name, server_id, game_uid,
                    note, display_order, enabled, locked_reason, is_primary, created_at, updated_at)
                VALUES ($id, $name, NULL, NULL, NULL, '', $order, 1, NULL, 0, $now, $now)
                """;
            insert.Parameters.AddWithValue("$id", profileId);
            insert.Parameters.AddWithValue("$name", "Local " + (existing.Count + 1));
            insert.Parameters.AddWithValue("$order", order);
            insert.Parameters.AddWithValue("$now", nowUnixMilliseconds);
            insert.ExecuteNonQuery();
        }
    }

    // Deliberately preserve per-profile on-disk backup/runtime data; removing a
    // registry identity must never delete an installation or recovery journal.
    internal void DeleteLocalSecondaryProfile(string profileId)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            ProfileRegistryEntry? entry = ReadProfiles().FirstOrDefault(
                profile => string.Equals(profile.Id, profileId, StringComparison.Ordinal));
            if (entry is null)
                throw new BridgeCommandException("PROFILE_NOT_FOUND", "PROFILE_NOT_FOUND");
            if (entry.IsPrimary)
                throw new BridgeCommandException("PROFILE_PRIMARY_FIXED", "The primary local profile cannot be deleted.");
            if (string.Equals(ReadSelectedProfileId(), profileId, StringComparison.Ordinal))
                throw new BridgeCommandException("PROFILE_SELECTED", "Select another profile before deleting this one.");
            using SqliteCommand delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM profiles WHERE id = $id";
            delete.Parameters.AddWithValue("$id", profileId);
            if (delete.ExecuteNonQuery() != 1)
                throw new BridgeCommandException("PROFILE_NOT_FOUND", "PROFILE_NOT_FOUND");
        }
    }

    internal ProfileRegistrySnapshot Read(int maxProfiles = 1)
    {
        if (maxProfiles < 1)
            throw new ArgumentOutOfRangeException(nameof(maxProfiles));

        lock (gate)
        {
            ThrowIfDisposed();
            List<ProfileRegistryEntry> profiles = ReadProfiles();
            string selectedProfileId = ReadSelectedProfileId();
            return new ProfileRegistrySnapshot(
                selectedProfileId,
                maxProfiles,
                profiles);
        }
    }

    internal void SelectProfile(string profileId)
    {
        lock (gate)
        {
            ThrowIfDisposed();

            using SqliteCommand check = connection.CreateCommand();
            check.CommandText = """
                SELECT enabled = 1 AND locked_reason IS NULL
                FROM profiles
                WHERE id = $id
                """;
            check.Parameters.AddWithValue("$id", profileId);
            object? selectable = check.ExecuteScalar();
            if (selectable is null)
            {
                throw new BridgeCommandException(
                    "PROFILE_NOT_FOUND",
                    "PROFILE_NOT_FOUND");
            }

            if (Convert.ToInt64(selectable) == 0)
            {
                throw new BridgeCommandException(
                    "PROFILE_LOCKED",
                    "PROFILE_LOCKED");
            }

            using SqliteCommand update = connection.CreateCommand();
            update.CommandText = """
                UPDATE controller_state
                SET value = $profileId
                WHERE key = 'selected_profile_id'
                """;
            update.Parameters.AddWithValue("$profileId", profileId);
            update.ExecuteNonQuery();
        }
    }

    internal void UpdateNote(
        string profileId,
        string note,
        long nowUnixMilliseconds)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE profiles
                SET note = $note, updated_at = $now
                WHERE id = $id
                """;
            command.Parameters.AddWithValue("$note", note);
            command.Parameters.AddWithValue("$now", nowUnixMilliseconds);
            command.Parameters.AddWithValue("$id", profileId);
            if (command.ExecuteNonQuery() != 1)
            {
                throw new BridgeCommandException(
                    "PROFILE_NOT_FOUND",
                    "PROFILE_NOT_FOUND");
            }
        }
    }

    internal void Reorder(
        IReadOnlyList<string> profileIds,
        long nowUnixMilliseconds)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            List<ProfileRegistryEntry> current = ReadProfiles();
            if (profileIds.Count != current.Count ||
                profileIds.Distinct(StringComparer.Ordinal).Count() !=
                    profileIds.Count)
            {
                throw InvalidProfileOrder();
            }

            var requested = profileIds.ToHashSet(StringComparer.Ordinal);
            if (current.Any(profile => !requested.Contains(profile.Id)))
                throw InvalidProfileOrder();

            using SqliteTransaction transaction =
                connection.BeginTransaction();
            for (int index = 0; index < profileIds.Count; index++)
            {
                using SqliteCommand update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE profiles
                    SET display_order = $displayOrder,
                        updated_at = $now
                    WHERE id = $id
                    """;
                update.Parameters.AddWithValue("$displayOrder", index);
                update.Parameters.AddWithValue("$now", nowUnixMilliseconds);
                update.Parameters.AddWithValue("$id", profileIds[index]);
                update.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    private static BridgeCommandException InvalidProfileOrder() =>
        new("INVALID_PROFILE_ORDER", "INVALID_PROFILE_ORDER");

    internal void AssertPrimary(string profileId)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            using SqliteCommand primary = connection.CreateCommand();
            primary.CommandText =
                "SELECT id FROM profiles WHERE is_primary = 1";
            string? primaryId = primary.ExecuteScalar() as string;
            if (string.Equals(
                primaryId,
                profileId,
                StringComparison.Ordinal))
            {
                return;
            }

            using SqliteCommand exists = connection.CreateCommand();
            exists.CommandText =
                "SELECT 1 FROM profiles WHERE id = $id";
            exists.Parameters.AddWithValue("$id", profileId);
            if (exists.ExecuteScalar() is null)
            {
                throw new BridgeCommandException(
                    "PROFILE_NOT_FOUND",
                    "PROFILE_NOT_FOUND");
            }

            throw new BridgeCommandException(
                "PROFILE_PRIMARY_FIXED",
                "PROFILE_PRIMARY_FIXED");
        }
    }

    private List<ProfileRegistryEntry> ReadProfiles()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, display_name, role_name, server_id, game_uid, note,
                   display_order, enabled, locked_reason, is_primary,
                   created_at, updated_at, last_launched_at
            FROM profiles
            ORDER BY display_order, created_at, id
            """;

        using SqliteDataReader reader = command.ExecuteReader();
        var profiles = new List<ProfileRegistryEntry>();
        while (reader.Read())
        {
            profiles.Add(new ProfileRegistryEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetInt64(6),
                reader.GetInt64(7) != 0,
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt64(9) != 0,
                reader.IsDBNull(12) ? null : reader.GetInt64(12)));
        }
        return profiles;
    }

    private string ReadSelectedProfileId()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT value
            FROM controller_state
            WHERE key = 'selected_profile_id'
            """;
        object? value = command.ExecuteScalar();
        return value as string ?? string.Empty;
    }

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
