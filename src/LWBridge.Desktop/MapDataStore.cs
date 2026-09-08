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
    string? Error);

internal sealed record MapClearResult(int ServerId, int DeletedRuns, int DeletedRecords);

internal sealed record MapSearchResult(IReadOnlyList<JsonElement> Rows, int Total);

internal sealed class MapDataStore : IDisposable
{
    private static readonly HashSet<string> AllowedKinds = new(MapScanContract.AllTypes, StringComparer.Ordinal);

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

    public int CountRecords(string kind, int serverId)
    {
        ValidateKind(kind);
        ValidateServerId(serverId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM map_records WHERE kind=$kind AND server_id=$server";
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$server", serverId);
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public MapSearchResult SearchIndexed(MapDataQueryOptions options)
    {
        ValidateKind(options.Kind);
        ValidateServerId(options.ServerId);
        MapDataQueryContract.RequireRecoveredIndexedSearch(options);

        string direction = options.Sorts[0].SortOrder == "asc" ? "ASC" : "DESC";
        long offset = checked(((long)options.Page - 1L) * options.PageSize);
        bool city = string.Equals(options.Kind, "city", StringComparison.Ordinal);

        lock (gate)
        {
            string join = city
                ? " LEFT JOIN player_marks mark ON mark.server_id=page.server_id AND mark.owner_uid=CAST(json_extract(page.data_json,'$.ownerUid') AS TEXT)"
                : string.Empty;
            var predicates = new List<string>
            {
                "page.kind=$kind",
                "page.server_id=$server",
            };
            // LWB-R6-005/006: verified original predicate shapes; unresolved filters remain gated by MapDataQueryContract.
            if (city && options.MarkedOnly)
                predicates.Add("mark.owner_uid IS NOT NULL");
            if (options.Alliance is not null)
                predicates.Add("page.alliance_name = $alliance");
            if (options.WithoutAlliance)
                predicates.Add("(page.alliance_name IS NULL OR page.alliance_name = '')");
            if (options.ResourceNameKey is not null)
                predicates.Add("CAST(json_extract(page.data_json,'$.resourceNameKey') AS TEXT) = $resourceNameKey");
            if (options.MonsterNameKey is not null)
                predicates.Add("CAST(json_extract(page.data_json,'$.monsterNameKey') AS TEXT) = $monsterNameKey");
            if (options.ItemKey is not null)
                predicates.Add("EXISTS (SELECT 1 FROM json_each(page.data_json,'$.currentGoods') AS good WHERE CAST(json_extract(good.value,'$.key') AS TEXT) = $itemKey)");
            if (options.SpecialOnly)
                predicates.Add("CAST(json_extract(page.data_json,'$.isSpecial') AS INTEGER) = 1");
            if (options.ReindeerOnly)
                predicates.Add("CAST(json_extract(page.data_json,'$.isSpecialURQuality') AS INTEGER) = 1");
            string where = string.Join(" AND ", predicates);

            int total;
            using (SqliteCommand count = connection.CreateCommand())
            {
                count.CommandText = $"SELECT COUNT(*) FROM map_records page{join} WHERE {where}";
                AddSearchParameters(count, options);
                total = Convert.ToInt32(count.ExecuteScalar());
            }

            using SqliteCommand page = connection.CreateCommand();
            page.CommandText = city
                ? $"SELECT page.data_json, CASE WHEN mark.owner_uid IS NULL THEN 0 ELSE 1 END FROM map_records page{join} WHERE {where} ORDER BY page.updated_at {direction}, page.record_key ASC LIMIT $limit OFFSET $offset"
                : $"SELECT page.data_json FROM map_records page WHERE {where} ORDER BY page.updated_at {direction}, page.record_key ASC LIMIT $limit OFFSET $offset";
            AddSearchParameters(page, options);
            page.Parameters.AddWithValue("$limit", options.PageSize);
            page.Parameters.AddWithValue("$offset", offset);

            var rows = new List<JsonElement>();
            using SqliteDataReader reader = page.ExecuteReader();
            while (reader.Read())
                rows.Add(ReadSearchRow(reader.GetString(0), city ? reader.GetInt32(1) != 0 : null));
            return new MapSearchResult(rows, total);
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
                INSERT INTO scan_runs(id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error)
                VALUES ($id,$server,$types,$status,$total,$completed,$failed,$created,$updated,$error)
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
            command.ExecuteNonQuery();
        }
    }

    public int CountScanRuns(int serverId)
    {
        ValidateServerId(serverId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM scan_runs WHERE server_id=$server";
            command.Parameters.AddWithValue("$server", serverId);
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public void UpsertPlayerMark(MapPlayerMark mark)
    {
        ValidateServerId(mark.ServerId);
        if (string.IsNullOrWhiteSpace(mark.OwnerUid))
            throw new BridgeCommandException("INVALID_PLAYER_MARK", "player mark requires serverId and ownerUid.");
        if (string.IsNullOrWhiteSpace(mark.State))
            throw new BridgeCommandException("INVALID_PLAYER_MARK", "player mark state is required.");
        ValidateJsonObject(mark.PlayerJson, "player mark row");

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO player_marks(server_id,owner_uid,state,marked_at,checked_at,player_json)
                VALUES ($server,$owner,$state,$marked,$checked,$json)
                ON CONFLICT(server_id,owner_uid) DO UPDATE SET
                  state=excluded.state,marked_at=excluded.marked_at,
                  checked_at=excluded.checked_at,player_json=excluded.player_json
                """;
            command.Parameters.AddWithValue("$server", mark.ServerId);
            command.Parameters.AddWithValue("$owner", mark.OwnerUid);
            command.Parameters.AddWithValue("$state", mark.State);
            command.Parameters.AddWithValue("$marked", mark.MarkedAt);
            command.Parameters.AddWithValue("$checked", (object?)mark.CheckedAt ?? DBNull.Value);
            command.Parameters.AddWithValue("$json", mark.PlayerJson);
            command.ExecuteNonQuery();
        }
    }

    public MapPlayerMark? GetPlayerMark(int serverId, string ownerUid)
    {
        ValidateServerId(serverId);
        if (string.IsNullOrWhiteSpace(ownerUid))
            throw new BridgeCommandException("INVALID_PLAYER_MARK", "player mark requires serverId and ownerUid.");

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
            if (!reader.Read()) return null;
            return new MapPlayerMark(
                serverId,
                ownerUid,
                reader.GetString(0),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetInt64(2),
                reader.GetString(3));
        }
    }

    public bool DeletePlayerMark(int serverId, string ownerUid)
    {
        ValidateServerId(serverId);
        if (string.IsNullOrWhiteSpace(ownerUid))
            throw new BridgeCommandException("INVALID_PLAYER_MARK", "player mark requires serverId and ownerUid.");

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM player_marks WHERE server_id=$server AND owner_uid=$owner";
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$owner", ownerUid);
            return command.ExecuteNonQuery() > 0;
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

    private static void AddSearchParameters(SqliteCommand command, MapDataQueryOptions options)
    {
        command.Parameters.AddWithValue("$kind", options.Kind);
        command.Parameters.AddWithValue("$server", options.ServerId);
        if (options.Alliance is not null)
            command.Parameters.AddWithValue("$alliance", options.Alliance);
        if (options.ResourceNameKey is not null)
            command.Parameters.AddWithValue("$resourceNameKey", options.ResourceNameKey);
        if (options.MonsterNameKey is not null)
            command.Parameters.AddWithValue("$monsterNameKey", options.MonsterNameKey);
        if (options.ItemKey is not null)
            command.Parameters.AddWithValue("$itemKey", options.ItemKey);
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

    private static JsonElement ReadSearchRow(string dataJson, bool? marked)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(dataJson);
            if (node is not JsonObject row)
                throw new BridgeCommandException("MAP_INDEX_CORRUPT", "Stored map row is not a JSON object.");
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
