using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace LWBridge.Map317;

public sealed partial class MapStore : IDisposable
{
    public const int SupportedSchemaVersion = 4;
    public const string DatabaseDirectoryName = "map-data";
    public const string DatabaseFileName = "map-data.db";
    public const int MaxCityExportRows = 200_000;

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS metadata (
          key TEXT PRIMARY KEY, value TEXT NOT NULL, updated_at INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS map_records (
          kind TEXT NOT NULL, server_id INTEGER NOT NULL, record_key TEXT NOT NULL,
          point_index INTEGER, uuid TEXT, name TEXT, alliance_name TEXT,
          level INTEGER, quality INTEGER, power INTEGER, distance REAL,
          shield_end_time INTEGER, updated_at INTEGER NOT NULL, data_json TEXT NOT NULL,
          PRIMARY KEY (kind, server_id, record_key)
        );
        CREATE TABLE IF NOT EXISTS scan_runs (
          id TEXT PRIMARY KEY, server_id INTEGER NOT NULL, selected_types TEXT NOT NULL,
          status TEXT NOT NULL, total_blocks INTEGER NOT NULL,
          completed_blocks INTEGER NOT NULL DEFAULT 0,
          failed_blocks INTEGER NOT NULL DEFAULT 0,
          created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL, error TEXT
        );
        CREATE TABLE IF NOT EXISTS scan_blocks (
          run_id TEXT NOT NULL REFERENCES scan_runs(id) ON DELETE CASCADE,
          block_index INTEGER NOT NULL, payload_json TEXT NOT NULL, status TEXT NOT NULL,
          attempts INTEGER NOT NULL DEFAULT 0, error TEXT, updated_at INTEGER NOT NULL,
          PRIMARY KEY (run_id, block_index)
        );
        CREATE TABLE IF NOT EXISTS scan_records (
          run_id TEXT NOT NULL REFERENCES scan_runs(id) ON DELETE CASCADE,
          kind TEXT NOT NULL, server_id INTEGER NOT NULL, record_key TEXT NOT NULL,
          point_index INTEGER, uuid TEXT, name TEXT, alliance_name TEXT,
          level INTEGER, quality INTEGER, power INTEGER, distance REAL,
          shield_end_time INTEGER, updated_at INTEGER NOT NULL, data_json TEXT NOT NULL,
          PRIMARY KEY (run_id, kind, server_id, record_key)
        );
        CREATE TABLE IF NOT EXISTS player_marks (
          server_id INTEGER NOT NULL, owner_uid TEXT NOT NULL, state TEXT NOT NULL,
          marked_at INTEGER NOT NULL, checked_at INTEGER, player_json TEXT NOT NULL,
          PRIMARY KEY (server_id, owner_uid)
        );
        CREATE TABLE IF NOT EXISTS app_settings (
          key TEXT PRIMARY KEY, value_json TEXT NOT NULL, updated_at INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS treasure_claim_states (
          server_id INTEGER NOT NULL, player_uid TEXT NOT NULL, treasure_uuid TEXT NOT NULL,
          expire_time INTEGER, updated_at INTEGER NOT NULL, state_json TEXT NOT NULL,
          PRIMARY KEY (server_id, player_uid, treasure_uuid)
        );
        CREATE TABLE IF NOT EXISTS dispatch_plunder_jobs (
          server_id INTEGER NOT NULL, task_uuid TEXT NOT NULL, task_json TEXT NOT NULL,
          completion_time INTEGER NOT NULL, plunder_at INTEGER NOT NULL, expire_at INTEGER,
          status TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, last_error TEXT,
          created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL,
          PRIMARY KEY (server_id, task_uuid)
        );
        CREATE TABLE IF NOT EXISTS truck_plunder_jobs (
          server_id INTEGER NOT NULL, train_uuid TEXT NOT NULL, truck_json TEXT NOT NULL,
          execute_at INTEGER NOT NULL, expire_at INTEGER,
          status TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, last_error TEXT,
          created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL,
          PRIMARY KEY (server_id, train_uuid)
        );
        CREATE TABLE IF NOT EXISTS truck_plunder_history (
          job_id TEXT PRIMARY KEY, server_id INTEGER NOT NULL, train_uuid TEXT NOT NULL,
          truck_json TEXT NOT NULL, execute_at INTEGER NOT NULL,
          status TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, last_error TEXT,
          created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS dispatch_assist_jobs (
          task_uuid TEXT PRIMARY KEY, target_server INTEGER NOT NULL,
          task_json TEXT NOT NULL, assist_at INTEGER NOT NULL,
          status TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0,
          last_error TEXT, created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_map_kind_server ON map_records(kind, server_id);
        CREATE INDEX IF NOT EXISTS idx_map_kind_server_quality_power
          ON map_records(kind, server_id, quality, power);
        CREATE INDEX IF NOT EXISTS idx_map_kind_server_level
          ON map_records(kind, server_id, level);
        CREATE INDEX IF NOT EXISTS idx_map_kind_server_updated
          ON map_records(kind, server_id, updated_at);
        CREATE INDEX IF NOT EXISTS idx_map_kind_server_point
          ON map_records(kind, server_id, point_index);
        CREATE INDEX IF NOT EXISTS idx_scan_records_run_kind
          ON scan_records(run_id, kind);
        CREATE INDEX IF NOT EXISTS idx_dispatch_plunder_due
          ON dispatch_plunder_jobs(status, plunder_at);
        CREATE INDEX IF NOT EXISTS idx_truck_plunder_due
          ON truck_plunder_jobs(status, execute_at);
        CREATE INDEX IF NOT EXISTS idx_truck_plunder_history_updated
          ON truck_plunder_history(updated_at);
        CREATE INDEX IF NOT EXISTS idx_dispatch_assist_due
          ON dispatch_assist_jobs(status, assist_at);
        CREATE INDEX IF NOT EXISTS idx_treasure_claim_states_expire
          ON treasure_claim_states(expire_time);
        """;

    private readonly object gate = new();
    private readonly SqliteConnection connection;

    public MapStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Map data database path is required.", nameof(databasePath));
        DatabasePath = Path.GetFullPath(databasePath);
        string? directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw DatabaseError("create map data directory", error);
            }
        }
        try
        {
            connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
            }.ToString());
            connection.Open();
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            throw DatabaseError("open map database", error);
        }
        try
        {
            ConfigureAndCreateSchema();
        }
        catch (BridgeCommandException)
        {
            connection.Dispose();
            throw;
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            connection.Dispose();
            throw DatabaseError("configure map database", error);
        }
    }

    private MapStore(SqliteConnection connection)
    {
        DatabasePath = ":memory:";
        this.connection = connection;
        connection.Open();
        ConfigureAndCreateSchema();
    }

    public string DatabasePath { get; }

    public static string DatabasePathForProfileRoot(string profileRoot) =>
        Path.Combine(Path.GetFullPath(profileRoot), DatabaseDirectoryName, DatabaseFileName);

    public static string DatabasePathForRuntimeRoot(string runtimeRoot, string profileId)
    {
        if (string.IsNullOrWhiteSpace(runtimeRoot))
            throw new ArgumentException("Runtime root is required.", nameof(runtimeRoot));
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        return Path.Combine(Path.GetFullPath(runtimeRoot), "profiles", profileId, DatabaseDirectoryName, DatabaseFileName);
    }

    public static MapStore CreateInMemory() => new(new SqliteConnection("Data Source=:memory:"));

    public IReadOnlyDictionary<string, string> ReadSchemaDefinitions()
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT name,sql FROM sqlite_master
                WHERE type IN ('table','index') AND name NOT LIKE 'sqlite_%' AND sql IS NOT NULL
                ORDER BY name
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            while (reader.Read()) result[reader.GetString(0)] = reader.GetString(1);
            return result;
        }
    }

    public void UpsertRecord(MapRecord record)
    {
        ValidateRecord(record);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO map_records(kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json)
                VALUES ($kind,$server,$key,$point,$uuid,$name,$alliance,$level,$quality,$power,$distance,$shield,$updated,$json)
                ON CONFLICT(kind,server_id,record_key) DO UPDATE SET
                  point_index=excluded.point_index,uuid=excluded.uuid,name=excluded.name,
                  alliance_name=excluded.alliance_name,level=excluded.level,
                  quality=excluded.quality,power=excluded.power,distance=excluded.distance,
                  shield_end_time=excluded.shield_end_time,updated_at=excluded.updated_at,
                  data_json=excluded.data_json
                """;
            AddRecordParameters(command, record);
            command.ExecuteNonQuery();
        }
    }

    public void StageRecord(string runId, MapRecord record)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new BridgeCommandException("INVALID_SCAN", "invalid map scan run");
        ValidateRecord(record);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO scan_records(run_id,kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json)
                VALUES ($run,$kind,$server,$key,$point,$uuid,$name,$alliance,$level,$quality,$power,$distance,$shield,$updated,$json)
                ON CONFLICT(run_id,kind,server_id,record_key) DO UPDATE SET
                  point_index=excluded.point_index,uuid=excluded.uuid,name=excluded.name,
                  alliance_name=excluded.alliance_name,level=excluded.level,
                  quality=excluded.quality,power=excluded.power,distance=excluded.distance,
                  shield_end_time=excluded.shield_end_time,updated_at=excluded.updated_at,
                  data_json=excluded.data_json
                """;
            command.Parameters.AddWithValue("$run", runId);
            AddRecordParameters(command, record);
            command.ExecuteNonQuery();
        }
    }

    public void InsertScanRun(MapScanRun run)
    {
        ValidateServerId(run.ServerId);
        ValidateSelectedTypes(run.SelectedTypes);
        if (string.IsNullOrWhiteSpace(run.Id) || run.TotalBlocks < 0 || run.CompletedBlocks < 0 || run.FailedBlocks < 0)
            throw new BridgeCommandException("INVALID_SCAN", "invalid map scan run");
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO scan_runs(id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error)
                VALUES ($id,$server,$types,$status,$total,$completed,$failed,$created,$updated,$error)
                """;
            command.Parameters.AddWithValue("$id", run.Id);
            command.Parameters.AddWithValue("$server", run.ServerId);
            command.Parameters.AddWithValue("$types", JsonSerializer.Serialize(run.SelectedTypes));
            command.Parameters.AddWithValue("$status", run.Status);
            command.Parameters.AddWithValue("$total", run.TotalBlocks);
            command.Parameters.AddWithValue("$completed", run.CompletedBlocks);
            command.Parameters.AddWithValue("$failed", run.FailedBlocks);
            command.Parameters.AddWithValue("$created", run.CreatedAt);
            command.Parameters.AddWithValue("$updated", run.UpdatedAt);
            command.Parameters.AddWithValue("$error", (object?)run.Error ?? DBNull.Value);
            command.ExecuteNonQuery();
        }
    }

