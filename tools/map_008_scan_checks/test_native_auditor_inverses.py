"""Verify that the independent native-vs-SQLite audit actually rejects errors.

Requires one completed owned scan and genuine native sampler JSONL. The source
database and native evidence are NEVER modified: each inverse uses a disposable
SQLite snapshot and a copied witness file under task-private output.
"""
import argparse
import json
import pathlib
import sqlite3
import subprocess
import sys
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--db", type=pathlib.Path, required=True)
    parser.add_argument("--native", type=pathlib.Path, required=True)
    parser.add_argument("--run", required=True)
    parser.add_argument("--server", type=int, required=True)
    parser.add_argument("--out", type=pathlib.Path, required=True)
    args = parser.parse_args()
    records = [json.loads(line) for line in args.native.read_text(encoding="utf-8").splitlines() if line]
    if not records:
        raise RuntimeError("No real native witnesses")
    witness = next((item for doc in records for item in doc.get("point_records", [])
                    if item.get("kind") in ("player_base", "resource_point", "dispatch_task")), None)
    if not witness:
        raise RuntimeError("No real positive native point witness")
    kind = {"player_base":"city", "resource_point":"resource", "dispatch_task":"dispatch"}[witness["kind"]]
    point_key = str(witness["pointId"])
    results = []
    with tempfile.TemporaryDirectory(prefix="map008-native-audit-") as root:
        root = pathlib.Path(root)
        for variant in ("baseline", "omitted_known_identity", "wrong_field_value", "wrong_server",
                        "dropped_selected_kind", "stale_native", "wrong_session", "same_tick_restore"):
            db_path = root / "witness.db"
            if db_path.exists():
                db_path.unlink()
            original = sqlite3.connect(f"file:{args.db.resolve().as_posix()}?mode=ro",uri=True)
            copy = sqlite3.connect(db_path)
            original.backup(copy)
            original.close()
            docs = json.loads(json.dumps(records))
            if variant == "omitted_known_identity":
                copy.execute("delete from map_records where server_id=? and kind=? and record_key=?",
                             (args.server, kind, point_key))
            elif variant == "wrong_field_value":
                row = copy.execute("select data_json from map_records where server_id=? and kind=? and record_key=?",
                                   (args.server,kind,point_key)).fetchone()
                if not row:
                    raise RuntimeError("Required native witness missing from baseline")
                data = json.loads(row[0]); data["x"] = int(data["x"]) + 1
                copy.execute("update map_records set data_json=? where server_id=? and kind=? and record_key=?",
                             (json.dumps(data),args.server,kind,point_key))
            elif variant == "wrong_server":
                copy.execute("update map_records set server_id=? where server_id=? and kind=? and record_key=?",
                             (args.server+1,args.server,kind,point_key))
            elif variant == "dropped_selected_kind":
                copy.execute("delete from map_records where server_id=? and kind=?",(args.server,kind))
            elif variant == "stale_native":
                docs[0]["capturedAt"] = "2000-01-01T00:00:00Z"
            elif variant == "wrong_session":
                docs[-1]["launchSessionId"] = "definitely-wrong-game-session"
            elif variant == "same_tick_restore":
                docs[0]["positionRestoredBeforeResponse"] = True
                docs[0]["positionRestoredAfterResponse"] = False
                docs[0]["deferRestoreUntilResponse"] = False
                docs[0]["requestMethod"] = "WorldPointManager.UpdateViewRequest(true)+same-tick-camera-restore"
            copy.commit(); copy.close()
            sample_file = root / "native.jsonl"
            sample_file.write_text("\n".join(json.dumps(doc) for doc in docs)+"\n", encoding="utf-8")
            output = root / "audit.json"
            command = [sys.executable, str(pathlib.Path(__file__).with_name("audit_native_fields.py")),
                       "--require-deferred", "--db", str(db_path), "--native", str(sample_file),
                       "--run", args.run, "--server", str(args.server), "--out", str(output)]
            completed = subprocess.run(command,capture_output=True,text=True,timeout=35,check=False)
            rejected = completed.returncode != 0
            if rejected == (variant == "baseline"):
                raise RuntimeError(f"Independent audit inverse {variant} did not behave as expected: " +
                                   (completed.stdout + completed.stderr)[-1400:])
            results.append({"inverse":variant,"result":"PASS_BASELINE" if not rejected else "REJECTED_AS_REQUIRED"})
    proof = {"kind":"real_native_witness_inverse_gate", "scanRunId":args.run,
             "evidence":"read-only genuine native JSONL and disposable SQLite backup mutations",
             "tests":results,"sourceFilesModified":False}
    args.out.parent.mkdir(parents=True,exist_ok=True)
    args.out.write_text(json.dumps(proof,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"passed":len(results),"negativeRejected":len(results)-1}))


if __name__ == "__main__":
    main()
