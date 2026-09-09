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

internal sealed record MapPersistedAllianceOption(string? Name, int Count);

internal sealed record MapPersistedNameOption(string Kind, string Key, int Count);

internal sealed record MapPersistedRewardItemOption(string Kind, string Key, string Name, string? IconPath);

internal sealed record MapPersistedTreasureTypeOption(
    int SuppliesType,
    int TreasureType,
    string TreasureNameKey,
    int Count);

internal sealed record MapPersistedScanProgress(
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

internal sealed record MapPersistedOptionAggregates(
    IReadOnlyList<MapPersistedAllianceOption> Alliances,
    IReadOnlyList<MapPersistedNameOption> Names,
    IReadOnlyList<int> DispatchLevels,
    IReadOnlyList<MapPersistedTreasureTypeOption> TreasureTypes,
    IReadOnlyList<MapPersistedRewardItemOption> RewardItems,
    IReadOnlyDictionary<string, int> Counts,
    int NoAllianceCount,
    MapPersistedScanProgress? ScanProgress);

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

    internal MapPersistedOptionAggregates ReadPersistedOptionAggregatesAtForTest(
        int serverId,
        long nowUnixMilliseconds)
    {
        ValidateServerId(serverId);

        lock (gate)
        {
            // IMPLEMENTATION POLICY LWB-R6-015: this helper deliberately evaluates
            // only the known persisted map_records source and one server scope. It is
            // not the public map_data_options source/run selector, which remains
            // UNKNOWN/BLOCKED. Keep one read snapshot so the offline aggregate set is
            // internally coherent while its recovered SQL families are validated.
            using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);

            var alliances = new List<MapPersistedAllianceOption>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = snapshot;
                command.CommandText = """
                    SELECT alliance_name,COUNT(*) FROM map_records
                    WHERE kind='city' AND server_id=$server
                    GROUP BY alliance_name ORDER BY alliance_name COLLATE NOCASE,alliance_name
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                    alliances.Add(new MapPersistedAllianceOption(
                        reader.IsDBNull(0) ? null : reader.GetString(0),
                        reader.GetInt32(1)));
            }

            var names = new List<MapPersistedNameOption>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = snapshot;
                command.CommandText = """
                    SELECT kind,
                      CASE kind
                        WHEN 'resource' THEN CAST(json_extract(data_json,'$.resourceNameKey') AS TEXT)
                        ELSE CAST(json_extract(data_json,'$.monsterNameKey') AS TEXT)
                      END AS name_key,
                      COUNT(*)
                    FROM map_records
                    WHERE server_id=$server AND kind IN ('resource','monster')
                    GROUP BY kind,name_key
                    HAVING name_key IS NOT NULL AND name_key<>''
                    ORDER BY kind,name_key COLLATE NOCASE
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                    names.Add(new MapPersistedNameOption(
                        reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
            }

            var dispatchLevels = new List<int>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = snapshot;
                command.CommandText = """
                    SELECT DISTINCT CAST(level AS INTEGER) FROM map_records
                    WHERE server_id=$server AND kind='dispatch' AND level>=1 ORDER BY 1
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read()) dispatchLevels.Add(reader.GetInt32(0));
            }

            var treasureTypes = new List<MapPersistedTreasureTypeOption>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = snapshot;
                command.CommandText = """
                    WITH treasure_options AS (
                      SELECT
                        CASE WHEN COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0)>0
                          THEN CAST(json_extract(data_json,'$.suppliesType') AS INTEGER) ELSE 0 END AS supplies_type,
                        CASE WHEN COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0)>0
                          THEN 0 ELSE COALESCE(CAST(json_extract(data_json,'$.treasureType') AS INTEGER),0) END AS treasure_type,
                        COALESCE(CAST(json_extract(data_json,'$.treasureNameKey') AS TEXT),'') AS name_key
                      FROM map_records WHERE server_id=$server AND kind='treasure'
                    )
                    SELECT supplies_type,treasure_type,MAX(name_key),COUNT(*)
                    FROM treasure_options
                    WHERE supplies_type>0 OR treasure_type>0
                    GROUP BY supplies_type,treasure_type
                    ORDER BY CASE WHEN supplies_type>0 THEN 1 ELSE 0 END,treasure_type,supplies_type
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                    treasureTypes.Add(new MapPersistedTreasureTypeOption(
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetInt32(3)));
            }

            var rewardItems = new List<MapPersistedRewardItemOption>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = snapshot;
                command.CommandText = """
                    SELECT map_records.kind,
                      CAST(json_extract(good.value,'$.key') AS TEXT),
                      CAST(json_extract(good.value,'$.name') AS TEXT),
                      CAST(json_extract(good.value,'$.iconPath') AS TEXT)
                    FROM map_records,json_each(map_records.data_json,'$.currentGoods') AS good
                    WHERE map_records.server_id=$server AND map_records.kind IN ('truck','railway')
                      AND (json_extract(map_records.data_json,'$.arriveTs') IS NULL
                        OR CAST(json_extract(map_records.data_json,'$.arriveTs') AS INTEGER)>$nowUnixMs)
                      AND json_extract(good.value,'$.key') IS NOT NULL
                      AND json_extract(good.value,'$.name') IS NOT NULL
                    GROUP BY map_records.kind,2,3,4 ORDER BY 3 COLLATE NOCASE,2
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                command.Parameters.AddWithValue("$nowUnixMs", nowUnixMilliseconds);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                    rewardItems.Add(new MapPersistedRewardItemOption(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3)));
            }

            // IMPLEMENTATION POLICY LWB-R6-021: R6-016 proves the exact eight
            // frontend count keys but not the original public producer/source branch.
            // This test-only persisted-source kernel counts map_records in the same
            // snapshot as the already recovered option families; it must not be exposed
            // as map_data_options until the native source/run selector is recovered.
            var counts = MapScanContract.AllTypes.ToDictionary(kind => kind, _ => 0, StringComparer.Ordinal);
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = snapshot;
                command.CommandText = """
                    SELECT kind,COUNT(*) FROM map_records
                    WHERE server_id=$server GROUP BY kind
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string kind = reader.GetString(0);
                    if (counts.ContainsKey(kind)) counts[kind] = reader.GetInt32(1);
                }
            }

            int noAllianceCount;
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = snapshot;
                command.CommandText = """
                    SELECT COUNT(*) FROM map_records
                    WHERE server_id=$server AND kind='city'
                      AND (alliance_name IS NULL OR alliance_name='')
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                noAllianceCount = Convert.ToInt32(command.ExecuteScalar());
            }

            MapPersistedScanProgress? scanProgress = null;
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = snapshot;
                command.CommandText = """
                    SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error
                    FROM scan_runs WHERE server_id=$server AND status<>'discarded' ORDER BY updated_at DESC LIMIT 1
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using SqliteDataReader reader = command.ExecuteReader();
                if (reader.Read())
                    scanProgress = new MapPersistedScanProgress(
                        reader.GetString(0),
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetInt32(4),
                        reader.GetInt32(5),
                        reader.GetInt32(6),
                        reader.GetInt64(7),
                        reader.GetInt64(8),
                        reader.IsDBNull(9) ? null : reader.GetString(9));
            }

            snapshot.Commit();
            return new MapPersistedOptionAggregates(
                alliances, names, dispatchLevels, treasureTypes, rewardItems,
                counts, noAllianceCount, scanProgress);
        }
    }

    public MapSearchResult SearchIndexed(MapDataQueryOptions options) =>
        SearchIndexedCore(options, RecoveredWallClock.UnixTimeMilliseconds(), afterCountObserved: null);

    internal MapSearchResult SearchIndexedForSnapshotTest(MapDataQueryOptions options, Action afterCountObserved) =>
        SearchIndexedCore(
            options,
            RecoveredWallClock.UnixTimeMilliseconds(),
            afterCountObserved ?? throw new ArgumentNullException(nameof(afterCountObserved)));

    internal MapSearchResult SearchIndexedAtForTest(MapDataQueryOptions options, long nowUnixMilliseconds) =>
        SearchIndexedCore(options, nowUnixMilliseconds, afterCountObserved: null);

    private MapSearchResult SearchIndexedCore(MapDataQueryOptions options, long nowUnixMilliseconds, Action? afterCountObserved)
    {
        ValidateKind(options.Kind);
        ValidateServerId(options.ServerId);
        MapDataQueryContract.RequireRecoveredIndexedSearch(options);

        string direction = options.Sorts[0].SortOrder == "asc" ? "ASC" : "DESC";
        long offset = checked(((long)options.Page - 1L) * options.PageSize);
        bool city = string.Equals(options.Kind, "city", StringComparison.Ordinal);

        lock (gate)
        {
            // IMPLEMENTATION POLICY LWB-R6-008: count and page must describe one SQLite
            // read snapshot even when another process/connection publishes new rows.
            using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
            string join = city
                ? " LEFT JOIN player_marks mark ON mark.server_id=page.server_id AND mark.owner_uid=CAST(json_extract(page.data_json,'$.ownerUid') AS TEXT)"
                : string.Empty;
            var predicates = new List<string>
            {
                "page.kind=$kind",
                "page.server_id=$server",
            };
            // LWB-R6-005/006/007/013/014: verified original predicate shapes; unresolved filters remain gated by MapDataQueryContract.
            if (options.Kind is "truck" or "railway")
                predicates.Add("(json_extract(page.data_json,'$.arriveTs') IS NULL OR CAST(json_extract(page.data_json,'$.arriveTs') AS INTEGER) > $nowUnixMs)");
            if (city && options.MarkedOnly)
                predicates.Add("mark.owner_uid IS NOT NULL");
            if (options.Keyword is not null)
                predicates.Add("(page.name LIKE $keywordName ESCAPE '\\' COLLATE NOCASE OR page.alliance_name LIKE $keywordAlliance ESCAPE '\\' COLLATE NOCASE OR page.uuid LIKE $keywordUuid ESCAPE '\\' COLLATE NOCASE OR page.data_json LIKE $keywordJson ESCAPE '\\' COLLATE NOCASE)");
            if (options.Alliance is not null)
                predicates.Add("page.alliance_name = $alliance");
            if (options.WithoutAlliance)
                predicates.Add("(page.alliance_name IS NULL OR page.alliance_name = '')");
            if (options.ResourceNameKey is not null)
                predicates.Add("CAST(json_extract(page.data_json,'$.resourceNameKey') AS TEXT) = $resourceNameKey");
            if (options.MonsterNameKey is not null)
                predicates.Add("CAST(json_extract(page.data_json,'$.monsterNameKey') AS TEXT) = $monsterNameKey");
            if (options.SuppliesType > 0)
                predicates.Add("CAST(json_extract(page.data_json,'$.suppliesType') AS INTEGER) = $suppliesType");
            if (options.TreasureType > 0)
            {
                predicates.Add("CAST(json_extract(page.data_json,'$.treasureType') AS INTEGER) = $treasureType");
                predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.suppliesType') AS INTEGER),0) = 0");
            }
            if (options.Quality is "n" or "r" or "sr" or "ssr")
                predicates.Add("page.quality = $quality");
            else if (options.Quality == "ur")
                predicates.Add("page.quality >= 5");
            // LWB-R6-013: the original applies this extra guard only to ordinary truck UR.
            if (options.Kind == "truck" && options.Quality == "ur")
                predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.isSpecialURQuality') AS INTEGER),0) = 0");
            if (options.ItemKey is not null)
                predicates.Add("EXISTS (SELECT 1 FROM json_each(page.data_json,'$.currentGoods') AS good WHERE CAST(json_extract(good.value,'$.key') AS TEXT) = $itemKey)");
            if (options.CompletionStatus == "pending")
                predicates.Add("(CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER) IS NULL OR CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER) <= 0 OR CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER) > $nowUnixMs)");
            else if (options.CompletionStatus == "completed")
                predicates.Add("CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER) > 0 AND CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER) <= $nowUnixMs");
            if (options.PlunderableOnly && options.Kind is "truck" or "railway")
            {
                predicates.Add("json_extract(page.data_json,'$.arriveTs') IS NOT NULL");
                predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.remainingLootCount') AS INTEGER),MAX(COALESCE(CAST(json_extract(page.data_json,'$.maxLootCount') AS INTEGER),0)-COALESCE(CAST(json_extract(page.data_json,'$.robTimes') AS INTEGER),0),0)) > 0");
            }
            else if (options.PlunderableOnly && options.Kind == "dispatch")
            {
                predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER),0) > 0");
                predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.plunderAt') AS INTEGER),CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER),0) > 0");
                predicates.Add("(COALESCE(CAST(json_extract(page.data_json,'$.taskExpireTime') AS INTEGER),0) <= 0 OR CAST(json_extract(page.data_json,'$.taskExpireTime') AS INTEGER) > $nowUnixMs)");
                predicates.Add("(COALESCE(CAST(json_extract(page.data_json,'$.maxStealCount') AS INTEGER),0) <= 0 OR COALESCE(CAST(json_extract(page.data_json,'$.stolenCount') AS INTEGER),0) < CAST(json_extract(page.data_json,'$.maxStealCount') AS INTEGER))");
            }
            if (options.SpecialOnly)
                predicates.Add("CAST(json_extract(page.data_json,'$.isSpecial') AS INTEGER) = 1");
            if (options.ReindeerOnly)
                predicates.Add("CAST(json_extract(page.data_json,'$.isSpecialURQuality') AS INTEGER) = 1");
            if (options.MinLevel is not null)
                predicates.Add("page.level >= $minLevel");
            if (options.MaxLevel is not null)
                predicates.Add("page.level <= $maxLevel");
            string where = string.Join(" AND ", predicates);

            int total;
            using (SqliteCommand count = connection.CreateCommand())
            {
                count.Transaction = snapshot;
                count.CommandText = $"SELECT COUNT(*) FROM map_records page{join} WHERE {where}";
                AddSearchParameters(count, options, nowUnixMilliseconds);
                total = Convert.ToInt32(count.ExecuteScalar());
            }

            afterCountObserved?.Invoke();

            using SqliteCommand page = connection.CreateCommand();
            page.Transaction = snapshot;
            page.CommandText = city
                ? $"SELECT page.data_json, CASE WHEN mark.owner_uid IS NULL THEN 0 ELSE 1 END FROM map_records page{join} WHERE {where} ORDER BY page.updated_at {direction}, page.record_key ASC LIMIT $limit OFFSET $offset"
                : $"SELECT page.data_json FROM map_records page WHERE {where} ORDER BY page.updated_at {direction}, page.record_key ASC LIMIT $limit OFFSET $offset";
            AddSearchParameters(page, options, nowUnixMilliseconds);
            page.Parameters.AddWithValue("$limit", options.PageSize);
            page.Parameters.AddWithValue("$offset", offset);

            var rows = new List<JsonElement>();
            using SqliteDataReader reader = page.ExecuteReader();
            while (reader.Read())
                rows.Add(ReadSearchRow(reader.GetString(0), city ? reader.GetInt32(1) != 0 : null));
            snapshot.Commit();
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

    internal void StageRecordForPublishTest(string runId, MapStoredRecord record)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new BridgeCommandException("INVALID_SCAN_RUN", "scan run id is required.");
        ValidateRecord(record);

        lock (gate)
        {
            // IMPLEMENTATION POLICY LWB-R6-019: this is offline/test infrastructure for
            // the recovered scan_records identity and publish transaction. It accepts an
            // already-derived recordKey and does not recover native ingestion identity.
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

    internal int ReplacePublishedKindFromStagingForTest(
        string runId,
        string kind,
        int serverId,
        Action? afterDeleteBeforeCopy = null)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new BridgeCommandException("INVALID_SCAN_RUN", "scan run id is required.");
        ValidateKind(kind);
        ValidateServerId(serverId);

        lock (gate)
        {
            // RECOVERED transaction slice: completed direct-scan publication deletes one
            // kind/server from map_records and copies the matching scan_records rows.
            // IMPLEMENTATION POLICY LWB-R6-019: this test-only helper deliberately does
            // not decide whether a run is complete/eligible. The production completion
            // gate remains UNKNOWN/BLOCKED and must execute before this transaction.
            using SqliteTransaction transaction = connection.BeginTransaction();

            using (SqliteCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM map_records WHERE kind=$kind AND server_id=$server";
                delete.Parameters.AddWithValue("$kind", kind);
                delete.Parameters.AddWithValue("$server", serverId);
                delete.ExecuteNonQuery();
            }

            // IMPLEMENTATION POLICY LWB-R6-020: deterministic test hook used only to
            // prove the recovered delete/copy transaction rolls back as one unit when
            // publication fails between its two statements. Production eligibility and
            // failure classification remain separately unrecovered.
            afterDeleteBeforeCopy?.Invoke();

            int inserted;
            using (SqliteCommand copy = connection.CreateCommand())
            {
                copy.Transaction = transaction;
                copy.CommandText = """
                    INSERT INTO map_records(kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json)
                    SELECT kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json
                    FROM scan_records
                    WHERE run_id=$run AND kind=$kind AND server_id=$server
                    """;
                copy.Parameters.AddWithValue("$run", runId);
                copy.Parameters.AddWithValue("$kind", kind);
                copy.Parameters.AddWithValue("$server", serverId);
                inserted = copy.ExecuteNonQuery();
            }

            transaction.Commit();
            return inserted;
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

    private static void AddSearchParameters(SqliteCommand command, MapDataQueryOptions options, long nowUnixMilliseconds)
    {
        command.Parameters.AddWithValue("$kind", options.Kind);
        command.Parameters.AddWithValue("$server", options.ServerId);
        if (options.Keyword is not null)
        {
            string keyword = BuildRecoveredKeywordPattern(options.Keyword);
            command.Parameters.AddWithValue("$keywordName", keyword);
            command.Parameters.AddWithValue("$keywordAlliance", keyword);
            command.Parameters.AddWithValue("$keywordUuid", keyword);
            command.Parameters.AddWithValue("$keywordJson", keyword);
        }
        if (options.Alliance is not null)
            command.Parameters.AddWithValue("$alliance", options.Alliance);
        if (options.ResourceNameKey is not null)
            command.Parameters.AddWithValue("$resourceNameKey", options.ResourceNameKey);
        if (options.MonsterNameKey is not null)
            command.Parameters.AddWithValue("$monsterNameKey", options.MonsterNameKey);
        if (options.SuppliesType > 0)
            command.Parameters.AddWithValue("$suppliesType", options.SuppliesType.Value);
        if (options.TreasureType > 0)
            command.Parameters.AddWithValue("$treasureType", options.TreasureType.Value);
        if (options.Quality is "n" or "r" or "sr" or "ssr")
            command.Parameters.AddWithValue("$quality", options.Quality switch
            {
                "n" => 1,
                "r" => 2,
                "sr" => 3,
                "ssr" => 4,
                _ => throw new InvalidOperationException("Recovered ordinary quality selector is invalid."),
            });
        if (options.ItemKey is not null)
            command.Parameters.AddWithValue("$itemKey", options.ItemKey);
        if (options.Kind is "truck" or "railway" || options.CompletionStatus is not null || options.PlunderableOnly)
            command.Parameters.AddWithValue("$nowUnixMs", nowUnixMilliseconds);
        if (options.MinLevel is not null)
            command.Parameters.AddWithValue("$minLevel", options.MinLevel.Value);
        if (options.MaxLevel is not null)
            command.Parameters.AddWithValue("$maxLevel", options.MaxLevel.Value);
    }

    // LWB-R6-007: original order is backslash, percent, underscore, then literal-percent wrapping.
    private static string BuildRecoveredKeywordPattern(string keyword)
    {
        string escaped = keyword
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
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
