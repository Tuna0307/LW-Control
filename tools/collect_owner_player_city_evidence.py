from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import time
import uuid

from collect_owner_resource_evidence import (
    CONFIG_ROOT, REPO, app_identity, message, process_snapshot,
    relevant_running, recovery_snapshot, sha256, utc_now, write_json,
)

OWNER_CITY_ROOT = CONFIG_ROOT / "owner-city-evidence"
DEFAULT_EXE = REPO / "src" / "LWBridge.Desktop" / "bin" / "Release" / "net10.0-windows10.0.17763.0" / "LWBridge.Desktop.exe"


def inspect_city_store() -> dict:
    result = {"profileId": None, "database": None, "cityRows": [], "error": None}
    config_path = CONFIG_ROOT / "config.json"
    try:
        config = json.loads(config_path.read_text(encoding="utf-8"))
        profile_id = str(config.get("profileId") or "").strip()
        result["profileId"] = profile_id or None
        if not profile_id:
            return result
        db = CONFIG_ROOT / "profiles" / profile_id / "map-data.db"
        result["database"] = str(db)
        if not db.is_file():
            return result
        uri = db.resolve().as_uri() + "?mode=ro"
        con = sqlite3.connect(uri, uri=True)
        con.row_factory = sqlite3.Row
        rows = con.execute(
            "SELECT server_id, record_key, point_index, level, updated_at, data_json "
            "FROM map_records WHERE kind='city' ORDER BY updated_at DESC LIMIT 20"
        ).fetchall()
        for row in rows:
            data = {}
            try:
                parsed = json.loads(row["data_json"])
                if isinstance(parsed, dict):
                    data = {key: parsed.get(key) for key in ("x", "y") if key in parsed}
            except Exception:
                data = {"dataJsonParseError": True}
            result["cityRows"].append({
                "serverId": row["server_id"],
                "recordKey": row["record_key"],
                "pointIndex": row["point_index"],
                "level": row["level"],
                "updatedAt": row["updated_at"],
                "data": data,
            })
        con.close()
    except Exception as exc:
        result["error"] = f"{type(exc).__name__}: {exc}"
    return result


def safe_signature(store: dict) -> dict | None:
    rows = store.get("cityRows")
    return rows[0] if isinstance(rows, list) and rows else None


def collect_city_ui(ui_dir: Path, expected_pid: int) -> dict:
    path = ui_dir / f"ui-session-{expected_pid}.jsonl"
    events: list[dict] = []
    errors: list[str] = []
    if not path.is_file():
        errors.append("current-session UI evidence file is missing")
    else:
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
            if not line.strip():
                continue
            try:
                value = json.loads(line)
            except Exception as exc:
                errors.append(f"line {number}: {type(exc).__name__}: {exc}")
                continue
            if isinstance(value, dict):
                events.append(value)
            else:
                errors.append(f"line {number}: UI event is not an object")

    starts = [e for e in events if e.get("eventType") == "session-start"]
    ends = [e for e in events if e.get("eventType") == "session-end"]
    def detail_pid(event: dict) -> int | None:
        details = event.get("details")
        value = details.get("processId") if isinstance(details, dict) else None
        return value if isinstance(value, int) else None
    session_ok = len(starts) == 1 and len(ends) == 1 and detail_pid(starts[0]) == expected_pid and detail_pid(ends[0]) == expected_pid

    searches = [(i, e) for i, e in enumerate(events) if e.get("eventType") == "city-search-response"]
    renders = [(i, e) for i, e in enumerate(events) if e.get("eventType") == "city-render-observation"]
    latest_request = searches[-1][1].get("requestId") if searches else None
    correlated = False
    if searches and latest_request:
        latest_index = searches[-1][0]
        correlated = any(
            render_index > latest_index and render.get("requestId") == latest_request and render.get("correlated") is True
            for render_index, render in renders
        )

    identity_markers = ("ownerName", "ownerUid", "uuid", "allianceName", "allianceId")
    city_lines = [json.dumps(e, ensure_ascii=False) for e in events if e.get("eventType") in {"city-search-response", "city-render-observation"}]
    identity_leak = any(marker in line for line in city_lines for marker in identity_markers)
    return {
        "path": str(path),
        "sha256": sha256(path),
        "eventCount": len(events),
        "sessionComplete": session_ok,
        "citySearchCount": len(searches),
        "cityRenderCount": len(renders),
        "latestRequestId": latest_request,
        "latestSearchCorrelated": correlated,
        "identityLeakDetected": identity_leak,
        "errors": errors,
    }