    public MapScanRun? ReadScanRun(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error
                FROM scan_runs WHERE id=$id
                """;
            command.Parameters.AddWithValue("$id", runId);
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? ReadScanRun(reader) : null;
        }
    }

    public MapScanRun? ReadLatestScanRun(int serverId)
    {
        ValidateServerId(serverId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error
                FROM scan_runs WHERE server_id=$server AND status<>'discarded'
                ORDER BY updated_at DESC LIMIT 1
                """;
            command.Parameters.AddWithValue("$server", serverId);
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? ReadScanRun(reader) : null;
        }
    }

    public int ReconcileInterruptedScans(string error, long updatedAt)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Restart interruption error is required.", nameof(error));
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE scan_runs SET status='failed',error=$error,updated_at=$updated
                WHERE status='running'
                """;
            command.Parameters.AddWithValue("$error", error);
            command.Parameters.AddWithValue("$updated", updatedAt);
            return command.ExecuteNonQuery();
        }
    }

    public void UpdateScanProgress(string runId, int completedBlocks, int failedBlocks, string? error, long updatedAt)
    {
        if (string.IsNullOrWhiteSpace(runId) || completedBlocks < 0 || failedBlocks < 0)
            throw new BridgeCommandException("INVALID_SCAN", "invalid map scan run");
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE scan_runs SET completed_blocks=$completed,failed_blocks=$failed,error=$error,updated_at=$updated
                WHERE id=$id AND status='running'
                """;
            command.Parameters.AddWithValue("$completed", completedBlocks);
            command.Parameters.AddWithValue("$failed", failedBlocks);
            command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
            command.Parameters.AddWithValue("$updated", updatedAt);
            command.Parameters.AddWithValue("$id", runId);
            if (command.ExecuteNonQuery() != 1)
                throw new BridgeCommandException("INVALID_SCAN", "map scan is not running");
        }
    }

    public void CancelScan(string runId, string? error, long updatedAt)
    {
        if (string.IsNullOrWhiteSpace(runId)) return;
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            using (SqliteCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE scan_runs SET status='cancelled',error=$error,updated_at=$updated
                    WHERE id=$id AND status IN ('running','paused')
                    """;
                update.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
                update.Parameters.AddWithValue("$updated", updatedAt);
                update.Parameters.AddWithValue("$id", runId);
                update.ExecuteNonQuery();
            }
            using (SqliteCommand clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM scan_records WHERE run_id=$id";
                clear.Parameters.AddWithValue("$id", runId);
                clear.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public void CompleteScan(string runId, long updatedAt)
    {
        lock (gate)
        {
            MapScanRun run = ReadScanRunLocked(runId)
                ?? throw new BridgeCommandException("INVALID_SCAN", "map scan run not found");
            if (!string.Equals(run.Status, "running", StringComparison.Ordinal))
                throw new BridgeCommandException("INVALID_SCAN", "map scan is not running");
            if (run.FailedBlocks > 0)
                throw new BridgeCommandException("INCOMPLETE_SCAN", "direct map scan contains failed batches");
            if (run.CompletedBlocks != run.TotalBlocks)
                throw new BridgeCommandException("INCOMPLETE_SCAN", "direct map scan is incomplete");

            using SqliteTransaction transaction = connection.BeginTransaction();
            foreach (string kind in run.SelectedTypes)
            {
                using (SqliteCommand delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    delete.CommandText = "DELETE FROM map_records WHERE kind=$kind AND server_id=$server";
                    delete.Parameters.AddWithValue("$kind", kind);
                    delete.Parameters.AddWithValue("$server", run.ServerId);
                    delete.ExecuteNonQuery();
                }
                using (SqliteCommand publish = connection.CreateCommand())
                {
                    publish.Transaction = transaction;
                    publish.CommandText = """
                        INSERT INTO map_records(kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json)
                        SELECT kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json
                        FROM scan_records WHERE run_id=$run AND kind=$kind AND server_id=$server
                        """;
                    publish.Parameters.AddWithValue("$run", run.Id);
                    publish.Parameters.AddWithValue("$kind", kind);
                    publish.Parameters.AddWithValue("$server", run.ServerId);
                    publish.ExecuteNonQuery();
                }
            }

            using (SqliteCommand finish = connection.CreateCommand())
            {
                finish.Transaction = transaction;
                finish.CommandText = """
                    UPDATE scan_runs SET status='completed',error=NULL,updated_at=$updated
                    WHERE id=$run AND status='running'
                    """;
                finish.Parameters.AddWithValue("$updated", updatedAt);
                finish.Parameters.AddWithValue("$run", run.Id);
                if (finish.ExecuteNonQuery() != 1)
                    throw new BridgeCommandException("INVALID_SCAN", "map scan is not running");
            }
            using (SqliteCommand staging = connection.CreateCommand())
            {
                staging.Transaction = transaction;
                staging.CommandText = "DELETE FROM scan_records WHERE run_id=$run";
                staging.Parameters.AddWithValue("$run", run.Id);
                staging.ExecuteNonQuery();
            }
            using (SqliteCommand prune = connection.CreateCommand())
            {
                prune.Transaction = transaction;
                prune.CommandText = "DELETE FROM scan_runs WHERE server_id=$server AND id<>$run";
                prune.Parameters.AddWithValue("$server", run.ServerId);
                prune.Parameters.AddWithValue("$run", run.Id);
                prune.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public void FailScan(string runId, string error, long updatedAt)
    {
        if (string.IsNullOrWhiteSpace(error)) error = "map scan failed";
        lock (gate)
        {
            MapScanRun run = ReadScanRunLocked(runId)
                ?? throw new BridgeCommandException("INVALID_SCAN", "map scan run not found");
            using SqliteTransaction transaction = connection.BeginTransaction();
            using (SqliteCommand fail = connection.CreateCommand())
            {
                fail.Transaction = transaction;
                fail.CommandText = """
                    UPDATE scan_runs SET status='failed',error=$error,updated_at=$updated
                    WHERE id=$run AND status='running'
                    """;
                fail.Parameters.AddWithValue("$error", error);
                fail.Parameters.AddWithValue("$updated", updatedAt);
                fail.Parameters.AddWithValue("$run", run.Id);
                if (fail.ExecuteNonQuery() != 1)
                    throw new BridgeCommandException("INVALID_SCAN", "map scan is not running");
            }
            using (SqliteCommand preserve = connection.CreateCommand())
            {
                preserve.Transaction = transaction;
                preserve.CommandText = """
                    INSERT OR REPLACE INTO map_records(kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json)
                    SELECT kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json
                    FROM scan_records WHERE run_id=$run
                    """;
                preserve.Parameters.AddWithValue("$run", run.Id);
                preserve.ExecuteNonQuery();
            }
            using (SqliteCommand clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM scan_records WHERE run_id=$run";
                clear.Parameters.AddWithValue("$run", run.Id);
                clear.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public (int DeletedRuns, int DeletedRecords) ClearServer(int serverId)
    {
        ValidateServerId(serverId);
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            int runs;
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM scan_runs WHERE server_id=$server";
                command.Parameters.AddWithValue("$server", serverId);
                runs = command.ExecuteNonQuery();
            }
            int records;
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM map_records WHERE server_id=$server";
                command.Parameters.AddWithValue("$server", serverId);
                records = command.ExecuteNonQuery();
            }
            transaction.Commit();
            return (runs, records);
        }
    }

    public void SetPlayerMark(MapPlayerMark mark, bool marked)
    {
        ValidateServerId(mark.ServerId);
        if (string.IsNullOrWhiteSpace(mark.OwnerUid))
            throw new BridgeCommandException("INVALID_PLAYER_MARK", "player mark requires serverId and ownerUid");
        ValidateJsonObject(mark.PlayerJson, "player mark row");
        lock (gate)
        {
            if (!marked)
            {
                using SqliteCommand delete = connection.CreateCommand();
                delete.CommandText = "DELETE FROM player_marks WHERE server_id=$server AND owner_uid=$owner";
                delete.Parameters.AddWithValue("$server", mark.ServerId);
                delete.Parameters.AddWithValue("$owner", mark.OwnerUid);
                delete.ExecuteNonQuery();
                return;
            }
            using SqliteCommand upsert = connection.CreateCommand();
            upsert.CommandText = """
                INSERT INTO player_marks(server_id,owner_uid,state,marked_at,checked_at,player_json)
                VALUES ($server,$owner,$state,$marked,$checked,$json)
                ON CONFLICT(server_id,owner_uid) DO UPDATE SET
                  state=excluded.state,marked_at=excluded.marked_at,
                  checked_at=excluded.checked_at,player_json=excluded.player_json
                """;
            upsert.Parameters.AddWithValue("$server", mark.ServerId);
            upsert.Parameters.AddWithValue("$owner", mark.OwnerUid);
            upsert.Parameters.AddWithValue("$state", mark.State);
            upsert.Parameters.AddWithValue("$marked", mark.MarkedAt);
            upsert.Parameters.AddWithValue("$checked", (object?)mark.CheckedAt ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$json", mark.PlayerJson);
            upsert.ExecuteNonQuery();
        }
    }

    public MapPlayerMark? ReadPlayerMark(int serverId, string ownerUid)
    {
        ValidateServerId(serverId);
        if (string.IsNullOrWhiteSpace(ownerUid)) return null;
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT state,marked_at,checked_at,player_json
                FROM player_marks WHERE server_id=$server AND owner_uid=$owner
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$owner", ownerUid);
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read()
                ? new MapPlayerMark(serverId, ownerUid, reader.GetString(0), reader.GetInt64(1),
                    reader.IsDBNull(2) ? null : reader.GetInt64(2), reader.GetString(3))
                : null;
        }
    }

    public string? ReadSetting(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT value_json FROM app_settings WHERE key=$key";
            command.Parameters.AddWithValue("$key", key);
            object? value = command.ExecuteScalar();
            return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    public void WriteSetting(string key, string valueJson, long updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueJson);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO app_settings(key,value_json,updated_at) VALUES ($key,$value,$updated)
                ON CONFLICT(key) DO UPDATE SET value_json=excluded.value_json,updated_at=excluded.updated_at
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", valueJson);
            command.Parameters.AddWithValue("$updated", updatedAt);
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<int> GetServerJumpHistory() => ReadHistory(ReadSetting("serverJumpHistory"));

    public IReadOnlyList<int> SetServerJumpHistory(IEnumerable<int> history, long updatedAt)
    {
        int[] normalized = NormalizeHistory(history);
        WriteSetting("serverJumpHistory", JsonSerializer.Serialize(normalized), updatedAt);
        return normalized;
    }

    public IReadOnlyList<int> ImportServerJumpHistory(IEnumerable<int> history, long updatedAt)
    {
        string? current = ReadSetting("serverJumpHistory");
        if (current is not null) return ReadHistory(current);
        return SetServerJumpHistory(history, updatedAt);
    }

    public void UpsertTreasureClaimState(int serverId, string playerUid, string treasureUuid,
        long? expireTime, long updatedAt, string stateJson, long nowUnixMilliseconds)
    {
        ValidateServerId(serverId);
        if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(treasureUuid))
            throw new BridgeCommandException("INVALID_TREASURE_STATE", "treasure claim state requires serverId, playerUid and treasureUuid");
        ValidateJsonObject(stateJson, "treasure claim state");
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            using (SqliteCommand cleanup = connection.CreateCommand())
            {
                cleanup.Transaction = transaction;
                cleanup.CommandText = "DELETE FROM treasure_claim_states WHERE expire_time IS NOT NULL AND expire_time>0 AND expire_time<=$now";
                cleanup.Parameters.AddWithValue("$now", nowUnixMilliseconds);
                cleanup.ExecuteNonQuery();
            }
            using (SqliteCommand upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = """
                    INSERT INTO treasure_claim_states(server_id,player_uid,treasure_uuid,expire_time,updated_at,state_json)
                    VALUES ($server,$player,$uuid,$expire,$updated,$json)
                    ON CONFLICT(server_id,player_uid,treasure_uuid) DO UPDATE SET
                      expire_time=excluded.expire_time,updated_at=excluded.updated_at,state_json=excluded.state_json
                    """;
                upsert.Parameters.AddWithValue("$server", serverId);
                upsert.Parameters.AddWithValue("$player", playerUid);
                upsert.Parameters.AddWithValue("$uuid", treasureUuid);
                upsert.Parameters.AddWithValue("$expire", (object?)expireTime ?? DBNull.Value);
                upsert.Parameters.AddWithValue("$updated", updatedAt);
                upsert.Parameters.AddWithValue("$json", stateJson);
                upsert.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public void Dispose()
    {
        lock (gate) connection.Dispose();
    }

    private static BridgeCommandException DatabaseError(string operation, Exception error) =>
        new("MAP_DATABASE_ERROR", $"{operation}: {error.Message}", error.Message);

    private void ConfigureAndCreateSchema()
    {
        using (SqliteCommand configure = connection.CreateCommand())
        {
            configure.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            configure.ExecuteNonQuery();
        }
        using SqliteCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        schema.ExecuteNonQuery();
        EnsureSchemaVersion();
    }

    private void EnsureSchemaVersion()
    {
        int version = 0;
        using (SqliteCommand read = connection.CreateCommand())
        {
            read.CommandText = "SELECT value FROM metadata WHERE key = 'schema_version'";
            object? raw = read.ExecuteScalar();
            if (raw is not null and not DBNull)
            {
                string text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out version) || version < 0)
                    throw new BridgeCommandException("MAP_DATABASE_ERROR", "read map schema version", text);
            }
        }
        if (version > SupportedSchemaVersion)
        {
            throw new BridgeCommandException(
                "MAP_SCHEMA_TOO_NEW",
                $"map database schema {version} is newer than supported {SupportedSchemaVersion}");
        }
        if (version < SupportedSchemaVersion)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using SqliteTransaction migration = connection.BeginTransaction();
            if (version < 2)
            {
                using SqliteCommand assist = connection.CreateCommand();
                assist.Transaction = migration;
                assist.CommandText = """
                    UPDATE dispatch_assist_jobs
                    SET status='cancelled',last_error='legacy assist schedule replaced',updated_at=$now
                    WHERE status IN ('scheduled','waiting_connection','running','retry_wait')
                    """;
                assist.Parameters.AddWithValue("$now", now);
                assist.ExecuteNonQuery();
            }
            using (SqliteCommand write = connection.CreateCommand())
            {
                write.Transaction = migration;
                write.CommandText = """
                    INSERT INTO metadata(key,value,updated_at) VALUES ('schema_version',$version,$updated)
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value,updated_at=excluded.updated_at
                    """;
                write.Parameters.AddWithValue("$version", SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture));
                write.Parameters.AddWithValue("$updated", now);
                write.ExecuteNonQuery();
            }
            migration.Commit();
        }
    }

    private MapScanRun? ReadScanRunLocked(string runId, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error
            FROM scan_runs WHERE id=$id
            """;
        command.Parameters.AddWithValue("$id", runId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadScanRun(reader) : null;
    }

    private static MapScanRun ReadScanRun(SqliteDataReader reader)
    {
        string typesJson = reader.GetString(2);
        string[] types;
        try
        {
            types = JsonSerializer.Deserialize<string[]>(typesJson) ?? [];
        }
        catch (JsonException error)
        {
            throw new BridgeCommandException("MAP_DATA_ERROR", "invalid map scan run", error.Message);
        }
        return new MapScanRun(
            reader.GetString(0), reader.GetInt32(1), types, reader.GetString(3),
            reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6),
            reader.GetInt64(7), reader.GetInt64(8), reader.IsDBNull(9) ? null : reader.GetString(9));
    }

    private static int[] NormalizeHistory(IEnumerable<int> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        var seen = new HashSet<int>();
        var result = new List<int>(5);
        foreach (int id in history)
        {
            if (id is < 1 or > 99999 || !seen.Add(id)) continue;
            result.Add(id);
            if (result.Count == 5) break;
        }
        return result.ToArray();
    }

    private static IReadOnlyList<int> ReadHistory(string? valueJson)
    {
        if (valueJson is null) return Array.Empty<int>();
        try
        {
            using JsonDocument document = JsonDocument.Parse(valueJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<int>();
            return NormalizeHistory(document.RootElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out _))
                .Select(item => item.GetInt32()));
        }
        catch (JsonException error)
        {
            throw new BridgeCommandException("INVALID_SETTING", error.Message);
        }
    }

    private static void AddRecordParameters(SqliteCommand command, MapRecord record)
    {
        command.Parameters.AddWithValue("$kind", record.Kind);
        command.Parameters.AddWithValue("$server", record.ServerId);
        command.Parameters.AddWithValue("$key", record.RecordKey);
        command.Parameters.AddWithValue("$point", (object?)record.PointIndex ?? DBNull.Value);
        command.Parameters.AddWithValue("$uuid", (object?)record.Uuid ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", (object?)record.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("$alliance", (object?)record.AllianceName ?? DBNull.Value);
        command.Parameters.AddWithValue("$level", (object?)record.Level ?? DBNull.Value);
        command.Parameters.AddWithValue("$quality", (object?)record.Quality ?? DBNull.Value);
        command.Parameters.AddWithValue("$power", (object?)record.Power ?? DBNull.Value);
        command.Parameters.AddWithValue("$distance", (object?)record.Distance ?? DBNull.Value);
        command.Parameters.AddWithValue("$shield", (object?)record.ShieldEndTime ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", record.UpdatedAt);
        command.Parameters.AddWithValue("$json", record.DataJson);
    }

    private static void ValidateRecord(MapRecord record)
    {
        ValidateKind(record.Kind);
        ValidateServerId(record.ServerId);
        if (string.IsNullOrWhiteSpace(record.RecordKey))
            throw new BridgeCommandException("INVALID_MAP_RECORD", $"missing record key for {record.Kind}");
        ValidateJsonObject(record.DataJson, "map record");
    }

    internal static void ValidateKind(string kind)
    {
        if (!MapKinds.IsValid(kind))
            throw new BridgeCommandException("INVALID_MAP_KIND", $"Unknown map data kind '{kind}'");
    }

    internal static void ValidateServerId(int serverId)
    {
        if (serverId is < 1 or > 99999)
            throw new BridgeCommandException("INVALID_SERVER_ID", "serverId must be an integer from 1 through 99999");
    }

    internal static void ValidateSelectedTypes(IReadOnlyList<string> selectedTypes)
    {
        if (selectedTypes.Count == 0 || selectedTypes.Any(type => !MapKinds.IsValid(type)))
            throw new BridgeCommandException("INVALID_SCAN_TYPES", "no valid map scan types selected");
    }

    internal static void ValidateJsonObject(string json, string description)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new BridgeCommandException("INVALID_MAP_DATA", $"{description} must be an object");
        }
        catch (JsonException error)
        {
            throw new BridgeCommandException("INVALID_MAP_DATA", $"{description} must contain valid JSON", error.Message);
        }
    }
}
