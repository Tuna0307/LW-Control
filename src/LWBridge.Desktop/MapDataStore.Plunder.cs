using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop;

internal sealed record MapPlunderJobsSnapshot(
    IReadOnlyList<JsonElement> DispatchJobs,
    IReadOnlyList<JsonElement> TruckJobs);

internal sealed partial class MapDataStore
{
    internal MapPlunderJobsSnapshot ReadPlunderJobs()
    {
        lock (gate)
        {
            return new MapPlunderJobsSnapshot(
                ReadDispatchPlunderJobsLocked(),
                ReadTruckPlunderJobsLocked());
        }
    }

    internal bool CancelTruckPlunder(int serverId, string trainUuid, long updatedAt)
    {
        ValidateServerId(serverId);
        if (string.IsNullOrWhiteSpace(trainUuid))
            throw new BridgeCommandException("INVALID_TARGET", "truck target is required");

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE truck_plunder_jobs SET status='cancelled',last_error=NULL,updated_at=$updated
                WHERE server_id=$server AND train_uuid=$uuid
                  AND status IN ('scheduled','waiting_connection')
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", trainUuid);
            command.Parameters.AddWithValue("$updated", updatedAt);
            return command.ExecuteNonQuery() > 0;
        }
    }

    private IReadOnlyList<JsonElement> ReadTruckPlunderJobsLocked()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT truck_json,status,attempts,last_error,created_at,updated_at,execute_at
            FROM (
              SELECT truck_json,status,attempts,last_error,created_at,updated_at,execute_at
              FROM truck_plunder_jobs
              UNION ALL
              SELECT truck_json,status,attempts,last_error,created_at,updated_at,execute_at
              FROM truck_plunder_history
            )
            ORDER BY CASE WHEN status IN ('scheduled','waiting_connection','running') THEN 0 ELSE 1 END,
                     execute_at ASC, updated_at DESC
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        var rows = new List<JsonElement>();
        while (reader.Read())
        {
            rows.Add(ReadPlunderJobRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                executeAt: reader.GetInt64(6),
                plunderAt: null));
        }
        return rows;
    }

    private IReadOnlyList<JsonElement> ReadDispatchPlunderJobsLocked()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT task_json,status,attempts,last_error,created_at,updated_at,plunder_at
            FROM dispatch_plunder_jobs
            ORDER BY CASE WHEN status IN ('scheduled','waiting_connection','running') THEN 0 ELSE 1 END,
                     plunder_at ASC, updated_at DESC
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        var rows = new List<JsonElement>();
        while (reader.Read())
        {
            rows.Add(ReadPlunderJobRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                executeAt: null,
                plunderAt: reader.GetInt64(6)));
        }
        return rows;
    }

    private static JsonElement ReadPlunderJobRow(
        string sourceJson,
        string status,
        int attempts,
        string? lastError,
        long createdAt,
        long updatedAt,
        long? executeAt,
        long? plunderAt)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(sourceJson);
            if (node is not JsonObject row)
                throw new BridgeCommandException("MAP_DATA_ERROR", "scheduled plunder job contains invalid JSON");

            row["scheduleStatus"] = status;
            row["attempts"] = attempts;
            row["lastError"] = lastError;
            row["scheduledAt"] = createdAt;
            row["scheduleUpdatedAt"] = updatedAt;
            if (executeAt.HasValue) row["executeAt"] = executeAt.Value;
            if (plunderAt.HasValue) row["plunderAt"] = plunderAt.Value;

            using JsonDocument document = JsonDocument.Parse(row.ToJsonString());
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new BridgeCommandException(
                "MAP_DATA_ERROR",
                "scheduled plunder job contains invalid JSON",
                ex.Message);
        }
    }

    internal void UpsertTruckPlunderJobForTest(
        int serverId,
        string trainUuid,
        string truckJson,
        long executeAt,
        long? expireAt,
        string status,
        int attempts,
        string? lastError,
        long createdAt,
        long updatedAt)
    {
        ValidateServerId(serverId);
        ValidateJsonObject(truckJson, "truck plunder job");
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO truck_plunder_jobs(
                  server_id,train_uuid,truck_json,execute_at,expire_at,status,attempts,last_error,created_at,updated_at
                ) VALUES ($server,$uuid,$json,$execute,$expire,$status,$attempts,$error,$created,$updated)
                ON CONFLICT(server_id,train_uuid) DO UPDATE SET
                  truck_json=excluded.truck_json,execute_at=excluded.execute_at,expire_at=excluded.expire_at,
                  status=excluded.status,attempts=excluded.attempts,last_error=excluded.last_error,
                  created_at=excluded.created_at,updated_at=excluded.updated_at
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", trainUuid);
            command.Parameters.AddWithValue("$json", truckJson);
            command.Parameters.AddWithValue("$execute", executeAt);
            command.Parameters.AddWithValue("$expire", (object?)expireAt ?? DBNull.Value);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$attempts", attempts);
            command.Parameters.AddWithValue("$error", (object?)lastError ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", createdAt);
            command.Parameters.AddWithValue("$updated", updatedAt);
            command.ExecuteNonQuery();
        }
    }

    internal void InsertTruckPlunderHistoryForTest(
        string jobId,
        int serverId,
        string trainUuid,
        string truckJson,
        long executeAt,
        string status,
        int attempts,
        string? lastError,
        long createdAt,
        long updatedAt)
    {
        ValidateServerId(serverId);
        ValidateJsonObject(truckJson, "truck plunder history");
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO truck_plunder_history(
                  job_id,server_id,train_uuid,truck_json,execute_at,status,attempts,last_error,created_at,updated_at
                ) VALUES ($job,$server,$uuid,$json,$execute,$status,$attempts,$error,$created,$updated)
                """;
            command.Parameters.AddWithValue("$job", jobId);
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", trainUuid);
            command.Parameters.AddWithValue("$json", truckJson);
            command.Parameters.AddWithValue("$execute", executeAt);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$attempts", attempts);
            command.Parameters.AddWithValue("$error", (object?)lastError ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", createdAt);
            command.Parameters.AddWithValue("$updated", updatedAt);
            command.ExecuteNonQuery();
        }
    }

    internal void UpsertDispatchPlunderJobForTest(
        int serverId,
        string taskUuid,
        string taskJson,
        long completionTime,
        long plunderAt,
        long? expireAt,
        string status,
        int attempts,
        string? lastError,
        long createdAt,
        long updatedAt)
    {
        ValidateServerId(serverId);
        ValidateJsonObject(taskJson, "dispatch plunder job");
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO dispatch_plunder_jobs(
                  server_id,task_uuid,task_json,completion_time,plunder_at,expire_at,status,attempts,last_error,created_at,updated_at
                ) VALUES ($server,$uuid,$json,$completion,$plunder,$expire,$status,$attempts,$error,$created,$updated)
                ON CONFLICT(server_id,task_uuid) DO UPDATE SET
                  task_json=excluded.task_json,completion_time=excluded.completion_time,plunder_at=excluded.plunder_at,
                  expire_at=excluded.expire_at,status=excluded.status,attempts=excluded.attempts,
                  last_error=excluded.last_error,created_at=excluded.created_at,updated_at=excluded.updated_at
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", taskUuid);
            command.Parameters.AddWithValue("$json", taskJson);
            command.Parameters.AddWithValue("$completion", completionTime);
            command.Parameters.AddWithValue("$plunder", plunderAt);
            command.Parameters.AddWithValue("$expire", (object?)expireAt ?? DBNull.Value);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$attempts", attempts);
            command.Parameters.AddWithValue("$error", (object?)lastError ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", createdAt);
            command.Parameters.AddWithValue("$updated", updatedAt);
            command.ExecuteNonQuery();
        }
    }
}
