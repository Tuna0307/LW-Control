"""Opt-in MAP-015 packaged UI acceptance using the existing owned MAP-014 runner.

This wrapper only runs supported production proof modes. It cannot add UI
automation that the packaged EXE does not contain. Nothing is launched without
--execute; the runner owns and audits its isolated game/app session.

Example (all paths belong to the current checkout's artifacts directory):
    python tools/map_015_package_ui.py --zip artifacts/.../candidate.zip \
        --extracted artifacts/.../extracted/LW-Control-UI \
        --commit <40-character SHA> --mode city --execute
"""

from __future__ import annotations

import argparse
from contextlib import closing
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import sqlite3
import subprocess
import sys
import zipfile


ROOT = Path(__file__).resolve().parents[1]
ARTIFACTS = (ROOT / "artifacts").resolve()
RUNNER = ROOT / "tools/map_014_lead_native/run_ui.py"
APP_FOLDER = "LW-Control-UI"
KIND_ORDER = ["city", "resource", "monster", "truck", "railway",
              "dispatch", "ghost", "treasure", "other", "zombie_boss"]
MODES = {
    "city": {"args": ["city"], "selected": ["city"], "requires_db": True},
    "truck": {"args": ["truck"], "selected": ["truck"], "requires_db": True},
    "all-nine": {"args": ["all-nine"], "selected": [
        "city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "zombie_boss"],
        "requires_db": True},
    "truck-stop": {"args": ["truck-stop"], "selected": None, "requires_db": True},
    "all-nontruck": {"args": ["all-nontruck"], "selected": [
        "city", "resource", "monster", "railway", "dispatch", "ghost", "treasure", "zombie_boss"],
        "requires_db": True},
    "other": {"args": ["other"], "selected": ["other"], "requires_db": True},
    "mixed-six": {"args": ["mixed"], "selected": KIND_ORDER[:6], "requires_db": True},
    "mixed-eight": {"args": ["mixed"], "selected": KIND_ORDER[:7] + ["zombie_boss"],
                    "new_kinds": True, "requires_db": True},
    "browse-new-kinds": {"args": ["browse"], "selected": None,
                         "new_kinds": True, "requires_saved_db": True},
    "remote-clear": {"args": ["remote"], "selected": None,
                     "remote": True, "requires_db": True},
}
KNOWN_LIMITS = [
    "Individual City and Other selections and the opt-in full eight-kind non-Truck selection are supported.",
    "mixed-six selects City/Resource/Monster/Truck/Railway/Dispatch; mixed-eight "
    "also selects Ghost/Zombie Boss, but excludes Treasure/Other.",
    "No existing packaged proof stops a scan mid-flight or exercises the Home UI regression.",
    "Ghost plunder-share rendering is source-calculated against native base_reward_show and steal_reward_ratio; "
    "the separate AND-filter proof still uses genuine saved native rows.",
    "remote-clear requires a 2212 authenticated owned start; it performs an Auto "
    "visit to 2213 and verifies Clear of 2213 saved rows after returning to 2212.",
    "The existing UI runner uses a retained native DB path even for new scans, "
    "and browse-new-kinds additionally needs an explicit saved complete genuine DB.",
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def within(path: Path, root: Path) -> bool:
    return path == root or root in path.parents


def require_artifact(path: str, *, file: bool) -> Path:
    resolved = Path(path).resolve(strict=True)
    if not within(resolved, ARTIFACTS):
        raise ValueError(f"Proof source must be under task artifacts: {resolved}")
    if (file and not resolved.is_file()) or (not file and not resolved.is_dir()):
        raise ValueError(f"Proof source has an unexpected type: {resolved}")
    return resolved


def verify_package(archive: Path, extracted: Path, commit: str) -> dict:
    """Compare every extracted regular file against the ZIP; never extract/write."""
    if not re.fullmatch(r"[0-9a-fA-F]{40}", commit):
        raise ValueError("--commit must be the full 40-character source commit SHA")
    if extracted.name != APP_FOLDER:
        raise ValueError(f"--extracted must point to the {APP_FOLDER} directory")
    expected = {}
    with zipfile.ZipFile(archive) as source:
        for info in source.infolist():
            name = info.filename.replace("\\", "/").rstrip("/")
            parts = PurePosixPath(name).parts
            if not parts or parts[0] != APP_FOLDER or any(p in ("..", ".") for p in parts):
                raise ValueError(f"Unexpected packaged path: {info.filename!r}")
            # Windows Compress-Archive can encode directories with backslashes,
            # which Python's ZipInfo.is_dir() alone does not recognize.
            if info.is_dir() or info.filename.endswith(("/", "\\")) or len(parts) == 1:
                continue
            relative = Path(*parts[1:])
            destination = (extracted / relative).resolve()
            if not within(destination, extracted) or not destination.is_file():
                raise ValueError(f"ZIP file is missing from the extracted app: {relative}")
            if info.file_size != destination.stat().st_size:
                raise ValueError(f"Extracted file size differs: {relative}")
            archive_digest = hashlib.sha256()
            with source.open(info) as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    archive_digest.update(block)
            if archive_digest.hexdigest() != sha256(destination):
                raise ValueError(f"Extracted file differs from ZIP: {relative}")
            key = relative.as_posix()
            if key in expected:
                raise ValueError(f"Duplicate packaged file: {relative}")
            expected[key] = archive_digest.hexdigest()
    actual = {p.relative_to(extracted).as_posix() for p in extracted.rglob("*") if p.is_file()}
    if actual != set(expected):
        raise ValueError(f"Extracted file list differs (extra={sorted(actual-set(expected))[:5]}, "
                         f"missing={sorted(set(expected)-actual)[:5]})")
    required = {"LWBridge.Desktop.exe", "LWBridge.Desktop.dll", "ProductionUi/index.html",
                "SOURCE-COMMIT.txt"}
    if not required.issubset(expected):
        raise ValueError(f"Missing required packaged files: {sorted(required-set(expected))}")
    source_commit = (extracted / "SOURCE-COMMIT.txt").read_text(encoding="ascii").strip()
    if source_commit.lower() != commit.lower():
        raise ValueError(f"Package source {source_commit!r} does not equal pinned commit {commit}")
    if commit[:12].lower() not in archive.name.lower():
        raise ValueError("ZIP name does not contain the pinned source commit prefix")
    return dict(zip=str(archive), zipSha256=sha256(archive), sourceCommit=source_commit,
                executable=str(extracted / "LWBridge.Desktop.exe"),
                executableSha256=expected["LWBridge.Desktop.exe"], fileCount=len(expected))


def check_saved_db(path: Path) -> None:
    # run_ui.py performs stronger native/genuine full-world checks before launch.
    with sqlite3.connect(path.as_uri() + "?mode=ro", uri=True) as database:
        row = database.execute("SELECT status,total_blocks,completed_blocks,failed_blocks "
                               "FROM scan_runs ORDER BY created_at DESC LIMIT 1").fetchone()
    if row != ("completed", 2500, 2500, 0):
        raise ValueError("Saved DB must contain a completed 2500/2500 native full-world scan")


def require_executable_source_commit(executable: str, commit: str) -> None:
    """Independently confirm the source stamp in the Windows PE version metadata."""
    if os.name != "nt":
        raise RuntimeError("Live packaged proof requires Windows")
    escaped = executable.replace("'", "''")
    result = subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         f"(Get-Item -LiteralPath '{escaped}').VersionInfo.ProductVersion"],
        capture_output=True, text=True, check=True,
    )
    if not result.stdout.strip().lower().endswith("+" + commit.lower()):
        raise ValueError("Packaged PE ProductVersion does not match the pinned source commit")


