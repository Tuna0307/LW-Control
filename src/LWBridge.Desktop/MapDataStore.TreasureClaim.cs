using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop;

internal sealed partial class MapDataStore
{
    internal IReadOnlyList<string> ReadTreasureClaimCandidateJson(
        int serverId,
        long now)
    {
        ValidateServerId(serverId);

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT data_json FROM map_records
                 WHERE kind='treasure' AND server_id=?1
                   AND (
                     COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0) IN (1,3,4)
                     OR (
                       COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0)=0
                       AND COALESCE(CAST(json_extract(data_json,'$.complete') AS INTEGER),0)=1
                     )
                   )
                   AND uuid IS NOT NULL AND TRIM(uuid)<>'' AND uuid<>'0'
                   AND (
                     COALESCE(CAST(json_extract(data_json,'$.expireTime') AS INTEGER),0)<=0
                     OR CAST(json_extract(data_json,'$.expireTime') AS INTEGER)>?2
                   )
                 ORDER BY point_index ASC
                """;
            command.Parameters.AddWithValue("?1", serverId);
            command.Parameters.AddWithValue("?2", now);

            var rows = new List<string>();
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                rows.Add(reader.GetString(0));
            return rows;
        }
    }
}
