using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop;

internal sealed record MapStoredRecord(
    string Kind,
    int ServerId,
    string RecordKey,
    int? PointIndex,
    string? Uuid,
    string? Name,
    string? AllianceName,
    int? Level,
    int? Quality,
    long? Power,
    double? Distance,
    long? ShieldEndTime,
    long UpdatedAt,
    string DataJson);

internal sealed record MapPlayerMark(
    int ServerId,
    string OwnerUid,
    string State,
    long MarkedAt,
    long? CheckedAt,
    string PlayerJson);

internal sealed record MapScanRunSeed(
    string Id,
    int ServerId,
    string SelectedTypesJson,
    string Status,
    int TotalBlocks,
    int CompletedBlocks,
    int FailedBlocks,
    long CreatedAt,
    long UpdatedAt,
    string? Error,
    long WorldId = 0,
    long TileWidth = 0,
    long TileHeight = 0,
    string ScanMode = "",
    int RequestedConcurrency = 0,
    string? LaunchSessionId = null,
    int? PlayerTileX = null,
    int? PlayerTileY = null,
    int MaxAttemptsPerBlock = 0);

internal sealed record MapClearResult(int ServerId, int DeletedRuns, int DeletedRecords);

internal sealed record MapSearchResult(IReadOnlyList<JsonElement> Rows, int Total);

internal sealed partial class MapDataStore : IDisposable
{
    internal const int MaxCityExportRows = 200_000;

    // R8-012 narrows Manual Scan to the original eight kinds. Keep the rebuild-only
    // Zombie Boss storage compatibility internal until the separate map_search checkpoint.
    private static readonly HashSet<string> AllowedKinds =
        new(MapScanContract.RecoveredDefaultTypes.Append("zombie_boss"), StringComparer.Ordinal);

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
          created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL, error TEXT,
          world_id INTEGER NOT NULL DEFAULT 0,
          tile_width INTEGER NOT NULL DEFAULT 0,
          tile_height INTEGER NOT NULL DEFAULT 0,
          scan_mode TEXT NOT NULL DEFAULT '',
          requested_concurrency INTEGER NOT NULL DEFAULT 0,
          launch_session_id TEXT,
          player_tile_x INTEGER,
          player_tile_y INTEGER,
          max_attempts_per_block INTEGER NOT NULL DEFAULT 0
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

    public MapDataStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Map data database path is required.", nameof(databasePath));

