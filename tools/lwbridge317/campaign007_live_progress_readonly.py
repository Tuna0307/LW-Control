"""Campaign 007 read-only positive-stage live audit. No game/control actions."""
import argparse,json,sqlite3
from pathlib import Path
def inspect(path):
 j=json.loads(path.read_text(encoding="utf-8-sig"))
 root=Path(j["isolatedRoot"]).resolve()
 assert root.name.startswith("LWB317-BACKGROUND-WITNESS-002-")
 assert j["workItem"]=="LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"
 events=j["events"];starts=[e["details"] for e in events if e["kind"]=="resource-start"]
 if not starts:return {"events":len(events),"last":events[-1]["kind"] if events else None,"status":"AWAITING_OWNED_RUN"}
 run=starts[-1]["run"]
 assert isinstance(run,str) and len(run)==32
 db=root/"profiles"/j["profileId"]/"map-data"/"map-data.db"
 with sqlite3.connect(db.resolve().as_uri()+"?mode=ro",uri=True,timeout=4) as c:
  c.execute("PRAGMA query_only=ON")
  row=c.execute("select status,server_id,total_blocks,completed_blocks,failed_blocks from scan_runs where id=?",(run,)).fetchone()
  staged=c.execute("select count(*) from scan_records where run_id=? and kind='resource'",(run,)).fetchone()[0]
  published=c.execute("select count(*) from map_records where kind='resource' and server_id=2212").fetchone()[0]
 return {"events":len(events),"last":events[-1]["kind"] if events else None,"run":run,"profile":j["profileId"],"durableState":row,"actualStagedResourceRecords":staged,"publishedResource":published,"reportedPositivePreStop":j.get("positiveStagingCountBeforeStop"),"reportedTerminal":j.get("terminal"),"ownedGameStop":j.get("ownedGameStopSucceeded"),"originalHashesRestored":j.get("originalHashesRestored")}
if __name__=="__main__":
 p=argparse.ArgumentParser();p.add_argument("report",type=Path);a=p.parse_args();print(json.dumps(inspect(a.report),indent=2))
