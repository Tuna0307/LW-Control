"""Prepare isolated copies of *published* genuine City + Resource datasets.
Never modifies or opens the archived source in write mode; no scan is invoked.
"""
import csv, hashlib, json, os, sqlite3, subprocess, sys, tempfile, time, uuid
from pathlib import Path

CITY=Path(os.environ["TEMP"])/"LWB317-LIVE-PILOT-001-root"/"profiles"/"lwb317-live-pilot-001"/"map-data"/"map-data.db"
RESOURCE=Path(os.environ["TEMP"])/"LWB317-A-TO-A-CAMPAIGN-007-RESOURCEREPLAY"/"profile-A"/"map-data"/"map-data.db"
EXPECTED={"city":"056bce6a28acdf14e4c7bad37207d1733126d42dbe621c2d11e816d41eed1678","resource":"641f70f6ce7c69c6d2b8af0d3afa9fe8ad50a0bbcabd0308667719509afc78a8"}
OUTPUT=Path(__file__).resolve().parents[1]/"artifacts"/"map-data-003"
RECEIPT=OUTPUT/"preflight.json"
def sha(p):
    h=hashlib.sha256()
    with open(p,"rb") as f:
        for chunk in iter(lambda:f.read(2*1024*1024),b""):h.update(chunk)
    return h.hexdigest()
def connect_ro(p):
    return sqlite3.connect('file:'+p.as_posix()+'?mode=ro',uri=True)
def active_game():
    output=subprocess.check_output(["tasklist","/fo","csv","/nh"],text=True,errors="replace")
    return [row[:2] for row in csv.reader(output.splitlines()) if len(row)>1 and row[0].lower() in
            ("lastwar.exe","lastwarlauncher.exe","lwbridge.desktop.exe")]
def counts(db):
    return {f"{s}:{k}":c for s,k,c in db.execute("select server_id,kind,count(*) from map_records group by server_id,kind")}
def prepare():
    if RECEIPT.exists():raise RuntimeError("Preflight already exists; do not silently replace test data")
    if active_game():raise RuntimeError(f"Active native process: {active_game()}")
    sources={"city":CITY,"resource":RESOURCE}
    for kind,p in sources.items():
        if not p.is_file() or sha(p)!=EXPECTED[kind]:raise RuntimeError(f"Archived {kind} dataset changed or missing")
        with connect_ro(p) as db:
            actual=counts(db)
            if actual!={f"2212:{kind}":{"city":7000,"resource":8008}[kind]}:raise RuntimeError(f"Unexpected published rows {kind}: {actual}")
    OUTPUT.mkdir(parents=True,exist_ok=True)
    root=Path(tempfile.mkdtemp(prefix="LWB317-MAP-DATA-003-"))
    profile="map-data-003-"+uuid.uuid4().hex[:12]
    dst=root/"profiles"/profile/"map-data"/"map-data.db"
    dst.parent.mkdir(parents=True)
    with connect_ro(CITY) as source,sqlite3.connect(dst) as destination:
        source.backup(destination)
        # Merge *published* Resource rows only; no stage table, scan execution
        # or public game records are added to the git repository.
        destination.execute("ATTACH DATABASE ? AS genuine_resource",(str(RESOURCE),))
        destination.execute("""
           INSERT INTO map_records
           (kind,server_id,record_key,point_index,uuid,name,alliance_name,level,
            quality,power,distance,shield_end_time,updated_at,data_json)
           SELECT kind,server_id,record_key,point_index,uuid,name,alliance_name,level,
            quality,power,distance,shield_end_time,updated_at,data_json
           FROM genuine_resource.map_records WHERE kind='resource' AND server_id=2212
        """)
        destination.commit()
        destination.execute("DETACH DATABASE genuine_resource")
        actual=counts(destination)
        if actual!={"2212:city":7000,"2212:resource":8008}:raise RuntimeError(f"Bad merged counts: {actual}")
        print("PUBLISHED_MERGED",actual,"staging",destination.execute("select count(*) from scan_records").fetchone()[0])
        if destination.execute("pragma quick_check").fetchone()[0]!="ok":raise RuntimeError("SQLite quick_check failed")
    data={"schemaVersion":1,"owner":"LWBridgeRebuild","profileId":profile,"gameRoot":"",
          "autoLaunchGame":False,"autoReconnect":False,"gameDesiredRunning":False,"serverJumpHistory":[]}
    (root/"config.json").write_text(json.dumps(data),encoding="utf8")
    with sqlite3.connect(root/"controller.db") as db:
        db.executescript("""CREATE TABLE controller_state(key TEXT PRIMARY KEY,value TEXT NOT NULL);
        CREATE TABLE profiles(id TEXT PRIMARY KEY,display_name TEXT NOT NULL,role_name TEXT,
        server_id TEXT,game_uid TEXT UNIQUE,note TEXT NOT NULL DEFAULT '',display_order INTEGER NOT NULL,
        enabled INTEGER NOT NULL DEFAULT 1,locked_reason TEXT,is_primary INTEGER NOT NULL DEFAULT 0,
        created_at INTEGER NOT NULL,updated_at INTEGER NOT NULL,last_launched_at INTEGER);
        CREATE UNIQUE INDEX idx_profiles_primary ON profiles(is_primary) WHERE is_primary=1;""")
        now=int(time.time()*1000)
        db.execute("INSERT INTO profiles(id,display_name,display_order,enabled,is_primary,created_at,updated_at) VALUES(?,? ,0,1,1,?,?)",(profile,"Isolated genuine stored Map data",now,now))
        db.execute("INSERT INTO controller_state(key,value) VALUES('selected_profile_id',?)",(profile,))
    report={"task":"LWB317-MAP-DATA-DELIVERY-003","isolatedRoot":str(root),
      "profile":profile,"dbPath":str(dst),"sourceSha256":EXPECTED,
      "sourcePaths":{k:str(v) for k,v in sources.items()},
      "publishedCounts":{"city":7000,"resource":8008},"copiedDbSha256":sha(dst),
      "scanRecordsCopied":0,"newScanOperations":0,"sourceHashesVerifiedAfter":{k:sha(v) for k,v in sources.items()},
      "nativeProcessesBefore":[],"stagingRecordsInCopy":0,
      "startupWarning":"Native Home auto-launch admission must be verified separately. Do not assume an initial config.json false suppresses all per-profile startup work."}
    RECEIPT.write_text(json.dumps(report,indent=2),encoding="utf8")
    print(json.dumps(report,indent=2))
if __name__=="__main__":prepare()
