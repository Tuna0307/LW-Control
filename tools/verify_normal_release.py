"""Safe normal packaged-UI smoke: disabled registry profile, intact shipped AutoLaunch preference.

Usage: python tools/verify_normal_release.py start path/to/published/LWBridge.Desktop.exe
       python tools/verify_normal_release.py stop path/to/receipt.json
Only closes the PID in its own receipt. Does not touch owner data or game processes.
"""
import csv
import ctypes
import datetime
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import time
import uuid

def game_tasks():
    output = subprocess.check_output(["tasklist", "/fo", "csv", "/nh"], text=True, errors="replace")
    return sorted((row[0],row[1]) for row in csv.reader(output.splitlines())
                  if len(row)>1 and ("lastwar" in row[0].lower() or "last war" in row[0].lower()))

def windows_for(pid):
    user32 = ctypes.windll.user32
    ids=[]
    CALLBACK=ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
    def visit(hwnd,unused):
        owner=ctypes.c_ulong()
        user32.GetWindowThreadProcessId(hwnd,ctypes.byref(owner))
        if owner.value==pid and user32.IsWindowVisible(hwnd):
            title=ctypes.create_unicode_buffer(256)
            user32.GetWindowTextW(hwnd,title,256)
            ids.append({"hwnd":int(hwnd),"title":title.value})
        return True
    user32.EnumWindows(CALLBACK(visit),0)
    return ids

def start(exe):
    exe=Path(exe).resolve(strict=True)
    if exe.name.lower()!="lwbridge.desktop.exe": raise ValueError("Expected packaged LWBridge.Desktop.exe")
    root=Path(tempfile.mkdtemp(prefix="lwb317-normal-ui-disabled-"))
    profile="release-disabled-"+uuid.uuid4().hex[:12]
    config={"schemaVersion":1,"owner":"LWBridgeRebuild","profileId":profile,
        "autoLaunchGame":True,"autoReconnect":False,"gameDesiredRunning":False}
    (root/"config.json").write_text(json.dumps(config),encoding="utf-8")
    with sqlite3.connect(root/"controller.db") as db:
        db.executescript("""CREATE TABLE controller_state (key TEXT PRIMARY KEY,value TEXT NOT NULL);
        CREATE TABLE profiles (
        id TEXT PRIMARY KEY, display_name TEXT NOT NULL,role_name TEXT,server_id TEXT,
        game_uid TEXT UNIQUE,note TEXT NOT NULL DEFAULT '',display_order INTEGER NOT NULL,
        enabled INTEGER NOT NULL DEFAULT 1,locked_reason TEXT,is_primary INTEGER NOT NULL DEFAULT 0,
        created_at INTEGER NOT NULL,updated_at INTEGER NOT NULL,last_launched_at INTEGER);
        CREATE UNIQUE INDEX idx_profiles_primary ON profiles(is_primary) WHERE is_primary=1;""")
        now=int(time.time()*1000)
        db.execute("INSERT INTO profiles (id,display_name,display_order,enabled,locked_reason,is_primary,created_at,updated_at) VALUES (?,?,0,0,'RELEASE_UI_SMOKE',1,?,?)",(profile,"Release UI smoke (disabled)",now,now))
        db.execute("INSERT INTO controller_state (key,value) VALUES ('selected_profile_id',?)",(profile,))
        db.commit()
        assert db.execute("SELECT enabled,locked_reason FROM profiles").fetchone()==(0,"RELEASE_UI_SMOKE")
    original=game_tasks()
    process=subprocess.Popen([str(exe),"--isolated-root",str(root)],cwd=str(exe.parent),
        creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
    time.sleep(8)
    current=game_tasks()
    found=windows_for(process.pid)
    info={"timestamp":datetime.datetime.now(datetime.timezone.utc).isoformat(),"exe":str(exe),
        "root":str(root),"pid":process.pid,"autolaunchShippedDefault":True,
        "registryEnabled":False,"registryLock":"RELEASE_UI_SMOKE",
        "gameBefore":original,"gameAfter":current,"newGameProcesses":sorted(set(current)-set(original)),
        "running":process.poll() is None,"windows":found}
    receipt=root/"normal-smoke.json"
    receipt.write_text(json.dumps(info,indent=2),encoding="utf-8")
    print(json.dumps({"receipt":str(receipt),**info},indent=2))
    if not info["running"] or not found or info["newGameProcesses"]:sys.exit(1)

def stop(receipt):
    info=json.loads(Path(receipt).read_text(encoding="utf-8"))
    pid=int(info["pid"])
    windows=windows_for(pid)
    for entry in windows:ctypes.windll.user32.PostMessageW(entry["hwnd"],0x0010,0,0)
    ended=False
    for i in range(20):
        result=subprocess.run(["tasklist","/fi",f"PID eq {pid}","/fo","csv","/nh"],capture_output=True,text=True)
        if str(pid) not in result.stdout:
            ended=True; break
        time.sleep(0.5)
    if not ended:
        raise RuntimeError(f"Owned packaged GUI PID {pid} did not close; do not kill unrelated processes")
    with sqlite3.connect(Path(info["root"])/"controller.db") as db:
        enabled,lock=db.execute("SELECT enabled,locked_reason FROM profiles").fetchone()
    after=game_tasks()
    result={"closedOwnedPid":pid,"registryStillDisabled":enabled==0 and lock=="RELEASE_UI_SMOKE",
        "newGameProcesses":sorted(set(after)-set(tuple(x) for x in info["gameBefore"])),
        "root":info["root"]}
    (Path(info["root"])/"normal-smoke-finished.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    print(json.dumps(result,indent=2))
    if not result["registryStillDisabled"] or result["newGameProcesses"]:sys.exit(1)

if __name__=="__main__":
    if len(sys.argv)!=3 or sys.argv[1] not in ("start","stop"):
        raise SystemExit(__doc__)
    if sys.argv[1]=="start":start(sys.argv[2])
    else:stop(sys.argv[2])
