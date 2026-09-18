using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop;


internal sealed record TruckPlunderScheduleResult(
    string JobId,
    int Attempts,
    bool ArchivedPreviousAttempt);

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



    internal TruckPlunderScheduleResult ScheduleTruckPlunder(
        int serverId,
        string trainUuid,
        string truckJson,
        long executeAt,
        long? expireAt,
        long now)
    {
        Span<byte> randomBytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(randomBytes);
        ulong randomValue = BitConverter.ToUInt64(randomBytes);
        return ScheduleTruckPlunderCore(
            serverId,
            trainUuid,
            truckJson,
            executeAt,
            expireAt,
            now,
            randomValue);
    }

    internal TruckPlunderScheduleResult ScheduleTruckPlunderForTest(
        int serverId,
        string trainUuid,
        string truckJson,
        long executeAt,
        long? expireAt,
        long now,
        ulong randomValue) =>
        ScheduleTruckPlunderCore(serverId, trainUuid, truckJson, executeAt, expireAt, now, randomValue);

    internal static string CreateTruckPlunderJobId(long unixTimeMilliseconds, ulong randomValue)
    {
        if (unixTimeMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(unixTimeMilliseconds));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"truck-{unixTimeMilliseconds}-{randomValue:x}");
    }

    internal static string CreateLegacyTruckPlunderJobId(int serverId, string trainUuid, long createdAt)
    {
        ValidateServerId(serverId);
        if (string.IsNullOrWhiteSpace(trainUuid))
            throw new BridgeCommandException("INVALID_TARGET", "truck target is required");
        return string.Create(
            CultureInfo.InvariantCulture,
            $"legacy-{serverId}-{trainUuid}-{createdAt}");
    }

    private TruckPlunderScheduleResult ScheduleTruckPlunderCore(
        int serverId,
        string trainUuid,
        string truckJson,
        long executeAt,
        long? expireAt,
        long now,
        ulong randomValue)
    {
        ValidateServerId(serverId);
        if (string.IsNullOrWhiteSpace(trainUuid))
            throw new BridgeCommandException("INVALID_TARGET", "truck target is required");
        if (executeAt < 0 || now < 0 || (expireAt.HasValue && expireAt.Value < 0))
            throw new BridgeCommandException("INVALID_MAP_DATA", "invalid truck plunder schedule time");

        JsonObject nextRow = ParseTruckPlunderJson(truckJson);
        nextRow.Remove("battleWon");
        nextRow.Remove("plunderRewards");
        nextRow.Remove("plunderRewardsComplete");
        string jobId = CreateTruckPlunderJobId(now, randomValue);
        nextRow["jobId"] = jobId;
        string nextJson = nextRow.ToJsonString(JsonOptions.Default);

        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            string? previousJson = null;
            long previousExecuteAt = 0;
            string? previousStatus = null;
            int previousAttempts = 0;
            string? previousLastError = null;
            long previousCreatedAt = 0;
            long previousUpdatedAt = 0;
            using (SqliteCommand read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = """
                    SELECT truck_json,execute_at,status,attempts,last_error,created_at,updated_at
                    FROM truck_plunder_jobs WHERE server_id=$server AND train_uuid=$uuid
                    """;
                read.Parameters.AddWithValue("$server", serverId);
                read.Parameters.AddWithValue("$uuid", trainUuid);
                using SqliteDataReader reader = read.ExecuteReader();
                if (reader.Read())
                {
                    previousJson = reader.GetString(0);
                    previousExecuteAt = reader.GetInt64(1);
                    previousStatus = reader.GetString(2);
                    previousAttempts = reader.GetInt32(3);
                    previousLastError = reader.IsDBNull(4) ? null : reader.GetString(4);
                    previousCreatedAt = reader.GetInt64(5);
                    previousUpdatedAt = reader.GetInt64(6);
                }
            }

            bool archivedPreviousAttempt = false;
            if (previousJson is not null &&
                previousStatus is "succeeded" or "failed" or "cancelled" or "expired")
            {
                JsonObject previousRow = ParseTruckPlunderJson(previousJson);
                string? previousJobId = ReadOptionalJsonString(previousRow, "jobId");
                if (string.IsNullOrWhiteSpace(previousJobId))
                {
                    previousJobId = CreateLegacyTruckPlunderJobId(serverId, trainUuid, previousCreatedAt);
                    previousRow["jobId"] = previousJobId;
                    previousJson = previousRow.ToJsonString(JsonOptions.Default);
                }

                using SqliteCommand archive = connection.CreateCommand();
                archive.Transaction = transaction;
                archive.CommandText = """
                    INSERT INTO truck_plunder_history(
                      job_id,server_id,train_uuid,truck_json,execute_at,status,
                      attempts,last_error,created_at,updated_at
                    ) VALUES ($job,$server,$uuid,$json,$execute,$status,$attempts,$error,$created,$updated)
                    ON CONFLICT(job_id) DO NOTHING
                    """;
                archive.Parameters.AddWithValue("$job", previousJobId);
                archive.Parameters.AddWithValue("$server", serverId);
                archive.Parameters.AddWithValue("$uuid", trainUuid);
                archive.Parameters.AddWithValue("$json", previousJson);
                archive.Parameters.AddWithValue("$execute", previousExecuteAt);
                archive.Parameters.AddWithValue("$status", previousStatus);
                archive.Parameters.AddWithValue("$attempts", previousAttempts);
                archive.Parameters.AddWithValue("$error", (object?)previousLastError ?? DBNull.Value);
                archive.Parameters.AddWithValue("$created", previousCreatedAt);
                archive.Parameters.AddWithValue("$updated", previousUpdatedAt);
                archive.ExecuteNonQuery();
                archivedPreviousAttempt = true;
            }

            using (SqliteCommand upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = """
                    INSERT INTO truck_plunder_jobs(
                      server_id,train_uuid,truck_json,execute_at,expire_at,
                      status,attempts,last_error,created_at,updated_at
                    ) VALUES ($server,$uuid,$json,$execute,$expire,'scheduled',0,NULL,$now,$now)
                    ON CONFLICT(server_id,train_uuid) DO UPDATE SET
                      truck_json=excluded.truck_json,
                      execute_at=excluded.execute_at,
                      expire_at=excluded.expire_at,
                      status='scheduled',
                      attempts=CASE WHEN truck_plunder_jobs.status IN ('scheduled','waiting_connection')
                          THEN truck_plunder_jobs.attempts ELSE 0 END,
                      last_error=NULL,
                      updated_at=excluded.updated_at
                    WHERE truck_plunder_jobs.status<>'running'
                    """;
                upsert.Parameters.AddWithValue("$server", serverId);
                upsert.Parameters.AddWithValue("$uuid", trainUuid);
                upsert.Parameters.AddWithValue("$json", nextJson);
                upsert.Parameters.AddWithValue("$execute", executeAt);
                upsert.Parameters.AddWithValue("$expire", (object?)expireAt ?? DBNull.Value);
                upsert.Parameters.AddWithValue("$now", now);
                upsert.ExecuteNonQuery();
            }

            int attempts;
            string status;
            string storedJobId;
            using (SqliteCommand verify = connection.CreateCommand())
            {
                verify.Transaction = transaction;
                verify.CommandText = """
                    SELECT truck_json,status,attempts
                    FROM truck_plunder_jobs WHERE server_id=$server AND train_uuid=$uuid
                    """;
                verify.Parameters.AddWithValue("$server", serverId);
                verify.Parameters.AddWithValue("$uuid", trainUuid);
                using SqliteDataReader reader = verify.ExecuteReader();
                if (!reader.Read())
                {
                    transaction.Rollback();
                    throw new BridgeCommandException("MAP_DATA_ERROR", "scheduled truck job is missing");
                }

                JsonObject stored = ParseTruckPlunderJson(reader.GetString(0));
                status = reader.GetString(1);
                attempts = reader.GetInt32(2);
                storedJobId = ReadOptionalJsonString(stored, "jobId") ?? string.Empty;
            }

            if (!string.Equals(status, "scheduled", StringComparison.Ordinal) ||
                !string.Equals(storedJobId, jobId, StringComparison.Ordinal))
            {
                transaction.Rollback();
                throw new BridgeCommandException("MAP_DATA_ERROR", "scheduled truck job is missing");
            }

            transaction.Commit();
            return new TruckPlunderScheduleResult(jobId, attempts, archivedPreviousAttempt);
        }
    }

    private static string? ReadOptionalJsonString(JsonObject row, string name) =>
        row[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static JsonObject ParseTruckPlunderJson(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new BridgeCommandException("MAP_DATA_ERROR", "invalid truck plunder job");
        }
        catch (JsonException ex)
        {
            throw new BridgeCommandException("MAP_DATA_ERROR", "invalid truck plunder job", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BridgeCommandException("MAP_DATA_ERROR", "invalid truck plunder job", ex.Message);
        }
    }

    internal bool RecordTruckPlunderSuccess(
        int serverId,
        string trainUuid,
        bool battleWon,
        JsonElement plunderRewards,
        bool rewardNormalizationComplete,
        long updatedAt)
    {
        ValidateServerId(serverId);
        if (string.IsNullOrWhiteSpace(trainUuid))
            throw new BridgeCommandException("INVALID_TARGET", "truck target is required");
        if (plunderRewards.ValueKind != JsonValueKind.Array)
            throw new BridgeCommandException("INVALID_MAP_DATA", "truck plunder rewards must be an array");

        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            string? truckJson;
            using (SqliteCommand read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = """
                    SELECT truck_json
                    FROM truck_plunder_jobs WHERE server_id=$server AND train_uuid=$uuid
                    """;
                read.Parameters.AddWithValue("$server", serverId);
                read.Parameters.AddWithValue("$uuid", trainUuid);
                truckJson = read.ExecuteScalar() as string;
            }
            if (truckJson is null)
            {
                transaction.Rollback();
                return false;
            }

            JsonObject row;
            try
            {
                row = JsonNode.Parse(truckJson) as JsonObject
                    ?? throw new BridgeCommandException("MAP_DATA_ERROR", "scheduled truck job contains invalid JSON");
            }
            catch (JsonException ex)
            {
                throw new BridgeCommandException(
                    "MAP_DATA_ERROR",
                    "scheduled truck job contains invalid JSON",
                    ex.Message);
            }

            row["battleWon"] = battleWon;
            row["plunderRewards"] = JsonNode.Parse(plunderRewards.GetRawText());
            // Rebuild-only diagnostic metadata. The recovered frontend ignores it,
            // while durable execution can distinguish complete display normalization
            // from a successful attack whose reward metadata was only partially resolved.
            row["plunderRewardsComplete"] = rewardNormalizationComplete;

            using (SqliteCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE truck_plunder_jobs
                    SET truck_json=$json,status='succeeded',last_error=NULL,updated_at=$updated
                    WHERE server_id=$server AND train_uuid=$uuid
                    """;
                update.Parameters.AddWithValue("$server", serverId);
                update.Parameters.AddWithValue("$uuid", trainUuid);
                update.Parameters.AddWithValue("$json", row.ToJsonString(JsonOptions.Default));
                update.Parameters.AddWithValue("$updated", updatedAt);
                if (update.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            transaction.Commit();
            return true;
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
