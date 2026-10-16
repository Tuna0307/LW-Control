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
import sqlite3
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
EXPECTED_26 = {
    "LWScripts.data": "49f6249d1530b5f16aad3750388d017ac3032f4b8ef584eec6cff9ea01d19b6d",
    "LWScripts.txt": "52d5b30c486e9ff222e125eb05a13cc01a6407b0bdb40f15cbf12af0389ffae0",
    "version.txt": "c2356069e9d1e79ca924378153cfbbfb4d4416b1f99d41a2940bfdb66c5319db",
}
# Later official idle-state refresh changed only the numeric version marker.
# Retain both exact v26 triplets; this does not recertify an older live receipt.
EXPECTED_26_CURRENT = {
    **EXPECTED_26,
    "version.txt": "5f9c4ab08cac7457e9111a30e4664920607ea2c115a1433d7be98e97e64244ca",
}
FILE_ROLES = {"data": "LWScripts.data", "metadata": "LWScripts.txt", "version": "version.txt"}


def admitted_original_version(hashes):
    if hashes == EXPECTED:
        return 24
    if hashes in (EXPECTED_26, EXPECTED_26_CURRENT):
        return 26
    raise RuntimeError("Original script triplet is not explicitly admitted")


def require_client_admission(client, hashes):
    version = admitted_original_version(hashes)
    anchors = {
        "gameSha256": "905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d",
        "assemblyCSharpSha256": "bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e",
        "xluaSha256": "d22d912f031c60f2649fdaf76d359d695511f7a37b93cd637b557f8346569d45",
        "luaEntrySha256": "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137",
    }
    if (client.get("contentVersion") != version or
            client.get("entryCount") != (18741 if version == 24 else 18743) or
            client.get("packageSha256") != hashes["LWScripts.data"] or
            client.get("compatibilityPolicy") != "lwbridge-current-client-critical-anchors-3" or
            any(client.get(key) != value for key, value in anchors.items())):
        raise RuntimeError("Exact original package/current critical-client admission failed")
    return version


def current_processes():
    checked = subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         "@(Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('LastWar.exe','LastWarLauncher.exe','LWBridge.Desktop.exe') } | Select-Object Name,ProcessId,ExecutablePath) | ConvertTo-Json -Compress"],
        capture_output=True, text=True, check=True,
    )
    rows = json.loads(checked.stdout) if checked.stdout.strip() else []
    return rows if isinstance(rows, list) else [rows]


def _same_path(a, b):
    return isinstance(a, str) and os.path.normcase(os.path.abspath(a)) == os.path.normcase(os.path.abspath(str(b)))


