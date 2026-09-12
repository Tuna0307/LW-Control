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
import uuid

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


def parse_process_snapshot(exit_code: int, stdout: str, stderr: str) -> dict:
    diagnostic = {
        "exitCode": exit_code,
        "stdout": stdout[-4000:],
        "stderr": stderr[-4000:],
    }
    if exit_code != 0:
        return {
            "ok": False, "rows": [],
            "error": {"kind": "PROCESS_QUERY_FAILED", "message": f"Windows process query exited with code {exit_code}."},
            "diagnostic": diagnostic,
        }
    text = stdout.strip()
    if not text:
        return {"ok": True, "rows": [], "error": None, "diagnostic": diagnostic}
    try:
        value = json.loads(text)
    except Exception as exc:
        return {
            "ok": False, "rows": [],
            "error": {"kind": "PROCESS_QUERY_INVALID_JSON", "message": f"{type(exc).__name__}: {exc}"},
            "diagnostic": diagnostic,
        }
    rows = value if isinstance(value, list) else [value] if isinstance(value, dict) else None
    if rows is None or any(not isinstance(item, dict) for item in rows):
        return {
            "ok": False, "rows": [],
            "error": {"kind": "PROCESS_QUERY_INVALID_SHAPE", "message": "Windows process query returned a non-object JSON shape."},
            "diagnostic": diagnostic,
        }
    return {"ok": True, "rows": rows, "error": None, "diagnostic": diagnostic}


def process_snapshot() -> dict:
    script = r"""
$names = 'LWBridge.Desktop','LastWar','LastWarLauncher','python','pythonw'
Get-CimInstance Win32_Process | Where-Object { $names -contains ([IO.Path]::GetFileNameWithoutExtension($_.Name)) } |
  ForEach-Object { [pscustomobject]@{ pid=$_.ProcessId; name=$_.Name; path=$_.ExecutablePath; commandLine=$_.CommandLine } } |
  ConvertTo-Json -Compress
"""
    cp = subprocess.run(["powershell.exe", "-NoProfile", "-Command", script], text=True, capture_output=True, encoding="utf-8", errors="replace")
    return parse_process_snapshot(cp.returncode, cp.stdout, cp.stderr)


def relevant_running(processes: list[dict]) -> list[dict]:
    result = []
    for item in processes:
        name = str(item.get("name", "")).lower()
        if name in {"lwbridge.desktop.exe", "lastwar.exe", "lastwarlauncher.exe"}:
            result.append(item)
    return result


def inspect_profile_store() -> dict:
    result: dict = {"configRoot": str(CONFIG_ROOT), "configExists": False, "profileId": None, "database": None, "publishedServerIds": [], "resourceRows": []}
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
        result["publishedServerIds"] = [
            int(row[0]) for row in con.execute("SELECT DISTINCT server_id FROM map_records ORDER BY server_id").fetchall()
        ]
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


