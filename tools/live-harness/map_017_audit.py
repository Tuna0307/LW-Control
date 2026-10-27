"""Read-only MAP017 packaged evidence auditor; never launches app/game.

Single receipt: python tools/live-harness/map_017_audit.py --root <session> --mode boss
Aggregate: python tools/live-harness/map_017_audit.py --aggregate --root <session> --root ...
Zero boss sources are observations and cannot satisfy positive acceptance.
"""
from __future__ import annotations

import argparse
from contextlib import closing
from datetime import datetime
import hashlib
import json
from pathlib import Path
import sqlite3

import map_015_package_ui as previous

PROFILE = "map014-native-ui"
FILES = {"data": "LWScripts.data", "metadata": "LWScripts.txt", "version": "version.txt"}


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def digest(path):
    return previous.sha256(Path(path))


def same_path(left, right):
    return isinstance(left, str) and isinstance(right, (str, Path)) and Path(left).resolve() == Path(right).resolve()


def png(root, name):
    path = Path(root) / name
    with path.open("rb") as stream:
        signature = stream.read(24)
    require(signature[:8] == b"\x89PNG\r\n\x1a\n" and signature[12:16] == b"IHDR" and
            int.from_bytes(signature[16:20], "big") > 0 and
            int.from_bytes(signature[20:24], "big") > 0, "Missing or invalid packaged screenshot: " + name)
    return {"path": str(path), "sha256": digest(path)}


def milliseconds(text):
    parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    require(parsed.tzinfo is not None, "Unzoned native evidence timestamp")
    return int(parsed.timestamp() * 1000)


def audit_restoration(root):
    """Reconcile captured exit audit with same-session host/helper receipts.

    This checks historical artifact evidence; it does not claim a fresh read of
    installed game scripts or the current operating-system process inventory.
    """
    root = Path(root)
    outcome, owned = read(root / "owned-exit.json"), read(root / "owned-process.json")
    restored = outcome.get("restorationAudit", {})
    require(outcome.get("exitCode") == 0 and outcome.get("proof") is True and outcome.get("error") is None and
            outcome.get("restorationAuditError") is None and restored.get("state") == "proven" and
            restored.get("diagnosticResultSucceeded") is True and outcome.get("pid") == owned.get("pid") == restored.get("hostPid") and
            type(owned.get("pid")) is int and owned["pid"] > 0 and
            outcome.get("recoveryJournalPresent") is False and restored.get("recoveryJournalPresent") is False,
            "Exact-owned exit/restoration audit failed")
    require(restored.get("profileId") == PROFILE and type(restored.get("gamePid")) is int and restored["gamePid"] > 0 and
            isinstance(restored.get("sessionId"), str) and restored["sessionId"], "Restoration owner missing")
    for check in (outcome.get("originalScriptsRestored"), restored.get("originalScriptsRestored")):
        require(isinstance(check, dict) and set(check) == set(FILES.values()) and all(v is True for v in check.values()),
                "Incomplete original-script restoration audit")
    hashes = restored.get("capturedOriginalHashes", {})
    require(set(hashes) == set(FILES.values()) and all(isinstance(v, str) and len(v) == 64 for v in hashes.values()) and
            restored.get("installedHashes") == hashes, "Captured and installed original hashes differ")
    directory = root / "overview-evidence" / restored["sessionId"]
    starts = list((root / "overview-evidence").glob("*/helper-start.json"))
    require(starts == [directory / "helper-start.json"], "Fresh packaged proof requires exactly one owned game launch")
    start, stop, host_start, host_stop = [read(directory / name) for name in
        ("helper-start.json", "helper-stop.json", "host-start.json", "host-stop.json")]
    for item in (start, stop, host_start, host_stop):
        require(item.get("profileId") == PROFILE and item.get("sessionId") == restored["sessionId"], "Foreign helper/host session")
    require(start.get("ok") is True and same_path(host_start.get("hostProcess", {}).get("path"), owned.get("publish")) and
            isinstance(start.get("challengeSha256"), str) and len(start["challengeSha256"]) == 64,
            "Helper start or native host identity incomplete")
    for item in (start, stop, host_start):
        require(item.get("gamePid") == restored["gamePid"] and item.get("gameStartedAtUtc") == start.get("gameStartedAtUtc") and
                item.get("gamePath") == start.get("gamePath"), "Game identity was replaced")
    close, restore = stop.get("close", {}), stop.get("restore", {})
    require(stop.get("ok") is True and stop.get("gameRunning") is False and close.get("accepted") is True and
            close.get("processExited") is True and close.get("exitProof", {}).get("established") is True and
            close.get("pid") == restored["gamePid"] and close.get("path") == start.get("gamePath") and
            close.get("startedAtUtc") == start.get("gameStartedAtUtc") and restore.get("restored") is True and
            host_stop.get("helper", {}).get("restore") == restore and
            host_stop.get("helper", {}).get("sessionId") == restored["sessionId"], "Exact Stop and same-session restore missing")
    for role, name in FILES.items():
        original, after = restore.get("originalFiles", {}).get(role, {}), restore.get("restoredFiles", {}).get(role, {})
        require(original == after and original.get("sha256") == hashes[name], "Restored file record differs: " + name)
    backups = [(path, read(path)) for path in (root / "overview-bridge-backups").glob("*/manifest.json")]
    backups = [(path, manifest) for path, manifest in backups if manifest.get("sessionId") == restored["sessionId"]]
    require(len(backups) == 1, "Exactly one original backup is required")
    manifest_path, manifest = backups[0]
    require(manifest.get("stage") == "restored" and manifest.get("originalFiles") == restore.get("originalFiles") and
            manifest.get("profileId") == PROFILE and manifest.get("gamePid") == restored["gamePid"] and
            manifest.get("gameStartedAtUtc") == start.get("gameStartedAtUtc") and
            manifest.get("challengeSha256") == start["challengeSha256"], "Original backup belongs to another session")
    for name in FILES.values():
        require(digest(manifest_path.parent / name) == hashes[name], "Preserved original backup hash mismatch: " + name)
    require(not (root / "overview-bridge/recovery.json").exists(), "Owned recovery journal remains")
    previous.audit_no_official_updater(root, restored)
    return dict(restored, challengeSha256=start["challengeSha256"], gameStartedAtUtc=start["gameStartedAtUtc"],
                helperStoppedAtUtc=host_stop["recordedAtUtc"], executable=owned["publish"],
                probeSourceSha256=start.get("candidate", {}).get("resourceProbeSourceSha256"),
                desktopAssemblySha256=host_start.get("desktopAssembly", {}).get("sha256"))


