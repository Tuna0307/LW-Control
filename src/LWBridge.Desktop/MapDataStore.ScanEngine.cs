using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop;

internal sealed partial class MapDataStore
{
    internal void BeginEngineScan(MapScanExecutionRequest request, int totalBlocks, long updatedAt)
    {
        InsertScanRun(new MapScanRunSeed(
            request.RunId,
            request.ServerId,
            JsonSerializer.Serialize(request.SelectedTypes),
            "running",
            totalBlocks,
            0,
            0,
            updatedAt,
            updatedAt,
            null));
    }

    internal void CommitEngineBlockSuccess(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        MapScanBlockCapture capture,
        int attempts,
        long updatedAt)
    {
        foreach (MapStoredRecord record in capture.Records) ValidateRecord(record);
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            UpsertEngineBlock(transaction, request.RunId, block.BlockIndex, capture.PayloadJson,
                "completed", attempts, null, updatedAt);
            foreach (MapStoredRecord record in capture.Records)
                UpsertEngineStagingRecord(transaction, request.RunId, record);
            RefreshEngineRunCounters(transaction, request.RunId, updatedAt);
            transaction.Commit();
        }
    }

    internal void CommitEngineBlockSuccessBatch(
        MapScanExecutionRequest request,
        IReadOnlyList<MapScanBlockSuccess> successes,
        int attempts,
        long updatedAt)
    {
        if (successes.Count == 0) throw new ArgumentException("Batch success list must not be empty.", nameof(successes));
        foreach (MapScanBlockSuccess success in successes)
            foreach (MapStoredRecord record in success.Capture.Records)
                ValidateRecord(record);
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            foreach (MapScanBlockSuccess success in successes)
            {
                UpsertEngineBlock(transaction, request.RunId, success.Block.BlockIndex, success.Capture.PayloadJson,
                    "completed", attempts, null, updatedAt);
                foreach (MapStoredRecord record in success.Capture.Records)
                    UpsertEngineStagingRecord(transaction, request.RunId, record);
            }
            RefreshEngineRunCounters(transaction, request.RunId, updatedAt);
            transaction.Commit();
        }
    }

    internal void CommitEngineBlockFailure(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        int attempts,
        string error,
        long updatedAt)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            UpsertEngineBlock(transaction, request.RunId, block.BlockIndex, "{}",
                "failed", attempts, error, updatedAt);
            RefreshEngineRunCounters(transaction, request.RunId, updatedAt);
            transaction.Commit();
        }
    }

    internal void PublishEngineScan(MapScanExecutionRequest request, long updatedAt)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            (string Status, int Total, int Completed, int Failed, int ServerId) run =
                ReadEngineRun(transaction, request.RunId);
            if (run.ServerId != request.ServerId || run.Status != "running")
                throw new BridgeCommandException("INVALID_SCAN", "map scan is not running");
            MapScanCompletionSafety.ValidateDirectCompletion(run.Total, run.Completed, run.Failed);

            foreach (string kind in request.SelectedTypes)
            {
                if (kind is "monster" or "zombie_boss")
                    CarryForwardUnresolvedMonsterProtection(
                        transaction, request.RunId, kind, request.ServerId, updatedAt);
                DeletePublishedEngineKind(transaction, kind, request.ServerId);
                CopyEngineStagingKind(transaction, request.RunId, kind, request.ServerId);
            }

            using (SqliteCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE scan_runs SET status='completed',error=NULL,updated_at=$updated WHERE id=$run AND status='running'";
                update.Parameters.AddWithValue("$updated", updatedAt);
                update.Parameters.AddWithValue("$run", request.RunId);
                MapScanPublicationOwnership.ValidateCompletionTransition(update.ExecuteNonQuery());
            }

            using (SqliteCommand cleanupRecords = connection.CreateCommand())
            {
                cleanupRecords.Transaction = transaction;
                cleanupRecords.CommandText = "DELETE FROM scan_records WHERE run_id=$run";
                cleanupRecords.Parameters.AddWithValue("$run", request.RunId);
                cleanupRecords.ExecuteNonQuery();
            }
            using (SqliteCommand cleanupBlocks = connection.CreateCommand())
            {
                cleanupBlocks.Transaction = transaction;
                cleanupBlocks.CommandText = "DELETE FROM scan_blocks WHERE run_id=$run";
                cleanupBlocks.Parameters.AddWithValue("$run", request.RunId);
                cleanupBlocks.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        lock (gate)
        {
            using SqliteCommand verify = connection.CreateCommand();
            verify.CommandText = "SELECT COUNT(*) FROM scan_runs WHERE id=$run";
            verify.Parameters.AddWithValue("$run", request.RunId);
            MapScanPublicationOwnership.ValidatePostCommitRunPresent(Convert.ToInt32(verify.ExecuteScalar()) == 1);
        }
    }

    internal void FailEngineScan(MapScanExecutionRequest request, string error, long updatedAt) =>
        TransitionEngineRun(request.RunId, "failed", error, updatedAt);

    internal void StopEngineScan(MapScanExecutionRequest request, long updatedAt) =>
        TransitionEngineRun(request.RunId, "discarded", null, updatedAt);

    private void TransitionEngineRun(string runId, string status, string? error, long updatedAt)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE scan_runs SET status=$status,error=$error,updated_at=$updated WHERE id=$run AND status='running'";
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
            command.Parameters.AddWithValue("$updated", updatedAt);
            command.Parameters.AddWithValue("$run", runId);
            MapScanPublicationOwnership.ValidateCompletionTransition(command.ExecuteNonQuery());
        }
    }

    private void UpsertEngineBlock(
        SqliteTransaction transaction,
        string runId,
        int blockIndex,
        string payloadJson,
        string status,
        int attempts,
        string? error,
        long updatedAt)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO scan_blocks(run_id,block_index,payload_json,status,attempts,error,updated_at)
            VALUES ($run,$block,$payload,$status,$attempts,$error,$updated)
            ON CONFLICT(run_id,block_index) DO UPDATE SET
              payload_json=excluded.payload_json,status=excluded.status,
              attempts=excluded.attempts,error=excluded.error,updated_at=excluded.updated_at
            """;
        command.Parameters.AddWithValue("$run", runId);
        command.Parameters.AddWithValue("$block", blockIndex);
        command.Parameters.AddWithValue("$payload", payloadJson);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$attempts", attempts);
        command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", updatedAt);
        command.ExecuteNonQuery();
    }

    private void UpsertEngineStagingRecord(
        SqliteTransaction transaction,
        string runId,
        MapStoredRecord record)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
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

    private void RefreshEngineRunCounters(
        SqliteTransaction transaction,
        string runId,
        long updatedAt)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE scan_runs SET
              completed_blocks=(SELECT COUNT(*) FROM scan_blocks WHERE run_id=$run AND status='completed'),
              failed_blocks=(SELECT COUNT(*) FROM scan_blocks WHERE run_id=$run AND status='failed'),
              updated_at=$updated
            WHERE id=$run AND status='running'
            """;
        command.Parameters.AddWithValue("$run", runId);
        command.Parameters.AddWithValue("$updated", updatedAt);
        MapScanPublicationOwnership.ValidateCompletionTransition(command.ExecuteNonQuery());
    }
    private (string Status, int Total, int Completed, int Failed, int ServerId) ReadEngineRun(
        SqliteTransaction transaction,
        string runId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT status,total_blocks,completed_blocks,failed_blocks,server_id
            FROM scan_runs WHERE id=$run
            """;
        command.Parameters.AddWithValue("$run", runId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
            throw new BridgeCommandException("INVALID_SCAN", "map scan is not running");
        return (
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4));
    }

    private void CarryForwardUnresolvedMonsterProtection(
        SqliteTransaction transaction,
        string runId,
        string kind,
        int serverId,
        long updatedAt)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE scan_records
            SET shield_end_time=(
                  SELECT old.shield_end_time
                  FROM map_records old
                  WHERE old.kind=$kind
                    AND old.server_id=scan_records.server_id
                    AND old.record_key=scan_records.record_key
                  LIMIT 1
                ),
                data_json=json_set(
                  data_json,
                  '$.monsterProtectionKnown',json('true'),
                  '$.monsterProtectionActive',json('true'),
                  '$.monsterProtectionEndTime',(
                    SELECT old.shield_end_time
                    FROM map_records old
                    WHERE old.kind=$kind
                      AND old.server_id=scan_records.server_id
                      AND old.record_key=scan_records.record_key
                    LIMIT 1
                  ),
                  '$.shieldEndTime',(
                    SELECT old.shield_end_time
                    FROM map_records old
                    WHERE old.kind=$kind
                      AND old.server_id=scan_records.server_id
                      AND old.record_key=scan_records.record_key
                    LIMIT 1
                  )
                )
            WHERE run_id=$run
              AND kind=$kind
              AND server_id=$server
              AND COALESCE(json_extract(data_json,'$.monsterProtectionEligible'),0)=1
              AND COALESCE(json_extract(data_json,'$.monsterProtectionKnown'),0)=0
              AND EXISTS (
                SELECT 1
                FROM map_records old
                WHERE old.kind=$kind
                  AND old.server_id=scan_records.server_id
                  AND old.record_key=scan_records.record_key
                  AND old.shield_end_time IS NOT NULL
                  AND (CASE
                         WHEN old.shield_end_time < 100000000000
                           THEN old.shield_end_time * 1000
                         ELSE old.shield_end_time
                       END) > $updated
                  AND COALESCE(json_extract(old.data_json,'$.monsterProtectionKnown'),0)=1
                  AND COALESCE(json_extract(old.data_json,'$.monsterProtectionActive'),0)=1
              )
            """;
        command.Parameters.AddWithValue("$run", runId);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$server", serverId);
        command.Parameters.AddWithValue("$updated", updatedAt);
        command.ExecuteNonQuery();
    }

    private void DeletePublishedEngineKind(
        SqliteTransaction transaction,
        string kind,
        int serverId)
    {
        ValidateKind(kind);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM map_records WHERE kind=$kind AND server_id=$server";
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$server", serverId);
        command.ExecuteNonQuery();
    }
    private void CopyEngineStagingKind(
        SqliteTransaction transaction,
        string runId,
        string kind,
        int serverId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO map_records(kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json)
            SELECT kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json
            FROM scan_records
            WHERE run_id=$run AND kind=$kind AND server_id=$server
            """;
        command.Parameters.AddWithValue("$run", runId);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$server", serverId);
        command.ExecuteNonQuery();
    }
}
