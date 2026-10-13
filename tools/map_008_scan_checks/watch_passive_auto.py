"""Observe a bounded owned multi-server Auto run without issuing any command.

Polls only the task-isolated read-only Map SQLite/native result producer. Copies
small correlated native samples and emits evidence for every completed target
before the next server scan may replace the current per-run ledger row. Does not
start, stop, switch servers, arm actions or alter the application.
"""
import argparse
import collections
import datetime as dt
import json
import pathlib
import shutil
import sqlite3
import time


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def millis(value):
    return int(dt.datetime.fromisoformat(value.replace("Z", "+00:00")).timestamp()*1000)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--trial", required=True)
    p.add_argument("--stop-file", required=True, type=pathlib.Path)
    p.add_argument("--servers", required=True, help="Comma-separated previously verified targets")
    p.add_argument("--after-epoch-ms",required=True,type=int,help="Do not count a previous completed scan as part of this Auto cycle")
    p.add_argument("--selected-types",required=True,help="Exact intended comma-separated kind selection")
    p.add_argument("--out-dir",required=True,type=pathlib.Path)
    args = p.parse_args()
    servers = [int(s) for s in args.servers.split(",")]
    if not 1 <= len(servers) <= 3 or len(servers) != len(set(servers)):
        raise ValueError("Only a bounded 1–3 server owned trial is supported")
    expected_types=sorted(x.strip() for x in args.selected_types.split(",") if x.strip())
    if not expected_types or len(set(expected_types))!=len(expected_types):
        raise ValueError("Select the exact unique expected Auto kinds")
    root = pathlib.Path("artifacts/map-006") / ("trial-"+args.trial)
    profile = "map-006-"+args.trial
    db_path = root / "isolated/profiles" / profile / "map-data/map-data.db"
    native_dir = root / "isolated/live-resource"
    sample_dir = args.out_dir
    if sample_dir.exists() and any(sample_dir.iterdir()):
        raise FileExistsError("Auto watcher evidence already exists; do not overwrite a prior run")
    sample_dir.mkdir(parents=True,exist_ok=True)
    seen_request = set()
    sampled = collections.defaultdict(int)
    recorded = set()
    captured_kind = collections.defaultdict(set)
    observations = []
    previous_owner = None
    while not args.stop_file.exists():
        try:
            conn = sqlite3.connect(f"file:{db_path.resolve().as_posix()}?mode=ro",uri=True)
            conn.row_factory = sqlite3.Row
            row = conn.execute("select * from scan_runs order by created_at desc limit 1").fetchone()
            if (row is None or row["server_id"] not in servers or
                row["created_at"]<args.after_epoch_ms):
                conn.close(); time.sleep(.2); continue
            run_id = row["id"]
            selected = json.loads(row["selected_types"])
            if sorted(selected)!=expected_types:
                conn.close(); time.sleep(.2); continue
            native_path = native_dir / "bulk-aoi-diagnostic-result.json"
            if row["status"] == "running" and native_path.exists():
                doc = read(native_path)
                if doc.get("state") == "proven" and doc.get("requestMode") == "coverage" and (
                    doc.get("positionRestoredBeforeResponse") is False and doc.get("positionRestoredAfterResponse") is True):
                    rid = doc.get("requestId")
                    stamp = millis(doc["capturedAt"])
                    owner = (doc.get("profileId"),doc.get("gamePid"),doc.get("launchSessionId"))
                    if owner[0] != profile or not owner[1] or not owner[2]:
                        raise RuntimeError("Unknown game/profile ownership in Auto native result")
                    if previous_owner is None:
                        previous_owner = owner
                    elif previous_owner != owner:
                        raise RuntimeError("Game session/PID changed during Multi Auto")
                    if rid and rid not in seen_request and row["created_at"]-3000 <= stamp <= row["updated_at"]+3000:
                        seen_request.add(rid)
                        positive = set()
                        for rec in doc.get("point_records",[]):
                            kind = {"player_base":"city", "resource_point":"resource", "dispatch_task":"dispatch", "treasure":"treasure"}.get(rec.get("kind"))
                            if kind and kind in selected and rec.get("serverId") == row["server_id"]:
                                positive.add(kind)
                        if doc.get("monster_march_records") and "monster" in selected: positive.add("monster")
                        if doc.get("train_march_records") and ("truck" in selected or "railway" in selected): positive.add("train")
                        if positive and sampled[run_id] < 16 and (positive - captured_kind[run_id] or sampled[run_id] in (0,2,5,8)):
                            payload = {key:doc.get(key) for key in (
                                "schemaVersion","state","requestId","scanRunId","requestMode",
                                "profileId","gamePid","launchSessionId","capturedAt","cameraTileStable",
                                "responseFlagsTransitioned","positionRestoredBeforeResponse","positionRestoredAfterResponse",
                                "deferRestoreUntilResponse","requestMethod","requestedIndices","point_records",
                                "monster_march_records","train_march_records")}
                            payload["samplerAssociatedRunId"] = run_id
                            with (sample_dir/("native-"+run_id+".jsonl")).open("a",encoding="utf-8") as out:
                                out.write(json.dumps(payload,separators=(",",":"),ensure_ascii=False)+"\n")
                            sampled[run_id]+=1
                            captured_kind[run_id].update(positive)
            if row["status"] == "completed" and run_id not in recorded:
                met_path = native_dir/"map-fullworld-probe-metrics.json"
                stage_path = native_dir/"map-engine-stage-metrics.json"
                if met_path.exists() and stage_path.exists():
                    m = read(met_path); st = read(stage_path)
                    if m.get("phase") == "prepared" and m.get("identity",{}).get("RunId") == run_id and st.get("runId") == run_id:
                        count_rows=conn.execute("select kind,count(*),count(distinct record_key) from map_records where server_id=? group by kind",(row["server_id"],)).fetchall()
                        stored = {str(k):{"count":int(n),"distinctKeys":int(d)} for k,n,d in count_rows}
                        staged = int(conn.execute("select count(*) from scan_records").fetchone()[0])
                        receipt={"runId":run_id,"serverId":row["server_id"],"state":row["status"],
                                 "selectedTypes":selected,"totalBlocks":row["total_blocks"],"completedBlocks":row["completed_blocks"],
                                 "failedBlocks":row["failed_blocks"],"createdAt":row["created_at"],
                                 "updatedAt":row["updated_at"],"error":row["error"],
                                 "stagedRows":staged,"perKindRows":stored,
                                 "sourceNativeSamples":sampled[run_id],"sourcePositiveKinds":sorted(captured_kind[run_id]),
                                 "source":"actual production native AOI + mapped SQLite and engine after publication"}
                        if receipt["completedBlocks"] != receipt["totalBlocks"] or receipt["failedBlocks"] or receipt["error"] or staged:
                            raise RuntimeError("Auto target did not publish its complete selected population")
                        # Copy source-owned timing before the next Auto target
                        # replaces the task-local native metrics envelope.
                        shutil.copy2(met_path,sample_dir/("metrics-"+run_id+".json"))
                        shutil.copy2(stage_path,sample_dir/("stage-"+run_id+".json"))
                        (sample_dir/("run-"+run_id+".json")).write_text(json.dumps(receipt,indent=2)+"\n",encoding="utf-8")
                        recorded.add(run_id);observations.append(receipt)
                        print(json.dumps({"observedCompletedTarget":run_id,"server":row["server_id"],
                                          "elapsedMs":row["updated_at"]-row["created_at"],
                                          "nativeSamples":sampled[run_id]}),flush=True)
            conn.close()
        except (OSError, sqlite3.OperationalError, json.JSONDecodeError, ValueError):
            pass # Atomic native file replacement, transient SQLite lock or launch transition
        time.sleep(.16)
    output={"kind":"MAP008_bounded_passive_multi_auto_observation","trial":args.trial,
            "targetServerIds":servers,"observations":observations,"issuedGameActions":False,
            "currentLiveReturnProofMustBeCheckedSeparately":True}
    (sample_dir/"cycle-observations.json").write_text(json.dumps(output,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"completedRecorded":len(observations),"serversRecorded":[r["serverId"] for r in observations]}),flush=True)


if __name__ == "__main__":
    main()
