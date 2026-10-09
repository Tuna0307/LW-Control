using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace LWBridge.Map317;

public sealed partial class MapStore
{
    public MapSearchResult Search(MapQuery query, long? nowUnixMilliseconds = null, bool cityExportRows = false)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateKind(query.Kind);
        ValidateServerId(query.ServerId);
        long pageNumber = Math.Max(query.Page, 1L);
        int pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        IReadOnlyList<MapSort> sorts = NormalizeSorts(query);
        long now = nowUnixMilliseconds ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Original 0.3.17 0x3E5883-0x3E58B8 subtracts one and multiplies
        // with unchecked signed-i64 machine arithmetic, then binds that exact
        // result as SQL OFFSET. Negative offsets are handled by SQLite itself.
        // Saturating here changes the original returned rows on overflow.
        long offset = unchecked((pageNumber - 1) * pageSize);
        bool city = query.Kind == "city";
        bool treasure = query.Kind == "treasure";
        bool staging = !string.IsNullOrWhiteSpace(query.ScanRunId);
        string table = staging ? "scan_records" : "map_records";
        string runPredicate = staging ? " AND page.run_id=$run" : string.Empty;
        string orderBy = BuildOrderBy(query, sorts, now);

        lock (gate)
        {
            using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
            string markJoin = city
                ? " LEFT JOIN player_marks mark ON mark.server_id=page.server_id AND mark.owner_uid=CAST(json_extract(page.data_json,'$.ownerUid') AS TEXT)"
                : string.Empty;
            string treasurePlayer = query.ViewerUid is { Length: > 0 }
                ? "$viewerUid"
                : "COALESCE(CAST(json_extract(page.data_json,'$.viewerUid') AS TEXT),'')";
            string rowJoin = city
                ? markJoin
                : treasure
                    ? $" LEFT JOIN treasure_claim_states state ON state.server_id=page.server_id AND state.player_uid={treasurePlayer} AND state.treasure_uuid=page.uuid"
                    : string.Empty;

            List<string> predicates = BuildPredicates(query, now);
            predicates.Add("page.server_id=$server");
            if (staging) predicates.Add("page.run_id=$run");
            string where = string.Join(" AND ", predicates);

            int total;
            using (SqliteCommand count = connection.CreateCommand())
            {
                count.Transaction = snapshot;
                count.CommandText = $"SELECT COUNT(*) FROM {table} page{markJoin} WHERE {where}";
                AddQueryParameters(count, query, now, staging);
                total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            using SqliteCommand page = connection.CreateCommand();
            page.Transaction = snapshot;
            string select = city
                ? cityExportRows
                    ? $"SELECT page.data_json,page.server_id,CASE WHEN mark.owner_uid IS NULL THEN 0 ELSE 1 END,page.shield_end_time,page.updated_at FROM {table} page{rowJoin} WHERE {where} ORDER BY {orderBy}"
                    : $"SELECT page.data_json,page.server_id,CASE WHEN mark.owner_uid IS NULL THEN 0 ELSE 1 END FROM {table} page{rowJoin} WHERE {where} ORDER BY {orderBy}"
                : treasure
                    ? $"SELECT page.data_json,page.server_id,state.state_json FROM {table} page{rowJoin} WHERE {where} ORDER BY {orderBy}"
                    : $"SELECT page.data_json,page.server_id FROM {table} page WHERE {where} ORDER BY {orderBy}";
            page.CommandText = select + " LIMIT $limit OFFSET $offset";
            AddQueryParameters(page, query, now, staging);
            page.Parameters.AddWithValue("$limit", pageSize);
            page.Parameters.AddWithValue("$offset", offset);

            var rows = new List<JsonElement>();
            using SqliteDataReader reader = page.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(city && cityExportRows
                    ? ReadCityExportRow(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2) != 0,
                        reader.IsDBNull(3) ? null : reader.GetInt64(3), reader.GetInt64(4))
                    : ReadSearchRow(reader.GetString(0), reader.GetInt32(1),
                        city ? reader.GetInt32(2) != 0 : null,
                        treasure && !reader.IsDBNull(2) ? reader.GetString(2) : null));
            }
            snapshot.Commit();
            return new MapSearchResult(rows, total, pageNumber, pageSize);
        }
    }

    public IReadOnlyDictionary<string, int> SummaryCounts(int serverId, string? scanRunId = null)
    {
        ValidateServerId(serverId);
        bool staging = !string.IsNullOrWhiteSpace(scanRunId);
        string table = staging ? "scan_records" : "map_records";
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = staging
                ? $"SELECT kind,COUNT(*) FROM {table} WHERE server_id=$server AND run_id=$run GROUP BY kind"
                : $"SELECT kind,COUNT(*) FROM {table} WHERE server_id=$server GROUP BY kind";
            command.Parameters.AddWithValue("$server", serverId);
            if (staging) command.Parameters.AddWithValue("$run", scanRunId!);
            using SqliteDataReader reader = command.ExecuteReader();
            var result = MapKinds.All.ToDictionary(kind => kind, _ => 0, StringComparer.Ordinal);
            while (reader.Read()) result[reader.GetString(0)] = reader.GetInt32(1);
            return result;
        }
    }

    public MapOptionSet ReadOptions(int serverId, string? scanRunId = null, long? nowUnixMilliseconds = null)
    {
        ValidateServerId(serverId);
        bool staging = !string.IsNullOrWhiteSpace(scanRunId);
        string table = staging ? "scan_records" : "map_records";
        string run = staging ? " AND run_id=$run" : string.Empty;
        lock (gate)
        {
            using SqliteTransaction snapshot = connection.BeginTransaction(deferred: true);
            IReadOnlyDictionary<string, int> counts = ReadCountsLocked(snapshot, table, serverId, scanRunId);
            List<MapAllianceOption> alliances = ReadAllianceOptionsLocked(snapshot,
                $"SELECT alliance_name,COUNT(*) FROM {table} WHERE server_id=$server{run} AND kind='city' GROUP BY alliance_name ORDER BY alliance_name COLLATE NOCASE,alliance_name",
                serverId, scanRunId);
            int noAlliance = ScalarIntLocked(snapshot,
                $"SELECT COUNT(*) FROM {table} WHERE server_id=$server{run} AND kind='city' AND (alliance_name IS NULL OR alliance_name='')",
                serverId, scanRunId);
            List<MapNameOption> resources = ReadNameOptionsLocked(snapshot, table, serverId, scanRunId, "resource", "resourceNameKey");
            List<MapNameOption> monsters = ReadNameOptionsLocked(snapshot, table, serverId, scanRunId, "monster", "monsterNameKey");
            List<int> dispatchLevels = ReadDistinctIntLocked(snapshot,
                $"SELECT level FROM {table} WHERE server_id=$server{run} AND kind='dispatch' AND level IS NOT NULL GROUP BY level ORDER BY level",
                serverId, scanRunId);
            IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> rewardItems =
                ReadRewardItemsLocked(snapshot, table, serverId, scanRunId,
                    nowUnixMilliseconds ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            List<JsonElement> treasureTypes = ReadTreasureTypesLocked(snapshot, table, serverId, scanRunId);
            MapScanRun? progress = staging
                ? ReadScanRunLocked(scanRunId!, snapshot)
                : ReadLatestScanRunLocked(serverId, snapshot);
            snapshot.Commit();
            return new MapOptionSet(serverId, counts, alliances,
                new Dictionary<string, IReadOnlyList<MapNameOption>>(StringComparer.Ordinal)
                {
                    ["resource"] = resources,
                    ["monster"] = monsters,
                },
                dispatchLevels, noAlliance, rewardItems, treasureTypes, progress);
        }
    }

    private IReadOnlyDictionary<string, int> ReadCountsLocked(SqliteTransaction tx, string table, int serverId, string? runId)
    {
        bool staging = !string.IsNullOrWhiteSpace(runId);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = staging
            ? $"SELECT kind,COUNT(*) FROM {table} WHERE server_id=$server AND run_id=$run GROUP BY kind"
            : $"SELECT kind,COUNT(*) FROM {table} WHERE server_id=$server GROUP BY kind";
        command.Parameters.AddWithValue("$server", serverId);
        if (staging) command.Parameters.AddWithValue("$run", runId!);
        using SqliteDataReader reader = command.ExecuteReader();
        var result = MapKinds.All.ToDictionary(kind => kind, _ => 0, StringComparer.Ordinal);
        while (reader.Read()) result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
    }

    private List<MapAllianceOption> ReadAllianceOptionsLocked(SqliteTransaction tx, string sql, int serverId, string? runId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$server", serverId);
        if (!string.IsNullOrWhiteSpace(runId)) command.Parameters.AddWithValue("$run", runId);
        using SqliteDataReader reader = command.ExecuteReader();
        var result = new List<MapAllianceOption>();
        while (reader.Read())
        {
            if (reader.IsDBNull(0) || string.IsNullOrEmpty(reader.GetString(0))) continue;
            result.Add(new MapAllianceOption(reader.GetString(0), reader.GetInt32(1)));
        }
        return result;
    }

    private List<int> ReadDistinctIntLocked(SqliteTransaction tx, string sql, int serverId, string? runId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$server", serverId);
        if (!string.IsNullOrWhiteSpace(runId)) command.Parameters.AddWithValue("$run", runId);
        using SqliteDataReader reader = command.ExecuteReader();
        var result = new List<int>();
        while (reader.Read()) result.Add(reader.GetInt32(0));
        return result;
    }

    private int ScalarIntLocked(SqliteTransaction tx, string sql, int serverId, string? runId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$server", serverId);
        if (!string.IsNullOrWhiteSpace(runId)) command.Parameters.AddWithValue("$run", runId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private List<MapNameOption> ReadNameOptionsLocked(SqliteTransaction tx, string table, int serverId, string? runId,
        string kind, string jsonKey)
    {
        bool staging = !string.IsNullOrWhiteSpace(runId);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = $"""
            SELECT CAST(json_extract(data_json,'$.{jsonKey}') AS TEXT),COUNT(*)
            FROM {table}
            WHERE server_id=$server {(staging ? "AND run_id=$run" : string.Empty)} AND kind=$kind
              AND json_extract(data_json,'$.{jsonKey}') IS NOT NULL
            GROUP BY CAST(json_extract(data_json,'$.{jsonKey}') AS TEXT)
            ORDER BY CAST(json_extract(data_json,'$.{jsonKey}') AS TEXT) COLLATE NOCASE
            """;
        command.Parameters.AddWithValue("$server", serverId);
        command.Parameters.AddWithValue("$kind", kind);
        if (staging) command.Parameters.AddWithValue("$run", runId!);
        using SqliteDataReader reader = command.ExecuteReader();
        var result = new List<MapNameOption>();
        while (reader.Read()) if (!reader.IsDBNull(0)) result.Add(new MapNameOption(reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    private IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> ReadRewardItemsLocked(
        SqliteTransaction tx,
        string table,
        int serverId,
        string? runId,
        long nowUnixMilliseconds)
    {
        bool staging = !string.IsNullOrWhiteSpace(runId);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = $"""
            SELECT page.kind,
                   CAST(json_extract(good.value,'$.key') AS TEXT),
                   CAST(json_extract(good.value,'$.name') AS TEXT),
                   CAST(json_extract(good.value,'$.iconPath') AS TEXT)
            FROM {table} page, json_each(page.data_json,'$.currentGoods') AS good
            WHERE page.server_id=$server {(staging ? "AND page.run_id=$run" : string.Empty)}
              AND page.kind IN ('truck','railway')
              AND (json_extract(page.data_json,'$.arriveTs') IS NULL
                   OR CAST(json_extract(page.data_json,'$.arriveTs') AS INTEGER)>$now)
              AND json_extract(good.value,'$.key') IS NOT NULL
              AND json_extract(good.value,'$.name') IS NOT NULL
            GROUP BY page.kind,2,3,4 ORDER BY 3 COLLATE NOCASE,2
            """;
        command.Parameters.AddWithValue("$server", serverId);
        command.Parameters.AddWithValue("$now", nowUnixMilliseconds);
        if (staging) command.Parameters.AddWithValue("$run", runId!);
        using SqliteDataReader reader = command.ExecuteReader();
        var truck = new List<JsonElement>();
        var railway = new List<JsonElement>();
        while (reader.Read())
        {
            JsonElement option = JsonSerializer.SerializeToElement(new
            {
                key = reader.GetString(1),
                name = reader.GetString(2),
                iconPath = reader.GetString(3),
            });
            if (reader.GetString(0) == "truck") truck.Add(option); else railway.Add(option);
        }
        return new Dictionary<string, IReadOnlyList<JsonElement>>(StringComparer.Ordinal)
        {
            ["truck"] = truck,
            ["railway"] = railway,
        };
    }

    private List<JsonElement> ReadTreasureTypesLocked(SqliteTransaction tx, string table, int serverId, string? runId)
    {
        bool staging = !string.IsNullOrWhiteSpace(runId);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = $"""
            WITH treasure_options AS (
              SELECT
                CASE WHEN COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0)>0
                  THEN CAST(json_extract(data_json,'$.suppliesType') AS INTEGER) ELSE 0 END AS supplies_type,
                CASE WHEN COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0)>0
                  THEN 0 ELSE COALESCE(CAST(json_extract(data_json,'$.treasureType') AS INTEGER),0) END AS treasure_type,
                COALESCE(CAST(json_extract(data_json,'$.treasureNameKey') AS TEXT),'') AS name_key
              FROM {table}
              WHERE server_id=$server {(staging ? "AND run_id=$run" : string.Empty)} AND kind='treasure'
            )
            SELECT supplies_type,treasure_type,MAX(name_key),COUNT(*)
            FROM treasure_options
            WHERE supplies_type>0 OR treasure_type>0
            GROUP BY supplies_type,treasure_type
            ORDER BY CASE WHEN supplies_type>0 THEN 1 ELSE 0 END,treasure_type,supplies_type
            """;
        command.Parameters.AddWithValue("$server", serverId);
        if (staging) command.Parameters.AddWithValue("$run", runId!);
        using SqliteDataReader reader = command.ExecuteReader();
        var result = new List<JsonElement>();
        while (reader.Read())
        {
            int suppliesType = reader.GetInt32(0);
            int treasureType = reader.GetInt32(1);
            string name = reader.GetString(2);
            result.Add(JsonSerializer.SerializeToElement(new
            {
                key = suppliesType > 0 ? $"supplies:{suppliesType}" : $"treasure:{treasureType}",
                count = reader.GetInt32(3),
                treasureType,
                suppliesType,
                treasureNameKey = name,
            }));
        }
        return result;
    }

    private MapScanRun? ReadLatestScanRunLocked(int serverId, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error
            FROM scan_runs WHERE server_id=$server AND status<>'discarded' ORDER BY updated_at DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$server", serverId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadScanRun(reader) : null;
    }

    private static List<string> BuildPredicates(MapQuery query, long now)
    {
        var predicates = new List<string> { "page.kind=$kind" };
        if (query.Kind is "truck" or "railway")
            predicates.Add("(json_extract(page.data_json,'$.arriveTs') IS NULL OR CAST(json_extract(page.data_json,'$.arriveTs') AS INTEGER)>$nowUnixMs)");
        if (query.Kind == "city" && query.MarkedOnly) predicates.Add("mark.owner_uid IS NOT NULL");
        if (!string.IsNullOrEmpty(query.Keyword))
            predicates.Add("(page.name LIKE $keyword ESCAPE '\\' COLLATE NOCASE OR page.alliance_name LIKE $keyword ESCAPE '\\' COLLATE NOCASE OR page.uuid LIKE $keyword ESCAPE '\\' COLLATE NOCASE OR page.data_json LIKE $keyword ESCAPE '\\' COLLATE NOCASE)");
        if (query.Alliance is not null) predicates.Add("page.alliance_name=$alliance");
        if (query.WithoutAlliance) predicates.Add("(page.alliance_name IS NULL OR page.alliance_name='')");
        if (query.ResourceNameKey is not null) predicates.Add("CAST(json_extract(page.data_json,'$.resourceNameKey') AS TEXT)=$resourceNameKey");
        if (query.MonsterNameKey is not null) predicates.Add("CAST(json_extract(page.data_json,'$.monsterNameKey') AS TEXT)=$monsterNameKey");
        if (query.SuppliesType > 0) predicates.Add("CAST(json_extract(page.data_json,'$.suppliesType') AS INTEGER)=$suppliesType");
        if (query.TreasureType > 0)
        {
            predicates.Add("CAST(json_extract(page.data_json,'$.treasureType') AS INTEGER)=$treasureType");
            predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.suppliesType') AS INTEGER),0)=0");
        }
        if (query.Kind == "treasure" && !query.IncludeForeignRadarTreasures)
        {
            predicates.Add(query.ViewerAllianceId is { Length: > 0 }
                ? "(COALESCE(CAST(json_extract(page.data_json,'$.treasureType') AS INTEGER),0)<>1 OR CAST(json_extract(page.data_json,'$.allianceId') AS TEXT)=$viewerAllianceId)"
                : "(COALESCE(CAST(json_extract(page.data_json,'$.treasureType') AS INTEGER),0)<>1 OR (COALESCE(CAST(json_extract(page.data_json,'$.allianceId') AS TEXT),'')<>'' AND CAST(json_extract(page.data_json,'$.allianceId') AS TEXT)=COALESCE(CAST(json_extract(page.data_json,'$.viewerAllianceId') AS TEXT),'')))");
        }
        if (query.Quality is "n" or "r" or "sr" or "ssr") predicates.Add("page.quality=$quality");
        else if (query.Quality == "ur") predicates.Add("page.quality>=5");
        else if (query.Quality is not null) throw new BridgeCommandException("INVALID_REQUEST", "invalid map quality");
        if (query.Kind == "truck" && query.Quality == "ur")
            predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.isSpecialURQuality') AS INTEGER),0)=0");
        if (query.ItemKey is not null)
            predicates.Add("EXISTS (SELECT 1 FROM json_each(page.data_json,'$.currentGoods') AS good WHERE CAST(json_extract(good.value,'$.key') AS TEXT)=$itemKey)");
        if (query.CompletionStatus == "pending")
            predicates.Add("(CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER) IS NULL OR CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER)<=0 OR CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER)>$nowUnixMs)");
        else if (query.CompletionStatus == "completed")
            predicates.Add("CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER)>0 AND CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER)<=$nowUnixMs");
        else if (query.CompletionStatus is not null) throw new BridgeCommandException("INVALID_REQUEST", "invalid completion status");
        if (query.PlunderableOnly && query.Kind is "truck" or "railway")
        {
            predicates.Add("json_extract(page.data_json,'$.arriveTs') IS NOT NULL");
            predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.remainingLootCount') AS INTEGER),MAX(COALESCE(CAST(json_extract(page.data_json,'$.maxLootCount') AS INTEGER),0)-COALESCE(CAST(json_extract(page.data_json,'$.robTimes') AS INTEGER),0),0))>0");
        }
        else if (query.PlunderableOnly && query.Kind == "dispatch")
        {
            predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER),0)>0");
            predicates.Add("COALESCE(CAST(json_extract(page.data_json,'$.plunderAt') AS INTEGER),CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER),0)>0");
            predicates.Add("(COALESCE(CAST(json_extract(page.data_json,'$.taskExpireTime') AS INTEGER),0)<=0 OR CAST(json_extract(page.data_json,'$.taskExpireTime') AS INTEGER)>$nowUnixMs)");
            predicates.Add("(COALESCE(CAST(json_extract(page.data_json,'$.maxStealCount') AS INTEGER),0)<=0 OR COALESCE(CAST(json_extract(page.data_json,'$.stolenCount') AS INTEGER),0)<CAST(json_extract(page.data_json,'$.maxStealCount') AS INTEGER))");
        }
        if (query.SpecialOnly) predicates.Add("CAST(json_extract(page.data_json,'$.isSpecial') AS INTEGER)=1");
        if (query.ReindeerOnly) predicates.Add("CAST(json_extract(page.data_json,'$.isSpecialURQuality') AS INTEGER)=1");
        if (query.MinLevel is not null) predicates.Add("page.level>=$minLevel");
        if (query.MaxLevel is not null) predicates.Add("page.level<=$maxLevel");
        if (query.MinPower is not null) predicates.Add("page.power>=$minPower");
        if (query.MaxPower is not null) predicates.Add("page.power<=$maxPower");
        return predicates;
    }

    private static IReadOnlyList<MapSort> NormalizeSorts(MapQuery query)
    {
        IReadOnlyList<MapSort> sorts = query.Sorts is { Count: > 0 }
            ? query.Sorts
            : [new MapSort("updatedAt", "desc")];
        foreach (MapSort sort in sorts)
        {
            if (sort.SortOrder is not ("asc" or "desc"))
                throw new BridgeCommandException("INVALID_REQUEST", "invalid map sort order");
        }
        return sorts;
    }

    private static string BuildOrderBy(MapQuery query, IReadOnlyList<MapSort> sorts, long now)
    {
        if (query.Kind == "treasure" && query.LuckyFirst)
        {
            string player = query.ViewerUid is { Length: > 0 }
                ? "$viewerUid"
                : "COALESCE(CAST(json_extract(page.data_json,'$.viewerUid') AS TEXT),'')";
            string priority = "COALESCE((SELECT CAST(json_extract(state2.state_json,'$.claimPriority') AS INTEGER) FROM treasure_claim_states state2 WHERE state2.server_id=page.server_id AND state2.player_uid=" + player + " AND state2.treasure_uuid=page.uuid),1)";
            return priority + " ASC, " + BuildOrdinaryOrderBy(query, sorts, now);
        }
        return BuildOrdinaryOrderBy(query, sorts, now);
    }

    private static string BuildOrdinaryOrderBy(MapQuery query, IReadOnlyList<MapSort> sorts, long now)
    {
        var clauses = new List<string>(sorts.Count * 2 + 1);
        foreach (MapSort sort in sorts)
        {
            string expression = SortExpression(query, sort.SortBy);
            string direction = query.Kind == "monster" && sort.SortBy == "distance"
                ? "ASC"
                : sort.SortOrder == "asc" ? "ASC" : "DESC";
            clauses.Add($"({expression} IS NULL) ASC");
            clauses.Add($"{expression} {direction}");
        }
        clauses.Add("page.record_key ASC");
        return string.Join(", ", clauses);
    }

    private static string SortExpression(MapQuery query, string key) => (query.Kind, key) switch
    {
        ("city", "level") => "page.level",
        ("city", "health") => "NULLIF(CAST(json_extract(page.data_json,'$.health') AS REAL),0)",
        ("city", "shield") => "CASE WHEN COALESCE(page.shield_end_time,CAST(json_extract(page.data_json,'$.protectEndTime') AS INTEGER),0)>=1000000000000 AND COALESCE(page.shield_end_time,CAST(json_extract(page.data_json,'$.protectEndTime') AS INTEGER),0)>$nowUnixMs THEN COALESCE(page.shield_end_time,CAST(json_extract(page.data_json,'$.protectEndTime') AS INTEGER),0) WHEN COALESCE(page.shield_end_time,CAST(json_extract(page.data_json,'$.protectEndTime') AS INTEGER),0)<1000000000000 AND COALESCE(page.shield_end_time,CAST(json_extract(page.data_json,'$.protectEndTime') AS INTEGER),0)>$nowUnixSeconds THEN COALESCE(page.shield_end_time,CAST(json_extract(page.data_json,'$.protectEndTime') AS INTEGER),0) ELSE NULL END",
        ("city", "updatedAt") => "page.updated_at",
        ("resource", "level") => "page.level",
        ("resource", "updatedAt") => "page.updated_at",
        ("monster", "level") => "page.level",
        ("monster", "distance") => "page.distance",
        ("monster", "updatedAt") => "page.updated_at",
        ("truck", "quality") => "CASE WHEN CAST(json_extract(page.data_json,'$.isSpecialURQuality') AS INTEGER)=1 THEN 100 ELSE page.quality END",
        ("truck", "power") => "page.power",
        ("truck", "itemCount") when query.ItemKey is { Length: > 0 } => "COALESCE((SELECT SUM(CAST(json_extract(good.value,'$.count') AS REAL)) FROM json_each(page.data_json,'$.currentGoods') AS good WHERE CAST(json_extract(good.value,'$.key') AS TEXT)=$itemKey),0)",
        ("truck", "remainingLootCount") => "COALESCE(CAST(json_extract(page.data_json,'$.remainingLootCount') AS INTEGER),0)",
        ("truck", "arriveTime") => "NULLIF(CAST(json_extract(page.data_json,'$.arriveTs') AS INTEGER),0)",
        ("truck", "updatedAt") => "page.updated_at",
        ("railway", "quality") => "page.quality",
        ("railway", "power") => "page.power",
        ("railway", "itemCount") when query.ItemKey is { Length: > 0 } => "COALESCE((SELECT SUM(CAST(json_extract(good.value,'$.count') AS REAL)) FROM json_each(page.data_json,'$.currentGoods') AS good WHERE CAST(json_extract(good.value,'$.key') AS TEXT)=$itemKey),0)",
        ("railway", "protectTime") => "NULLIF(CAST(json_extract(page.data_json,'$.protectTime') AS INTEGER),0)",
        ("railway", "updatedAt") => "page.updated_at",
        ("dispatch", "level") or ("ghost", "level") => "page.level",
        ("dispatch", "quality") or ("ghost", "quality") => "CASE WHEN CAST(json_extract(page.data_json,'$.isSpecial') AS INTEGER)=1 THEN 100 ELSE page.quality END",
        ("dispatch", "completionTime") or ("ghost", "completionTime") => "NULLIF(CAST(json_extract(page.data_json,'$.completionTime') AS INTEGER),0)",
        ("dispatch", "updatedAt") or ("ghost", "updatedAt") => "page.updated_at",
        ("treasure", "updatedAt") => "page.updated_at",
        _ => throw new BridgeCommandException("INVALID_REQUEST", $"unsupported {query.Kind} sort column '{key}'"),
    };

    private static void AddQueryParameters(SqliteCommand command, MapQuery query, long now, bool staging)
    {
        command.Parameters.AddWithValue("$kind", query.Kind);
        command.Parameters.AddWithValue("$server", query.ServerId);
        if (staging) command.Parameters.AddWithValue("$run", query.ScanRunId!);
        if (!string.IsNullOrEmpty(query.Keyword)) command.Parameters.AddWithValue("$keyword", KeywordPattern(query.Keyword));
        if (query.Alliance is not null) command.Parameters.AddWithValue("$alliance", query.Alliance);
        if (query.ResourceNameKey is not null) command.Parameters.AddWithValue("$resourceNameKey", query.ResourceNameKey);
        if (query.MonsterNameKey is not null) command.Parameters.AddWithValue("$monsterNameKey", query.MonsterNameKey);
        if (query.SuppliesType > 0) command.Parameters.AddWithValue("$suppliesType", query.SuppliesType.Value);
        if (query.TreasureType > 0) command.Parameters.AddWithValue("$treasureType", query.TreasureType.Value);
        if (query.ViewerUid is { Length: > 0 }) command.Parameters.AddWithValue("$viewerUid", query.ViewerUid);
        if (query.Kind == "treasure" && !query.IncludeForeignRadarTreasures && query.ViewerAllianceId is { Length: > 0 })
            command.Parameters.AddWithValue("$viewerAllianceId", query.ViewerAllianceId);
        if (query.Quality is "n" or "r" or "sr" or "ssr")
            command.Parameters.AddWithValue("$quality", query.Quality switch
            {
                "n" => 1, "r" => 2, "sr" => 3, "ssr" => 4, _ => 0,
            });
        if (query.ItemKey is not null) command.Parameters.AddWithValue("$itemKey", query.ItemKey);
        if (query.Kind is "truck" or "railway" || query.CompletionStatus is not null || query.PlunderableOnly ||
            query.Sorts?.Any(sort => query.Kind == "city" && sort.SortBy == "shield") == true)
        {
            command.Parameters.AddWithValue("$nowUnixMs", now);
        }
        if (query.Sorts?.Any(sort => query.Kind == "city" && sort.SortBy == "shield") == true)
            command.Parameters.AddWithValue("$nowUnixSeconds", now / 1000L);
        if (query.MinLevel is not null) command.Parameters.AddWithValue("$minLevel", query.MinLevel.Value);
        if (query.MaxLevel is not null) command.Parameters.AddWithValue("$maxLevel", query.MaxLevel.Value);
        if (query.MinPower is not null) command.Parameters.AddWithValue("$minPower", query.MinPower.Value);
        if (query.MaxPower is not null) command.Parameters.AddWithValue("$maxPower", query.MaxPower.Value);
    }

    private static string KeywordPattern(string keyword) => "%" + keyword
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal) + "%";

    private static JsonElement ReadCityExportRow(string json, int serverId, bool marked, long? shieldEndTime, long updatedAt)
    {
        JsonObject row = ParseRow(json);
        row["serverId"] = serverId;
        row["marked"] = marked;
        row["updatedAt"] = updatedAt;
        if (shieldEndTime.HasValue) row["shieldEndTime"] = shieldEndTime.Value;
        return JsonSerializer.SerializeToElement(row);
    }

    private static JsonElement ReadSearchRow(string json, int serverId, bool? marked, string? stateJson)
    {
        JsonObject row = ParseRow(json);
        if (!string.IsNullOrEmpty(stateJson))
        {
            JsonObject state = ParseRow(stateJson);
            foreach ((string key, JsonNode? value) in state) row[key] = value?.DeepClone();
        }
        row["serverId"] = serverId;
        if (marked.HasValue) row["marked"] = marked.Value;
        return JsonSerializer.SerializeToElement(row);
    }

    private static JsonObject ParseRow(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new BridgeCommandException("MAP_DATA_ERROR", "stored map row is not a JSON object");
        }
        catch (JsonException error)
        {
            throw new BridgeCommandException("MAP_DATA_ERROR", "stored map row contains invalid JSON", error.Message);
        }
    }
}