def collect_ui_events(ui_dir: Path, expected_process_id: int) -> dict:
    events: list[dict] = []
    errors: list[dict] = []
    paths = sorted(ui_dir.glob("ui-session-*.jsonl")) if ui_dir.exists() else []
    expected_path = ui_dir / f"ui-session-{expected_process_id}.jsonl"
    unexpected_paths = [path for path in paths if path != expected_path]
    if not expected_path.is_file():
        errors.append({"path": str(expected_path), "error": "current-session UI evidence file is missing"})
    else:
        try:
            for line_number, line in enumerate(expected_path.read_text(encoding="utf-8").splitlines(), start=1):
                if not line.strip():
                    continue
                try:
                    event = json.loads(line)
                except Exception as exc:
                    errors.append({"path": str(expected_path), "line": line_number, "error": f"{type(exc).__name__}: {exc}"})
                    continue
                if not isinstance(event, dict):
                    errors.append({"path": str(expected_path), "line": line_number, "error": "UI evidence event is not a JSON object"})
                    continue
                events.append(event)
        except Exception as exc:
            errors.append({"path": str(expected_path), "error": f"{type(exc).__name__}: {exc}"})
    for path in unexpected_paths:
        errors.append({"path": str(path), "error": "unexpected UI evidence file in current collector session directory"})

    def detail_pid(event: dict) -> int | None:
        details = event.get("details")
        value = details.get("processId") if isinstance(details, dict) else None
        return value if isinstance(value, int) else None

    session_starts = [e for e in events if e.get("eventType") == "session-start"]
    session_ends = [e for e in events if e.get("eventType") == "session-end"]
    session_log_complete = (
        len(session_starts) == 1 and len(session_ends) == 1
        and detail_pid(session_starts[0]) == expected_process_id
        and detail_pid(session_ends[0]) == expected_process_id
    )
    if not session_log_complete:
        errors.append({
            "path": str(expected_path),
            "error": "current-session start/end evidence is missing or does not match the launched process",
        })

    indexed_searches = [(index, event) for index, event in enumerate(events) if event.get("eventType") == "resource-search-response"]
    indexed_renders = [(index, event) for index, event in enumerate(events) if event.get("eventType") == "resource-render-observation"]
    map_summary_errors = [
        event for event in events
        if event.get("eventType") == "command-error" and event.get("command") == "map_summary"
    ]
    search_ids = [event.get("requestId") for _, event in indexed_searches if event.get("requestId")]
    correlated_ids: list[str] = []
    matched_ids: list[str] = []
    for search_index, search in indexed_searches:
        request_id = search.get("requestId")
        if not request_id:
            continue
        matching_render = next((
            render for render_index, render in indexed_renders
            if render_index > search_index and render.get("requestId") == request_id and render.get("correlated") is True
        ), None)
        if matching_render is not None:
            correlated_ids.append(request_id)
            matched_ids.append(request_id)

    latest_request_id = None
    latest_signature = None
    latest_total = None
    latest_search_correlated = False
    if indexed_searches:
        latest_index, latest_search = indexed_searches[-1]
        latest_request_id = latest_search.get("requestId")
        result = latest_search.get("result")
        if isinstance(result, dict):
            latest_total = result.get("total")
            rows = result.get("rows")
            if isinstance(rows, list) and rows and isinstance(rows[0], dict):
                row = rows[0]
                latest_signature = {key: row.get(key) for key in ("serverId", "recordKey", "pointIndex", "x", "y", "level", "updatedAt")}
        latest_search_correlated = bool(latest_request_id) and any(
            render_index > latest_index
            and render.get("requestId") == latest_request_id
            and render.get("correlated") is True
            for render_index, render in indexed_renders
        )

    return {
        "directory": str(ui_dir),
        "expectedProcessId": expected_process_id,
        "expectedFile": str(expected_path),
        "files": [str(path) for path in paths],
        "unexpectedFiles": [str(path) for path in unexpected_paths],
        "fileEvidence": [{"path": str(path), "sha256": sha256(path), "size": path.stat().st_size} for path in paths],
        "eventCount": len(events),
        "sessionStartCount": len(session_starts),
        "sessionEndCount": len(session_ends),
        "sessionLogComplete": session_log_complete,
        "healthy": not errors and session_log_complete and len(paths) == 1 and expected_path.is_file(),
        "searchCount": len(indexed_searches),
        "renderCount": len(indexed_renders),
        "correlatedRenderCount": len(correlated_ids),
        "requestIds": search_ids,
        "correlatedRequestIds": correlated_ids,
        "matchedRequestIds": matched_ids,
        "latestRequestId": latest_request_id,
        "latestSearchCorrelated": latest_search_correlated,
        "latestResultTotal": latest_total,
        "latestSearchSignature": latest_signature,
        "mapSummaryErrorCount": len(map_summary_errors),
        "latestMapSummaryErrorCode": map_summary_errors[-1].get("code") if map_summary_errors else None,
        "latestMapSummaryErrorRequestId": map_summary_errors[-1].get("requestId") if map_summary_errors else None,
        "errors": errors,
    }