def preflight(exe: Path) -> tuple[bool, dict]:
    processes = process_snapshot()
    store = inspect_city_store()
    recovery = recovery_snapshot()
    running = relevant_running(processes.get("rows", [])) if processes.get("ok") is True else []
    identity = app_identity(exe)
    blockers: list[str] = []
    if not exe.is_file() or not exe.with_suffix(".dll").is_file():
        blockers.append("prepared Player City build is missing")
    if processes.get("ok") is not True:
        blockers.append("Windows process observation failed")
    elif running:
        blockers.append("LWBridge or Last War is already open")
    if recovery.get("recoveryExists") or recovery.get("operationOwnerExists"):
        blockers.append("a previous live-map recovery or owner journal is active")
    if store.get("error"):
        blockers.append("the active profile map store could not be read safely")
    if not store.get("profileId") or not store.get("cityRows"):
        blockers.append("the active profile has no saved Player City row")
    value = {
        "schemaVersion": 1, "scope": "read-only owner Player City Search/render check",
        "observedUtc": utc_now(), "app": identity, "processObservation": processes,
        "relevantRunning": running, "recovery": recovery, "profileStore": store,
        "blockers": blockers,
    }
    return not blockers, value


def run_owner_check(exe: Path) -> int:
    ok, before = preflight(exe)
    stamp = time.strftime("%Y%m%dT%H%M%SZ", time.gmtime())
    sha = (before.get("app") or {}).get("sha256") or "missing"
    attempt = OWNER_CITY_ROOT / f"{stamp}-{sha[:8]}-{uuid.uuid4().hex[:8]}"
    attempt.mkdir(parents=True, exist_ok=False)
    ui_dir = attempt / "ui"
    ui_dir.mkdir()
    write_json(attempt / "preflight.json", before)
    if not ok:
        write_json(attempt / "attempt-summary.json", {"status": "BLOCKED_PREP", "blockers": before["blockers"]})
        message("LWBridge Player City check", "The Player City check is not ready.\n\n" + "\n".join(before["blockers"]) + "\n\nStop here and tell ChatGPT what this box says.", True)
        return 2

    message(
        "LWBridge Player City check",
        "LWBridge will open on Map Data in read-only evidence mode.\n\nLeave Player City selected and click Search once. Take a screenshot of the city row, then close LWBridge.\n\nDo not click Start Scan, Clear, Export, or Jump.",
    )
    process = subprocess.Popen([str(exe), "--owner-evidence", str(ui_dir), "--view", "map-data"], cwd=str(REPO))
    exit_code = process.wait()
    after_processes = process_snapshot()
    after_store = inspect_city_store()
    after_recovery = recovery_snapshot()
    ui = collect_city_ui(ui_dir, process.pid)
    same_profile = before["profileStore"].get("profileId") == after_store.get("profileId")
    same_city = safe_signature(before["profileStore"]) == safe_signature(after_store)
    cleanup = (
        after_processes.get("ok") is True
        and not relevant_running(after_processes.get("rows", []))
        and not after_recovery.get("recoveryExists")
        and not after_recovery.get("operationOwnerExists")
    )
    complete = (
        exit_code == 0 and same_profile and same_city and cleanup
        and ui.get("sessionComplete") is True
        and ui.get("citySearchCount", 0) >= 1
        and ui.get("latestSearchCorrelated") is True
        and ui.get("identityLeakDetected") is False
        and not ui.get("errors")
    )
    post = {
        "observedUtc": utc_now(), "appExitCode": exit_code,
        "processObservation": after_processes, "recovery": after_recovery,
        "profileStore": after_store, "uiEvidence": ui,
        "sameProfile": same_profile, "sameSavedCity": same_city, "cleanupClean": cleanup,
    }
    write_json(attempt / "postflight.json", post)
    summary = {
        "schemaVersion": 1,
        "status": "COMPLETE" if complete else "INCOMPLETE",
        "attemptDirectory": str(attempt),
        "profileId": after_store.get("profileId"),
        "serverId": (safe_signature(after_store) or {}).get("serverId"),
        "sameProfile": same_profile,
        "sameSavedCity": same_city,
        "cleanupClean": cleanup,
        "citySearchCorrelated": ui.get("latestSearchCorrelated") is True,
        "identityLeakDetected": ui.get("identityLeakDetected") is True,
    }
    write_json(attempt / "attempt-summary.json", summary)
    if complete:
        message("LWBridge Player City check", "The read-only Player City Search and visible row were recorded successfully.\n\nDo not run another check. Send ChatGPT your screenshot and what you saw.")
        return 0
    message("LWBridge Player City check", "The recorder could not fully verify the read-only Player City check.\n\nDo not retry. Send ChatGPT your screenshot and what you saw.", True)
    return 3


