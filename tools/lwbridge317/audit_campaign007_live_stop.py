"""Independent, read-only verification of 007 bounded positive-staging attempt.

Never converts a negative current-world witness into positive evidence.
"""
import json,sqlite3,hashlib,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"
SOURCE=BASE/"live-positive-staged-stop-001.json"
ORIGINALS={
"LWScripts.data":"248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22",
"LWScripts.txt":"d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a",
"version.txt":"785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09",
}
def digest(path):
 h=hashlib.sha256()
 with open(path,"rb") as f:
  for chunk in iter(lambda:f.read(1048576),b""):h.update(chunk)
 return h.hexdigest()
def audit():
 j=json.loads(SOURCE.read_text(encoding="utf-8-sig"))
 assert j["workItem"]=="LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"
 assert j["mode"]=="explicit-real-launch-positive-stage-stop"
 assert j["positiveStagingStopRequested"] is True
 root=Path(j["isolatedRoot"]).resolve()
 assert root.name.startswith("LWB317-BACKGROUND-WITNESS-002-") and root.parent.name=="Temp"
 runs=[e["details"] for e in j["events"] if e["kind"]=="resource-start"]
 assert len(runs)==1
 run=runs[0]["run"];server=runs[0]["serverId"]
 assert len(run)==32 and server==2212
 started=[e["details"] for e in j["events"] if e["kind"]=="real-home-start-returned"]
 assert len(started)==1
 pid=started[0]["ownedPid"];session=started[0]["instance"]
 world=[e["details"] for e in j["events"] if e["kind"]=="resource-start"][0].get("worldReady")
 # Keep world evidence separate; do not infer original callback route.
 assert isinstance(session,str) and isinstance(pid,int)
 assert j["originalHashesRestored"] and j["ownedGameStopSucceeded"] and j["resourceStopSucceeded"]
 assert j["recoveryJournalAfter"] is False
 assert all(j["installedAfter"][name]==value for name,value in ORIGINALS.items())
 assert not [e for e in j["events"] if e["kind"]=="resource-positive-staged-before-stop"]
 assert j["positiveStagingCountBeforeStop"]==0
 assert j["positiveStageStopVerified"] is False
 assert j["terminal"]=="POSITIVE_STAGING_STOP_NOT_VERIFIED"
 stop=[e["details"] for e in j["events"] if e["kind"]=="production-active-stop-before"]
 assert len(stop)==1 and stop[0]["scanRunId"]==run and stop[0]["isReading"] is True
 assert stop[0]["inflightBlocks"]>=1 and stop[0]["completedBlocks"]==0
 db=root/"profiles"/j["profileId"]/"map-data"/"map-data.db"
 with sqlite3.connect(db.resolve().as_uri()+"?mode=ro",uri=True,timeout=5) as conn:
  conn.execute("PRAGMA query_only=ON")
  row=conn.execute("select status,server_id,total_blocks,completed_blocks,failed_blocks from scan_runs where id=?",(run,)).fetchone()
  stage=conn.execute("select count(*) from scan_records where run_id=?",(run,)).fetchone()[0]
  published=conn.execute("select count(*) from map_records where kind='resource' and server_id=2212").fetchone()[0]
 assert row==("cancelled",2212,2500,0,0),row
 assert stage==0 and published==0,(stage,published)
 assert j["reopenedPersistence"]["stagedAfterStop"]==0
 assert j["reopenedPersistence"]["total"]==0
 owned_backup=root/"overview-bridge-backups"/("preflight-originals-"+j["attemptId"])
 assert all(digest(owned_backup/name)==h for name,h in ORIGINALS.items())
 processes=subprocess.run(["tasklist","/FO","CSV","/NH"],text=True,capture_output=True,check=True).stdout.lower()
 assert not any(line.startswith(('"lastwar.exe"','"lwbridge.desktop.exe"')) for line in processes.splitlines())
 result={
  "workItem":j["workItem"],"status":"NEGATIVE_UNMET_POSITIVE_STAGING_WITNESS",
  "evidenceClass":"actual current-v22 authenticated production isolated background session",
  "run":run,"profileId":j["profileId"],"ownedSession":session,"ownedPid":pid,
  "worldReady":world if isinstance(world,dict) else {"observed":False},
  "preStop":{"isReading":True,"inflight":stop[0]["inflightBlocks"],"completed":0,"staged":0},
  "durableScan":{"status":row[0],"serverId":row[1],"total":row[2],"completed":row[3],"failed":row[4]},
  "afterStop":{"staged":stage,"publishedResource":published,"gameProcesses":0,"journal":False},
  "restorationVerified":True,"ownedHomeStop":True,"nativeMapStop":True,
  "positiveStagedCancellationProved":False,
  "originalReferenceParity":False,
  "missingWitness":"real game scan_records must become >0 while run remains isReading=true before Stop",
  "noDesktopInputOrScreenshots":True
 }
 (BASE/"live-positive-staged-stop-independent-audit.json").write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
 print("CAMPAIGN007_LIVE_NEGATIVE_AUDIT_PASS cancelled=1 staged=0 published=0 restored=1 no_game_processes=1 POSITIVE_STAGING_UNPROVED")
if __name__=="__main__":audit()