def is_complete_no_saved_context(ui: dict, pre_store: dict, post_store: dict) -> bool:
    return (
        ui.get("latestMapSummaryErrorCode") == "MAP_SAVED_CONTEXT_UNAVAILABLE"
        and pre_store.get("publishedServerIds") == []
        and post_store.get("publishedServerIds") == []
        and not pre_store.get("databaseReadError")
        and not post_store.get("databaseReadError")
    )


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


def process_observation_blocker(observation: dict) -> str | None:
    if observation.get("ok") is True:
        return None
    return "Windows process observation failed; the recorder cannot safely verify that LWBridge and Last War are closed."


def create_evidence_session(attempt: Path, session_index: int) -> tuple[str, Path]:
    session_id = f"session-{session_index}-{uuid.uuid4().hex}"
    ui_dir = attempt / "ui" / session_id
    ui_dir.mkdir(parents=True, exist_ok=False)
    return session_id, ui_dir


def evidence_health_errors(
    exit_code: int,
    ui: dict,
    process_observation: dict,
    runtime: dict,
    profile_store: dict,
) -> list[str]:
    errors: list[str] = []
    if exit_code != 0:
        errors.append(f"LWBridge exited with code {exit_code}")
    if ui.get("healthy") is not True:
        errors.append("current-session UI evidence is missing, malformed, or not owned by the launched app process")
    if process_observation.get("ok") is not True:
        errors.append("postflight Windows process observation failed")
    if runtime.get("exitCode") != 0:
        errors.append("postflight current-client fingerprint observation failed")
    if profile_store.get("databaseReadError"):
        errors.append("profile map store could not be read safely")
    return errors


def session_evidence_complete(post: dict) -> bool:
    return post.get("completeSearchRender") is True or post.get("completeNoSavedContext") is True


def completion_failure_reasons(post: dict) -> list[str]:
    reasons = list(post.get("evidenceHealthErrors") or [])
    if not session_evidence_complete(post):
        reasons.append("neither current-session Resource Search/render correlation nor the normal no-saved-context branch was recorded")
    if post.get("runtimeIntegrityMatchesPreflight") is not True:
        reasons.append("official runtime fingerprint changed or could not be verified during the permitted check")
    if post.get("cleanupClean") is not True:
        reasons.append("app/game/helper cleanup could not be verified cleanly")
    return reasons


def evaluate_reopen(first: dict, second: dict) -> dict:
    first_profile = (first.get("profileStore") or {}).get("profileId") if isinstance(first.get("profileStore"), dict) else None
    second_profile = (second.get("profileStore") or {}).get("profileId") if isinstance(second.get("profileStore"), dict) else None
    first_signature = (first.get("uiEvidence") or {}).get("latestSearchSignature") if isinstance(first.get("uiEvidence"), dict) else None
    second_signature = (second.get("uiEvidence") or {}).get("latestSearchSignature") if isinstance(second.get("uiEvidence"), dict) else None
    first_valid = (
        first.get("evidenceHealthy") is True
        and first.get("completeSearchRender") is True
        and first.get("runtimeIntegrityMatchesPreflight") is True
        and first.get("cleanupClean") is True
    )
    second_valid = (
        second.get("evidenceHealthy") is True
        and second.get("completeSearchRender") is True
        and second.get("runtimeIntegrityMatchesPreflight") is True
        and second.get("cleanupClean") is True
    )
    same_profile = first_profile is not None and first_profile == second_profile
    same_saved_row = first_signature is not None and first_signature == second_signature
    return {
        "firstSessionValid": first_valid,
        "secondSessionValid": second_valid,
        "sameProfile": same_profile,
        "sameSavedRow": same_saved_row,
        "complete": first_valid and second_valid and same_profile and same_saved_row,
    }