def database(root, owner):
    path = Path(root) / "profiles" / owner["profileId"] / "map-data/map-data.db"
    return closing(sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True))


def runs_from_db(connection):
    rows = connection.execute("SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error FROM scan_runs ORDER BY created_at,id").fetchall()
    return [dict(zip(("id", "server", "selected", "status", "total", "completed", "failed", "created", "updated", "error"), row)) for row in rows]


def completed_run(run, kinds, server):
    require(run["server"] == server and json.loads(run["selected"]) == kinds and run["status"] == "completed" and
            run["total"] == run["completed"] == 2500 and run["failed"] == 0 and run["error"] is None and
            type(run["created"]) is int and run["updated"] >= run["created"], "SQLite run is incomplete or selected wrong kinds/server")


def published_rows(connection, table, kind, server, run_id=None):
    # Table is internal fixed data, never CLI input.
    require(table in ("scan_records", "map_records"), "Unsupported table")
    condition, args = "kind=? AND server_id=?", [kind, server]
    if table == "scan_records":
        condition += " AND run_id=?"
        args.append(run_id)
    output = {}
    for key, uuid, raw in connection.execute(f"SELECT record_key,uuid,data_json FROM {table} WHERE {condition}", args):
        row = json.loads(raw)
        require(isinstance(uuid, str) and uuid and uuid == key == row.get("uuid") and
                row.get("kind") == kind and row.get("serverId") == server and
                type(row.get("x")) in (int, float) and 0 <= row["x"] <= 999 and
                type(row.get("y")) in (int, float) and 0 <= row["y"] <= 999 and uuid not in output,
                "Published identity/server/coordinates mismatch")
        output[uuid] = row
    return output


