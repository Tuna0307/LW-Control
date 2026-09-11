from __future__ import annotations

import argparse
import ctypes
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import sys
import traceback

REPO = Path(__file__).resolve().parents[1]
CONFIG_ROOT = Path(os.environ.get("LOCALAPPDATA", "")) / "LWBridgeRebuild"
OWNER_ROOT = CONFIG_ROOT / "owner-evidence"
ACTIVE_POINTER = OWNER_ROOT / "active-owner-check.json"
DEFAULT_EXE = REPO / "src" / "LWBridge.Desktop" / "bin" / "Release" / "net10.0-windows10.0.17763.0" / "LWBridge.Desktop.exe"


class OwnerPreparationBlocked(RuntimeError):
    pass


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def sha256(path: Path) -> str | None:
    if not path.is_file():
        return None
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def app_identity(exe: Path) -> dict:
    dll = exe.with_suffix(".dll")
    return {
        "path": str(exe),
        "exists": exe.is_file(),
        "sha256": sha256(exe),
        "size": exe.stat().st_size if exe.is_file() else None,
        "managedDllPath": str(dll),
        "managedDllExists": dll.is_file(),
        "managedDllSha256": sha256(dll),
        "managedDllSize": dll.stat().st_size if dll.is_file() else None,
    }


def write_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(tmp, path)


