using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LWBridge.Map317;

public sealed partial class MapStore
{
    // Other is a browsing category, not a change to the native inspection
    // identity. Keep the existing read-only Supplies/Party workflows after
    // partitioning, without duplicating rows or admitting arbitrary Other.
    private const string TreasureInspectionKindPredicate = """
        (kind='treasure' OR (kind='other' AND json_extract(data_json,'$.kind')='other' AND (
          (CAST(json_extract(data_json,'$.pointType') AS INTEGER)=27
           AND CAST(json_extract(data_json,'$.runtimeClass') AS TEXT) GLOB '*WorldSuppliesPoint'
           AND CAST(json_extract(data_json,'$.treasureType') AS INTEGER)=0
           AND CAST(json_extract(data_json,'$.suppliesType') AS INTEGER) IN (1,3,4)
           AND CAST(json_extract(data_json,'$.suppliesType') AS TEXT)
               =CAST(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER) AS TEXT)
           AND json_extract(data_json,'$.otherSubtype')
               ='supplies:' || CAST(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER) AS TEXT))
          OR
          (CAST(json_extract(data_json,'$.pointType') AS INTEGER)=21
           AND CAST(json_extract(data_json,'$.runtimeClass') AS TEXT) GLOB '*TreasurePointInfo'
           AND CAST(json_extract(data_json,'$.suppliesType') AS INTEGER)=0
           AND CAST(json_extract(data_json,'$.treasureType') AS INTEGER)>0
           AND CAST(json_extract(data_json,'$.eventId') AS INTEGER)=27000
           AND json_extract(data_json,'$.otherSubtype')='event:27000')
        )))
        """;

    public IReadOnlyList<JsonElement> ReadTreasureClaimCandidates(int serverId, long nowUnixMilliseconds)
    {
        ValidateServerId(serverId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT data_json FROM map_records
                WHERE {TreasureInspectionKindPredicate} AND server_id=$server
                  AND (
                    COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0) IN (1,3,4)
                    OR (COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0)=0
                        AND COALESCE(CAST(json_extract(data_json,'$.complete') AS INTEGER),0)=1)
                  )
                  AND uuid IS NOT NULL AND TRIM(uuid)<>'' AND uuid<>'0'
                  AND (COALESCE(CAST(json_extract(data_json,'$.expireTime') AS INTEGER),0)<=0
                       OR CAST(json_extract(data_json,'$.expireTime') AS INTEGER)>$now)
                ORDER BY point_index ASC
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$now", nowUnixMilliseconds);
            return ReadJsonRows(command);
        }
    }

    public IReadOnlyList<JsonElement> ReadSeasonSupplyTreasureRows(int serverId)
    {
        ValidateServerId(serverId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT data_json FROM map_records
                WHERE {TreasureInspectionKindPredicate} AND server_id=$server
                  AND COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0) IN (1,3,4)
                ORDER BY point_index ASC
                """;
            command.Parameters.AddWithValue("$server", serverId);
            return ReadJsonRows(command);
        }
    }

    public IReadOnlyList<JsonElement> ReadDispatchPlunderJobs() => ReadPlunderJobs().DispatchJobs;
    public IReadOnlyList<JsonElement> ReadTruckPlunderJobs() => ReadPlunderJobs().TruckJobs;

    private static IReadOnlyList<JsonElement> ReadJsonRows(SqliteCommand command)
    {
        using SqliteDataReader reader = command.ExecuteReader();
        var rows = new List<JsonElement>();
        while (reader.Read())
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(reader.GetString(0));
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new BridgeCommandException("MAP_DATA_ERROR", "invalid treasure record");
                rows.Add(document.RootElement.Clone());
            }
            catch (JsonException error)
            {
                throw new BridgeCommandException("MAP_DATA_ERROR", "invalid treasure record", error.Message);
            }
        }
        return rows;
    }
}
