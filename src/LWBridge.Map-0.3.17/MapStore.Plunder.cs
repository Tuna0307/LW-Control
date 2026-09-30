using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace LWBridge.Map317;

public sealed record TruckPlunderScheduleResult(
    string JobId,
    int Attempts,
    bool ArchivedPreviousAttempt);

public sealed record MapPlunderJobsSnapshot(
    IReadOnlyList<JsonElement> DispatchJobs,
    IReadOnlyList<JsonElement> TruckJobs,
    long? ServerDayStartAt = null);

internal sealed record DispatchPlunderWorkItem(
    long ServerId,
    string TaskUuid,
    JsonElement Task,
    long CompletionTime,
    long PlunderAt,
    long? ExpireAt,
    string Status,
    int Attempts,
    string? LastError,
    long CreatedAt,
    long UpdatedAt);

internal sealed record TruckPlunderWorkItem(
    long ServerId,
    string TrainUuid,
    JsonElement Truck,
    long ExecuteAt,
    long? ExpireAt,
    string Status,
    int Attempts,
    string? LastError,
    long CreatedAt,
    long UpdatedAt);

public sealed partial class MapStore
{
    internal int ExpireDispatchPlunder(long now)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE dispatch_plunder_jobs
                SET status='expired',last_error='DISPATCH_PLUNDER_TASK_EXPIRED',updated_at=$now
                WHERE status IN ('scheduled','waiting_connection')
                  AND expire_at IS NOT NULL AND expire_at<=$now
                """;
            command.Parameters.AddWithValue("$now", now);
            return command.ExecuteNonQuery();
        }
    }

    internal int ExpireTruckPlunder(long now)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE truck_plunder_jobs
                SET status='expired',last_error='truck expired',updated_at=$now
                WHERE status IN ('scheduled','waiting_connection')
                  AND expire_at IS NOT NULL AND expire_at<=$now
                """;
            command.Parameters.AddWithValue("$now", now);
            return command.ExecuteNonQuery();
        }
    }

    internal int RecoverDispatchPlunderJobs(long now)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE dispatch_plunder_jobs
                SET status='waiting_connection',last_error='DISPATCH_PLUNDER_CLIENT_RESTARTED',updated_at=$now
                WHERE status='running'
                """;
            command.Parameters.AddWithValue("$now", now);
            return command.ExecuteNonQuery();
        }
    }

    internal int RecoverTruckPlunderJobs(long now)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE truck_plunder_jobs
                SET status='waiting_connection',last_error='client restarted',updated_at=$now
                WHERE status='running'
                """;
            command.Parameters.AddWithValue("$now", now);
            return command.ExecuteNonQuery();
        }
    }

    internal int MarkDueDispatchPlunderWaitingConnection(long now)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE dispatch_plunder_jobs
                SET status='waiting_connection',last_error='DISPATCH_PLUNDER_GAME_DISCONNECTED',updated_at=$now
                WHERE status='scheduled' AND plunder_at<=$now
                  AND (expire_at IS NULL OR expire_at>$now)
                """;
            command.Parameters.AddWithValue("$now", now);
            return command.ExecuteNonQuery();
        }
    }

    internal int MarkDueTruckPlunderWaitingConnection(long now, long leadMilliseconds)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE truck_plunder_jobs
                SET status='waiting_connection',last_error='game disconnected',updated_at=$now
                WHERE status='scheduled' AND execute_at<=$now+$lead
                  AND (expire_at IS NULL OR expire_at>$now)
                """;
            command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$lead", leadMilliseconds);
            return command.ExecuteNonQuery();
        }
    }

    internal DispatchPlunderWorkItem? ReadArmableDispatchPlunder(long now, long leadMilliseconds)
        => ReadArmableDispatchPlunderBatch(now, leadMilliseconds, 1).FirstOrDefault();

    internal IReadOnlyList<DispatchPlunderWorkItem> ReadArmableDispatchPlunderBatch(
        long now,
        long leadMilliseconds,
        int limit)
    {
        if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT server_id,task_uuid,task_json,completion_time,plunder_at,expire_at,
                       status,attempts,last_error,created_at,updated_at
                FROM dispatch_plunder_jobs
                WHERE status IN ('scheduled','waiting_connection') AND plunder_at<=$now+$lead
                  AND (expire_at IS NULL OR expire_at>$now)
                ORDER BY plunder_at ASC LIMIT $limit
                """;
            command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$lead", leadMilliseconds);
            command.Parameters.AddWithValue("$limit", limit);
            using SqliteDataReader reader = command.ExecuteReader();
            var result = new List<DispatchPlunderWorkItem>(limit);
            while (reader.Read())
            {
                result.Add(new DispatchPlunderWorkItem(
                    reader.GetInt64(0), reader.GetString(1), ParseObject(reader.GetString(2), "dispatch plunder job"),
                    reader.GetInt64(3), reader.GetInt64(4), reader.IsDBNull(5) ? null : reader.GetInt64(5),
                    reader.GetString(6), reader.GetInt32(7), reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.GetInt64(9), reader.GetInt64(10)));
            }
            return result;
        }
    }

    internal TruckPlunderWorkItem? ReadArmableTruckPlunder(long now, long leadMilliseconds)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT server_id,train_uuid,truck_json,execute_at,expire_at,
                       status,attempts,last_error,created_at,updated_at
                FROM truck_plunder_jobs
                WHERE status IN ('scheduled','waiting_connection') AND execute_at<=$now+$lead
                  AND (expire_at IS NULL OR expire_at>$now)
                ORDER BY execute_at ASC LIMIT 1
                """;
            command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$lead", leadMilliseconds);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            return new TruckPlunderWorkItem(
                reader.GetInt64(0), reader.GetString(1), ParseObject(reader.GetString(2), "truck plunder job"),
                reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetInt64(4), reader.GetString(5),
                reader.GetInt32(6), reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetInt64(8), reader.GetInt64(9));
        }
    }

    internal bool UpdateDispatchPlunderStatus(
        long serverId, string taskUuid, string status, string? lastError, bool incrementAttempts, long updatedAt)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE dispatch_plunder_jobs
                SET status=$status,last_error=$error,
                    attempts=attempts+CASE WHEN $increment THEN 1 ELSE 0 END,
                    updated_at=$updated
                WHERE server_id=$server AND task_uuid=$uuid
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", taskUuid);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$error", (object?)lastError ?? DBNull.Value);
            command.Parameters.AddWithValue("$increment", incrementAttempts);
            command.Parameters.AddWithValue("$updated", updatedAt);
            return command.ExecuteNonQuery() == 1;
        }
    }

    internal bool UpdateTruckPlunderStatus(
        long serverId, string trainUuid, string status, string? lastError, bool incrementAttempts, long updatedAt)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE truck_plunder_jobs
                SET status=$status,last_error=$error,
                    attempts=attempts+CASE WHEN $increment THEN 1 ELSE 0 END,
                    updated_at=$updated
                WHERE server_id=$server AND train_uuid=$uuid
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", trainUuid);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$error", (object?)lastError ?? DBNull.Value);
            command.Parameters.AddWithValue("$increment", incrementAttempts);
            command.Parameters.AddWithValue("$updated", updatedAt);
            return command.ExecuteNonQuery() == 1;
        }
    }

    internal int StopActiveDispatchPlunderAtDailyLimit(long updatedAt)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE dispatch_plunder_jobs
                SET status='failed',last_error='DISPATCH_PLUNDER_DAILY_LIMIT_REACHED',updated_at=$updated
                WHERE status IN ('scheduled','waiting_connection') AND task_uuid NOT LIKE 'ghost:%'
                """;
            command.Parameters.AddWithValue("$updated", updatedAt);
            return command.ExecuteNonQuery();
        }
    }

    internal bool RecordTruckPlunderResult(
        long serverId,
        string trainUuid,
        string jobId,
        bool success,
        string? lastError,
        long updatedAt,
        bool? battleWon,
        JsonElement? plunderRewards,
        int? robTimes,
        int? remainingLootCount,
        int? dailyRobCount)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            string? source;
            using (SqliteCommand read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT truck_json FROM truck_plunder_jobs WHERE server_id=$server AND train_uuid=$uuid";
                read.Parameters.AddWithValue("$server", serverId);
                read.Parameters.AddWithValue("$uuid", trainUuid);
                source = read.ExecuteScalar() as string;
            }
            if (source is null) { transaction.Rollback(); return false; }
            JsonObject row = ParseTruckPlunderJson(source);
            if (!string.Equals(ReadOptionalJsonString(row, "jobId"), jobId, StringComparison.Ordinal))
            {
                transaction.Rollback();
                return false;
            }
            row["jobId"] = jobId;
            if (battleWon.HasValue) row["battleWon"] = battleWon.Value;
            if (plunderRewards is JsonElement rewards) row["plunderRewards"] = JsonNode.Parse(rewards.GetRawText());
            if (robTimes.HasValue) row["robTimes"] = robTimes.Value;
            if (remainingLootCount.HasValue) row["remainingLootCount"] = remainingLootCount.Value;
            if (dailyRobCount.HasValue) row["dailyRobCount"] = dailyRobCount.Value;
            using SqliteCommand update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE truck_plunder_jobs
                SET truck_json=$json,status=$status,last_error=$error,updated_at=$updated
                WHERE server_id=$server AND train_uuid=$uuid
                """;
            update.Parameters.AddWithValue("$json", row.ToJsonString(new JsonSerializerOptions()));
            update.Parameters.AddWithValue("$status", success ? "succeeded" : "failed");
            update.Parameters.AddWithValue("$error", (object?)lastError ?? DBNull.Value);
            update.Parameters.AddWithValue("$updated", updatedAt);
            update.Parameters.AddWithValue("$server", serverId);
            update.Parameters.AddWithValue("$uuid", trainUuid);
            bool changed = update.ExecuteNonQuery() == 1;
            if (changed) transaction.Commit(); else transaction.Rollback();
            return changed;
        }
    }

    public MapPlunderJobsSnapshot ReadPlunderJobs()
    {
        lock (gate)
        {
            return new MapPlunderJobsSnapshot(
                ReadDispatchPlunderJobsLocked(),
                ReadTruckPlunderJobsLocked());
        }
    }

    public JsonElement? ScheduleDispatchPlunderRow(
        long serverId,
        string taskUuid,
        string taskJson,
        long completionTime,
        long plunderAt,
        long? expireAt,
        long now)
    {
        ValidatePlunderServerId(serverId);
        ValidateJsonObject(taskJson, "dispatch plunder job");

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO dispatch_plunder_jobs(
                  server_id,task_uuid,task_json,completion_time,plunder_at,expire_at,
                  status,attempts,last_error,created_at,updated_at
                ) VALUES ($server,$uuid,$json,$completion,$plunder,$expire,'scheduled',0,NULL,$now,$now)
                ON CONFLICT(server_id,task_uuid) DO UPDATE SET
                  task_json=excluded.task_json,
                  completion_time=excluded.completion_time,
                  plunder_at=excluded.plunder_at,
                  expire_at=excluded.expire_at,
                  status='scheduled',
                  last_error=NULL,
                  updated_at=excluded.updated_at
                WHERE dispatch_plunder_jobs.status IN ('scheduled','waiting_connection','failed','cancelled','expired')
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", taskUuid);
            command.Parameters.AddWithValue("$json", taskJson);
            command.Parameters.AddWithValue("$completion", completionTime);
            command.Parameters.AddWithValue("$plunder", plunderAt);
            command.Parameters.AddWithValue("$expire", (object?)expireAt ?? DBNull.Value);
            command.Parameters.AddWithValue("$now", now);
            if (command.ExecuteNonQuery() <= 0) return null;

            using SqliteCommand read = connection.CreateCommand();
            read.CommandText = """
                SELECT task_json,status,attempts,last_error,created_at,updated_at,plunder_at
                FROM dispatch_plunder_jobs
                WHERE server_id=$server AND task_uuid=$uuid
                """;
            read.Parameters.AddWithValue("$server", serverId);
            read.Parameters.AddWithValue("$uuid", taskUuid);
            using SqliteDataReader reader = read.ExecuteReader();
            if (!reader.Read()) return null;
            return ReadPlunderJobRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                executeAt: null,
                plunderAt: reader.GetInt64(6));
        }
    }

    public bool CancelDispatchPlunder(long serverId, string taskUuid, long updatedAt)
    {
        ValidatePlunderServerId(serverId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE dispatch_plunder_jobs
                SET status='cancelled',last_error=NULL,updated_at=$updated
                WHERE server_id=$server AND task_uuid=$uuid
                  AND status IN ('scheduled','waiting_connection')
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", taskUuid);
            command.Parameters.AddWithValue("$updated", updatedAt);
            return command.ExecuteNonQuery() > 0;
        }
    }

    public TruckPlunderScheduleResult ScheduleTruckPlunder(
        long serverId,
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
        long serverId,
        string trainUuid,
        string truckJson,
        long executeAt,
        long? expireAt,
        long now,
        ulong randomValue) =>
        ScheduleTruckPlunderCore(serverId, trainUuid, truckJson, executeAt, expireAt, now, randomValue);

    public static string CreateTruckPlunderJobId(long unixTimeMilliseconds, ulong randomValue)
    {
        if (unixTimeMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(unixTimeMilliseconds));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"truck-{unixTimeMilliseconds}-{randomValue:x}");
    }

    private static string CreateLegacyTruckPlunderJobId(long serverId, string trainUuid, long createdAt)
    {
        ValidatePlunderServerId(serverId);
        if (string.IsNullOrWhiteSpace(trainUuid))
            throw new BridgeCommandException("INVALID_REQUEST", "invalid truck plunder job");
        return string.Create(
            CultureInfo.InvariantCulture,
            $"legacy-{serverId}-{trainUuid}-{createdAt}");
    }

    private TruckPlunderScheduleResult ScheduleTruckPlunderCore(
        long serverId,
        string trainUuid,
        string truckJson,
        long executeAt,
        long? expireAt,
        long now,
        ulong randomValue)
    {
        ValidatePlunderServerId(serverId);
        if (string.IsNullOrWhiteSpace(trainUuid) ||
            executeAt <= 0 ||
            now < 0 ||
            (expireAt.HasValue && expireAt.Value <= 0))
        {
            throw new BridgeCommandException("INVALID_REQUEST", "invalid truck plunder job");
        }

        JsonObject nextRow = ParseTruckPlunderJson(truckJson);
        nextRow.Remove("battleWon");
        nextRow.Remove("plunderRewards");
        nextRow.Remove("plunderRewardsComplete");
        string jobId = CreateTruckPlunderJobId(now, randomValue);
        nextRow["jobId"] = jobId;
        string nextJson = nextRow.ToJsonString(new JsonSerializerOptions());

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
                    previousJson = previousRow.ToJsonString(new JsonSerializerOptions());
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

    public bool CancelTruckPlunder(long serverId, string trainUuid, long updatedAt)
    {
        ValidatePlunderServerId(serverId);
        if (string.IsNullOrWhiteSpace(trainUuid))
            throw new BridgeCommandException("INVALID_REQUEST", "invalid truck plunder job");

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE truck_plunder_jobs
                SET status='cancelled',last_error=NULL,updated_at=$updated
                WHERE server_id=$server AND train_uuid=$uuid
                  AND status IN ('scheduled','waiting_connection')
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", trainUuid);
            command.Parameters.AddWithValue("$updated", updatedAt);
            return command.ExecuteNonQuery() > 0;
        }
    }

    public int ClearDispatchPlunderHistory(long before, string? taskKind)
    {
        if (before <= 0)
            throw new BridgeCommandException("INVALID_REQUEST", "clear cutoff is required");
        if (taskKind is not null && taskKind is not ("dispatch" or "ghost"))
            throw new BridgeCommandException("INVALID_REQUEST", "invalid plunder task kind");
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM dispatch_plunder_jobs
                WHERE status IN ('succeeded','failed','cancelled','expired') AND updated_at<$before
                  AND ($kind IS NULL OR ($kind='ghost' AND task_uuid LIKE 'ghost:%')
                    OR ($kind='dispatch' AND task_uuid NOT LIKE 'ghost:%'))
                """;
            command.Parameters.AddWithValue("$before", before);
            command.Parameters.AddWithValue("$kind", (object?)taskKind ?? DBNull.Value);
            return command.ExecuteNonQuery();
        }
    }

    public int ClearTruckPlunderHistory(long before)
    {
        if (before <= 0)
            throw new BridgeCommandException("INVALID_REQUEST", "clear cutoff is required");
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            int deleted;
            using (SqliteCommand current = connection.CreateCommand())
            {
                current.Transaction = transaction;
                current.CommandText = """
                    DELETE FROM truck_plunder_jobs
                    WHERE status IN ('succeeded','failed','cancelled','expired') AND updated_at<$before
                    """;
                current.Parameters.AddWithValue("$before", before);
                deleted = current.ExecuteNonQuery();
            }
            using (SqliteCommand archived = connection.CreateCommand())
            {
                archived.Transaction = transaction;
                archived.CommandText = "DELETE FROM truck_plunder_history WHERE updated_at<$before";
                archived.Parameters.AddWithValue("$before", before);
                deleted += archived.ExecuteNonQuery();
            }
            transaction.Commit();
            return deleted;
        }
    }

    public bool RetryDispatchPlunder(long serverId, string taskUuid, long updatedAt)
    {
        ValidatePlunderServerId(serverId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE dispatch_plunder_jobs SET status='scheduled',last_error=NULL,updated_at=$updated
                WHERE server_id=$server AND task_uuid=$uuid AND status IN ('failed','expired')
                """;
            command.Parameters.AddWithValue("$server", serverId);
            command.Parameters.AddWithValue("$uuid", taskUuid);
            command.Parameters.AddWithValue("$updated", updatedAt);
            return command.ExecuteNonQuery() > 0;
        }
    }

    public TruckPlunderScheduleResult RetryTruckPlunder(long serverId, string trainUuid, long now)
    {
        ValidatePlunderServerId(serverId);
        lock (gate)
        {
            string json;
            string status;
            long? expireAt = null;
            using (SqliteCommand read = connection.CreateCommand())
            {
                read.CommandText = "SELECT truck_json,status,expire_at FROM truck_plunder_jobs WHERE server_id=$server AND train_uuid=$uuid";
                read.Parameters.AddWithValue("$server", serverId);
                read.Parameters.AddWithValue("$uuid", trainUuid);
                using SqliteDataReader reader = read.ExecuteReader();
                if (!reader.Read()) throw new BridgeCommandException("NOT_FOUND", "scheduled truck job not found");
                json = reader.GetString(0);
                status = reader.GetString(1);
                if (!reader.IsDBNull(2)) expireAt = reader.GetInt64(2);
            }
            if (status is not ("failed" or "expired"))
                throw new BridgeCommandException("NOT_FOUND", "scheduled truck job not found");
            return ScheduleTruckPlunder(serverId, trainUuid, json, now, expireAt, now);
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

            using JsonDocument document = JsonDocument.Parse(row.ToJsonString(new JsonSerializerOptions()));
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

    private static string? ReadOptionalJsonString(JsonObject row, string name) =>
        row[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static JsonElement ParseObject(string json, string context)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new BridgeCommandException("MAP_DATA_ERROR", $"invalid {context}");
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new BridgeCommandException("MAP_DATA_ERROR", $"invalid {context}", ex.Message);
        }
    }

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

    private static void ValidatePlunderServerId(long serverId)
    {
        if (serverId <= 0)
            throw new BridgeCommandException("INVALID_SERVER_ID", "serverId must be a positive integer.");
    }
}