def audit_city_camera_free(destination: Path, proof: dict, restoration: dict,
                           mode: str = "city") -> dict:
    """Only the current owned run's direct source can establish MAP-015 proof.

    The normal UI proof intentionally certifies Map publication regardless of
    whether the C# source used the camera fallback. Require a separate direct
    native receipt and the C# prepared metrics for that *same* committed run.
    """
    profile = restoration.get("profileId")
    session = restoration.get("sessionId")
    game_pid = restoration.get("gamePid")
    if (profile != "map014-native-ui" or not isinstance(session, str) or not session or
            type(game_pid) is not int or game_pid <= 0):
        raise ValueError("City direct proof has no exact owned profile/session/game PID")
    helper_start = json.loads((destination / "overview-evidence" / session / "helper-start.json")
                              .read_text(encoding="utf-8"))
    if (helper_start.get("profileId") != profile or helper_start.get("sessionId") != session or
            helper_start.get("gamePid") != game_pid or
            not isinstance(helper_start.get("challengeSha256"), str) or
            not helper_start["challengeSha256"]):
        raise ValueError("City direct receipt does not match the genuine helper-start owner")

    selected = MODES[mode]["selected"]
    all_nontruck = mode in ("all-nontruck", "all-nine")
    completed = proof.get("completedStatus", {})
    ui_run = completed.get("scanRunId")
    if ui_run is not None and (not isinstance(ui_run, str) or not ui_run):
        raise ValueError("City UI completedStatus has an invalid scanRunId")
    if completed.get("selectedTypes") is not None and completed["selectedTypes"] != selected:
        raise ValueError("Packaged completed native scan selected unexpected kinds")
    if (proof.get("genuineGame") is not True or proof.get("controlledProvider") is not False or
            proof.get("proof") != ("actual_packaged_ui_all_nontruck_fullworld_scan_and_results"
                                   if all_nontruck else "actual_packaged_ui_fullworld_selected_scan_and_results") or
            proof.get("selectedTypes") != selected or proof.get("selection") != mode):
        raise ValueError("Packaged UI proof has an unexpected scan selection")
    if all_nontruck:
        browse = proof.get("browse", {})
        results = browse.get("results", [])
        native_gate = browse.get("cameraFree", {})
        if (browse.get("ghostNativeRewardDisplayProven") is not True or
                not isinstance(browse.get("scanRunId"), str) or not browse["scanRunId"] or
                (ui_run is not None and browse["scanRunId"] != ui_run) or
                browse.get("serverId") != proof.get("serverId") or
                browse.get("selectedKinds") != selected or
                native_gate.get("source") != "direct" or native_gate.get("completedAois") != 10000 or
                native_gate.get("repliesCaptured") != 63 or native_gate.get("batchesCaptured") != 63 or
                native_gate.get("cameraStable") is not True or
                native_gate.get("curViewRestored") is not True or native_gate.get("metricsPhase") != "prepared" or
                type(native_gate.get("requestsSent")) is not int or
                not 63 <= native_gate["requestsSent"] <= 65 or
                not isinstance(results, list) or [item.get("kind") for item in results] != selected or
                any(item.get("serverId") != proof.get("serverId") or
                    type(item.get("firstPageRows")) is not int or
                    type(item.get("total")) is not int or item["total"] < item["firstPageRows"] or
                    not isinstance(item.get("nativeSearchRequestId"), str) or not item["nativeSearchRequestId"]
                    for item in results)):
            raise ValueError("Packaged eight-kind native search/render proof is missing or inconsistent")
        ghost_result = next(item for item in results if item["kind"] == "ghost")
        reward_witness = ghost_result.get("positiveGhostRewardRendering")
        if (not isinstance(reward_witness, dict) or reward_witness.get("displayed") is not True or
                reward_witness.get("sourceCalculatedPlunderShareMatches") is not True or
                reward_witness.get("formula") !=
                "floor(sum(base_reward_show count for item) * steal_reward_ratio / 100)" or
                type(reward_witness.get("stealRewardRatio")) not in (int, float) or
                not 0 < reward_witness["stealRewardRatio"] <= 100 or
                not isinstance(reward_witness.get("nativeRewardCounts"), list) or
                not reward_witness["nativeRewardCounts"] or
                any(type(v) is not int or v <= 0 for v in reward_witness["nativeRewardCounts"])):
            raise ValueError("Packaged Ghost plunder-share calculation and native reward rendering were not proven")

    # SQLite is the independent publication record and supplies the scanRunId
    # when older UI receipts omit it. Refuse to use another profile's database.
    db_path = destination / "profiles" / profile / "map-data" / "map-data.db"
    with closing(sqlite3.connect(db_path.resolve().as_uri() + "?mode=ro", uri=True)) as database:
        rows = database.execute(
            "SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,"
            "failed_blocks,created_at,updated_at FROM scan_runs ORDER BY created_at DESC LIMIT 2"
        ).fetchall()
    if len(rows) != 1:
        raise ValueError("City direct proof needs exactly one published owned scan_run")
    (run_id, server_id, selected_json, status, total, finished, failed,
     created_at, updated_at) = rows[0]
    if (not isinstance(run_id, str) or not run_id or
            ui_run is not None and ui_run != run_id or
            json.loads(selected_json) != selected or status != "completed" or
            total != 2500 or finished != 2500 or failed != 0 or
            type(server_id) is not int or server_id <= 0 or
            type(created_at) is not int or type(updated_at) is not int or
            updated_at < created_at):
        raise ValueError("Published owned City scan_run disagrees with completed UI selection/run")
    if completed.get("serverId") is not None and completed["serverId"] != server_id:
        raise ValueError("City UI completed status has a different server")
    if proof.get("serverId") != server_id:
        raise ValueError("City UI proof has a different published server")
    if all_nontruck and proof["browse"]["scanRunId"] != run_id:
        raise ValueError("Packaged eight-kind UI browse references another scan run")

    direct_path = destination / "live-resource" / "camera-free-scan-result.json"
    metrics_path = destination / "live-resource" / "map-fullworld-probe-metrics.json"
    direct = json.loads(direct_path.read_text(encoding="utf-8"))
    metrics = json.loads(metrics_path.read_text(encoding="utf-8"))
    challenge = direct.get("challenge")
    if (not isinstance(challenge, str) or not challenge or
            hashlib.sha256(challenge.encode("utf-8")).hexdigest() != helper_start["challengeSha256"]):
        raise ValueError("City direct native challenge does not match the exact owned helper")
    if (direct.get("schemaVersion") != 1 or direct.get("state") != "proven" or
            direct.get("profileId") != profile or direct.get("launchSessionId") != session or
            direct.get("gamePid") != game_pid or direct.get("scanRunId") != run_id or
            direct.get("serverId") != server_id or direct.get("worldId") != 0 or
            not isinstance(direct.get("requestId"), str) or
            not direct["requestId"].startswith("direct") or
            direct.get("cameraStable") is not True or direct.get("curViewRestored") is not True or
            direct.get("completedAois") != 10000 or direct.get("repliesCaptured") != 63 or
            type(direct.get("requestsSent")) is not int or
            not 63 <= direct["requestsSent"] <= 65 or
            not isinstance(direct.get("batches"), list) or len(direct["batches"]) != 63):
        raise ValueError("City direct native receipt lacks complete exact-owned camera-free acquisition")
    if all_nontruck and proof["browse"]["cameraFree"]["requestsSent"] != direct["requestsSent"]:
        raise ValueError("Packaged eight-kind UI direct request count disagrees with native source")
    indices = set()
    for batch in direct["batches"]:
        if not isinstance(batch, dict) or not isinstance(batch.get("indices"), list):
            raise ValueError("City direct batch omits AOI indices")
        area = batch["indices"]
        if (not 1 <= len(area) <= 160 or any(type(index) is not int or not 0 <= index < 10000
                                            for index in area) or len(area) != len(set(area)) or
                not indices.isdisjoint(area)):
            raise ValueError("City direct batches contain duplicate or invalid AOI indices")
        indices.update(area)
    if len(indices) != 10000 or indices != set(range(10000)):
        raise ValueError("City direct batches do not prove every 10,000 AOI exactly once")

    identity = metrics.get("identity", {})
    record_counts = metrics.get("uniqueRecordKeys", {})
    if (metrics.get("schemaVersion") != 1 or
            metrics.get("source") != "CurrentClientMapBlockSource.ProbeCameraFreeMapAsync" or
            metrics.get("phase") != "prepared" or metrics.get("acquisitionPolicy") != "direct" or
            identity.get("RunId") != run_id or identity.get("ServerId") != server_id or
            identity.get("WorldId") != 0 or
            identity.get("SelectedTypesKey") != "\x1f".join(sorted(selected)) or
            metrics.get("uniqueCoveredAois") != 10000 or metrics.get("requiredAois") != 10000 or
            metrics.get("newlyCoveredAois") != 10000 or
            metrics.get("validatedProbeResponses") != 63 or
            metrics.get("attributableLuaProbeAttempts") != direct["requestsSent"] or
            type(record_counts.get("city")) is not int or record_counts["city"] < 1 or
            (all_nontruck and (type(record_counts.get("ghost")) is not int or
                               record_counts["ghost"] < 1)) or
            (not all_nontruck and (type(proof.get("total")) is not int or proof["total"] < 1))):
        raise ValueError("City prepared metrics do not prove the same complete direct acquisition")
    if completed.get("scanMode") is not None and identity.get("ScanMode") != completed["scanMode"]:
        raise ValueError("City direct metrics and UI scan mode differ")

    # Capture timestamps have whole-second precision. Use a two-second allowance
    # for serialization/filesystem rounding, never wall-clock 'now' (audits can
    # be repeated later). Freshness is relative to this exact DB scan run.
    captured_raw = direct.get("capturedAt")
    if not isinstance(captured_raw, str):
        raise ValueError("City direct native result has no capture time")
    captured = datetime.fromisoformat(captured_raw.replace("Z", "+00:00"))
    if captured.tzinfo is None:
        raise ValueError("City direct native capture time lacks UTC offset")
    captured_ms = captured.timestamp() * 1000
    native_ms = direct_path.stat().st_mtime_ns / 1_000_000
    metrics_ms = metrics_path.stat().st_mtime_ns / 1_000_000
    if (not created_at - 2000 <= captured_ms <= updated_at + 2000 or
            not created_at - 2000 <= native_ms <= updated_at + 2000 or
            not native_ms - 2000 <= metrics_ms <= updated_at + 2000):
        raise ValueError("City direct native receipt or prepared metrics are stale for this scan_run")
    return dict(source="direct", runId=run_id, serverId=server_id,
                completedAois=10000, successfulReplies=63, successfulBatches=63,
                requestsSent=direct["requestsSent"], cameraStable=True, curViewRestored=True,
                metricsPhase="prepared", metricsPath=str(metrics_path), nativePath=str(direct_path))


