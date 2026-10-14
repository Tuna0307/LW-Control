"""Isolated, genuine MAP014 packaged WebView proof using preserved native rows.

Never imports synthetic Map rows, never interacts with owner desktop controls,
and never stops a process unless this invocation started it.
"""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
GAME = Path.home() / "AppData/Local/FunFly/Last War-Survival Game"
LUA = Path.home() / "AppData/LocalLow/FunFly/Last War-Survival Game/lwScripts"
EXPECTED = {
    "LWScripts.data": "d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495",
    "LWScripts.txt": "b84cfa8762d9f8b93fcbe9e64b8715e84d1549248c2e98dc7dc272321476933b",
    "version.txt": "c2356069e9d1e79ca924378153cfbbfb4d4416b1f99d41a2940bfdb66c5319db",
}


def admit():
    check = subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         "Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('LastWar.exe','LastWarLauncher.exe','LWBridge.Desktop.exe') } | Select-Object -ExpandProperty ProcessId"],
        capture_output=True, text=True, check=True,
    )
    if check.stdout.strip():
        raise RuntimeError("An existing app/game process must remain untouched: " + check.stdout)
    journal = Path(os.environ["LOCALAPPDATA"]) / "LWBridgeRebuild/overview-bridge/recovery.json"
    if journal.exists():
        raise RuntimeError("Existing recovery journal requires independent ownership review")
    hashes = {name: hashlib.sha256((LUA / name).read_bytes()).hexdigest() for name in EXPECTED}
    if hashes != EXPECTED:
        raise RuntimeError("Current installed game scripts no longer match admitted originals")
    check = subprocess.run(
        [sys.executable, str(ROOT / "tools/run_overview_bridge_current.py"),
         "check-only", "--game-root", str(GAME)],
        capture_output=True, text=True, check=True,
    )
    admission = json.loads(check.stdout)
    if not admission["ok"] or admission["currentClient"]["contentVersion"] != 24:
        raise RuntimeError("Game compatibility admission failed")
    return hashes


def main():
    if len(sys.argv) != 2 or sys.argv[1] not in ("browse", "remote", "remote-reuse", "track", "expiry", "city", "other"):
        raise SystemExit("usage: python tools/map_014_lead_native/run_ui.py browse|remote|remote-reuse|track|expiry|city|other")
    mode = sys.argv[1]
    prior_hashes = admit()
    destination = ROOT / "artifacts/map014-takeover" / (
        "native-ui-" + datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S"))
    destination.mkdir(parents=True, exist_ok=False)
    profile = "map014-native-ui"
    source = (ROOT / "artifacts/map014-takeover/native-ui-20261013-123802/profiles/map014-native-ui/map-data"
              if mode == "remote-reuse" else
              ROOT / "artifacts/map014-lead-ui/native-20261013-104453/profiles/map014-native-ui/map-data")
    if not (source / "map-data.db").is_file():
        raise FileNotFoundError("Preserved genuine published Map database is unavailable")
    store = destination / "profiles" / profile / "map-data"
    store.mkdir(parents=True)
    if mode in ("browse", "remote", "remote-reuse", "track", "expiry"):
        for item in source.glob("map-data.db*"):
            if item.name in ("map-data.db", "map-data.db-wal", "map-data.db-shm"):
                shutil.copy2(item, store / item.name)
    (destination / "source.json").write_text(json.dumps({
        "genuine": True, "fixture": False, "mode": mode,
        "source": str(source),
        "sha256": hashlib.sha256((source / "map-data.db").read_bytes()).hexdigest()
    }, indent=2), encoding="utf-8")
    (destination / "config.json").write_text(json.dumps({
        "schemaVersion": 1, "owner": "LWBridgeRebuild", "profileId": profile,
        "gameRoot": str(GAME), "autoLaunchGame": False, "autoReconnect": False,
        "gameDesiredRunning": False, "serverJumpHistory": []
    }), encoding="utf-8")
    publish = ROOT / "artifacts/map014-takeover/publish/LWBridge.Desktop.exe"
    proof = destination / "native-ui-map.json"
    environment = dict(os.environ, LWBRIDGE_MAP014_NATIVE_UI="1",
                       LWBRIDGE_MAP014_SELECTION_UI=mode if mode in ("city", "other") else "",
                       LWBRIDGE_MAP014_BROWSE_ONLY="1" if mode in ("browse", "track", "expiry") else "0",
                       LWBRIDGE_MAP014_REMOTE_UI="1" if mode in ("remote","remote-reuse") else "0",
                       LWBRIDGE_MAP014_REMOTE_SAVED_HOME="1" if mode in ("remote","remote-reuse") else "0",
                       LWBRIDGE_MAP014_REMOTE_REUSE="1" if mode == "remote-reuse" else "0",
                       LWBRIDGE_MAP014_TRACKING_UI="1" if mode == "track" else "0",
                       LWBRIDGE_MAP014_EXPIRY_UI="1" if mode == "expiry" else "0")
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 0
    owned = subprocess.Popen([
        str(publish), "--isolated-root", str(destination),
        "--normal-ui-live-map-proof", str(proof),
        "--view", "map-data", "--language", "en", "--theme", "light"
    ], startupinfo=startup, env=environment)
    (destination / "owned-process.json").write_text(json.dumps({
        "pid": owned.pid, "publish": str(publish), "source": str(source)
    }, indent=2), encoding="utf-8")
    print(f"MAP014_OWNED_UI_PID={owned.pid} ROOT={destination}", flush=True)
    deadline = time.monotonic() + (1200 if mode == "remote" else 900)
    while owned.poll() is None:
        if time.monotonic() >= deadline:
            raise TimeoutError(f"Owned app PID {owned.pid} exceeded proof bound; inspect exact owner before cleanup")
        time.sleep(5)
    final = dict(pid=owned.pid, exitCode=owned.returncode, proof=proof.exists(),
                 error=Path(str(proof) + ".error.txt").read_text(encoding="utf-8")
                 if Path(str(proof) + ".error.txt").exists() else None,
                 originalScriptsRestored={
                     name: hashlib.sha256((LUA / name).read_bytes()).hexdigest() == sha
                     for name, sha in prior_hashes.items()},
                 recoveryJournalPresent=(Path(os.environ["LOCALAPPDATA"]) /
                                         "LWBridgeRebuild/overview-bridge/recovery.json").exists())
    (destination / "owned-exit.json").write_text(json.dumps(final, indent=2), encoding="utf-8")
    print("MAP014_OWNED_UI_RESULT", json.dumps(final), flush=True)
    if owned.returncode or not final["proof"] or final["error"] or not all(final["originalScriptsRestored"].values()) or final["recoveryJournalPresent"]:
        raise SystemExit(2)


if __name__ == "__main__":
    main()
