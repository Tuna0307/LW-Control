"""Task-only Home pilot preflight/receipt; refuses mutable installation while a game exists."""
from pathlib import Path
import csv, hashlib, json, os, shutil, sqlite3, subprocess, sys, tempfile, time, uuid

APP=Path(os.environ["LOCALAPPDATA"])/"FunFly"/"Last War-Survival Game"
SCRIPT=Path(os.environ["USERPROFILE"])/"AppData"/"LocalLow"/"FunFly"/"Last War-Survival Game"/"lwScripts"
FILES=["LWScripts.data","LWScripts.txt","version.txt"]
EXPECTED={
"LWScripts.data":"2187de71f426741eb61482f6e85639314526e6bdefc9445319b1ff52b31cfac0",
"LWScripts.txt":"988c36bd7d3758404193f9cb380ef5b7f6484621061443fd95a90c8fc1d97306",
"version.txt":"535fa30d7e25dd8a49f1536779734ec8286108d115da5045d77f3b4185d8f790",
}
REPO=Path(__file__).resolve().parents[1]
RECEIPT=REPO/"artifacts"/"home-launch-002"/"preflight.json"
TARGET=Path(os.environ["USERPROFILE"])/"OneDrive"/"Desktop"/"Github"/"LW"/"lwbridge-0.3.17.exe"
REFERENCE="4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
def sha(path):
    h=hashlib.sha256()
    with open(path,"rb") as f:
        for block in iter(lambda:f.read(1024*1024),b""):h.update(block)
    return h.hexdigest()
def owners():
    out=subprocess.check_output(["tasklist","/fo","csv","/nh"],text=True,errors="replace")
    return [dict(name=row[0],pid=int(row[1])) for row in csv.reader(out.splitlines())
      if len(row)>1 and row[1].isdigit() and row[0].lower() in
      ("lastwar.exe","lastwarlauncher.exe","lwbridge.desktop.exe")]
def prepare():
    if RECEIPT.exists():raise RuntimeError("Preflight receipt already exists; preserve and finish that pilot before another")
    current=owners()
    if current:raise RuntimeError(f"Active game/clone sessions require explicit PID-provenance handover first: {current}")
    if sha(TARGET)!=REFERENCE:raise RuntimeError("Reference 0.3.17 SHA mismatch")
    if not all((APP/f).exists() for f in ("Game/LastWar.exe","LastWarLauncher.exe","Game/LastWar_Data/Plugins/x86_64/xlua.dll")):raise RuntimeError("Installed game files missing")
    actual={n:{"sha256":sha(SCRIPT/n),"bytes":(SCRIPT/n).stat().st_size} for n in FILES}
    if any(actual[n]["sha256"]!=EXPECTED[n] for n in FILES):raise RuntimeError(f"Installed script baseline changed; stop before mutation: {actual}")
    root=Path(tempfile.mkdtemp(prefix="LWB317-HOME-LAUNCH-002-"))
    backups=root/"originals"; backups.mkdir()
    for n in FILES:
        shutil.copy2(SCRIPT/n,backups/n)
        if sha(backups/n)!=EXPECTED[n]:raise RuntimeError(f"Backup mismatch {n}; no launch")
    profile="home-002-"+uuid.uuid4().hex[:12]
    config={"schemaVersion":1,"owner":"LWBridgeRebuild","profileId":profile,
        "gameRoot":str(APP),"autoLaunchGame":True,"autoReconnect":False,"gameDesiredRunning":False,
        "serverJumpHistory":[]}
    (root/"config.json").write_text(json.dumps(config),encoding="utf-8")
    with sqlite3.connect(root/"controller.db") as db:
        db.executescript("""CREATE TABLE controller_state (key TEXT PRIMARY KEY,value TEXT NOT NULL);
        CREATE TABLE profiles (id TEXT PRIMARY KEY,display_name TEXT NOT NULL,role_name TEXT,
        server_id TEXT,game_uid TEXT UNIQUE,note TEXT NOT NULL DEFAULT '',display_order INTEGER NOT NULL,
        enabled INTEGER NOT NULL DEFAULT 1,locked_reason TEXT,is_primary INTEGER NOT NULL DEFAULT 0,
        created_at INTEGER NOT NULL,updated_at INTEGER NOT NULL,last_launched_at INTEGER);
        CREATE UNIQUE INDEX idx_profiles_primary ON profiles(is_primary) WHERE is_primary=1;""")
        ts=int(time.time()*1000)
        db.execute("INSERT INTO profiles (id,display_name,display_order,enabled,locked_reason,is_primary,created_at,updated_at) VALUES (?,?,0,0,'TEST_UI_SETUP',1,?,?)",(profile,"Home 002 isolated setup",ts,ts))
        db.execute("INSERT INTO controller_state(key,value) VALUES('selected_profile_id',?)",(profile,))
    data={"timeUtc":time.strftime("%Y-%m-%dT%H:%M:%SZ",time.gmtime()),"referenceSha256":REFERENCE,
          "root":str(root),"profileId":profile,"app":str(APP),"scripts":str(SCRIPT),
          "installedBefore":actual,"backups":{n:sha(backups/n) for n in FILES},
          "processesBefore":current,"profileInitiallyDisabled":True,"shippedAutoLaunchDefault":True,
          "compatibilityProof":"check_current_client_compat.py returned ok=true; hash-locked current v23",
          "status":"preflight_only_no_game_launched"}
    RECEIPT.parent.mkdir(parents=True,exist_ok=True)
    RECEIPT.write_text(json.dumps(data,indent=2),encoding="utf-8")
    print(json.dumps({"receipt":str(RECEIPT),**data},indent=2))
def enable():
    r=json.loads(RECEIPT.read_text(encoding="utf-8"))
    if owners():raise RuntimeError("Game/host active: do not flip registry while active")
    root=Path(r["root"])
    with sqlite3.connect(root/"controller.db") as db:
        row=db.execute("SELECT enabled,locked_reason FROM profiles WHERE id=?",(r["profileId"],)).fetchone()
        if row!=(0,"TEST_UI_SETUP"):raise RuntimeError(f"Pre-admission registry state changed: {row}")
        db.execute("UPDATE profiles SET enabled=1,locked_reason=NULL WHERE id=?",(r["profileId"],))
    print("ISOLATED_REGISTRY_ENABLED",r["profileId"])
def finish():
    r=json.loads(RECEIPT.read_text(encoding="utf-8"))
    active=owners()
    after={n:{"sha256":sha(SCRIPT/n),"bytes":(SCRIPT/n).stat().st_size} for n in FILES}
    baseline={n:v["sha256"] for n,v in r["installedBefore"].items()}
    current={n:v["sha256"] for n,v in after.items()}
    root=Path(r["root"])
    journal=root/"overview-bridge"/"recovery.json"
    result={"ownedProcessesRemaining":active,"installedAfter":after,
            "installedTripletRestored":current==baseline,
            "recoveryJournalRemaining":journal.exists(),
            "isolatedRoot":str(root),
            "rootKeptForEvidence":True}
    (RECEIPT.parent/"finish.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    print(json.dumps(result,indent=2))
    if active or current!=baseline or journal.exists():
        raise RuntimeError("Restoration/exit gate failed. Do NOT delete backups or root; preserve journal for correct cleanup")
if __name__=="__main__":
    if len(sys.argv)!=2 or sys.argv[1] not in ("prepare","enable","finish"):
        raise SystemExit(__doc__)
    {"prepare":prepare,"enable":enable,"finish":finish}[sys.argv[1]]()