def audit_fresh_truck_filter(destination: Path, proof: dict, restoration: dict) -> dict:
    """Require a positive real packaged filter against this run's freshly scanned Trucks."""
    selected = MODES["mixed-eight"]["selected"]
    scan = proof.get("scan", {})
    run_id = scan.get("scanRunId")
    server = scan.get("serverId")
    if (proof.get("proof") != "normal_production_map_window_start_scan_search_render" or
            proof.get("selectedTypes") != selected or scan.get("phase") != "completed" or
            not isinstance(run_id, str) or not run_id or type(server) is not int or server <= 0):
        raise ValueError("Packaged Truck proof has no fresh genuine completed mixed scan")
    profile = restoration.get("profileId")
    if profile != "map014-native-ui":
        raise ValueError("Packaged Truck proof has an unexpected native profile")
    db_path = destination / "profiles" / profile / "map-data" / "map-data.db"
    with closing(sqlite3.connect(db_path.resolve().as_uri() + "?mode=ro", uri=True)) as database:
        runs = database.execute("SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,"
                                "failed_blocks FROM scan_runs ORDER BY created_at DESC LIMIT 2").fetchall()
        if runs != [(run_id, server, json.dumps(selected, separators=(",", ":")),
                     "completed", 2500, 2500, 0)]:
            raise ValueError("Packaged Truck filter does not belong to one fresh completed scan")
        scanned = {row[0] for row in database.execute(
            "SELECT uuid FROM map_records WHERE kind='truck' AND server_id=? AND uuid IS NOT NULL", (server,))}
    if not scanned:
        raise ValueError("Fresh published Truck scan has no positive source-backed rows")
    extras = json.loads((destination / "map014-ui-extras.json").read_text(encoding="utf-8"))
    witnesses = [item for item in extras.get("receipts", [])
                 if item.get("gate") == "genuine_truck_combined_filters"]
    if len(witnesses) != 1:
        raise ValueError("Packaged Truck filter lacks a unique actual UI witness")
    witness = witnesses[0]
    keys = witness.get("witnessKeys")
    payload = witness.get("payload", {})
    query = payload.get("query", {})
    result = witness.get("result", {})
    if (not isinstance(keys, list) or len(keys) != 2 or
            any(not isinstance(key, str) or not key for key in keys) or keys[0] == keys[1] or
            payload.get("kind") != "truck" or query.get("serverId") != server or
            query.get("escortPowerMaxMillions") != 25 or
            sorted(query.get("itemKeys", [])) != sorted(keys) or
            type(result.get("total")) is not int or result["total"] < 1 or
            not isinstance(result.get("rows"), list) or not result["rows"] or
            not (destination / "map014-truck-filtered.png").is_file()):
        raise ValueError("Packaged Truck UI did not apply its positive power and two-item filters")
    for item in result["rows"]:
        if (item.get("uuid") not in scanned or item.get("serverId") != server or
                type(item.get("power")) not in (int, float) or item["power"] > 25_000_000 or
                not isinstance(item.get("currentGoods"), list) or
                not set(keys).issubset({good.get("key") for good in item["currentGoods"]})):
            raise ValueError("Packaged Truck query included an unsaved or nonmatching native row")
    return dict(scanRunId=run_id, serverId=server, scannedTruckCount=len(scanned),
                filteredTotal=result["total"], selectedItemKeys=keys,
                screenshot=str(destination / "map014-truck-filtered.png"))