def search(stage, kind, server, expected_total):
    observation = stage.get("selectedResponse", {})
    payload, result = observation.get("payload", {}), observation.get("result", {})
    query = payload.get("query", {})
    require(payload.get("kind") == kind and query.get("serverId") == server and
            payload.get("profileId") == PROFILE and query.get("page") == 1 and result.get("total") == expected_total and
            isinstance(result.get("rows"), list) and len(result["rows"]) <= expected_total and
            stage.get("total") == expected_total, "Mounted UI search total/kind/server differs from SQLite")
    if expected_total:
        require(result["rows"], "Positive UI query has no first-page rows")
    return result["rows"]


def rendered_stage(stage, index, count):
    snapshot = stage.get("renderedSnapshot")
    if snapshot is None:
        # Early baseline observations retain their source/SQLite result, but
        # cannot count as final mounted-render acceptance without this join.
        return False
    require(snapshot.get("tablePresent") is True and snapshot.get("busy") in (False, "false") and
            snapshot.get("activeTabIndex") == index and snapshot.get("displayedRows") == min(count, 50) and
            snapshot.get("resultCountValue") == count, "Rendered tab/row/visible count differs from native SQLite query")
    return True


def persisted_auto(root):
    return read(Path(root) / "profiles" / PROFILE / "runtime/map-auto-scan.json")


def audit_native_boss(root, label, run, owner, db_rows):
    def source(name):
        retained = Path(root) / f"map017-{label}-{name}"
        # Early harness receipts predate sidecar copies. Terminal files are
        # admitted only after the exact run/session checks below, never by age.
        return retained if retained.is_file() else Path(root) / "live-resource" / name
    native_path = source("camera-free-scan-result.json")
    native = read(native_path)
    metrics = read(source("map-fullworld-probe-metrics.json"))
    require(native.get("state") == "proven" and native.get("scanRunId") == run["id"] and
            native.get("profileId") == owner["profileId"] and native.get("launchSessionId") == owner["sessionId"] and
            native.get("gamePid") == owner["gamePid"] and native.get("serverId") == 2212 and native.get("worldId") == 0 and
            isinstance(native.get("challenge"), str) and
            hashlib.sha256(native["challenge"].encode()).hexdigest() == owner["challengeSha256"] and
            native.get("cameraStable") is True and native.get("curViewRestored") is True and
            native.get("completedAois") == 10000 and native.get("repliesCaptured") == 63 and
            type(native.get("requestsSent")) is int and 63 <= native["requestsSent"] <= 65,
            "Boss native source owner/coverage/restoration mismatch")
    captured = milliseconds(native["capturedAt"])
    require(milliseconds(owner["gameStartedAtUtc"]) <= captured <= milliseconds(owner["helperStoppedAtUtc"]) and
            run["created"] - 2000 <= captured <= run["updated"] + 2000, "Boss native receipt is stale")
    identity = metrics.get("identity", {})
    require(metrics.get("phase") == "prepared" and metrics.get("source") == "CurrentClientMapBlockSource.ProbeCameraFreeMapAsync" and
            identity.get("RunId") == run["id"] and identity.get("ServerId") == 2212 and
            identity.get("SelectedTypesKey") == "zombie_boss" and metrics.get("uniqueCoveredAois") == 10000 and
            metrics.get("validatedProbeResponses") == 63 and metrics.get("attributableLuaProbeAttempts") == native["requestsSent"],
            "Boss prepared source metrics mismatch")
    covered, bosses = set(), {}
    batches = native.get("batches", [])
    require(len(batches) == 63, "Missing raw native batches")
    for batch in batches:
        indices = batch.get("indices", [])
        require(1 <= len(indices) <= 160 and all(type(i) is int and 0 <= i < 10000 and i not in covered for i in indices) and
                len(indices) == len(set(indices)), "Missing, duplicate or invalid native AOIs")
        covered.update(indices)
        for row in batch.get("monster_march_records", []):
            # Game MonsterInvasionBoss special10 is independent of the emitted
            # isBoss flag whose first-use failure MAP017 fixes.
            if row.get("configSpecial") == 10:
                uuid = row.get("uuid")
                require(isinstance(uuid, str) and uuid and row.get("serverId") == 2212, "Malformed raw native Boss identity")
                bosses[uuid] = row
    require(len(covered) == 10000, "Raw native source misses AOIs")
    equal = set(bosses) == set(db_rows)
    require(equal or not db_rows, "Raw special10 Boss UUID set differs from SQLite publication")
    for uuid, row in db_rows.items():
        require(row.get("configSpecial") == 10 and row["x"] == bosses[uuid].get("x") and row["y"] == bosses[uuid].get("y"),
                "Published Boss fields differ from exact raw source")
    return dict(sourceBossCount=len(bosses), sourcePublicationIdentityMatches=equal,
                uuidSetSha256=hashlib.sha256("\n".join(sorted(bosses)).encode()).hexdigest(),
                nativeSourceSha256=digest(native_path), coverage=10000)


