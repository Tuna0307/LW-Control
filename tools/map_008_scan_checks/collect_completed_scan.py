"""Collect a completed owned passive scan's genuine, source-identified proof.

Refuses running/incomplete/wrong-server runs; never modifies the game, scanner
database or selected populations. Files remain task-private under artifacts/.
"""
import argparse
import json
import pathlib
import shutil
import sqlite3
import subprocess
import sys


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--trial", required=True)
    p.add_argument("--run", required=True)
    p.add_argument("--label", required=True)
    p.add_argument("--server", type=int, required=True)
    p.add_argument("--sampler-stop", type=pathlib.Path)
    p.add_argument("--allow-empty-railway", action="store_true", help="Collect an observed zero native Railway list, explicitly without a positive accuracy gate")
    args = p.parse_args()
    if not args.label.isidentifier() or args.label.startswith("_"):
        raise ValueError("Audit label must match the native gate's identifier requirement (use underscores)")
    root = pathlib.Path("artifacts/map-006") / ("trial-" + args.trial)
    profile = "map-006-" + args.trial
    native_root = root / "isolated/live-resource"
    db = root / "isolated/profiles" / profile / "map-data/map-data.db"
    conn = sqlite3.connect(f"file:{db.resolve().as_posix()}?mode=ro", uri=True)
    conn.row_factory = sqlite3.Row
    run = conn.execute("select * from scan_runs order by created_at desc limit 1").fetchone()
    if run is None or run["id"] != args.run or run["server_id"] != args.server or run["status"] != "completed":
        raise ValueError("The exact owned scan is not the latest completed target-server run")
    selection = json.loads(run["selected_types"])
    source_metrics = native_root / "map-fullworld-probe-metrics.json"
    metrics = json.loads(source_metrics.read_text(encoding="utf-8"))
    if metrics["identity"]["RunId"] != args.run or metrics["identity"]["ServerId"] != args.server or metrics["phase"] != "prepared":
        raise ValueError("Native acquisition not prepared for the current run")
    stage_src = native_root / "map-engine-stage-metrics.json"
    stage = json.loads(stage_src.read_text(encoding="utf-8"))
    if stage["runId"] != args.run or stage["serverId"] != args.server:
        raise ValueError("Engine checkpoint/publication proof not owned by current run")
    sample = root / "native-samples" / ("native-samples-"+args.run+".jsonl")
    has_positive_sample = sample.exists() and sample.stat().st_size > 0
    positive_railway_sample = False
    if selection == ["railway"] and has_positive_sample:
        for line in sample.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            doc = json.loads(line)
            positive_railway_sample |= any(
                row.get("trainType") == 2 and row.get("serverId") == args.server
                for row in doc.get("train_march_records") or []
            )
        has_positive_sample = positive_railway_sample
    if not has_positive_sample:
        if not args.allow_empty_railway or selection != ["railway"]:
            raise ValueError("No positive independently copied native response samples")
        train = json.loads((native_root / "train-list-diagnostic-result.json").read_text(encoding="utf-8"))
        published_target_railway = conn.execute(
            "select count(*) from map_records where kind='railway' and server_id=?", (args.server,)
        ).fetchone()[0]
        if (train.get("state") != "proven" or train.get("scanRunId") != args.run or
            train.get("serverId") != args.server or args.server in (train.get("railwayServerIds") or []) or
            published_target_railway != 0):
            raise ValueError("No exact-run official zero Railway scope witness")
    metrics_dst = root / ("metrics-" + args.label + ".json")
    stage_dst = root / ("stage-" + args.label + ".json")
    if metrics_dst.exists() or stage_dst.exists():
        raise FileExistsError("Existing run receipt must never be overwritten")
    if args.sampler_stop:
        args.sampler_stop.touch(exist_ok=True)
    shutil.copy2(source_metrics, metrics_dst)
    shutil.copy2(stage_src, stage_dst)
    command = [sys.executable,"tools/map_006_live_gate.py","--trial",args.trial,"audit",args.label]
    subprocess.run(command,check=True)
    if has_positive_sample:
        audit_args = [sys.executable,"tools/map_008_scan_checks/audit_native_fields.py","--require-deferred",
                      "--db",str(db),"--server",str(args.server),"--run",args.run,"--native",str(sample),
                      "--out",str(root/("audit-"+args.label+".json"))]
        if "resource" in selection:
            audit_args.extend(["--resource-details",str(native_root/"resource-scan-detail-result.json")])
        subprocess.run(audit_args,check=True)
    else:
        (root / ("observed_zero_railway_"+args.label+".json")).write_text(json.dumps({
            "scanRunId":args.run,"serverId":args.server,"selected":selection,
            "officialRailwaySourceCount":train["railwaySourceCount"],
            "targetServerRailwayListCount":0,"publishedTargetRailwayCount":0,
            "nativePositiveRailwayGate":"PENDING",
            "fullWorldGroundTruth":False,"speedAccuracyQualified":False,
            "source":"genuine current v24 native list refresh and full AOI acquisition"},indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"run":args.run,"selection":selection,"mode":metrics["identity"]["ScanMode"],
                      "wholeMs":run["updated_at"]-run["created_at"],
                      "stageCheckpointMs":stage["checkpointMilliseconds"],
                      "stagePublicationMs":stage["publicationMilliseconds"],
                      "stageSourceCaptureMs":stage["sourceCaptureMilliseconds"]}))


if __name__ == "__main__":
    main()