def preflight(
    attempt: Path,
    exe: Path,
    preflight_only: bool,
    session_index: int = 1,
    evidence_session_id: str | None = None,
    ui_dir: Path | None = None,
) -> dict:
    before = process_snapshot()
    runtime_path = attempt / ("official-runtime-pre.json" if preflight_only else f"official-runtime-pre-session-{session_index}.json")
    value = {
        "schemaVersion": 2,
        "scope": "passive owner-assisted resource Search/reopen evidence; no scan trigger",
        "observedUtc": utc_now(),
        "git": git_identity(),
        "app": app_identity(exe),
        "evidenceSessionId": evidence_session_id,
        "uiDirectory": str(ui_dir) if ui_dir is not None else None,
        "processObservation": before,
        "processes": before.get("rows", []),
        "profileStore": inspect_profile_store(),
        "recovery": recovery_snapshot(),
        "officialRuntime": run_runtime_inspector(runtime_path),
        "runtimeFingerprint": runtime_fingerprint(runtime_path),
        "helperCheckOnly": run_helper_check(),
        "preflightOnly": preflight_only,
    }
    blockers: list[str] = []
    if not exe.is_file():
        blockers.append("prepared owner-test executable is missing")
    if not exe.with_suffix(".dll").is_file():
        blockers.append("prepared owner-test managed DLL is missing")
    process_blocker = process_observation_blocker(before)
    if process_blocker:
        blockers.append(process_blocker)
    if value["officialRuntime"]["exitCode"] != 0:
        blockers.append("current-client fingerprint failed")
    if value["helperCheckOnly"]["exitCode"] != 0:
        blockers.append("current-client compatibility check failed")
    if value["profileStore"].get("databaseReadError"):
        blockers.append("profile map store could not be read safely")
    if value["recovery"]["recoveryExists"] or value["recovery"]["operationOwnerExists"]:
        blockers.append("a previous live-resource recovery/owner journal is still active")
    if not preflight_only and before.get("ok") is True and relevant_running(before.get("rows", [])):
        blockers.append("LWBridge or Last War is already open; this attempt will not take ownership")
    value["blockers"] = blockers
    preflight_name = "preflight.json" if preflight_only else f"preflight-session-{session_index}.json"
    write_json(attempt / preflight_name, value)
    return value