def audit_resource_first(root, stage, owner, connection, boss_run):
    """The next same-server publication prunes the Resource scan_runs row.

    Require its immediate SQLite snapshot AND retained independent raw source,
    prepared metric identity and surviving Resource publication, never just its
    self-reported count. This follows MapStore.CommitScan's per-server pruning.
    """
    snapshot = stage.get("persisted", {})
    run = dict(id=snapshot.get("id"), server=snapshot.get("serverId"), selected=json.dumps(snapshot.get("selectedTypes")),
        status=snapshot.get("status"), total=snapshot.get("totalBlocks"), completed=snapshot.get("completedBlocks"),
        failed=snapshot.get("failedBlocks"), created=snapshot.get("createdAt"), updated=snapshot.get("updatedAt"), error=snapshot.get("error"))
    completed_run(run, ["resource"], 2212)
    require(run["id"] != boss_run["id"] and run["updated"] <= boss_run["created"] and
            stage.get("completedStatus", {}).get("scanRunId") == run["id"] and stage.get("selectedTypes") == ["resource"],
            "Resource-first run snapshot did not precede the Boss scan")
    native_path = Path(root) / "map017-resource-first-camera-free-scan-result.json"
    native = read(native_path)
    metrics = read(Path(root) / "map017-resource-first-map-fullworld-probe-metrics.json")
    require(native.get("state") == "proven" and native.get("scanRunId") == run["id"] and
            native.get("profileId") == owner["profileId"] and native.get("launchSessionId") == owner["sessionId"] and
            native.get("gamePid") == owner["gamePid"] and native.get("serverId") == 2212 and
            native.get("cameraStable") is True and native.get("curViewRestored") is True and
            hashlib.sha256(native.get("challenge", "").encode()).hexdigest() == owner["challengeSha256"] and
            native.get("completedAois") == 10000 and native.get("repliesCaptured") == 63 and
            metrics.get("phase") == "prepared" and metrics.get("identity", {}).get("RunId") == run["id"] and
            metrics.get("identity", {}).get("ServerId") == 2212 and metrics.get("identity", {}).get("SelectedTypesKey") == "resource",
            "Resource-first native source/metrics ownership mismatch")
    captured = milliseconds(native["capturedAt"])
    require(run["created"] - 2000 <= captured <= run["updated"] + 2000 and
            milliseconds(owner["gameStartedAtUtc"]) <= captured, "Resource-first source is stale")
    indices = [index for batch in native.get("batches", []) for index in batch.get("indices", [])]
    require(len(indices) == 10000 and set(indices) == set(range(10000)), "Resource-first source misses/duplicates AOIs")
    source = {}
    for batch in native["batches"]:
        for row in batch.get("point_records", []):
            if row.get("kind") == "resource_point":
                require(row.get("serverId") == 2212 and type(row.get("pointId")) is int, "Malformed Resource-first native identity")
                source[str(row["pointId"])] = row
    published = {key: json.loads(raw) for key, raw in connection.execute(
        "SELECT record_key,data_json FROM map_records WHERE kind='resource' AND server_id=2212")}
    require(source and set(source) == set(published), "Resource-first raw identities differ from surviving SQLite publication")
    search(stage, "resource", 2212, len(published))
    png(root, "map017-resource-first-selected.png")
    png(root, "map017-resource-first-results.png")
    return rendered_stage(stage, 1, len(published))