def read_json(path: Path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None


def message(title: str, text: str, error: bool = False) -> None:
    flags = 0x10 if error else 0x40
    ctypes.windll.user32.MessageBoxW(None, text, title, flags)


def run_capture(args: list[str], cwd: Path = REPO) -> dict:
    cp = subprocess.run(args, cwd=str(cwd), text=True, capture_output=True, encoding="utf-8", errors="replace")
    return {"args": args, "exitCode": cp.returncode, "stdout": cp.stdout[-12000:], "stderr": cp.stderr[-12000:]}


def process_snapshot() -> list[dict]:
    script = r"""
$names = 'LWBridge.Desktop','LastWar','LastWarLauncher','python','pythonw'
Get-CimInstance Win32_Process | Where-Object { $names -contains ([IO.Path]::GetFileNameWithoutExtension($_.Name)) } |
  ForEach-Object { [pscustomobject]@{ pid=$_.ProcessId; name=$_.Name; path=$_.ExecutablePath; commandLine=$_.CommandLine } } |
  ConvertTo-Json -Compress
"""
    cp = subprocess.run(["powershell.exe", "-NoProfile", "-Command", script], text=True, capture_output=True, encoding="utf-8", errors="replace")
    if cp.returncode != 0 or not cp.stdout.strip():
        return []
    try:
        value = json.loads(cp.stdout)
        return value if isinstance(value, list) else [value]
    except Exception:
        return [{"parseError": cp.stdout[-4000:], "stderr": cp.stderr[-4000:]}]


def relevant_running(processes: list[dict]) -> list[dict]:
    result = []
    for item in processes:
        name = str(item.get("name", "")).lower()
        if name in {"lwbridge.desktop.exe", "lastwar.exe", "lastwarlauncher.exe"}:
            result.append(item)
    return result


def inspect_profile_store() -> dict:
    result: dict = {"configRoot": str(CONFIG_ROOT), "configExists": False, "profileId": None, "database": None, "resourceRows": []}
    config_path = CONFIG_ROOT / "config.json"
    config = read_json(config_path)
    if isinstance(config, dict):
        result["configExists"] = True
        result["profileId"] = config.get("profileId")
        result["gameRoot"] = config.get("gameRoot")
    profile_id = result.get("profileId")
    if not profile_id:
        return result
    db = CONFIG_ROOT / "profiles" / str(profile_id) / "map-data.db"
    result["database"] = str(db)
    result["databaseExists"] = db.is_file()
    if not db.is_file():
        return result
    uri = db.resolve().as_uri() + "?mode=ro"
    try:
        con = sqlite3.connect(uri, uri=True)
        con.row_factory = sqlite3.Row
        rows = con.execute(
            "SELECT server_id, record_key, point_index, level, updated_at, data_json FROM map_records WHERE kind='resource' ORDER BY updated_at DESC LIMIT 100"
        ).fetchall()
        for row in rows:
            data = {}
            try:
                parsed = json.loads(row["data_json"])
                if isinstance(parsed, dict):
                    for key in ("x", "y", "resourceTypeId", "source", "sourceKind", "occupied", "occupancyKnown", "gatherMarchUuid", "gatherUid"):
                        if key in parsed:
                            data[key] = parsed[key]
            except Exception:
                data = {"dataJsonParseError": True}
            result["resourceRows"].append({
                "serverId": row["server_id"], "recordKey": row["record_key"], "pointIndex": row["point_index"],
                "level": row["level"], "updatedAt": row["updated_at"], "data": data,
            })
        con.close()
    except Exception as exc:
        result["databaseReadError"] = f"{type(exc).__name__}: {exc}"
    return result


def recovery_snapshot() -> dict:
    live = CONFIG_ROOT / "live-resource"
    return {
        "recoveryExists": (live / "recovery.json").is_file(),
        "operationOwnerExists": (live / "operation-owner.json").is_file(),
        "resultExists": (live / "result.json").is_file(),
        "resultsDirectoryExists": (live / "results").is_dir(),
    }


def collect_ui_events(ui_dir: Path) -> dict:
    events = []
    errors = []
    paths = sorted(ui_dir.glob("ui-session-*.jsonl")) if ui_dir.exists() else []
    for path in paths:
        try:
            for line in path.read_text(encoding="utf-8").splitlines():
                if line.strip():
                    events.append(json.loads(line))
        except Exception as exc:
            errors.append({"path": str(path), "error": f"{type(exc).__name__}: {exc}"})
    searches = [e for e in events if e.get("eventType") == "resource-search-response"]
    renders = [e for e in events if e.get("eventType") == "resource-render-observation"]
    search_ids = [e.get("requestId") for e in searches if e.get("requestId")]
    correlated_ids = [e.get("requestId") for e in renders if e.get("correlated") is True and e.get("requestId")]
    correlated_set = set(correlated_ids)
    matched_ids = [request_id for request_id in search_ids if request_id in correlated_set]
    latest_request_id = search_ids[-1] if search_ids else None
    latest_signature = None
    latest_total = None
    if searches:
        result = searches[-1].get("result")
        if isinstance(result, dict):
            latest_total = result.get("total")
            rows = result.get("rows")
            if isinstance(rows, list) and rows and isinstance(rows[0], dict):
                row = rows[0]
                latest_signature = {key: row.get(key) for key in ("serverId", "recordKey", "pointIndex", "x", "y", "level", "updatedAt")}
    return {
        "files": [str(p) for p in paths],
        "fileEvidence": [{"path": str(p), "sha256": sha256(p), "size": p.stat().st_size} for p in paths],
        "eventCount": len(events), "searchCount": len(searches), "renderCount": len(renders),
        "correlatedRenderCount": len(correlated_ids), "requestIds": search_ids,
        "correlatedRequestIds": correlated_ids, "matchedRequestIds": matched_ids,
        "latestRequestId": latest_request_id,
        "latestSearchCorrelated": latest_request_id is not None and latest_request_id in correlated_set,
        "latestResultTotal": latest_total, "latestSearchSignature": latest_signature,
        "errors": errors,
    }


def run_runtime_inspector(output: Path) -> dict:
    cmd = [sys.executable, str(REPO / "tools" / "inspect_official_runtime.py"), "--output", str(output)]
    result = run_capture(cmd)
    result["outputSha256"] = sha256(output)
    return result


def runtime_fingerprint(path: Path) -> dict:
    data = read_json(path)
    if not isinstance(data, dict):
        return {"error": "runtime evidence unreadable"}
    pe = data.get("pe") if isinstance(data.get("pe"), dict) else {}
    containers = data.get("containerSamples") if isinstance(data.get("containerSamples"), dict) else {}
    hot = data.get("hotupdate") if isinstance(data.get("hotupdate"), dict) else {}
    scripts = hot.get("lwScripts") if isinstance(hot.get("lwScripts"), dict) else {}
    return {
        "gameSha256": (pe.get("Game/LastWar.exe") or {}).get("sha256"),
        "xluaSha256": (pe.get("Game/LastWar_Data/Plugins/x86_64/xlua.dll") or {}).get("sha256"),
        "assemblyCSharpSha256": (containers.get("Assembly-CSharp.rdl") or {}).get("sha256"),
        "luaDataSha256": (scripts.get("data") or {}).get("sha256"),
        "luaDataSize": (scripts.get("data") or {}).get("size"),
        "luaMetadataValue": scripts.get("metadataValue"),
        "luaVersionValue": scripts.get("versionValue"),
    }


def run_helper_check() -> dict:
    return run_capture([sys.executable, str(REPO / "tools" / "run_live_resource_probe.py"), "--check-only"])


def git_identity() -> dict:
    head = run_capture(["git", "rev-parse", "HEAD"])
    branch = run_capture(["git", "branch", "--show-current"])
    return {"head": head.get("stdout", "").strip(), "branch": branch.get("stdout", "").strip()}


def create_attempt(exe: Path, preflight_only: bool) -> tuple[Path, int]:
    OWNER_ROOT.mkdir(parents=True, exist_ok=True)
    session_index = 1
    active = read_json(ACTIVE_POINTER)
    identity = app_identity(exe)
    if not preflight_only and isinstance(active, dict) and active.get("phase") == "awaiting-reopen":
        candidate = Path(str(active.get("attemptDirectory", "")))
        if (candidate.is_dir() and active.get("appSha256") == identity.get("sha256") and
                active.get("managedDllSha256") == identity.get("managedDllSha256")):
            return candidate, 2
        raise OwnerPreparationBlocked(
            "A saved reopen check is pending, but its evidence bundle or prepared build identity no longer matches. "
            "Do not start a replacement check; ChatGPT must inspect the saved attempt first.")
    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    app_sha = identity.get("sha256") or "missing"
    dll_sha = identity.get("managedDllSha256") or "missing"
    attempt = OWNER_ROOT / f"{stamp}-{app_sha[:8]}-{dll_sha[:8]}"
    attempt.mkdir(parents=True, exist_ok=False)
    return attempt, session_index


def preflight(attempt: Path, exe: Path, preflight_only: bool, session_index: int = 1) -> dict:
    before = process_snapshot()
    runtime_path = attempt / ("official-runtime-pre.json" if preflight_only else f"official-runtime-pre-session-{session_index}.json")
    value = {
        "schemaVersion": 1,
        "scope": "passive owner-assisted resource Search/reopen evidence; no scan trigger",
        "observedUtc": utc_now(),
        "git": git_identity(),
        "app": app_identity(exe),
        "processes": before,
        "profileStore": inspect_profile_store(),
        "recovery": recovery_snapshot(),
        "officialRuntime": run_runtime_inspector(runtime_path),
        "runtimeFingerprint": runtime_fingerprint(runtime_path),
        "helperCheckOnly": run_helper_check(),
        "preflightOnly": preflight_only,
    }
    blockers = []
    if not exe.is_file(): blockers.append("prepared owner-test executable is missing")
    if not exe.with_suffix(".dll").is_file(): blockers.append("prepared owner-test managed DLL is missing")
    if value["officialRuntime"]["exitCode"] != 0: blockers.append("current-client fingerprint failed")
    if value["helperCheckOnly"]["exitCode"] != 0: blockers.append("current-client compatibility check failed")
    if value["recovery"]["recoveryExists"] or value["recovery"]["operationOwnerExists"]: blockers.append("a previous live-resource recovery/owner journal is still active")
    if not preflight_only and relevant_running(before): blockers.append("LWBridge or Last War is already open; this attempt will not take ownership")
    value["blockers"] = blockers
    preflight_name = "preflight.json" if preflight_only else f"preflight-session-{session_index}.json"
    write_json(attempt / preflight_name, value)
    return value


def postflight(attempt: Path, exe: Path, session_index: int, exit_code: int) -> dict:
    runtime_path = attempt / f"official-runtime-post-session-{session_index}.json"
    ui = collect_ui_events(attempt / "ui")
    pre = read_json(attempt / f"preflight-session-{session_index}.json") or {}
    runtime = run_runtime_inspector(runtime_path)
    processes = process_snapshot()
    recovery = recovery_snapshot()
    value = {
        "observedUtc": utc_now(), "sessionIndex": session_index, "appExitCode": exit_code,
        "app": app_identity(exe), "processes": processes, "profileStore": inspect_profile_store(),
        "recovery": recovery, "uiEvidence": ui, "officialRuntime": runtime,
        "runtimeFingerprint": runtime_fingerprint(runtime_path),
    }
    value["runtimeIntegrityMatchesPreflight"] = value["runtimeFingerprint"] == pre.get("runtimeFingerprint")
    value["cleanupClean"] = not relevant_running(processes) and not recovery["recoveryExists"] and not recovery["operationOwnerExists"]
    value["completeSearchRender"] = ui["latestSearchCorrelated"] is True
    write_json(attempt / f"postflight-session-{session_index}.json", value)
    return value


def run_owner_session(exe: Path) -> int:
    try:
        attempt, session_index = create_attempt(exe, False)
    except OwnerPreparationBlocked as exc:
        OWNER_ROOT.mkdir(parents=True, exist_ok=True)
        blocked = OWNER_ROOT / f"blocked-reopen-{dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ')}.json"
        write_json(blocked, {"observedUtc": utc_now(), "status": "BLOCKED_PREP", "reason": str(exc)})
        message("LWBridge owner check", str(exc) + "\n\nStop here and tell ChatGPT what this box says.", True)
        return 2
    pre = preflight(attempt, exe, False, session_index)
    if pre["blockers"]:
        write_json(attempt / "attempt-summary.json", {"status": "BLOCKED_PREP", "blockers": pre["blockers"], "attemptDirectory": str(attempt)})
        message("LWBridge owner check", "The recorder is not ready to start.\n\n" + "\n".join(f"• {x}" for x in pre["blockers"]) + "\n\nStop here and tell ChatGPT what this box says.", True)
        return 2

    if session_index == 1:
        message("LWBridge owner check", "Technical recording is ready.\n\nThe LWBridge test app will open on Map Data. Follow only the PM-approved guide. Do not press Start Scan.\n\nWhen you finish the permitted Search check, close LWBridge normally.")
    else:
        message("LWBridge owner check", "Reopen recording is ready.\n\nThe same LWBridge build and evidence bundle will be reused. Follow only the PM-approved reopen/Search step, then close LWBridge normally.")

    ui_dir = attempt / "ui"
    ui_dir.mkdir(parents=True, exist_ok=True)
    cp = subprocess.run([str(exe), "--owner-evidence", str(ui_dir), "--view", "map-data"], cwd=str(REPO))
    post = postflight(attempt, exe, session_index, cp.returncode)
    profile = post.get("profileStore", {}).get("profileId")
    if not post["completeSearchRender"] or not post["runtimeIntegrityMatchesPreflight"] or not post["cleanupClean"]:
        ACTIVE_POINTER.unlink(missing_ok=True)
        reasons = []
        if not post["completeSearchRender"]:
            reasons.append("no same-request Resource Search/render correlation was recorded")
        if not post["runtimeIntegrityMatchesPreflight"]:
            reasons.append("official runtime fingerprint changed during the permitted check")
        if not post["cleanupClean"]:
            reasons.append("app/game/helper cleanup is incomplete")
        summary = {"status": "INCOMPLETE", "sessionIndex": session_index, "attemptDirectory": str(attempt), "profileId": profile, "reasons": reasons}
        write_json(attempt / "attempt-summary.json", summary)
        message("LWBridge owner check", "Recording stopped before the permitted check was fully verified.\n\nDo not repeat anything. Send ChatGPT your screenshot and what you saw.", True)
        return 3

    if session_index == 1:
        latest_total = post.get("uiEvidence", {}).get("latestResultTotal")
        if latest_total == 0:
            ACTIVE_POINTER.unlink(missing_ok=True)
            write_json(attempt / "attempt-summary.json", {"status": "COMPLETE_EMPTY", "attemptDirectory": str(attempt), "profileId": profile, "session1": post})
            message("LWBridge owner check", "The permitted Search check was recorded successfully and returned no saved Resource rows.\n\nStop here. Do not press Start Scan and do not run the shortcut again. Send ChatGPT your screenshot and what you saw.")
            return 0
        identity = app_identity(exe)
        pointer = {"phase": "awaiting-reopen", "attemptDirectory": str(attempt), "appSha256": identity.get("sha256"), "managedDllSha256": identity.get("managedDllSha256"), "profileId": profile, "savedUtc": utc_now()}
        write_json(ACTIVE_POINTER, pointer)
        write_json(attempt / "attempt-summary.json", {"status": "AWAITING_REOPEN", "attemptDirectory": str(attempt), "profileId": profile, "session1": post})
        message("LWBridge owner check", "The first permitted Search check found saved Resource data and was recorded successfully.\n\nStop here. Do not press Start Scan. Run the same shortcut one more time only if the PM-approved guide tells you to verify reopen persistence.")
        return 0

    first = read_json(attempt / "postflight-session-1.json") or {}
    same_profile = first.get("profileStore", {}).get("profileId") == profile
    first_signature = first.get("uiEvidence", {}).get("latestSearchSignature")
    second_signature = post.get("uiEvidence", {}).get("latestSearchSignature")
    same_saved_row = first_signature is not None and first_signature == second_signature
    complete = same_profile and same_saved_row
    summary = {"status": "COMPLETE" if complete else "FAILED_REOPEN_MISMATCH", "attemptDirectory": str(attempt), "sameProfile": same_profile, "sameSavedRow": same_saved_row, "session1": first, "session2": post}
    write_json(attempt / "attempt-summary.json", summary)
    ACTIVE_POINTER.unlink(missing_ok=True)
    message("LWBridge owner check", "The reopen/Search recording is complete and the same saved Resource row was verified.\n\nDo not run another check. Send ChatGPT your screenshot and what you saw." if complete else "The reopen did not match the same saved Resource row/profile. Stop here and send ChatGPT your screenshot and what you saw.", not complete)
    return 0 if complete else 4


def self_test() -> int:
    import tempfile
    with tempfile.TemporaryDirectory(prefix="lwbridge-owner-evidence-selftest-") as tmp:
        base = Path(tmp)
        ui = base / "ui"
        ui.mkdir()
        search_result = {"rows": [{"serverId": 2212, "recordKey": "32482", "pointIndex": 32482, "x": 481, "y": 32, "level": 3, "updatedAt": 1789017285000}], "total": 1}
        (ui / "ui-session-1.jsonl").write_text(
            json.dumps({"eventType": "resource-search-response", "requestId": "r1", "result": search_result}) + "\n" +
            json.dumps({"eventType": "resource-render-observation", "requestId": "r1", "correlated": True}) + "\n",
            encoding="utf-8")
        summary = collect_ui_events(ui)
        assert summary["searchCount"] == 1 and summary["correlatedRenderCount"] == 1
        assert summary["requestIds"] == ["r1"] and summary["matchedRequestIds"] == ["r1"]
        assert summary["latestSearchCorrelated"] is True and summary["fileEvidence"][0]["sha256"]
        assert summary["latestResultTotal"] == 1 and summary["latestSearchSignature"]["recordKey"] == "32482"
        with (ui / "ui-session-1.jsonl").open("a", encoding="utf-8") as f:
            f.write(json.dumps({"eventType": "resource-search-response", "requestId": "r2", "result": search_result}) + "\n")
        summary = collect_ui_events(ui)
        assert summary["latestRequestId"] == "r2" and summary["latestSearchCorrelated"] is False
        with (ui / "ui-session-1.jsonl").open("a", encoding="utf-8") as f:
            f.write(json.dumps({"eventType": "resource-render-observation", "requestId": "r2", "correlated": True}) + "\n")
        summary = collect_ui_events(ui)
        assert summary["latestSearchCorrelated"] is True
        assert relevant_running([{"name": "LastWar.exe"}]) and not relevant_running([{"name": "pythonw.exe"}])
        target = base / "atomic.json"
        write_json(target, {"ok": True})
        assert read_json(target) == {"ok": True}
    print(json.dumps({"ok": True, "scope": "collector self-test; no app/game/process operation"}))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Passive technical evidence collector for PM-approved owner Resource Search/reopen checks.")
    parser.add_argument("--exe", type=Path, default=DEFAULT_EXE)
    parser.add_argument("--preflight-only", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return self_test()
    exe = args.exe.resolve()
    attempt, _ = create_attempt(exe, True) if args.preflight_only else (None, None)
    if args.preflight_only:
        pre = preflight(attempt, exe, True)
        write_json(attempt / "attempt-summary.json", {"status": "PREFLIGHT_ONLY", "blockers": pre["blockers"], "attemptDirectory": str(attempt)})
        print(json.dumps({"ok": not pre["blockers"], "attemptDirectory": str(attempt), "blockers": pre["blockers"]}, indent=2))
        return 0 if not pre["blockers"] else 2
    return run_owner_session(exe)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except SystemExit:
        raise
    except Exception as exc:
        try:
            OWNER_ROOT.mkdir(parents=True, exist_ok=True)
            fatal = OWNER_ROOT / "collector-fatal.json"
            write_json(fatal, {"observedUtc": utc_now(), "error": f"{type(exc).__name__}: {exc}", "traceback": traceback.format_exc()})
            message("LWBridge owner check", "The recorder stopped unexpectedly.\n\nDo not repeat the test. Tell ChatGPT that the recorder failed before completion.", True)
        finally:
            raise