def postflight(
    attempt: Path,
    exe: Path,
    session_index: int,
    exit_code: int,
    evidence_session_id: str,
    ui_dir: Path,
    app_process_id: int,
) -> dict:
    runtime_path = attempt / f"official-runtime-post-session-{session_index}.json"
    ui = collect_ui_events(ui_dir, app_process_id)
    pre = read_json(attempt / f"preflight-session-{session_index}.json") or {}
    runtime = run_runtime_inspector(runtime_path)
    process_observation = process_snapshot()
    recovery = recovery_snapshot()
    profile_store = inspect_profile_store()
    value = {
        "observedUtc": utc_now(),
        "sessionIndex": session_index,
        "evidenceSessionId": evidence_session_id,
        "uiDirectory": str(ui_dir),
        "appProcessId": app_process_id,
        "appExitCode": exit_code,
        "app": app_identity(exe),
        "processObservation": process_observation,
        "processes": process_observation.get("rows", []),
        "profileStore": profile_store,
        "recovery": recovery,
        "uiEvidence": ui,
        "officialRuntime": runtime,
        "runtimeFingerprint": runtime_fingerprint(runtime_path),
    }
    value["runtimeIntegrityMatchesPreflight"] = (
        runtime.get("exitCode") == 0
        and value["runtimeFingerprint"] == pre.get("runtimeFingerprint")
    )
    value["cleanupClean"] = (
        process_observation.get("ok") is True
        and not relevant_running(process_observation.get("rows", []))
        and not recovery["recoveryExists"]
        and not recovery["operationOwnerExists"]
    )
    value["completeSearchRender"] = ui.get("latestSearchCorrelated") is True
    pre_store = pre.get("profileStore") if isinstance(pre.get("profileStore"), dict) else {}
    value["completeNoSavedContext"] = is_complete_no_saved_context(ui, pre_store, profile_store)
    value["evidenceHealthErrors"] = evidence_health_errors(
        exit_code, ui, process_observation, runtime, profile_store
    )
    value["evidenceHealthy"] = not value["evidenceHealthErrors"]
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

    evidence_session_id, ui_dir = create_evidence_session(attempt, session_index)
    pre = preflight(attempt, exe, False, session_index, evidence_session_id, ui_dir)
    if pre["blockers"]:
        write_json(attempt / "attempt-summary.json", {
            "status": "BLOCKED_PREP",
            "sessionIndex": session_index,
            "evidenceSessionId": evidence_session_id,
            "blockers": pre["blockers"],
            "attemptDirectory": str(attempt),
        })
        message(
            "LWBridge owner check",
            "The recorder is not ready to start.\n\n" + "\n".join(f"? {item}" for item in pre["blockers"])
            + "\n\nStop here and tell ChatGPT what this box says.",
            True,
        )
        return 2

    if session_index == 1:
        message("LWBridge owner check", "Technical recording is ready.\n\nThe LWBridge test app will open on Map Data. Follow only the PM-approved guide. Do not press Start Scan.\n\nWhen you finish the permitted Search check, close LWBridge normally.")
    else:
        message("LWBridge owner check", "Reopen recording is ready.\n\nThe same LWBridge build and evidence bundle will be reused. Follow only the PM-approved reopen/Search step, then close LWBridge normally.")

    app_process = subprocess.Popen(
        [str(exe), "--owner-evidence", str(ui_dir), "--view", "map-data"],
        cwd=str(REPO),
    )
    app_process_id = app_process.pid
    exit_code = app_process.wait()
    post = postflight(
        attempt, exe, session_index, exit_code,
        evidence_session_id, ui_dir, app_process_id,
    )
    profile = post.get("profileStore", {}).get("profileId")
    failures = completion_failure_reasons(post)
    if failures:
        ACTIVE_POINTER.unlink(missing_ok=True)
        summary = {
            "status": "INCOMPLETE",
            "sessionIndex": session_index,
            "evidenceSessionId": evidence_session_id,
            "attemptDirectory": str(attempt),
            "profileId": profile,
            "reasons": failures,
        }
        write_json(attempt / "attempt-summary.json", summary)
        message(
            "LWBridge owner check",
            "Recording stopped before the permitted check was fully verified.\n\nDo not repeat anything. Send ChatGPT your screenshot and what you saw.",
            True,
        )
        return 3

    if session_index == 1:
        if post["completeNoSavedContext"]:
            ACTIVE_POINTER.unlink(missing_ok=True)
            write_json(attempt / "attempt-summary.json", {
                "status": "COMPLETE_NO_SAVED_CONTEXT",
                "attemptDirectory": str(attempt),
                "profileId": profile,
                "session1": post,
            })
            message("LWBridge owner check", "This profile has no saved map server, so normal Search cannot send a map_search request yet.\n\nThe permitted passive check is complete. Do not press Start Scan and do not run the shortcut again. Send ChatGPT your screenshot and what you saw.")
            return 0
        latest_total = post.get("uiEvidence", {}).get("latestResultTotal")
        if latest_total == 0:
            ACTIVE_POINTER.unlink(missing_ok=True)
            write_json(attempt / "attempt-summary.json", {
                "status": "COMPLETE_EMPTY",
                "attemptDirectory": str(attempt),
                "profileId": profile,
                "session1": post,
            })
            message("LWBridge owner check", "The permitted Search check was recorded successfully and returned no saved Resource rows.\n\nStop here. Do not press Start Scan and do not run the shortcut again. Send ChatGPT your screenshot and what you saw.")
            return 0
        identity = app_identity(exe)
        pointer = {
            "phase": "awaiting-reopen",
            "attemptDirectory": str(attempt),
            "appSha256": identity.get("sha256"),
            "managedDllSha256": identity.get("managedDllSha256"),
            "profileId": profile,
            "session1EvidenceSessionId": evidence_session_id,
            "savedUtc": utc_now(),
        }
        write_json(ACTIVE_POINTER, pointer)
        write_json(attempt / "attempt-summary.json", {
            "status": "AWAITING_REOPEN",
            "attemptDirectory": str(attempt),
            "profileId": profile,
            "session1": post,
        })
        message("LWBridge owner check", "The first permitted Search check found saved Resource data and was recorded successfully.\n\nStop here. Do not press Start Scan. Run the same shortcut one more time only if the PM-approved guide tells you to verify reopen persistence.")
        return 0

    first = read_json(attempt / "postflight-session-1.json") or {}
    outcome = evaluate_reopen(first, post)
    summary = {
        "status": "COMPLETE" if outcome["complete"] else "FAILED_REOPEN_MISMATCH",
        "attemptDirectory": str(attempt),
        **outcome,
        "session1": first,
        "session2": post,
    }
    write_json(attempt / "attempt-summary.json", summary)
    ACTIVE_POINTER.unlink(missing_ok=True)
    message(
        "LWBridge owner check",
        "The reopen/Search recording is complete and the same saved Resource row was verified.\n\nDo not run another check. Send ChatGPT your screenshot and what you saw."
        if outcome["complete"] else
        "The reopen did not provide a distinct verified Search/render for the same saved Resource row/profile. Stop here and send ChatGPT your screenshot and what you saw.",
        not outcome["complete"],
    )
    return 0 if outcome["complete"] else 4