def audit_boss(root, proof, owner):
    mode = proof.get("mode")
    require(mode in ("boss-first", "boss-after-resource"), "Unexpected boss mode")
    expected = ["zombie_boss"] if mode == "boss-first" else ["resource", "zombie_boss"]
    stages = proof.get("stages", [])
    require([stage.get("kind") for stage in stages] == expected, "Boss scans did not follow exact first/after-resource order")
    with database(root, owner) as connection:
        runs = runs_from_db(connection)
        require(len(runs) == 1, "Fresh boss proof has extra/missing persisted scans")
        rendered = []
        if mode == "boss-after-resource":
            rendered.append(audit_resource_first(root, stages[0], owner, connection, runs[0]))
        for stage, run, kind in zip(stages[-1:], runs, expected[-1:]):
            completed_run(run, [kind], 2212)
            require(stage.get("completedStatus", {}).get("scanRunId") == run["id"] and stage.get("selectedTypes") == [kind], "Boss UI references another run/selection")
            count = connection.execute("SELECT COUNT(*) FROM map_records WHERE kind=? AND server_id=2212", (kind,)).fetchone()[0]
            search(stage, kind, 2212, count)
            rendered.append(rendered_stage(stage, 9 if kind == "zombie_boss" else 1, count))
            png(root, f"map017-{stage['label']}-selected.png")
            png(root, f"map017-{stage['label']}-results.png")
        boss_run, boss_stage = runs[-1], stages[-1]
        rows = published_rows(connection, "map_records", "zombie_boss", 2212)
        scanned = published_rows(connection, "scan_records", "zombie_boss", 2212, boss_run["id"])
        # Successful MapStore publication atomically copies then deletes this
        # run's staging rows; retained staging indicates incomplete cleanup.
        require(not scanned, "Completed Boss scan retains unpublished staging rows")
        require(not connection.execute("SELECT 1 FROM map_records WHERE kind NOT IN (%s) LIMIT 1" % ",".join("?" for _ in expected), expected).fetchone(),
                "Unexpected kind in fresh Boss-only publication")
        native = audit_native_boss(root, boss_stage["label"], boss_run, owner, rows)
        for row in search(boss_stage, "zombie_boss", 2212, len(rows)):
            require(row.get("uuid") in rows, "Mounted first-page Boss is not a published native identity")
    accepted = bool(rows) and all(rendered)
    return dict(mode=mode, accepted=accepted,
                state="proven" if accepted else "observed_source_positive_without_render_join" if rows else "observed_zero",
                renderedQueryCorrelated=all(rendered), count=len(rows), native=native,
                runId=boss_run["id"], firstScan=mode == "boss-first")


def audit_picker(root, proof, owner):
    require(proof.get("mode") == "picker-empty", "Unexpected picker mode")
    stages = proof.get("stages", [])
    require([s.get("stage") for s in stages] == ["manual-last-untick", "auto-last-untick"], "Missing manual/Auto picker observations")
    accepted = True
    for stage, prefix in zip(stages, ("manual", "auto")):
        snap = stage.get("snapshot", {})
        passed = snap.get("selectedIndices") == [] and snap.get("actionDisabled") is True and bool(snap.get("hint"))
        if prefix == "auto":
            config = stage.get("native", {}).get("config", {})
            passed = passed and config.get("selectedTypes") == [] and config.get("enabled") is False and config.get("nextRunAt") == 0
        require(stage.get("passed") is passed, "Picker self-reported pass differs from captured controls")
        accepted = accepted and passed
        png(root, f"map017-{prefix}-last-selected.png")
        png(root, f"map017-{prefix}-after-untick-last.png")
    with database(root, owner) as connection:
        require(not runs_from_db(connection), "Empty picker proof unexpectedly launched a scan")
    if accepted:
        final = persisted_auto(root).get("config", {})
        require(final.get("selectedTypes") == [] and final.get("enabled") is False and final.get("nextRunAt") == 0,
                "Persisted empty picker does not match native snapshot")
    return dict(mode="picker-empty", accepted=accepted, state="proven" if accepted else "observed_failure")