        DatabasePath = Path.GetFullPath(databasePath);
        string? directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
        }.ToString());
        connection.Open();
        ConfigureAndCreateSchema();
    }

    private MapDataStore(SqliteConnection connection)
    {
        DatabasePath = ":memory:";
        this.connection = connection;
        connection.Open();
        ConfigureAndCreateSchema();
    }

    public string DatabasePath { get; }

    public static MapDataStore CreateInMemory() => new(new SqliteConnection("Data Source=:memory:"));

    public void UpsertRecord(MapStoredRecord record)
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

    public MapStoredRecord? GetRecord(string kind, int serverId, string recordKey)
    {
        ValidateKind(kind);
        ValidateServerId(serverId);
        if (string.IsNullOrWhiteSpace(recordKey)) throw new BridgeCommandException("INVALID_MAP_RECORD", "recordKey is required.");

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT kind,server_id,record_key,point_index,uuid,name,alliance_name,
                       level,quality,power,distance,shield_end_time,updated_at,data_json
                FROM map_records WHERE kind=$kind AND server_id=$server AND record_key=$key
                """;
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$key", recordKey);
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? ReadRecord(reader) : null;
        }
    }

    public void InsertScanRun(MapScanRunSeed run)
    {
        if (string.IsNullOrWhiteSpace(run.Id)) throw new BridgeCommandException("INVALID_SCAN_RUN", "scan run id is required.");
        ValidateServerId(run.ServerId);
        ValidateJsonArray(run.SelectedTypesJson, "selectedTypes");
        if (string.IsNullOrWhiteSpace(run.Status)) throw new BridgeCommandException("INVALID_SCAN_RUN", "scan run status is required.");
        if (run.TotalBlocks < 0 || run.CompletedBlocks < 0 || run.FailedBlocks < 0)
            throw new BridgeCommandException("INVALID_SCAN_RUN", "scan run block counts cannot be negative.");

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO scan_runs(
                  id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,
                  created_at,updated_at,error,world_id,tile_width,tile_height,scan_mode,
                  requested_concurrency,launch_session_id,player_tile_x,player_tile_y,max_attempts_per_block)
                VALUES (
                  $id,$server,$types,$status,$total,$completed,$failed,
                  $created,$updated,$error,$world,$width,$height,$mode,$concurrency,$session,
                  $playerX,$playerY,$maxAttempts)
                """;
            command.Parameters.AddWithValue("$id", run.Id);
            command.Parameters.AddWithValue("$server", run.ServerId);
            command.Parameters.AddWithValue("$types", run.SelectedTypesJson);
            command.Parameters.AddWithValue("$status", run.Status);
            command.Parameters.AddWithValue("$total", run.TotalBlocks);
            command.Parameters.AddWithValue("$completed", run.CompletedBlocks);
            command.Parameters.AddWithValue("$failed", run.FailedBlocks);
            command.Parameters.AddWithValue("$created", run.CreatedAt);
            command.Parameters.AddWithValue("$updated", run.UpdatedAt);
            command.Parameters.AddWithValue("$error", (object?)run.Error ?? DBNull.Value);
            command.Parameters.AddWithValue("$world", run.WorldId);
            command.Parameters.AddWithValue("$width", run.TileWidth);
            command.Parameters.AddWithValue("$height", run.TileHeight);
            command.Parameters.AddWithValue("$mode", run.ScanMode);
            command.Parameters.AddWithValue("$concurrency", run.RequestedConcurrency);
            command.Parameters.AddWithValue("$session", (object?)run.LaunchSessionId ?? DBNull.Value);
            command.Parameters.AddWithValue("$playerX", (object?)run.PlayerTileX ?? DBNull.Value);
            command.Parameters.AddWithValue("$playerY", (object?)run.PlayerTileY ?? DBNull.Value);
            command.Parameters.AddWithValue("$maxAttempts", run.MaxAttemptsPerBlock);
            command.ExecuteNonQuery();
        }
    }

    public MapClearResult ClearServer(int serverId)
    {
        ValidateServerId(serverId);
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            int deletedRuns;
            using (SqliteCommand runs = connection.CreateCommand())
            {
                runs.Transaction = transaction;
                runs.CommandText = "DELETE FROM scan_runs WHERE server_id=$server";
                runs.Parameters.AddWithValue("$server", serverId);
                deletedRuns = runs.ExecuteNonQuery();
            }

            int deletedRecords;
            using (SqliteCommand records = connection.CreateCommand())
            {
                records.Transaction = transaction;
                records.CommandText = "DELETE FROM map_records WHERE server_id=$server";
                records.Parameters.AddWithValue("$server", serverId);
                deletedRecords = records.ExecuteNonQuery();
            }

            transaction.Commit();
            return new MapClearResult(serverId, deletedRuns, deletedRecords);
        }
    }

    public void ClearAllScanData()
    {
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            // Scan blocks/records cascade from scan_runs. Player marks, app settings,
            // and durable non-scan settings are intentionally preserved.
            using (SqliteCommand states = connection.CreateCommand())
            {
                states.Transaction = transaction;
                states.CommandText = "DELETE FROM treasure_claim_states";
                states.ExecuteNonQuery();
            }
            using (SqliteCommand records = connection.CreateCommand())
            {
                records.Transaction = transaction;
                records.CommandText = "DELETE FROM map_records";
                records.ExecuteNonQuery();
            }
            using (SqliteCommand runs = connection.CreateCommand())
            {
                runs.Transaction = transaction;
                runs.CommandText = "DELETE FROM scan_runs";
                runs.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

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
        EnsureScanRunIdentityColumns();
    }

    private void EnsureScanRunIdentityColumns()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (SqliteCommand inspect = connection.CreateCommand())
        {
            inspect.CommandText = "PRAGMA table_info(scan_runs)";
            using SqliteDataReader reader = inspect.ExecuteReader();
            while (reader.Read()) existing.Add(reader.GetString(1));
        }

        (string Name, string Definition)[] required =
        [
            ("world_id", "INTEGER NOT NULL DEFAULT 0"),
            ("tile_width", "INTEGER NOT NULL DEFAULT 0"),
            ("tile_height", "INTEGER NOT NULL DEFAULT 0"),
            ("scan_mode", "TEXT NOT NULL DEFAULT ''"),
            ("requested_concurrency", "INTEGER NOT NULL DEFAULT 0"),
            ("launch_session_id", "TEXT"),
            ("player_tile_x", "INTEGER"),
            ("player_tile_y", "INTEGER"),
            ("max_attempts_per_block", "INTEGER NOT NULL DEFAULT 0"),
        ];
        foreach ((string name, string definition) in required)
        {
            if (existing.Contains(name)) continue;
            using SqliteCommand alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE scan_runs ADD COLUMN {name} {definition}";
            alter.ExecuteNonQuery();
        }
    }

    private static void AddRecordParameters(SqliteCommand command, MapStoredRecord record)
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

    private static MapStoredRecord ReadRecord(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetInt32(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetInt32(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetInt32(7),
        reader.IsDBNull(8) ? null : reader.GetInt32(8),
        reader.IsDBNull(9) ? null : reader.GetInt64(9),
        reader.IsDBNull(10) ? null : reader.GetDouble(10),
        reader.IsDBNull(11) ? null : reader.GetInt64(11),
        reader.GetInt64(12),
        reader.GetString(13));

    private static JsonElement ReadSearchRow(
        string dataJson,
        int serverId,
        bool? marked,
        string? treasureStateJson = null)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(dataJson);
            if (node is not JsonObject row)
                throw new BridgeCommandException("MAP_INDEX_CORRUPT", "Stored map row is not a JSON object.");

            // RECOVERED LWB-R7-068: Treasure page SQL selects state.state_json from
            // treasure_claim_states and the frontend overlays state over the stored
            // row by uuid. Apply the same state-over-row precedence here.
            if (!string.IsNullOrEmpty(treasureStateJson))
            {
                JsonNode? stateNode = JsonNode.Parse(treasureStateJson);
                if (stateNode is not JsonObject state)
                    throw new BridgeCommandException(
                        "MAP_INDEX_CORRUPT",
                        "Stored treasure claim state is not a JSON object.");
                foreach ((string key, JsonNode? value) in state)
                    row[key] = value?.DeepClone();
            }

            // The indexed server scope is authoritative. The recovered Map Data
            // frontend expects every returned row to carry serverId for stale-row
            // guards and row actions, while source payload JSON is not required to
            // duplicate the enclosing store key.
            row["serverId"] = serverId;
            if (marked.HasValue) row["marked"] = marked.Value;
            using JsonDocument document = JsonDocument.Parse(row.ToJsonString());
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new BridgeCommandException("MAP_INDEX_CORRUPT", "Stored map row contains invalid JSON.", ex.Message);
        }
    }

    private static void ValidateRecord(MapStoredRecord record)
    {
        ValidateKind(record.Kind);
        ValidateServerId(record.ServerId);
        if (string.IsNullOrWhiteSpace(record.RecordKey))
            throw new BridgeCommandException("INVALID_MAP_RECORD", $"missing record key for {record.Kind}.");
        ValidateJsonObject(record.DataJson, "map record");
    }

    private static void ValidateKind(string kind)
    {
        if (!AllowedKinds.Contains(kind))
            throw new BridgeCommandException("INVALID_MAP_KIND", $"Unknown map data kind '{kind}'.");
    }

    private static void ValidateServerId(int serverId)
    {
        if (serverId < 1 || serverId > 99999)
            throw new BridgeCommandException("INVALID_SERVER_ID", "serverId must be an integer from 1 through 99999.");
    }

    private static void ValidateJsonObject(string json, string description)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new BridgeCommandException("INVALID_MAP_DATA", $"{description} must be an object.");
        }
        catch (JsonException ex)
        {
            throw new BridgeCommandException("INVALID_MAP_DATA", $"{description} must contain valid JSON.", ex.Message);
        }
    }

    private static void ValidateJsonArray(string json, string description)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new BridgeCommandException("INVALID_SCAN_RUN", $"{description} must be an array.");
        }
        catch (JsonException ex)
        {
            throw new BridgeCommandException("INVALID_SCAN_RUN", $"{description} must contain valid JSON.", ex.Message);
        }
    }

    public void Dispose()
    {
        lock (gate) connection.Dispose();
    }
}