def self_test() -> int:
    with tempfile.TemporaryDirectory(prefix="lwbridge-owner-city-selftest-") as tmp:
        ui = Path(tmp) / "ui"
        ui.mkdir()
        pid = 4242
        events = [
            {"eventType": "session-start", "details": {"processId": pid, "profileId": "p1", "initialView": "map-data"}},
            {"eventType": "city-search-response", "requestId": "city-1", "result": {
                "rows": [{"serverId": 2212, "recordKey": "40496", "pointIndex": 40496, "x": 495, "y": 40, "level": 27, "updatedAt": 1789250251000}], "total": 1}},
            {"eventType": "city-render-observation", "requestId": "city-1", "correlated": True,
             "snapshot": {"busy": False, "rows": [{"isEmpty": False, "coordinate": "495,40", "level": "27", "updatedAt": "x"}]}},
            {"eventType": "session-end", "details": {"processId": pid}},
        ]
        path = ui / f"ui-session-{pid}.jsonl"
        path.write_text("".join(json.dumps(event) + "\n" for event in events), encoding="utf-8")
        result = collect_city_ui(ui, pid)
        assert result["sessionComplete"] is True
        assert result["citySearchCount"] == 1 and result["cityRenderCount"] == 1
        assert result["latestSearchCorrelated"] is True
        assert result["identityLeakDetected"] is False
        assert result["sha256"]

        events[1]["result"]["rows"][0]["ownerName"] = "PRIVATE"
        path.write_text("".join(json.dumps(event) + "\n" for event in events), encoding="utf-8")
        assert collect_city_ui(ui, pid)["identityLeakDetected"] is True
    print(json.dumps({"ok": True, "scope": "Player City owner collector self-test; no app/game operation"}))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Read-only owner evidence collector for saved Player City Search/render confirmation.")
    parser.add_argument("--exe", type=Path, default=DEFAULT_EXE)
    parser.add_argument("--preflight-only", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return self_test()
    exe = args.exe.resolve()
    if args.preflight_only:
        ok, value = preflight(exe)
        OWNER_CITY_ROOT.mkdir(parents=True, exist_ok=True)
        write_json(OWNER_CITY_ROOT / "latest-preflight.json", value)
        print(json.dumps({"ok": ok, "preflight": value}, ensure_ascii=False))
        return 0 if ok else 2
    OWNER_CITY_ROOT.mkdir(parents=True, exist_ok=True)
    return run_owner_check(exe)


if __name__ == "__main__":
    raise SystemExit(main())