def audit_once(root, proof, owner):
    require(proof.get("mode") == "once-two-server", "Unexpected one-off mode")
    stages = proof.get("stages", [])
    require(stages and stages[0].get("stage") == "schedule-off-configured", "One-off configuration missing")
    config = stages[0].get("configured", {}).get("config", {})
    require(config.get("enabled") is False and config.get("nextRunAt") == 0 and config.get("serverIds") == [2211, 2213] and
            config.get("selectedTypes") == ["railway"] and config.get("returnToOriginalServer") is True, "One-off schedule/target config differs")
    png(root, "map017-once-schedule-off-before.png")
    with database(root, owner) as connection:
        runs = runs_from_db(connection)
        if stages[0].get("snapshot", {}).get("actionDisabled") is True:
            require(len(stages) == 2 and stages[1].get("stage") == "one-off-unavailable" and stages[1].get("passed") is False and not runs,
                    "Disabled baseline one-off unexpectedly produced scan results")
            return dict(mode="once-two-server", accepted=False, state="observed_disabled")
        require(len(stages) == 4 and stages[1].get("stage") == "one-off-completed" and len(runs) == 2, "One-off did not provide exactly two real runs")
        completed = stages[1]
        cycle, last = completed.get("cycle", {}), completed.get("cycle", {}).get("lastCycle", {})
        require(completed.get("runNowClicks") == 1 and completed.get("originalServerId") == completed.get("restoredServerId") == 2212 and
                {2211, 2213}.issubset(set(completed.get("serversSeen", []))) and cycle.get("running") is False and
                cycle.get("config", {}).get("enabled") is False and cycle.get("config", {}).get("nextRunAt") == 0 and
                last.get("outcome") == "completed" and last.get("lastError") is None and last.get("originalServerId") == 2212 and
                last.get("targetServerIds") == last.get("completedServerIds") == [2211, 2213], "One-off completion/home return/schedule disabled mismatch")
        png(root, "map017-once-completed-returned-home.png")
        for run, stage, server in zip(runs, stages[2:], (2211, 2213)):
            completed_run(run, ["railway"], server)
            require(stage.get("stage") in ("saved-target-results", "empty-target-results") and stage.get("serverId") == server, "Saved-server proof mismatched")
            count = connection.execute("SELECT COUNT(*) FROM map_records WHERE kind='railway' AND server_id=?", (server,)).fetchone()[0]
            staged = connection.execute("SELECT record_key FROM scan_records WHERE run_id=? AND kind='railway' AND server_id=?", (run["id"], server)).fetchall()
            published = connection.execute("SELECT record_key FROM map_records WHERE kind='railway' AND server_id=?", (server,)).fetchall()
            require(not staged, "Completed one-off retains unpublished staging rows")
            if stage["stage"] == "empty-target-results":
                require(count == 0 and stage.get("total") == 0 and
                        stage.get("nativeQuery") == {"kind": "railway", "query": {"serverId": server, "page": 1, "pageSize": 50}} and
                        stage.get("nativeResponse", {}).get("total") == 0 and stage.get("nativeResponse", {}).get("rows") == [] and
                        isinstance(stage.get("nativeSaved", {}).get("servers"), list) and
                        server not in stage["nativeSaved"]["servers"] and
                        isinstance(stage.get("savedServerSnapshot", {}).get("optionValues"), list) and
                        str(server) not in stage["savedServerSnapshot"]["optionValues"] and
                        not connection.execute("SELECT 1 FROM map_records WHERE server_id=? LIMIT 1", (server,)).fetchone(),
                        "Empty target has retained rows, phantom Saved option, or inconsistent native zero query")
                png(root, f"map017-once-empty-{server}-no-saved-phantom.png")
            else:
                search(stage, "railway", server, count)
                png(root, f"map017-once-saved-{server}-railway.png")
    final = persisted_auto(root)
    require(final.get("config", {}).get("enabled") is False and final.get("config", {}).get("nextRunAt") == 0 and
            final.get("lastCycle", {}).get("outcome") == "completed" and
            final.get("lastCycle", {}).get("originalServerId") == 2212 and
            final.get("lastCycle", {}).get("completedServerIds") == [2211, 2213],
            "Persisted one-off schedule/cycle does not match return receipt")
    return dict(mode="once-two-server", accepted=True, state="proven", targetServers=[2211, 2213], restoredServer=2212)