def self_test() -> int:
    import tempfile
    with tempfile.TemporaryDirectory(prefix="lwbridge-owner-evidence-selftest-") as tmp:
        base = Path(tmp)
        ui = base / "ui" / "session-1-selftest"
        ui.mkdir(parents=True)
        pid = 101
        search_result = {"rows": [{"serverId": 2212, "recordKey": "32482", "pointIndex": 32482, "x": 481, "y": 32, "level": 3, "updatedAt": 1789017285000}], "total": 1}
        events = [
            {"eventType": "session-start", "details": {"processId": pid, "profileId": "p1", "initialView": "map-data"}},
            {"eventType": "resource-search-response", "requestId": "r1", "result": search_result},
            {"eventType": "resource-render-observation", "requestId": "r1", "correlated": True},
            {"eventType": "session-end", "details": {"processId": pid}},
        ]
        (ui / f"ui-session-{pid}.jsonl").write_text(
            "".join(json.dumps(event) + "\n" for event in events), encoding="utf-8"
        )
        summary = collect_ui_events(ui, pid)
        assert summary["healthy"] is True and summary["sessionLogComplete"] is True
        assert summary["searchCount"] == 1 and summary["correlatedRenderCount"] == 1
        assert summary["requestIds"] == ["r1"] and summary["matchedRequestIds"] == ["r1"]
        assert summary["latestSearchCorrelated"] is True and summary["fileEvidence"][0]["sha256"]
        assert summary["latestResultTotal"] == 1 and summary["latestSearchSignature"]["recordKey"] == "32482"

        no_context_ui = base / "ui" / "session-1-no-context"
        no_context_ui.mkdir(parents=True)
        no_context_events = [
            {"eventType": "session-start", "details": {"processId": pid, "profileId": "p1", "initialView": "map-data"}},
            {"eventType": "command-error", "requestId": "summary-1", "command": "map_summary", "code": "MAP_SAVED_CONTEXT_UNAVAILABLE"},
            {"eventType": "session-end", "details": {"processId": pid}},
        ]
        (no_context_ui / f"ui-session-{pid}.jsonl").write_text(
            "".join(json.dumps(event) + "\n" for event in no_context_events), encoding="utf-8"
        )
        no_context = collect_ui_events(no_context_ui, pid)
        assert no_context["healthy"] is True
        assert is_complete_no_saved_context(no_context, {"publishedServerIds": []}, {"publishedServerIds": []})
        assert not is_complete_no_saved_context(no_context, {"publishedServerIds": [2212]}, {"publishedServerIds": [2212]})

        good_empty = parse_process_snapshot(0, "", "")
        failed = parse_process_snapshot(7, "", "query failed")
        malformed = parse_process_snapshot(0, "not-json", "")
        assert good_empty["ok"] is True and good_empty["rows"] == [] and process_observation_blocker(good_empty) is None
        assert failed["ok"] is False and failed["error"]["kind"] == "PROCESS_QUERY_FAILED"
        assert malformed["ok"] is False and malformed["error"]["kind"] == "PROCESS_QUERY_INVALID_JSON"
        assert process_observation_blocker(failed) and process_observation_blocker(malformed)
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
