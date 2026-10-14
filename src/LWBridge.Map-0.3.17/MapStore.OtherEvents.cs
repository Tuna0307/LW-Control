using Microsoft.Data.Sqlite;

namespace LWBridge.Map317;

public sealed partial class MapStore
{
    // Prior revisions published native supplies and event 27000 as Treasure.
    // Move only source-identifiable rows into the independent Other namespace.
    // Existing new-format rows take precedence; the migration is idempotent.
    private void MigratePreviouslyPublishedOtherEvents()
    {
        // SQLite CAST accepts malformed suffixes ("1.5" -> 1, "27000junk" ->
        // 27000) and COALESCE previously treated missing source fields as zero.
        // The native classifier only accepts actual integers or parseable
        // integer strings. For old persisted rows, accept canonical decimal
        // strings as well, while refusing any ambiguous historical identity.
        string eligible = $"""
            kind='treasure' AND (
              ({ExactSourceInteger("pointType")}
               AND CAST(json_extract(data_json,'$.pointType') AS INTEGER)=27
               AND json_type(data_json,'$.runtimeClass')='text'
               AND substr(json_extract(data_json,'$.runtimeClass'),-length('WorldSuppliesPoint'))='WorldSuppliesPoint'
               AND {ExactSourceInteger("treasureType")}
               AND CAST(json_extract(data_json,'$.treasureType') AS INTEGER)=0
               AND {ExactSourceInteger("suppliesType")}
               AND CAST(json_extract(data_json,'$.suppliesType') AS INTEGER)>0)
              OR
              ({ExactSourceInteger("pointType")}
               AND CAST(json_extract(data_json,'$.pointType') AS INTEGER)=21
               AND json_type(data_json,'$.runtimeClass')='text'
               AND substr(json_extract(data_json,'$.runtimeClass'),-length('TreasurePointInfo'))='TreasurePointInfo'
               AND {ExactSourceInteger("suppliesType")}
               AND CAST(json_extract(data_json,'$.suppliesType') AS INTEGER)=0
               AND {ExactSourceInteger("treasureType")}
               AND CAST(json_extract(data_json,'$.treasureType') AS INTEGER)>0
               AND {ExactSourceInteger("eventId")}
               AND CAST(json_extract(data_json,'$.eventId') AS INTEGER)=27000)
            )
            """;
        const string subtype = """
            CASE WHEN CAST(json_extract(data_json,'$.pointType') AS INTEGER)=27
              THEN 'supplies:' || CAST(json_extract(data_json,'$.suppliesType') AS TEXT)
              ELSE 'event:27000' END
            """;
        const string nameKey = """
            CASE
              WHEN CAST(json_extract(data_json,'$.pointType') AS INTEGER)=21
                THEN 'activity_wajueji_27000_name'
              WHEN CAST(json_extract(data_json,'$.suppliesType') AS INTEGER)=1
                THEN 'season_s2_ice_supplies_11'
              ELSE NULL
            END
            """;

        using SqliteTransaction transaction = connection.BeginTransaction();
        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = $"""
                INSERT OR IGNORE INTO map_records(
                    kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,
                    power,distance,shield_end_time,updated_at,data_json)
                SELECT 'other',server_id,record_key,point_index,uuid,name,alliance_name,level,quality,
                       power,distance,shield_end_time,updated_at,
                       json_set(data_json,'$.kind','other','$.otherSubtype',{subtype},'$.otherNameKey',{nameKey})
                FROM map_records WHERE {eligible}
                """;
            insert.ExecuteNonQuery();
        }
        using (SqliteCommand delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = $"DELETE FROM map_records WHERE {eligible}";
            delete.ExecuteNonQuery();
        }
        // An earlier Other preview assigned these guesses to type 3/4.
        // Remove only the speculative values, preserving future real keys.
        using (SqliteCommand scrub = connection.CreateCommand())
        {
            scrub.Transaction = transaction;
            scrub.CommandText = """
                UPDATE map_records
                SET data_json=json_remove(data_json,'$.otherNameKey')
                WHERE kind='other'
                  AND CAST(json_extract(data_json,'$.pointType') AS INTEGER)=27
                  AND json_extract(data_json,'$.otherNameKey')
                      IN ('season_s3_supplies_1','season4_supplies_name_2')
                """;
            scrub.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static string ExactSourceInteger(string name)
    {
        // Only source-controlled property names enter this SQL helper. SQLite
        // distinguishes integer JSON numbers from fractional/exponent numbers.
        // A text value must exactly round-trip through the integer conversion;
        // CAST alone would silently truncate or parse a valid-looking prefix.
        string path = $"'$.{name}'";
        string value = $"json_extract(data_json,{path})";
        string type = $"json_type(data_json,{path})";
        return $"(({type}='integer' OR ({type}='text' AND {value}=CAST(CAST({value} AS INTEGER) AS TEXT))) " +
            $"AND CAST({value} AS INTEGER) BETWEEN -2147483648 AND 2147483647)";
    }
}