def audit(root, mode=None):
    root = Path(root).resolve()
    owner, proof = audit_restoration(root), read(root / "native-ui-map.json")
    if proof.get("selection") in ("all-nine", "auto-all-nine"):
        require(mode in (None, "all-nine"), "Requested audit mode differs from receipt")
        validated = previous.audit_receipts(root, proof["selection"], {"executable": owner["executable"]})
        result = dict(mode="all-nine", accepted=True, state="proven", selection=proof["selection"], priorAudit=validated)
    else:
        require(proof.get("state") == "observed" and proof.get("proof") == "actual_packaged_map017_ui" and
                proof.get("genuineGame") is True and proof.get("controlledProvider") is False and
                proof.get("profileId") == owner["profileId"] and proof.get("sessionId") == owner["sessionId"] and
                proof.get("gamePid") == owner["gamePid"], "MAP017 proof identity differs from exact owner")
        group = "boss" if proof.get("mode") in ("boss-first", "boss-after-resource") else "picker" if proof.get("mode") == "picker-empty" else "once"
        require(mode in (None, group), "Requested audit mode differs from receipt")
        result = {"boss": audit_boss, "picker": audit_picker, "once": audit_once}[group](root, proof, owner)
    return dict(result, root=str(root), sessionId=owner["sessionId"], gamePid=owner["gamePid"],
                gameStartedAtUtc=owner["gameStartedAtUtc"], originalScriptsRestored=True,
                probeSourceSha256=owner.get("probeSourceSha256"), desktopAssemblySha256=owner.get("desktopAssemblySha256"),
                proofSha256=digest(root / "native-ui-map.json"))


def aggregate(receipts):
    require(len({r["root"] for r in receipts}) == len(receipts), "Duplicate proof root cannot count twice")
    first = [r for r in receipts if r["mode"] == "boss-first" and r["accepted"]]
    identities = {(r["sessionId"], r["gamePid"], r["gameStartedAtUtc"]) for r in first}
    require(len({r["sessionId"] for r in first}) == len(first) == len(identities), "Repeated game session cannot count as a fresh launch")
    gates = dict(threeFreshBossFirst=len(first) >= 3,
                 sameFirstScanBuild=bool(first) and len({(r.get("probeSourceSha256"), r.get("desktopAssemblySha256")) for r in first}) == 1 and
                    all(r.get("probeSourceSha256") and r.get("desktopAssemblySha256") for r in first),
                 afterResource=any(r["mode"] == "boss-after-resource" and r["accepted"] for r in receipts),
                 allNine=any(r["mode"] == "all-nine" and r["accepted"] for r in receipts),
                 emptyPicker=any(r["mode"] == "picker-empty" and r["accepted"] for r in receipts),
                 scheduleOffTwoServers=any(r["mode"] == "once-two-server" and r["accepted"] for r in receipts))
    accepted = [r for r in receipts if r["accepted"]]
    gates["sameAcceptedBuild"] = bool(accepted) and len({(r.get("probeSourceSha256"), r.get("desktopAssemblySha256")) for r in accepted}) == 1
    return dict(state="PACKAGED_GATES_PASS" if all(gates.values()) else "PARTIAL", gates=gates,
                rootCauseAndZeroReproductionNotEstablishedByThisAudit=True,
                positiveFreshBossScans=len(first), receipts=receipts)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", action="append", type=Path, required=True)
    parser.add_argument("--mode", choices=("boss", "picker", "once", "all-nine"))
    parser.add_argument("--aggregate", action="store_true")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args(argv)
    require(args.aggregate or len(args.root) == 1, "Multiple roots require --aggregate")
    receipts = [audit(root, args.mode) for root in args.root]
    result = aggregate(receipts) if args.aggregate else receipts[0]
    text = json.dumps(result, indent=2)
    if args.output:
        args.output.write_text(text, encoding="utf-8")
    print(text)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, TypeError, sqlite3.Error) as error:
        raise SystemExit("MAP017_AUDIT_ERROR: " + str(error))