def audit_receipts(destination: Path, mode: str, package: dict) -> dict:
    read = lambda name: json.loads((destination / name).read_text(encoding="utf-8"))
    exit_receipt, owned = read("owned-exit.json"), read("owned-process.json")
    proof = read("native-ui-map.json")
    restoration = exit_receipt.get("restorationAudit", {})
    if (exit_receipt.get("exitCode") != 0 or exit_receipt.get("proof") is not True or
            exit_receipt.get("error") is not None or restoration.get("state") != "proven" or
            restoration.get("diagnosticResultSucceeded") is not True or
            restoration.get("hostPid") != owned.get("pid") or
            not all(exit_receipt.get("originalScriptsRestored", {}).values()) or
            exit_receipt.get("recoveryJournalPresent") is not False or
            Path(owned.get("publish", "")).resolve() != Path(package["executable"])):
        raise ValueError("Exact-owned app/game exit and original-script restoration were not proven")
    config = MODES[mode]
    if proof.get("state") != "proven":
        raise ValueError("Packaged native UI proof did not succeed")
    if config["selected"] is not None:
        if proof.get("selectedTypes") != config["selected"] or proof.get("scan", {}).get("phase") != "completed":
            # City/Other selection proof records completedStatus instead of scan.
            if not (mode in ("city", "other", "all-nontruck", "truck", "all-nine") and proof.get("selectedTypes") == config["selected"]
                    and proof.get("completedStatus", {}).get("phase") == "completed"):
                raise ValueError("Packaged proof does not certify the requested completed selection")
    if mode in ("truck", "all-nine"):
        browse = proof.get("browse", {})
        native = browse.get("native") if mode == "truck" else browse.get("fastTruck")
        if (not isinstance(native, dict) or native.get("pushes") != 625 or
                native.get("requestsPerSecond") not in (15, 20) or
                native.get("cameraStable") is not True or native.get("curViewRestored") is not True or
                not isinstance(native.get("trainRecordCount"), int) or native["trainRecordCount"] <= 0):
            raise ValueError("Packaged fast Truck native push/field/restoration proof missing")
        if mode == "truck":
            filter_result = browse.get("filter", {})
        else:
            filter_result = browse.get("truckFilter")
        if (not isinstance(filter_result, dict) or filter_result.get("filteredTotal", 0) < 1 or
                not Path(filter_result.get("screenshot", "")).is_file()):
            raise ValueError("Packaged fast Truck positive combined filter is missing")
    if mode == "truck-stop":
        if (proof.get("proof") != "actual_packaged_ui_truck_stop_mid_sweep" or
                proof.get("startedStatus", {}).get("phase") not in ("scanning", "reading", "running") and
                proof.get("startedStatus", {}).get("isReading") is not True or
                proof.get("stoppedStatus", {}).get("phase") != "idle" or
                proof.get("stoppedStatus", {}).get("isReading") is not False):
            raise ValueError("Packaged Truck mid-sweep Stop did not durably cancel")
        native_started = proof.get("nativeStarted", {})
        run_id = proof.get("stopRunId")
        if (not isinstance(run_id, str) or not run_id or
                proof.get("startedStatus", {}).get("scanRunId") != run_id or
                proof.get("stoppedStatus", {}).get("scanRunId") != run_id or
                native_started.get("scanRunId") != run_id or
                native_started.get("profileId") != restoration.get("profileId") or
                native_started.get("launchSessionId") != restoration.get("sessionId") or
                native_started.get("gamePid") != restoration.get("gamePid") or
                native_started.get("requestsSent") != 20 or
                native_started.get("nativeActive") is not True):
            raise ValueError("Truck Stop did not interrupt the exact native owned request after 20 sends")
        native = read("live-resource/fast-truck-scan-result.json")
        if (native.get("state") != "failed" or native.get("error") != "cancelled" or
                not isinstance(native_started.get("requestId"), str) or
                not native_started["requestId"] or
                native.get("requestId") != native_started["requestId"] or
                native.get("scanRunId") != run_id or
                native.get("profileId") != restoration.get("profileId") or
                native.get("launchSessionId") != restoration.get("sessionId") or
                native.get("gamePid") != restoration.get("gamePid") or
                native.get("cameraStable") is not True or
                native.get("curViewRestored") is not True or
                type(native.get("requestsSent")) is not int or
                not 20 <= native["requestsSent"] < 625):
            raise ValueError("Native Truck Stop lacks a correlated safe cancellation and restoration receipt")
        db_path = destination / "profiles" / restoration["profileId"] / "map-data" / "map-data.db"
        with closing(sqlite3.connect(db_path.resolve().as_uri() + "?mode=ro", uri=True)) as database:
            runs = database.execute(
                "SELECT id,status,selected_types,total_blocks,completed_blocks,failed_blocks "
                "FROM scan_runs ORDER BY created_at DESC LIMIT 2").fetchall()
            staged = database.execute("SELECT COUNT(*) FROM scan_records WHERE run_id=?", (run_id,)).fetchone()[0]
            live = database.execute("SELECT COUNT(*) FROM scan_runs WHERE status IN ('running','paused')").fetchone()[0]
        if (len(runs) != 1 or runs[0][0:4] != (run_id, "cancelled", '["truck"]', 2500) or
                runs[0][4] >= 2500 or runs[0][5] != 0 or staged != 0 or live != 0):
            raise ValueError("Packaged Truck Stop left incomplete durability or staged scan records")
    if config.get("remote"):
        remote = read("map014-remote-ui.json")
        stages = [entry.get("stage") for entry in remote.get("results", [])]
        if "auto_clear_ui_correct_server" not in stages or "cleanup" not in stages or not any(
                entry.get("stage") == "cleanup" and entry.get("restored") is True
                for entry in remote.get("results", [])):
            raise ValueError("Remote saved-server Clear and home cleanup were not proven")
    if mode == "browse-new-kinds":
        extras = read("map014-ui-extras.json")
        gates = [entry.get("gate") for entry in extras.get("receipts", [])]
        if not {"native_zombie_boss_tab_level_filter", "native_ghost_reward_and_filter"}.issubset(gates):
            raise ValueError("Saved genuine Zombie Boss/Ghost positive UI filters were not proven")
    direct = audit_city_camera_free(destination, proof, restoration, mode) if mode in ("city", "all-nontruck", "all-nine") else None
    truck = audit_fresh_truck_filter(destination, proof, restoration) if mode == "mixed-eight" else None
    return dict(mode=mode, state="proven", proof=str(destination / "native-ui-map.json"),
                ownedExit=str(destination / "owned-exit.json"), hostPid=restoration["hostPid"],
                gamePid=restoration["gamePid"], sessionId=restoration["sessionId"],
                **({"cameraFree": direct} if direct else {}),
                **({"freshTruckFilter": truck} if truck else {}))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--zip", required=True, help="Final source-pinned Windows release ZIP under artifacts")
    parser.add_argument("--extracted", required=True, help="Extracted LW-Control-UI directory under artifacts")
    parser.add_argument("--commit", required=True, help="Exact 40-character source commit used in the release")
    parser.add_argument("--mode", required=True, choices=sorted(MODES))
    parser.add_argument("--saved-db", help="Existing genuine completed scan DB for browse-new-kinds")
    parser.add_argument("--allow-remote", action="store_true", help="Explicitly allow owned 2212->2213->2212 Auto test")
    parser.add_argument("--execute", action="store_true", help="Start the exact-owned app/game proof")
    args = parser.parse_args(argv)
    package = verify_package(require_artifact(args.zip, file=True),
                             require_artifact(args.extracted, file=False), args.commit)
    mode = MODES[args.mode]
    if mode.get("remote") and not args.allow_remote:
        parser.error("remote-clear requires --allow-remote")
    saved_db = require_artifact(args.saved_db, file=True) if args.saved_db else None
    if mode.get("requires_saved_db"):
        if saved_db is None or saved_db.name != "map-data.db":
            parser.error("browse-new-kinds requires --saved-db artifacts/.../map-data.db")
        check_saved_db(saved_db)
    elif saved_db is not None:
        parser.error("--saved-db is only supported by browse-new-kinds")
    # run_ui.py requires this specific retained native source even for a new scan.
    if mode.get("requires_db") and not (
            ROOT / "artifacts/map014-lead-ui/native-20261013-104453/profiles/map014-native-ui/map-data/map-data.db").is_file():
        raise FileNotFoundError("The existing MAP014 run_ui.py requires its hardcoded retained native DB")
    command = [sys.executable, str(RUNNER), *mode["args"]]
    if saved_db:
        command.extend(["--saved-db", str(saved_db)])
    result = dict(package=package, mode=args.mode, command=command, limits=KNOWN_LIMITS,
                  executesGame=args.execute)
    print(json.dumps(result, indent=2), flush=True)
    if not args.execute:
        return 0
    require_executable_source_commit(package["executable"], args.commit)
    environment = os.environ.copy()
    environment["LWBRIDGE_MAP014_UI_EXE"] = package["executable"]
    environment["LWBRIDGE_MAP014_NEW_KINDS_UI"] = "1" if mode.get("new_kinds") else "0"
    before = set((ARTIFACTS / "map014-takeover").glob("native-ui-*"))
    completed = subprocess.run(command, cwd=ROOT, env=environment, check=False)
    after = set((ARTIFACTS / "map014-takeover").glob("native-ui-*")) - before
    if completed.returncode != 0:
        raise RuntimeError(f"Owned packaged UI runner exited {completed.returncode}; inspect its receipts")
    if len(after) != 1:
        raise RuntimeError("Cannot identify exactly one new owned native UI proof directory")
    receipt = audit_receipts(after.pop(), args.mode, package)
    print("MAP015_PACKAGED_UI_RECEIPT", json.dumps(receipt, indent=2), flush=True)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, ValueError, zipfile.BadZipFile) as exc:
        print("MAP015_PACKAGE_UI_ERROR: " + str(exc), file=sys.stderr)
        sys.exit(2)