def audit_session_restoration(destination, prior_hashes, *, installed_dir, game_root,
                              process_rows, normal_recovery_journal, host_exit=None):
    """Read-only same-session audit. Never changes the historical exit receipt.

    All bytes, start/stop receipts and backup manifest must agree with an
    explicitly admitted original. Preflight is not treated as authoritative
    after the official launcher has settled a documented newer package.
    """
    destination, installed_dir, game_root = map(Path, (destination, installed_dir, game_root))
    read = lambda path: json.loads(path.read_text(encoding="utf-8"))
    owned = read(destination / "owned-process.json")
    config = read(destination / "config.json")
    outcome = host_exit if host_exit is not None else read(destination / "owned-exit.json")
    if (type(owned.get("pid")) is not int or owned["pid"] <= 0 or
            outcome.get("pid") != owned["pid"] or type(outcome.get("exitCode")) is not int or
            config.get("profileId") != "map014-native-ui" or
            not _same_path(config.get("gameRoot"), game_root)):
        raise RuntimeError("Task-owned host exit/profile/path was not established")
    evidence = destination / "overview-evidence"
    sessions = [path for path in evidence.iterdir() if path.is_dir() and (path / "helper-start.json").is_file()]
    if len(sessions) != 1:
        raise RuntimeError("Exactly one task-owned helper session is required")
    session_dir = sessions[0]
    start, stop = read(session_dir / "helper-start.json"), read(session_dir / "helper-stop.json")
    host_start, host_stop = read(session_dir / "host-start.json"), read(session_dir / "host-stop.json")
    session = start.get("sessionId")
    game_pid = start.get("gamePid")
    game_path = game_root / "Game" / "LastWar.exe"
    if (start.get("ok") is not True or session != session_dir.name or
            start.get("profileId") != config["profileId"] or
            not isinstance(game_pid, int) or game_pid <= 0 or
            not start.get("gameStartedAtUtc") or not _same_path(start.get("gamePath"), game_path) or
            not _same_path(host_start.get("hostProcess", {}).get("path"), owned.get("publish"))):
        raise RuntimeError("Captured game/helper ownership is incomplete")
    for receipt in (stop, host_start, host_stop):
        if receipt.get("sessionId") != session or receipt.get("profileId") != config["profileId"]:
            raise RuntimeError("Foreign helper/host session or profile")
    for receipt in (stop, host_start):
        if (receipt.get("gamePid") != game_pid or not _same_path(receipt.get("gamePath"), game_path) or
                receipt.get("gameStartedAtUtc") != start["gameStartedAtUtc"]):
            raise RuntimeError("Foreign or replaced game identity")
    close = stop.get("close", {})
    if (stop.get("ok") is not True or stop.get("gameRunning") is not False or
            close.get("processExited") is not True or close.get("accepted") is not True or
            close.get("pid") != game_pid or not _same_path(close.get("path"), game_path) or
            close.get("startedAtUtc") != start["gameStartedAtUtc"] or
            close.get("exitProof", {}).get("established") is not True):
        raise RuntimeError("Exact captured game exit/Stop proof is missing")
    restore = stop.get("restore", {})
    if (restore.get("restored") is not True or host_stop.get("helper", {}).get("restore") != restore or
            host_stop.get("helper", {}).get("gamePid") != game_pid or
            host_stop.get("helper", {}).get("sessionId") != session or
            not _same_path(host_stop.get("helper", {}).get("evidencePath"), session_dir / "helper-stop.json")):
        raise RuntimeError("Same-session host/helper restoration receipt is missing")
    original = restore.get("originalFiles", {})
    restored = restore.get("restoredFiles", {})
    actual_hashes = {name: hashlib.sha256((installed_dir / name).read_bytes()).hexdigest() for name in EXPECTED}
    expected_hashes = {name: original.get(role, {}).get("sha256") for role, name in FILE_ROLES.items()}
    content_version = require_client_admission(start.get("currentClient", {}), expected_hashes)
    if actual_hashes != expected_hashes or restore.get("packageSha256") != expected_hashes["LWScripts.data"]:
        raise RuntimeError("Installed bytes do not match the captured admitted original")
    backups = [path for path in (destination / "overview-bridge-backups").glob("*/manifest.json")
               if read(path).get("sessionId") == session]
    if len(backups) != 1:
        raise RuntimeError("Exactly one same-session original backup is required")
    manifest_path = backups[0]
    manifest = read(manifest_path)
    if (manifest.get("requestId") != session or manifest.get("profileId") != config["profileId"] or
            manifest.get("gamePid") != game_pid or manifest.get("gameStartedAtUtc") != start["gameStartedAtUtc"] or
            not _same_path(manifest.get("gamePath"), game_path) or
            not _same_path(manifest.get("backupPath"), manifest_path.parent) or
            manifest.get("stage") != "restored" or manifest.get("originalFiles") != original or
            manifest.get("challengeSha256") != start.get("challengeSha256") or
            not start.get("challengeSha256")):
        raise RuntimeError("Backup manifest belongs to another owner or is not restored")
    candidate_sha = manifest.get("candidate", {}).get("packageSha256")
    if (not isinstance(candidate_sha, str) or len(candidate_sha) != 64 or
            start.get("candidate", {}).get("packageSha256") != candidate_sha or
            candidate_sha == actual_hashes["LWScripts.data"]):
        raise RuntimeError("Injected candidate identity is missing, foreign or still installed")
    for role, name in FILE_ROLES.items():
        saved = manifest_path.parent / name
        file_size = saved.stat().st_size
        if (hashlib.sha256(saved.read_bytes()).hexdigest() != expected_hashes[name] or
                any(not _same_path(value.get("path"), installed_dir / name) or
                    value.get("sha256") != expected_hashes[name] or value.get("size") != file_size
                    for value in (original.get(role, {}), restored.get(role, {}))) or
                (installed_dir / name).stat().st_size != file_size):
            raise RuntimeError("Original backup bytes/restored file record disagree")
    changed = expected_hashes != prior_hashes
    admitted_original_version(prior_hashes)
    if changed:
        settled = read(evidence / "official-settled-client.json")
        known_forward_change = (
            prior_hashes == EXPECTED and expected_hashes in (EXPECTED_26, EXPECTED_26_CURRENT) or
            prior_hashes == EXPECTED_26 and expected_hashes == EXPECTED_26_CURRENT)
        if (not known_forward_change or
                settled.get("schemaVersion") != 1 or not _same_path(settled.get("gameRoot"), game_root) or
                settled.get("packageSha256") != expected_hashes["LWScripts.data"] or
                datetime.datetime.fromisoformat(settled["settledAtUtc"]) >=
                datetime.datetime.fromisoformat(start["gameStartedAtUtc"].replace("Z", "+00:00"))):
            raise RuntimeError("Undocumented or unsupported official pre-install package change")
    if (destination / "overview-bridge/recovery.json").exists() or Path(normal_recovery_journal).exists():
        raise RuntimeError("A recovery journal remains")
    for row in process_rows:
        executable = row.get("ExecutablePath")
        installation_process = (isinstance(executable, str) and
                                Path(executable).resolve().is_relative_to(game_root.resolve()))
        unknown_game_process = (row.get("Name") in ("LastWar.exe", "LastWarLauncher.exe") and not executable)
        if row.get("ProcessId") in (owned["pid"], game_pid) or installation_process or unknown_game_process:
            raise RuntimeError("Captured host/game or surviving installation process remains")
    return {
        "state": "proven", "profileId": config["profileId"], "sessionId": session,
        "gamePid": game_pid, "hostPid": owned["pid"], "contentVersion": content_version,
        "diagnosticResultSucceeded": (outcome["exitCode"] == 0 and
                                      outcome.get("proof") is True and outcome.get("error") is None),
        "officialSettledOriginalChangedBeforeInstall": changed,
        "preflightOriginalHashes": prior_hashes, "capturedOriginalHashes": expected_hashes,
        "installedHashes": actual_hashes, "backupManifest": str(manifest_path),
        "originalScriptsRestored": {name: True for name in expected_hashes},
        "recoveryJournalPresent": False,
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
    admitted_original_version(hashes)
    check = subprocess.run(
        [sys.executable, str(ROOT / "tools/run_overview_bridge_current.py"),
         "check-only", "--game-root", str(GAME)],
        capture_output=True, text=True, check=True,
    )
    admission = json.loads(check.stdout)
    if not admission["ok"]:
        raise RuntimeError("Game compatibility admission failed")
    require_client_admission(admission["currentClient"], hashes)
    return hashes


def main():
    if len(sys.argv) not in (2, 4) or sys.argv[1] not in ("browse", "remote", "remote-railway", "remote-reuse", "track", "expiry", "city", "other", "railway", "mixed", "all-nontruck", "truck", "all-nine", "truck-stop"):
        raise SystemExit("usage: python tools/map_014_lead_native/run_ui.py browse|remote|remote-railway|remote-reuse|track|expiry|city|other|railway|mixed|all-nontruck|truck|all-nine|truck-stop")
    mode = sys.argv[1]
    saved_db = None
    if len(sys.argv) == 4:
        if mode != "browse" or sys.argv[2] != "--saved-db":
            raise RuntimeError("Explicit saved native data is supported only for browse proof")
        saved_db = Path(sys.argv[3]).resolve()
        if not saved_db.is_relative_to((ROOT / "artifacts").resolve()) or not saved_db.is_file():
            raise RuntimeError("Saved proof DB must be an existing task artifact")
        with sqlite3.connect(saved_db.as_uri() + "?mode=ro", uri=True) as readonly:
            last = readonly.execute("SELECT status,total_blocks,completed_blocks,failed_blocks FROM scan_runs ORDER BY created_at DESC LIMIT 1").fetchone()
            if last != ("completed", 2500, 2500, 0):
                raise RuntimeError("Saved native DB is not a complete successful full-world run")
    prior_hashes = admit()
    destination = ROOT / "artifacts/map014-takeover" / (
        "native-ui-" + datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S"))
    destination.mkdir(parents=True, exist_ok=False)
    (destination / "preflight-originals.json").write_text(json.dumps(prior_hashes, indent=2), encoding="utf-8")
    profile = "map014-native-ui"
    source = (ROOT / "artifacts/map014-takeover/native-ui-20261013-123802/profiles/map014-native-ui/map-data"
              if mode == "remote-reuse" else
              ROOT / "artifacts/map014-lead-ui/native-20261013-104453/profiles/map014-native-ui/map-data")
    if saved_db is not None:
        source = saved_db.parent
    if not (source / "map-data.db").is_file():
        raise FileNotFoundError("Preserved genuine published Map database is unavailable")
    store = destination / "profiles" / profile / "map-data"
    store.mkdir(parents=True)
    if mode in ("browse", "remote", "remote-reuse", "track", "expiry"):
        with sqlite3.connect((source / "map-data.db").resolve().as_uri() + "?mode=ro", uri=True) as readonly, \
             sqlite3.connect(store / "map-data.db") as snapshot:
            readonly.backup(snapshot)
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
    # LWBRIDGE_MAP014_UI_EXE lets the same proof drive an extracted release ZIP instead of the publish folder.
    publish = Path(os.environ["LWBRIDGE_MAP014_UI_EXE"]).resolve(strict=True) if os.environ.get("LWBRIDGE_MAP014_UI_EXE") else ROOT / "artifacts/map014-takeover/publish/LWBridge.Desktop.exe"
    proof = destination / "native-ui-map.json"
    environment = dict(os.environ, LWBRIDGE_MAP014_NATIVE_UI="1",
                       LWBRIDGE_MAP014_SELECTION_UI=mode if mode in ("city", "other", "railway", "all-nontruck", "truck", "all-nine", "truck-stop") else "",
                       LWBRIDGE_MAP014_BROWSE_ONLY="1" if mode in ("browse", "track", "expiry") else "0",
                       LWBRIDGE_MAP014_REMOTE_UI="1" if mode in ("remote","remote-reuse","remote-railway") else "0",
                       LWBRIDGE_MAP014_REMOTE_SAVED_HOME="1" if mode in ("remote","remote-reuse","remote-railway") else "0",
                       LWBRIDGE_MAP014_REMOTE_REUSE="1" if mode == "remote-reuse" else "0",
                       LWBRIDGE_MAP016_REMOTE_RAILWAY="1" if mode == "remote-railway" else "0",
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
    deadline = time.monotonic() + (1200 if mode in ("remote", "remote-railway") else 900)
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
    try:
        restoration = audit_session_restoration(
            destination, prior_hashes, installed_dir=LUA, game_root=GAME,
            process_rows=current_processes(), normal_recovery_journal=
            Path(os.environ["LOCALAPPDATA"]) / "LWBridgeRebuild/overview-bridge/recovery.json",
            host_exit=final)
        final["restorationAudit"] = restoration
        final["originalScriptsRestored"] = restoration["originalScriptsRestored"]
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        final["restorationAuditError"] = str(error)
    (destination / "owned-exit.json").write_text(json.dumps(final, indent=2), encoding="utf-8")
    print("MAP014_OWNED_UI_RESULT", json.dumps(final), flush=True)
    if owned.returncode or not final["proof"] or final["error"] or final.get("restorationAuditError") or not all(final["originalScriptsRestored"].values()) or final["recoveryJournalPresent"]:
        raise SystemExit(2)


if __name__ == "__main__":
    main()
