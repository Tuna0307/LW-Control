"""007 exact-owned snapshot of the accepted 004 Resource SQLite DB.
Never opens historical source for writing. This is a one-time copy, not a scan.
"""
from __future__ import annotations

import argparse, hashlib, json, os, sqlite3
from pathlib import Path

EXPECTED_RUN = "b439bfa57cd54405a14e803d21964c9d"
EXPECTED_SERVER = 2212
EXPECTED_COUNT = 8008

def sha256(path):
    h=hashlib.sha256()
    with open(path, "rb") as f:
        for part in iter(lambda:f.read(1024*1024),b""):
            h.update(part)
    return h.hexdigest()

def read_only(path):
    return sqlite3.connect(path.resolve().as_uri()+"?mode=ro",uri=True,timeout=10)

def get_snapshot(c):
    c.execute("PRAGMA query_only=ON")
    rows=c.execute("SELECT id,server_id,status,total_blocks,completed_blocks,failed_blocks FROM scan_runs WHERE id=?",(EXPECTED_RUN,)).fetchall()
    n=c.execute("SELECT count(*) FROM map_records WHERE kind='resource' AND server_id=?",(EXPECTED_SERVER,)).fetchone()[0]
    staging=c.execute("SELECT count(*) FROM scan_records").fetchone()[0]
    jobs={}
    for name in ("dispatch_plunder_jobs","truck_plunder_jobs","dispatch_assist_jobs"):
        if c.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name=?",(name,)).fetchone():
            jobs[name]=c.execute(f"SELECT count(*) FROM {name}").fetchone()[0]
    return {"run":rows,"publishedResource":n,"staged":staging,"jobs":jobs}

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--source",required=True,type=Path)
    p.add_argument("--destination",required=True,type=Path)
    p.add_argument("--report",required=True,type=Path)
    a=p.parse_args()
    src=a.source.resolve()
    dst=a.destination.resolve()
    assert src!=dst and "LWB317-BACKGROUND-WITNESS-002-" in str(src)
    assert "LWB317-A-TO-A-CAMPAIGN-007-" in str(dst)
    assert not dst.exists(), "destination must not preexist"
    assert src.is_file()
    before=sha256(src)
    source_parts={}
    for ext in ("","-wal","-shm"):
        name=Path(str(src)+ext)
        if name.exists():
            source_parts[ext or "db"]={"sha256":sha256(name),"bytes":name.stat().st_size}
    with read_only(src) as c:
        orig=get_snapshot(c)
        assert orig["run"]==[(EXPECTED_RUN,EXPECTED_SERVER,"completed",2500,2500,0)],orig
        assert orig["publishedResource"]==EXPECTED_COUNT and orig["staged"]==0,orig
        assert not any(orig["jobs"].values()),"real dataset contains scheduled jobs; do not start replay workers"
        dst.parent.mkdir(parents=True,exist_ok=True)
        with sqlite3.connect(dst) as copy:
            c.backup(copy)
    with read_only(dst) as c:
        copied=get_snapshot(c)
        assert copied==orig
        check=c.execute("PRAGMA integrity_check").fetchone()[0]
        assert check=="ok",check
    assert sha256(src)==before,"source DB mutated"
    for ext, fingerprint in source_parts.items():
        path=Path(str(src)+(ext if ext!="db" else ""))
        assert sha256(path)==fingerprint["sha256"],f"source sidecar mutated {ext}"
    report={
        "mode":"source-query-only-sqlite-online-backup-into-new-task-owned-destination",
        "originalSourcePath":str(src),"sourceBytesSha256BeforeAfter":before,
        "originalSidecarHashes":source_parts,
        "snapshotPath":str(dst),"snapshotSha256":sha256(dst),
        "state":copied,"sqliteIntegrity":"ok",
        "proofType":"real-v22-captured-dataset-readonly-copy; NOT original 0.3.17 parity",
        "negativeGuarantees":["source opened mode=ro/query_only","no scheduled jobs","no game actions","never replaces original"]
    }
    a.report.parent.mkdir(parents=True,exist_ok=True)
    a.report.write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8")
    print("CAMPAIGN007_SAFE_REAL_SQLITE_SNAPSHOT_PASS rows",copied["publishedResource"],"jobs",copied["jobs"])
    print("SOURCE_SHA256",before,"SNAPSHOT_SHA256",report["snapshotSha256"])

if __name__=="__main__":
    main()
