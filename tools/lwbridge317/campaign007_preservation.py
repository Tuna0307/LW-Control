"""Campaign 007 read-only final preservation audit. No game actions or owner file mutations."""
from __future__ import annotations
import hashlib,json,sqlite3,subprocess,os
from pathlib import Path
HERE=Path(__file__).resolve().parents[2]
ROOT=HERE/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"
LOCAL=Path(r"C:\Users\chimw\AppData\LocalLow\FunFly\Last War-Survival Game\lwScripts")
REFERENCE=Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
R17="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
TRIPLET={
 "LWScripts.data":"248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22",
 "LWScripts.txt":"d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a",
 "version.txt":"785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09"
}
def sha(path):
 h=hashlib.sha256()
 with path.open("rb") as f:
  for part in iter(lambda:f.read(1024*1024),b""):h.update(part)
 return h.hexdigest()
def run():
 safe=json.loads((ROOT/"safe-real-db-snapshot.json").read_text(encoding="utf-8-sig"))
 expected=safe["originalSidecarHashes"]
 original=Path(safe["originalSourcePath"])
 copy=Path(safe["snapshotPath"])
 assert original.is_file() and copy.is_file()
 assert sha(REFERENCE)==R17
 installed={name:{"file":str(LOCAL/name),"sha256":sha(LOCAL/name),"matches":sha(LOCAL/name)==digest} for name,digest in TRIPLET.items()}
 assert all(x["matches"] for x in installed.values()), "installed original script triplet changed"
 source={"db":sha(original),"wal":None,"shm":None}
 assert source["db"]==safe["sourceBytesSha256BeforeAfter"]
 for suffix,key in [("-wal","wal"),("-shm","shm")]:
  target=Path(str(original)+suffix)
  if target.exists():source[key]=sha(target)
 assert all(source[{"db":"db","-wal":"wal","-shm":"shm"}[k]]==val["sha256"] for k,val in expected.items()),"source sidecar changed"
 assert sha(copy)==safe["snapshotSha256"]
 with sqlite3.connect(copy.resolve().as_uri()+"?mode=ro",uri=True,timeout=5) as conn:
  conn.execute("PRAGMA query_only=ON")
  q=lambda sql:conn.execute(sql).fetchone()[0]
  assert q("SELECT count(*) FROM map_records WHERE kind='resource' AND server_id=2212")==8008
  assert q("SELECT count(*) FROM scan_records")==0
  assert q("SELECT count(*) FROM dispatch_plunder_jobs")==0
  assert q("SELECT count(*) FROM truck_plunder_jobs")==0
  assert q("SELECT count(*) FROM dispatch_assist_jobs")==0
  assert q("PRAGMA integrity_check")=="ok"
 run004=json.loads((HERE/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-world-resource-004/live-attempt-001.json").read_text(encoding="utf-8-sig"))
 assert run004["originalHashesRestored"] and run004["ownedGameStopSucceeded"]
 assert run004["profileId"] in str(original)
 assert run004["isolatedRoot"] in str(original)
 assert run004["terminal"]=="BLOCKED_LIVE_PREREQUISITE_OR_FAILURE", "historical trace must retain original invalid-command envelope failure"
 # tasklist is passive process inventory, not desktop capture/control.
 task=subprocess.run(["tasklist","/FO","CSV","/NH"],capture_output=True,text=True,check=True)
 residual=[line for line in task.stdout.splitlines() if line.lower().startswith(('"lastwar.exe"','"lwbridge.desktop.exe"'))]
 assert not residual, "Game/bridge processes active; not claiming cleanup"
 leftover=[str(p) for p in Path(os.environ["TEMP"]).glob("LWB317-A-TO-A-CAMPAIGN-007-COMMAND-*")]
 assert not leftover, "replay-owned SQLite temp paths still exist"
 report={
  "campaign":"LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007",
  "check":"PASS","originalExeSha256":sha(REFERENCE),
  "installedOriginals":installed,"originalLiveRootDbHashUnchanged":source["db"],
  "sourceSidecarHashes":source,"safeSnapshotSha256":sha(copy),
  "sourceDatasetPublishedResource":8008,"safeSnapshotNoJobs":True,
  "historicalRunId":"b439bfa57cd54405a14e803d21964c9d",
  "historicalLiveRestoration":True,"historicalPostRunCommandEnvelopeFailurePreserved":True,"gameProcesses":[],"tempCommandReplayRoots":leftover,
  "newGameLaunchesCampaign007":0,
  "boundary":"does not prove original protected provider parity, source DB queried ro only"
 }
 (ROOT/"preservation-final.json").write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8")
 print("CAMPAIGN007_PRESERVATION_PASS reference, scripts, 004 source, copy, zero jobs/processes/temp roots")
if __name__=="__main__":run()
