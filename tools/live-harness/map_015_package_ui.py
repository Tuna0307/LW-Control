"""Opt-in MAP-015 packaged UI acceptance using the existing owned MAP-014 runner.

This wrapper only runs supported production proof modes. It cannot add UI
automation that the packaged EXE does not contain. Nothing is launched without
--execute; the runner owns and audits its isolated game/app session.

Example (all paths belong to the current checkout's artifacts directory):
    python tools/live-harness/map_015_package_ui.py --zip artifacts/.../candidate.zip \
        --extracted artifacts/.../extracted/LW-Control-UI \
        --commit <40-character SHA> --mode city --execute
"""

from __future__ import annotations

import argparse
from contextlib import closing
from datetime import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import re
import sqlite3
import subprocess
import sys
import zipfile


ROOT = Path(__file__).resolve().parents[2]
ARTIFACTS = (ROOT / "artifacts").resolve()
RUNNER = ROOT / "tools/live-harness/run_ui.py"
APP_FOLDER = "LW-Control-UI"
PROGRAMME_MANIFEST = "PROGRAMME-RELEASE-INTEGRITY.json"
KIND_ORDER = ["city", "resource", "monster", "truck", "railway",
              "dispatch", "ghost", "treasure", "other", "zombie_boss"]
MODES = {
    "city": {"args": ["city"], "selected": ["city"], "requires_db": True},
    "railway": {"args": ["railway"], "selected": ["railway"], "requires_db": True},
    "truck": {"args": ["truck"], "selected": ["truck"], "requires_db": True},
    "all-nine": {"args": ["all-nine"], "selected": [
        "city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "zombie_boss"],
        "requires_db": True},
    "all-ten": {"args": ["all-ten"], "selected": KIND_ORDER.copy(), "requires_db": True},
    "auto-all-nine": {"args": ["auto-all-nine"], "selected": [
        "city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "zombie_boss"],
        "requires_db": True, "remote": True},
    "auto-all-ten": {"args": ["auto-all-ten"], "selected": KIND_ORDER.copy(),
                     "requires_db": True, "remote": True},
    "saved-clear-reopen": {"args": ["saved-clear-reopen"], "selected": None,
                           "requires_db": True, "remote": True},
    "auto-interval-20m": {"args": ["auto-interval-20m"], "selected": None,
                          "requires_db": True, "remote": True},
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
    "remote-railway": {"args": ["remote-railway"], "selected": None,
                       "remote": True, "requires_db": True},
}
KNOWN_LIMITS = [
    "Individual City, Railway and Other selections and the opt-in full eight-kind non-Truck selection are supported.",
    "mixed-six selects City/Resource/Monster/Truck/Railway/Dispatch; mixed-eight "
    "also selects Ghost/Zombie Boss, but excludes Treasure/Other.",
    "Truck Stop has historical packaged proof; the final candidate replays it because Map staging changed.",
    "Ghost plunder-share rendering is source-calculated against native base_reward_show and steal_reward_ratio; "
    "the separate AND-filter proof still uses genuine saved native rows.",
    "remote-clear requires a 2212 authenticated owned start; it performs an Auto "
    "visit to 2213 and verifies Clear of 2213 saved rows after returning to 2212.",
    "The existing UI runner uses a retained native DB path even for new scans, "
    "and browse-new-kinds additionally needs an explicit saved complete genuine DB.",
    "auto-all-nine/auto-all-ten are opt-in only: a single real Auto Run Now scans the requested kinds on 2211 "
    "from original 2212 and must return home. Interval runs, Manual Start, and "
    "partial native/SQLite/UI evidence are not accepted.",
    "saved-clear-reopen clones genuine preserved 2212 source SQLite, performs one "
    "owned Resource Auto 2213 scan, clears only 2213 via real Auto UI and reloads "
    "the same packaged WebView; the original 2212 data and game server must remain.",
    "auto-interval-20m observes >=1200 real monotonic seconds of an enabled "
    "but idle native Auto Railway scheduler, then its due 2211 scan/return. No "
    "clock substitution, Run Now, Manual Start or updater is permitted.",
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def exact_unordered_selected_types(actual, expected):
    """React checkbox selection order is not semantic; duplicates still fail."""
    return (isinstance(actual, list) and len(actual) == len(expected) and
            all(isinstance(value, str) for value in actual) and
            len(set(actual)) == len(expected) and set(actual) == set(expected))


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
    programme_manifest = None
    with zipfile.ZipFile(archive) as source:
        for info in source.infolist():
            name = info.filename.replace("\\", "/").rstrip("/")
            parts = PurePosixPath(name).parts
            if name == PROGRAMME_MANIFEST and len(parts) == 1:
                if info.is_dir():
                    raise ValueError("Programme integrity manifest cannot be a directory")
                if programme_manifest is not None:
                    raise ValueError("Duplicate programme integrity manifest")
                programme_manifest = json.loads(source.read(info))
                continue
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
    if programme_manifest is not None:
        if str(programme_manifest.get("sourceCommit", "")).lower() != commit.lower():
            raise ValueError("Programme integrity manifest source commit differs from pinned commit")
        manifest_files = programme_manifest.get("files")
        if not isinstance(manifest_files, dict):
            raise ValueError("Programme integrity manifest has no file inventory")
        expected_manifest_names = {f"{APP_FOLDER}/{name}" for name in expected}
        if set(manifest_files) != expected_manifest_names:
            raise ValueError("Programme integrity manifest file inventory differs from extracted package")
        for relative, archive_digest in expected.items():
            entry = manifest_files[f"{APP_FOLDER}/{relative}"]
            path = extracted / relative
            if (not isinstance(entry, dict) or entry.get("sha256") != archive_digest or
                    entry.get("size") != path.stat().st_size):
                raise ValueError(f"Programme integrity manifest payload differs: {relative}")
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
    all_nontruck = mode in ("all-nontruck", "all-nine", "all-ten", "auto-all-nine", "auto-all-ten")
    completed = proof.get("completedStatus", {})
    ui_run = completed.get("scanRunId")
    if ui_run is not None and (not isinstance(ui_run, str) or not ui_run):
        raise ValueError("City UI completedStatus has an invalid scanRunId")
    if completed.get("selectedTypes") is not None and not (
            exact_unordered_selected_types(completed["selectedTypes"], selected)
            if mode in ("auto-all-nine", "auto-all-ten") else completed["selectedTypes"] == selected):
        raise ValueError("Packaged completed native scan selected unexpected kinds")
    expected_proof = ("actual_packaged_ui_auto_all_ten_run_now_fullworld_scan_and_results"
                      if mode == "auto-all-ten" else
                      "actual_packaged_ui_auto_all_nine_run_now_fullworld_scan_and_results"
                      if mode == "auto-all-nine" else
                      "actual_packaged_ui_all_ten_fullworld_scan_and_results"
                      if mode == "all-ten" else
                      "actual_packaged_ui_all_nontruck_fullworld_scan_and_results"
                      if all_nontruck else "actual_packaged_ui_fullworld_selected_scan_and_results")
    if (proof.get("genuineGame") is not True or proof.get("controlledProvider") is not False or
            proof.get("proof") != expected_proof or
            proof.get("selectedTypes") != selected or proof.get("selection") != mode):
        raise ValueError("Packaged UI proof has an unexpected scan selection")
    if all_nontruck:
        browse = proof.get("browse", {})
        results = browse.get("results", [])
        native_gate = browse.get("cameraFree", {})
        ghost_tab = next((item for item in results if item.get("kind") == "ghost"), {})
        ghost_positive_proven = browse.get("ghostNativeRewardDisplayProven") is True
        if (not (ghost_positive_proven or mode in ("all-nine", "all-ten", "auto-all-nine", "auto-all-ten") and
                 ghost_tab.get("total") == 0 and ghost_tab.get("firstPageRows") == 0 and
                 ghost_tab.get("positiveGhostRewardRendering") is None) or
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
        if (ghost_positive_proven and (not isinstance(reward_witness, dict) or reward_witness.get("displayed") is not True or
                reward_witness.get("sourceCalculatedPlunderShareMatches") is not True or
                reward_witness.get("formula") !=
                "floor(sum(base_reward_show count for item) * steal_reward_ratio / 100)" or
                type(reward_witness.get("stealRewardRatio")) not in (int, float) or
                not 0 < reward_witness["stealRewardRatio"] <= 100 or
                not isinstance(reward_witness.get("nativeRewardCounts"), list) or
                not reward_witness["nativeRewardCounts"] or
                any(type(v) is not int or v <= 0 for v in reward_witness["nativeRewardCounts"]))):
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
            not (exact_unordered_selected_types(json.loads(selected_json), selected)
                 if mode in ("auto-all-nine", "auto-all-ten") else json.loads(selected_json) == selected) or status != "completed" or
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
    native_selected = sorted({"treasure" if kind == "other" else kind for kind in selected})
    if (metrics.get("schemaVersion") != 1 or
            metrics.get("source") != "CurrentClientMapBlockSource.ProbeCameraFreeMapAsync" or
            metrics.get("phase") != "prepared" or metrics.get("acquisitionPolicy") != "direct" or
            identity.get("RunId") != run_id or identity.get("ServerId") != server_id or
            identity.get("WorldId") != 0 or
            identity.get("SelectedTypesKey") != "\x1f".join(native_selected) or
            metrics.get("uniqueCoveredAois") != 10000 or metrics.get("requiredAois") != 10000 or
            metrics.get("newlyCoveredAois") != 10000 or
            metrics.get("validatedProbeResponses") != 63 or
            metrics.get("attributableLuaProbeAttempts") != direct["requestsSent"] or
            type(record_counts.get("city")) is not int or record_counts["city"] < 1 or
            (mode == "all-nontruck" and (type(record_counts.get("ghost")) is not int or
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


def audit_published_fast_truck_rows(db_path: Path, server_id: int) -> int:
    """Prove actual SQLite publication came from the verified fast producer.

    The presence of a proven 625-rectangle receipt by itself is insufficient:
    managed import can fail after native cleanup and the scan can complete with
    camera fallback. Check every saved Truck and its capture-time deadline.
    """
    with closing(sqlite3.connect(db_path.resolve().as_uri() + "?mode=ro", uri=True)) as database:
        published = database.execute(
            "SELECT updated_at,data_json FROM map_records WHERE kind='truck' AND server_id=?",
            (server_id,)).fetchall()
    if not published:
        raise ValueError("Fast Truck scan published no current owned Truck records")
    for updated_at, raw in published:
        data = json.loads(raw)
        if type(updated_at) is not int or not isinstance(data, dict):
            raise ValueError("Fast Truck row lacks an auditable capture timestamp")
        if data.get("captureProtocol") != "current_fast_truck_625_v1":
            raise ValueError("Published Truck records originated from camera fallback despite a proven native receipt")
        for field in ("endTime", "arriveTs"):
            value = data.get(field)
            if value is None:
                continue
            if type(value) is not int or value <= 0:
                raise ValueError("Fast Truck row has an invalid arrival deadline")
            arrive_ms = value * 1000 if value < 100_000_000_000 else value
            if arrive_ms <= updated_at:
                raise ValueError("Fast Truck published a row already arrived at capture")
    return len(published)


def require_qualified_fast_truck_proof(native: dict) -> None:
    if (not isinstance(native, dict) or native.get("pushes") != 625 or
            native.get("requestsPerSecond") != 20 or native.get("responseFenceSize") != 25 or
            native.get("cameraStable") is not True or native.get("curViewRestored") is not True or
            not isinstance(native.get("trainRecordCount"), int) or native["trainRecordCount"] <= 0):
        raise ValueError("Packaged fast Truck native proof does not match qualified 20/s fence25 acquisition")


def audit_fast_railway(destination: Path, proof: dict, restoration: dict,
                       screenshot_name: str = "map014-railway-packaged.png",
                       returned_to_server: int | None = None) -> dict:
    """Admit an exact-owned 625-rectangle Railway publication, including a verified empty one.

    A positive native receipt alone cannot establish that managed import was used:
    a subsequent camera fallback may have completed the scan. Reconcile the
    native identity set, persisted source markers and real UI search together.
    A genuinely empty result additionally requires an absence of the fallback
    sidecar in this fresh isolated owner root.
    """
    selected = ["railway"]
    profile = restoration.get("profileId")
    session = restoration.get("sessionId")
    game_pid = restoration.get("gamePid")
    completed = proof.get("completedStatus", {})
    run_id = completed.get("scanRunId")
    # Map317 overlays the *live* server after Auto returns home, while the
    # scan_run and native receipt retain the scanned server. Only the remote
    # proof has this separately verified home-return exception.
    observed_server = completed.get("serverId")
    server_id = proof.get("serverId") if returned_to_server is not None else observed_server
    if (proof.get("proof") != "actual_packaged_ui_fast_railway_fullworld_scan_and_results" or
            proof.get("genuineGame") is not True or proof.get("controlledProvider") is not False or
            proof.get("selection") != "railway" or proof.get("selectedTypes") != selected or
            completed.get("selectedTypes") != selected or completed.get("phase") != "completed" or
            type(completed.get("totalBlocks")) is not int or completed["totalBlocks"] != 2500 or
            type(completed.get("readBlocks")) is not int or completed["readBlocks"] != 2500 or
            completed.get("failedBlocks") != 0 or completed.get("unreadBlocks") != 0 or
            not isinstance(run_id, str) or not run_id or
            type(server_id) is not int or server_id <= 0 or proof.get("serverId") != server_id or
            (returned_to_server is not None and
             observed_server not in (server_id, returned_to_server)) or
            profile != "map014-native-ui" or not isinstance(session, str) or not session or
            type(game_pid) is not int or game_pid <= 0):
        raise ValueError("Railway packaged UI lacks one completed exact-owned selected scan")

    screenshot = destination / screenshot_name
    if (not screenshot.is_file() or not isinstance(proof.get("screenshot"), str) or
            Path(proof["screenshot"]).resolve() != screenshot.resolve()):
        raise ValueError("Railway packaged results screenshot is missing or belongs to another root")
    selected_response = proof.get("selectedResponse", {})
    payload = selected_response.get("payload", {})
    query = payload.get("query", {})
    result = selected_response.get("result", {})
    total = result.get("total")
    rows = result.get("rows")
    mounted = proof.get("mountedDom", {})
    if (payload.get("kind") != "railway" or query.get("serverId") != server_id or
            type(total) is not int or total < 0 or proof.get("total") != total or
            not isinstance(rows, list) or len(rows) > total or
            not isinstance(mounted, dict) or mounted.get("tablePresent") is not True or
            type(mounted.get("displayedRows")) is not int or mounted["displayedRows"] < 0):
        raise ValueError("Railway packaged results lack a genuine same-server map_search and mounted table")

    db_path = destination / "profiles" / profile / "map-data" / "map-data.db"
    with closing(sqlite3.connect(db_path.resolve().as_uri() + "?mode=ro", uri=True)) as database:
        runs = database.execute(
            "SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,"
            "failed_blocks,created_at,updated_at FROM scan_runs ORDER BY created_at DESC LIMIT 2"
        ).fetchall()
        if len(runs) != 1:
            raise ValueError("Railway packaged scan lacks a unique owned SQLite run")
        (saved_id, saved_server, selected_json, status, blocks, done, failed,
         created_at, updated_at) = runs[0]
        if (saved_id != run_id or saved_server != server_id or
                json.loads(selected_json) != selected or status != "completed" or
                (blocks, done, failed) != (2500, 2500, 0) or
                type(created_at) is not int or type(updated_at) is not int or
                updated_at < created_at):
            raise ValueError("Railway published SQLite scan does not match the owned UI run")
        published = database.execute(
            "SELECT record_key,uuid,updated_at,data_json FROM map_records "
            "WHERE kind='railway' AND server_id=?", (server_id,)
        ).fetchall()

    helper = json.loads((destination / "overview-evidence" / session / "helper-start.json")
                        .read_text(encoding="utf-8"))
    native_path = destination / "live-resource" / "fast-truck-scan-result.json"
    native = json.loads(native_path.read_text(encoding="utf-8"))
    challenge = native.get("challenge")
    if (helper.get("profileId") != profile or helper.get("sessionId") != session or
            helper.get("gamePid") != game_pid or
            not isinstance(challenge, str) or not challenge or
            hashlib.sha256(challenge.encode("utf-8")).hexdigest() != helper.get("challengeSha256")):
        raise ValueError("Railway native challenge does not belong to the exact owned helper")
    attempts = native.get("requestsSent")
    passes = native.get("repairPasses")
    pushes = native.get("pushesObserved")
    railway_rows = native.get("railwayRecords")
    if (native.get("schemaVersion") != 1 or native.get("state") != "proven" or
            not isinstance(native.get("requestId"), str) or
            not native["requestId"].startswith("truck") or
            native.get("profileId") != profile or native.get("launchSessionId") != session or
            native.get("gamePid") != game_pid or native.get("scanRunId") != run_id or
            native.get("serverId") != server_id or native.get("worldId") != 0 or
            native.get("railwayIncluded") is not True or native.get("railwayOnly") is not True or
            native.get("cameraStable") is not True or native.get("curViewRestored") is not True or
            native.get("initialRequestsSent") != 625 or native.get("rectanglesCompleted") != 625 or
            native.get("unprovenRectangles") != 0 or native.get("requestsPerSecond") != 20 or
            native.get("responseFenceSize") != 25 or
            native.get("responseAttribution") != "bounded_sequential_fences" or
            type(attempts) is not int or attempts < 625 or
            type(passes) is not int or not 0 <= passes <= 2 or
            native.get("repairRequestsSent") != attempts - 625 or
            type(pushes) is not int or not 625 <= pushes <= attempts or
            not isinstance(railway_rows, list) or
            type(native.get("railwayRecordCount")) is not int or
            native["railwayRecordCount"] != len(railway_rows) or
            native.get("trainRecordCount") != 0 or native.get("trainRecords") != []):
        raise ValueError("Railway native source lacks 625 verified typed rectangles or restoration")
    captured_raw = native.get("capturedAt")
    if not isinstance(captured_raw, str):
        raise ValueError("Railway native source has no capture time")
    try:
        captured = datetime.fromisoformat(captured_raw.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError("Railway native source has an invalid capture time") from error
    if captured.tzinfo is None:
        raise ValueError("Railway native capture time lacks UTC offset")
    captured_ms = captured.timestamp() * 1000
    native_file_ms = native_path.stat().st_mtime_ns / 1_000_000
    if (not created_at - 2000 <= captured_ms <= updated_at + 2000 or
            not created_at - 2000 <= native_file_ms <= updated_at + 2000 or
            (destination / "live-resource" / "map015-direct-fallback.json").exists()):
        raise ValueError("Railway native receipt is stale or a camera fallback was used")

    source = "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train"
    identities = set()
    for row in railway_rows:
        if not isinstance(row, dict):
            raise ValueError("Railway native record has an invalid shape")
        uuid = row.get("uuid")
        observed = row.get("capturedAtMs")
        if (not isinstance(uuid, str) or not uuid or uuid in identities or
                type(row.get("trainType")) is not int or row["trainType"] != 2 or
                row.get("serverId") != server_id or row.get("worldId") != 0 or
                type(row.get("x")) is not int or not 0 <= row["x"] < 1000 or
                type(row.get("y")) is not int or not 0 <= row["y"] < 1000 or
                type(row.get("trainQuality")) is not int or row["trainQuality"] < 1 or
                type(row.get("trainCfgId")) is not int or
                type(row.get("carriageNum")) is not int or
                type(observed) is not int or not captured_ms - 240_000 <= observed <= captured_ms + 2000 or
                row.get("source") != source):
            raise ValueError("Railway native typed source has a foreign, duplicate or invalid row")
        identities.add(uuid)

    published_ids = set()
    for key, uuid, observed, raw in published:
        data = json.loads(raw)
        if (not isinstance(data, dict) or not isinstance(key, str) or not key or
                key != uuid or uuid in published_ids or
                uuid not in identities or data.get("uuid") != uuid or
                data.get("trainType") != 2 or data.get("source") != source or
                data.get("captureProtocol") != "current_fast_railway_625_v1" or
                type(observed) is not int or
                type(data.get("updatedAt")) is not int or data["updatedAt"] != observed):
            raise ValueError("Railway published a foreign or camera fallback row")
        published_ids.add(uuid)
    if published_ids != identities or total != len(identities):
        raise ValueError("Railway native, SQLite and real UI result counts disagree")
    if any(not isinstance(row, dict) or row.get("uuid") not in published_ids or
           row.get("serverId") != server_id for row in rows):
        raise ValueError("Railway UI displayed a foreign or unpublished native record")
    return dict(source="fast_railway_625", runId=run_id, serverId=server_id,
                rectanglesVerified=625, railwayCount=len(identities),
                verifiedEmpty=not identities, repairPasses=passes,
                cameraStable=True, curViewRestored=True,
                screenshot=str(screenshot), nativePath=str(native_path))


def audit_remote_railway(destination: Path, proof: dict, restoration: dict) -> dict:
    """Reconcile a single owned Auto 2212→2211 Railway scan and natural return."""
    marker = "real_native_auto_remote_railway_2211_fast_625_return"
    remote = json.loads((destination / "map014-remote-ui.json").read_text(encoding="utf-8"))
    results = remote.get("results")
    if (proof.get("proof") != marker or remote.get("proof") != marker or
            remote.get("genuineGame") is not True or remote.get("controlledProvider") is not False or
            remote.get("homeServer") != 2212 or remote.get("remoteServer") != 2211 or
            remote.get("profileId") != restoration.get("profileId") or
            remote.get("sessionId") != restoration.get("sessionId") or
            remote.get("gamePid") != restoration.get("gamePid") or
            not isinstance(results, list)):
        raise ValueError("Remote Railway proof lacks one genuine exact-owned 2212→2211 session")
    stages = [item.get("stage") for item in results if isinstance(item, dict)]
    required = ["railway_original_server", "railway_auto_config_persisted",
                "railway_auto_returned_home", "railway_remote_ui_saved_result", "cleanup"]
    if (stages != required or any(stage in stages for stage in
            ("remote_ui_stored_row_native_jump", "auto_clear_ui_correct_server"))):
        raise ValueError("Remote Railway proof included unwanted Jump/Clear or missing lifecycle stages")
    original, config, returned, saved, cleanup = results
    cycle = returned.get("lastCycle", {})
    if (original.get("serverId") != 2212 or config.get("serverIds") != [2211] or
            config.get("selectedTypes") != ["railway"] or
            config.get("scanMode") != "fast" or config.get("returnToOriginalServer") is not True or
            returned.get("originalServerId") != 2212 or
            returned.get("targetServerId") != 2211 or returned.get("liveServerId") != 2212 or
            returned.get("nativeRectangles") != 625 or
            not isinstance(returned.get("scanRunId"), str) or not returned["scanRunId"] or
            type(returned.get("nativeRailwayRecords")) is not int or
            cycle.get("outcome") != "completed" or cycle.get("originalServerId") != 2212 or
            cycle.get("targetServerIds") != [2211] or cycle.get("completedServerIds") != [2211] or
            cycle.get("lastError") is not None or
            cleanup.get("restored") is not True or cleanup.get("homeServer") != 2212 or
            cleanup.get("liveServerId") != 2212 or cleanup.get("autoEnabled") is not False or
            cleanup.get("autoRunning") is not False or
            cleanup.get("noAdditionalNavigation") is not True or
            saved.get("scanRunId") != returned["scanRunId"] or
            saved.get("serverId") != 2211):
        raise ValueError("Remote Railway Auto cycle or exact original-server return is unproven")
    verified_empty = returned["nativeRailwayRecords"] == 0
    if verified_empty:
        if (saved.get("selectedServerId") is not None or
                saved.get("source") != "production_map_search_verified_empty" or
                saved.get("total") != 0 or
                not isinstance(saved.get("mountedDom"), dict) or
                saved["mountedDom"].get("serverOptionAbsent") is not True):
            raise ValueError("Verified empty Railway invented a saved server or skipped backend empty search")
    elif (saved.get("selectedServerId") != 2211 or
          saved.get("source") != "real_packaged_webview_map_search"):
        raise ValueError("Positive Railway did not browse the real saved 2211 result")
    try:
        triggered = datetime.fromisoformat(returned["triggeredAt"].replace("Z", "+00:00"))
        remote_at = datetime.fromisoformat(returned["sawRemoteAt"].replace("Z", "+00:00"))
        native_at = datetime.fromisoformat(returned["nativeReceiptAt"].replace("Z", "+00:00"))
        home_at = datetime.fromisoformat(returned["observedHomeAt"].replace("Z", "+00:00"))
    except (KeyError, AttributeError, ValueError) as error:
        raise ValueError("Remote Railway missing source-observed stage timestamps") from error
    if (any(item.tzinfo is None for item in (triggered, remote_at, native_at, home_at)) or
            not triggered <= remote_at <= native_at <= home_at or
            type(returned.get("switchObservedMilliseconds")) not in (int, float) or
            type(returned.get("sweepElapsedSeconds")) not in (int, float) or
            returned["sweepElapsedSeconds"] <= 0 or
            type(returned.get("returnObservedMilliseconds")) not in (int, float) or
            abs((remote_at - triggered).total_seconds() * 1000 -
                returned["switchObservedMilliseconds"]) > 250 or
            abs((home_at - native_at).total_seconds() * 1000 -
                returned["returnObservedMilliseconds"]) > 250):
        raise ValueError("Remote Railway switch/sweep/return observations are inconsistent")
    observed = dict(
        proof="actual_packaged_ui_fast_railway_fullworld_scan_and_results",
        genuineGame=True, controlledProvider=False, selection="railway",
        selectedTypes=["railway"], serverId=2211,
        completedStatus=saved.get("completedStatus"),
        selectedResponse=saved.get("selectedResponse"),
        total=saved.get("total"), mountedDom=saved.get("mountedDom"),
        screenshot=saved.get("screenshot"))
    if (not isinstance(saved.get("completedStatus"), dict) or
            saved["completedStatus"].get("source") != "persisted_completed_scan_run" or
            not isinstance(saved.get("postReturnLiveStatus"), dict) or
            saved["postReturnLiveStatus"].get("isReading") is not False):
        raise ValueError("Remote Railway lacks distinct persisted run and idle post-return live status")
    fast = audit_fast_railway(destination, observed, restoration,
                              screenshot_name="map016-remote-railway-2211.png",
                              returned_to_server=2212)
    if fast["runId"] != returned["scanRunId"] or fast["railwayCount"] != returned["nativeRailwayRecords"]:
        raise ValueError("Remote Railway native, published and UI owner/count disagree")
    return dict(**fast, originalServerId=2212, restoredServerId=2212,
                switchObservedMilliseconds=returned["switchObservedMilliseconds"],
                sweepElapsedSeconds=returned["sweepElapsedSeconds"],
                returnObservedMilliseconds=returned["returnObservedMilliseconds"],
                noCoordinateJump=True, noClear=True)


def audit_auto_all_nine(destination: Path, proof: dict, restoration: dict,
                        mode: str = "auto-all-nine") -> dict:
    """Supplement existing exact-owned native/SQLite/UI gates with Auto ownership."""
    auto, run, browse = proof.get("auto", {}), proof.get("completedStatus", {}), proof.get("browse", {})
    cycle = auto.get("lastCycle", {}) if isinstance(auto, dict) else {}
    if mode not in ("auto-all-nine", "auto-all-ten"):
        raise ValueError("Auto combined proof requested an unsupported mode")
    selected = MODES[mode]["selected"]
    expected_proof = ("actual_packaged_ui_auto_all_ten_run_now_fullworld_scan_and_results"
                      if mode == "auto-all-ten" else
                      "actual_packaged_ui_auto_all_nine_run_now_fullworld_scan_and_results")
    if (proof.get("proof") != expected_proof or
            proof.get("selection") != mode or proof.get("selectedTypes") != selected or
            proof.get("genuineGame") is not True or proof.get("controlledProvider") is not False or
            not isinstance(cycle, dict) or
            auto.get("originalServerId") != 2212 or auto.get("targetServerId") != 2211 or
            auto.get("restoredServerId") != 2212 or type(auto.get("runNowClicks")) is not int or
            auto.get("runNowClicks") != 1 or auto.get("noIntervalRunBeforeExplicit") is not True or
            auto.get("sawRunning") is not True or auto.get("sawRemote") is not True or
            auto.get("serverIds") != [2211] or
            not exact_unordered_selected_types(auto.get("selectedTypes"), selected) or
            auto.get("returnToOriginalServer") is not True or auto.get("scanMode") != "fast" or
            auto.get("cleanupEnabled") is not False or auto.get("cleanupRunning") is not False or
            cycle.get("outcome") != "completed" or cycle.get("lastError") is not None or
            cycle.get("originalServerId") != 2212 or cycle.get("targetServerIds") != [2211] or
            cycle.get("completedServerIds") != [2211] or
            run.get("phase") != "completed" or run.get("serverId") != 2211 or
            not exact_unordered_selected_types(run.get("selectedTypes"), selected) or
            run.get("scanMode") != "fast" or
            run.get("isReading") is not False or run.get("totalBlocks") != 2500 or
            run.get("readBlocks") != 2500 or run.get("failedBlocks") != 0 or
            run.get("unreadBlocks") != 0 or browse.get("serverId") != 2211 or
            browse.get("scanRunId") != run.get("scanRunId")):
        raise ValueError("MAP016 Auto combined scan lacks a single explicit complete Run Now and verified original-server return")
    if (type(cycle.get("startedAt")) is not int or type(cycle.get("completedAt")) is not int or
            cycle["completedAt"] <= cycle["startedAt"]):
        raise ValueError("MAP016 Auto cycle timestamps invalid")
    try:
        triggered = datetime.fromisoformat(auto["triggeredAt"].replace("Z", "+00:00"))
    except (KeyError, TypeError, AttributeError, ValueError) as error:
        raise ValueError("MAP016 Auto explicit trigger timestamp missing") from error
    if triggered.tzinfo is None or cycle["startedAt"] < triggered.timestamp() * 1000 - 2000:
        raise ValueError("MAP016 Auto cycle began before the Run Now trigger")
    if (proof.get("profileId") != restoration.get("profileId") or
            proof.get("gamePid") != restoration.get("gamePid") or
            not isinstance(run.get("scanRunId"), str) or not run["scanRunId"]):
        raise ValueError("MAP016 Auto profile/game/run is not the exact owned session")

    stage = json.loads((destination / "live-resource/map-engine-stage-metrics.json").read_text(encoding="utf-8"))
    if (stage != proof.get("timings") or stage.get("runId") != run["scanRunId"] or
            stage.get("serverId") != 2211 or stage.get("scanMode") != "fast"):
        raise ValueError("MAP016 Auto timings are not from the same completed scan")
    stages = ("sourceCaptureMilliseconds", "checkpointMilliseconds",
              "publicationMilliseconds", "wholeEngineMilliseconds")
    if any(type(stage.get(key)) not in (int, float) or not 0 < stage[key] < 900_000
           for key in stages):
        raise ValueError("MAP016 Auto timings are incomplete")
    if abs(stage[stages[-1]] - sum(stage[key] for key in stages[:-1])) > 2000:
        raise ValueError("MAP016 Auto stage timings disagree with the whole engine")

    results = browse.get("results")
    if not isinstance(results, list) or [row.get("kind") for row in results] != selected:
        raise ValueError("MAP016 Auto did not browse every requested current native Map result tab")
    ids = set()
    for row in results:
        screenshot = Path(row.get("screenshotPath", "")).resolve()
        request_id = row.get("nativeSearchRequestId")
        if (row.get("serverId") != 2211 or type(row.get("total")) is not int or
                type(row.get("firstPageRows")) is not int or
                not 0 <= row["firstPageRows"] <= row["total"] or
                not isinstance(request_id, str) or not request_id or request_id in ids or
                not within(screenshot, destination.resolve()) or
                not screenshot.is_file() or screenshot.stat().st_size < 1000):
            raise ValueError("MAP016 Auto native UI search or screenshot is missing or foreign")
        with screenshot.open("rb") as stream:
            if stream.read(8) != b"\x89PNG\r\n\x1a\n":
                raise ValueError("MAP016 Auto screenshot is not PNG evidence")
        ids.add(request_id)
    return dict(originalServerId=2212, targetServerId=2211, restoredServerId=2212,
                runNowClicks=1, scanRunId=run["scanRunId"], timings=stage, nativeUiKinds=selected)


def audit_saved_clear_reopen(destination: Path, proof: dict, restoration: dict) -> dict:
    """Exact owned Resource Auto and persisted 2213 Clear/reopen, no fake rows."""
    result = json.loads((destination / "map016-saved-clear-reopen.json").read_text(encoding="utf-8"))
    stages = result.get("stages")
    required = ["preserved_home", "two_server_auto_published",
                "clear_remote_preserve_home", "reopened_saved_dropdown", "cleanup"]
    if (proof.get("state") != "proven" or
            proof.get("proof") != "real_map016_saved_multi_server_clear_reopen" or
            result.get("proof") != "real_map016_saved_multi_server_clear_reopen" or
            result.get("schemaVersion") != 1 or result.get("genuineGame") is not True or
            result.get("controlledProvider") is not False or
            result.get("homeServer") != 2212 or result.get("remoteServer") != 2213 or
            result.get("profileId") != restoration.get("profileId") or
            result.get("sessionId") != restoration.get("sessionId") or
            result.get("gamePid") != restoration.get("gamePid") or
            proof.get("profileId") != restoration.get("profileId") or
            proof.get("gamePid") != restoration.get("gamePid") or
            not isinstance(stages, list) or
            [entry.get("stage") for entry in stages if isinstance(entry, dict)] != required or
            len(stages) != len(required)):
        raise ValueError("MAP016 Saved Clear lacks exactly one genuine owned scan/clear/reopen sequence")
    home, published, cleared, reopened, cleanup = stages
    # R18 intentionally clears every old map row at normal app startup. The
    # accepted home data must therefore come from a NEW same-owned, full-world
    # native Resource scan, not merely source.json's pre-start SQLite clone.
    home_run_id = home.get("homeScanRunId")
    home_state = home.get("homeScan")
    if (home.get("acquisition") !=
            "same_owned_session_genuine_manual_resource_after_r18_startup_clear" or
            home.get("initialHomeRows") != 0 or home.get("initialSavedServers") != [] or
            home.get("homeDurableStatus") != "completed" or
            not isinstance(home_run_id, str) or not home_run_id or
            not isinstance(home_state, dict) or
            home_state.get("scanRunId") != home_run_id or
            home_state.get("serverId") != 2212 or
            home_state.get("serverIdSource") != "live" or
            home_state.get("selectedTypes") != ["resource"] or
            home_state.get("scanMode") != "fast" or
            home_state.get("phase") != "completed" or
            home_state.get("isReading") is not False or
            home_state.get("totalBlocks") != 2500 or
            home_state.get("completedBlocks") != 2500 or
            home_state.get("readBlocks") != 2500 or
            home_state.get("failedBlocks") != 0 or
            home_state.get("nativeDroppedRecords") != 0 or
            home_state.get("nativePendingRecords") != 0 or
            home.get("nativeAois") != 10000 or home.get("nativeReplies") != 63 or
            home.get("homeNativeReceipt") != "map016-home-native-resource-receipt.json" or
            home.get("homeNativeMetrics") != "map016-home-native-resource-metrics.json"):
        raise ValueError("MAP016 home rows lack the new exact-owned genuine R18-session full-world Resource scan")
    home_native = json.loads((destination / home["homeNativeReceipt"]).read_text(encoding="utf-8"))
    home_metrics = json.loads((destination / home["homeNativeMetrics"]).read_text(encoding="utf-8"))
    helper = json.loads((destination / "overview-evidence" / restoration["sessionId"] / "helper-start.json")
                        .read_text(encoding="utf-8"))
    if (home_native.get("schemaVersion") != 1 or
            home_native.get("probeVersion") != "lwbridge-live-resource-probe-2" or
            home_native.get("state") != "proven" or
            home_native.get("scanRunId") != home_run_id or
            home_native.get("serverId") != 2212 or home_native.get("worldId") != 0 or
            home_native.get("profileId") != restoration["profileId"] or
            home_native.get("launchSessionId") != restoration["sessionId"] or
            home_native.get("gamePid") != restoration["gamePid"] or
            hashlib.sha256(str(home_native.get("challenge", "")).encode()).hexdigest() !=
                helper.get("challengeSha256") or
            not str(home_native.get("requestId", "")).startswith("direct") or
            home_native.get("cameraStable") is not True or
            home_native.get("curViewRestored") is not True or
            home_native.get("completedAois") != 10000 or
            home_native.get("repliesCaptured") != 63 or
            type(home_native.get("requestsSent")) is not int or
            not 63 <= home_native["requestsSent"] <= 65):
        raise ValueError("MAP016 new home native Resource receipt lacks exact ownership or complete direct work")
    home_batches = home_native.get("batches")
    if not isinstance(home_batches, list) or len(home_batches) != 63:
        raise ValueError("MAP016 new home did not receive 63 native AOI replies")
    home_covered = set()
    for batch in home_batches:
        indices = batch.get("indices") if isinstance(batch, dict) else None
        if (not isinstance(indices, list) or not 1 <= len(indices) <= 160 or
                any(type(index) is not int or index < 0 or index >= 10000
                    for index in indices) or
                len(indices) != len(set(indices)) or not home_covered.isdisjoint(indices)):
            raise ValueError("MAP016 new home native AOI reply has missing/duplicate cells")
        home_covered.update(indices)
    if len(home_covered) != 10000:
        raise ValueError("MAP016 new home lacks exact 10000 native AOIs")
    home_identity = home_metrics.get("identity") or {}
    if (home_metrics.get("source") != "CurrentClientMapBlockSource.ProbeCameraFreeMapAsync" or
            home_metrics.get("phase") != "prepared" or
            home_metrics.get("acquisitionPolicy") != "direct" or
            home_metrics.get("validatedProbeResponses") != 63 or
            home_metrics.get("requiredAois") != 10000 or
            home_metrics.get("uniqueCoveredAois") != 10000 or
            home_identity.get("RunId") != home_run_id or
            home_identity.get("ServerId") != 2212 or
            home_identity.get("WorldId") != 0 or
            home_identity.get("SelectedTypesKey") != "resource" or
            home_identity.get("ScanMode") != "fast"):
        raise ValueError("MAP016 new home independent native metrics disagree with direct Resource run")
    source = json.loads((destination / "source.json").read_text(encoding="utf-8"))
    if (source.get("genuine") is not True or source.get("fixture") is not False or
            source.get("mode") != "saved-clear-reopen" or
            not isinstance(source.get("sha256"), str) or len(source["sha256"]) != 64):
        raise ValueError("MAP016 Saved Clear did not clone genuine preserved home data")
    original_count = home.get("count")
    cycle = published.get("scan")
    if (home.get("serverId") != 2212 or type(original_count) is not int or original_count <= 0 or
            home.get("priorRemote") != 0 or
            2212 not in home.get("beforeServers", []) or 2213 in home.get("beforeServers", []) or
            not isinstance(cycle, dict) or cycle.get("outcome") != "completed" or
            cycle.get("lastError") is not None or cycle.get("originalServerId") != 2212 or
            cycle.get("targetServerIds") != [2213] or
            cycle.get("completedServerIds") != [2213] or
            published.get("observedRemote") is not True or
            published.get("liveServerId") != 2212 or
            published.get("homeRows") != original_count or
            type(published.get("remoteRows")) is not int or published["remoteRows"] <= 0 or
            not {2212, 2213}.issubset(set(published.get("savedServers", []))) or
            cleared.get("clearedServerId") != 2213 or cleared.get("remoteRows") != 0 or
            cleared.get("homeRows") != original_count or
            cleared.get("liveServerId") != 2212 or
            2212 not in cleared.get("savedServers", []) or 2213 in cleared.get("savedServers", []) or
            reopened.get("absentServerId") != 2213 or
            reopened.get("retainedServerId") != 2212 or
            reopened.get("retainedRows") != original_count or
            reopened.get("liveServerId") != 2212 or
            reopened.get("reloadType") != "same_exact_owned_app_webview" or
            2212 not in reopened.get("savedServers", []) or
            2213 in reopened.get("savedServers", []) or
            cleanup.get("restored") is not True or cleanup.get("autoEnabled") is not False or
            cleanup.get("autoRunning") is not False or cleanup.get("liveServerId") != 2212):
        raise ValueError("MAP016 Saved Clear did not preserve 2212/restore home/remove 2213 after real WebView reload")
    try:
        requested = datetime.fromisoformat(published["requestedAt"].replace("Z", "+00:00"))
    except (KeyError, TypeError, AttributeError, ValueError) as error:
        raise ValueError("MAP016 Saved Clear explicit Auto trigger time missing") from error
    if (requested.tzinfo is None or type(cycle.get("startedAt")) is not int or
            type(cycle.get("completedAt")) is not int or
            cycle["startedAt"] < requested.timestamp() * 1000 - 2000 or
            cycle["completedAt"] <= cycle["startedAt"]):
        raise ValueError("MAP016 Saved Clear scan predates its explicit Auto request")

    for name in ("map016-saved-before-clear.png", "map016-saved-after-clear.png",
                 "map016-saved-reopened-manual.png", "map016-saved-reopened-auto.png",
                 "map016-saved-after-reopen.png"):
        screenshot = destination / name
        if not screenshot.is_file() or screenshot.stat().st_size < 1000:
            raise ValueError("MAP016 Saved Clear missing actual mounted WebView screenshot: " + name)
        with screenshot.open("rb") as stream:
            if stream.read(8) != b"\x89PNG\r\n\x1a\n":
                raise ValueError("MAP016 Saved Clear screenshot is invalid: " + name)

    # The Clear action can retire the 2213 durable run, so cross-check against
    # its preserved *native* receipt and stage metrics, never fabricated rows.
    direct = json.loads((destination / "live-resource/camera-free-scan-result.json").read_text(encoding="utf-8"))
    metrics = json.loads((destination / "live-resource/map-fullworld-probe-metrics.json").read_text(encoding="utf-8"))
    stage = json.loads((destination / "live-resource/map-engine-stage-metrics.json").read_text(encoding="utf-8"))
    run_id = direct.get("scanRunId")
    helper = json.loads((destination / "overview-evidence" / restoration["sessionId"] / "helper-start.json")
                        .read_text(encoding="utf-8"))
    if (not isinstance(run_id, str) or not run_id or run_id == home_run_id or
            direct.get("state") != "proven" or direct.get("serverId") != 2213 or
            direct.get("profileId") != restoration["profileId"] or
            direct.get("launchSessionId") != restoration["sessionId"] or
            direct.get("gamePid") != restoration["gamePid"] or
            direct.get("worldId") != 0 or
            hashlib.sha256(str(direct.get("challenge", "")).encode()).hexdigest() !=
                helper.get("challengeSha256") or
            direct.get("cameraStable") is not True or
            direct.get("curViewRestored") is not True or
            direct.get("completedAois") != 10000 or direct.get("repliesCaptured") != 63 or
            type(direct.get("requestsSent")) is not int or
            not 63 <= direct["requestsSent"] <= 65 or
            metrics.get("source") != "CurrentClientMapBlockSource.ProbeCameraFreeMapAsync" or
            metrics.get("phase") != "prepared" or
            metrics.get("identity", {}).get("RunId") != run_id or
            metrics["identity"].get("ServerId") != 2213 or
            metrics["identity"].get("SelectedTypesKey") != "resource" or
            metrics.get("validatedProbeResponses") != 63 or
            metrics.get("uniqueCoveredAois") != 10000 or
            stage.get("runId") != run_id or stage.get("serverId") != 2213 or
            stage.get("scanMode") != "fast" or
            type(stage.get("wholeEngineMilliseconds")) not in (int, float) or
            not 0 < stage["wholeEngineMilliseconds"] < 900_000):
        raise ValueError("MAP016 Saved Clear native Resource scan identity, coverage or timing was not verified")
    areas = direct.get("batches")
    if not isinstance(areas, list) or len(areas) != 63:
        raise ValueError("MAP016 Saved Clear lacks exactly 63 owned native Resource replies")
    seen = set()
    for batch in areas:
        indices = batch.get("indices") if isinstance(batch, dict) else None
        if (not isinstance(indices, list) or not 1 <= len(indices) <= 160 or
                any(type(index) is not int or index < 0 or index >= 10000 for index in indices) or
                len(indices) != len(set(indices)) or not seen.isdisjoint(indices)):
            raise ValueError("MAP016 Saved Clear native AOI footprint is duplicated or missing")
        seen.update(indices)
    if len(seen) != 10000:
        raise ValueError("MAP016 Saved Clear did not prove all 10000 unique native AOIs")
    db_path = destination / "profiles" / restoration["profileId"] / "map-data/map-data.db"
    with closing(sqlite3.connect(db_path.resolve().as_uri() + "?mode=ro", uri=True)) as database:
        home_rows, remote_rows = (
            database.execute("SELECT COUNT(*) FROM map_records WHERE kind='resource' AND server_id=?",
                             (server,)).fetchone()[0] for server in (2212, 2213))
    if home_rows != original_count or remote_rows != 0:
        raise ValueError("MAP016 Saved Clear SQLite contains phantom remote or missing genuine home Resource data")
    return dict(originalServerId=2212, clearedServerId=2213, restoredServerId=2212,
                preservedResourceRows=home_rows, remoteResourceRows=0,
                ownedScanRunId=run_id, nativeAois=10000,
                sourceTimingMilliseconds=stage["wholeEngineMilliseconds"],
                reopenedManualAndAuto=True)


def audit_no_official_updater(destination: Path, restoration: dict) -> dict:
    """Require an exact, *current-version* pre-proven settlement, never an updater.

    The game upgraded from v26 to v27 after the first archived MAP016 proof.
    A fixed 2026-10-16 v26 marker is not the source that the owned runner
    actually selected for the current v27 installation. Reuse its exact,
    fail-closed trusted-settlement selector, and still compare raw bytes with
    the pre-existing, independently proven original; never generate a marker.
    """
    evidence = destination / "overview-evidence"
    source_spec = importlib.util.spec_from_file_location(
        "map014_trusted_official_settlement_for_audit", RUNNER)
    if source_spec is None or source_spec.loader is None:
        raise ValueError("MAP016 packaged trusted settlement selector is unavailable")
    owned_runner = importlib.util.module_from_spec(source_spec)
    source_spec.loader.exec_module(owned_runner)
    originals = restoration.get("capturedOriginalHashes", {})
    trusted_proof = owned_runner.trusted_settled_proof(originals)
    if trusted_proof is None:
        raise ValueError("MAP016 no independently admitted official settlement for this package")
    source = trusted_proof / "overview-evidence/official-settled-client.json"
    prior_exit_path = trusted_proof / "owned-exit.json"
    installed = evidence / "official-settled-client.json"
    admission = json.loads((evidence / "map016-official-settle-admission.json").read_text(encoding="utf-8"))
    if (not source.is_file() or not prior_exit_path.is_file() or not installed.is_file() or
            source.read_bytes() != installed.read_bytes()):
        raise ValueError("MAP016 isolated proof changed or invented its officially settled marker")
    prior_exit = json.loads(prior_exit_path.read_text(encoding="utf-8"))
    prior_restoration = prior_exit.get("restorationAudit", {})
    if (prior_exit.get("exitCode") != 0 or prior_exit.get("proof") is not True or
            prior_exit.get("error") is not None or
            prior_exit.get("recoveryJournalPresent") is not False or
            prior_restoration.get("state") != "proven" or
            prior_restoration.get("diagnosticResultSucceeded") is not True or
            prior_restoration.get("capturedOriginalHashes") != originals or
            prior_restoration.get("installedHashes") != originals):
        raise ValueError("MAP016 official settlement source lacks independently proven original restoration")
    marker = json.loads(installed.read_text(encoding="utf-8"))
    if (marker.get("schemaVersion") != 1 or
            marker.get("gameRoot") != admission.get("gameRoot") or
            marker.get("packageSha256") != admission.get("packageSha256") or
            marker.get("packageSha256") != restoration.get("capturedOriginalHashes", {}).get("LWScripts.data") or
            admission.get("schemaVersion") != 1 or
            admission.get("state") != "settled_reuse_allowed" or
            admission.get("forceOfficialSettle") is not False or
            admission.get("officialLauncherStartedByThisAdmission") is not False or
            admission.get("updaterActionAuthorized") is not False):
        raise ValueError("MAP016 source-pinned official client admission did not prohibit updater actions")
    return dict(originalPackageSha256=marker["packageSha256"],
                reusedExistingSettlement=True, updaterAuthorized=False)


def audit_auto_interval(destination: Path, proof: dict, restoration: dict) -> dict:
    """Require 20 real idle minutes before the first owned Auto Railway cycle."""
    marker = "real_20_minute_enabled_auto_railway_scheduler_2212_to_2211_return"
    progress_path = destination / "map016-auto-interval-progress.json"
    progress = json.loads(progress_path.read_text(encoding="utf-8"))
    if progress != proof or proof.get("proof") != marker or proof.get("state") != "proven" or \
            proof.get("genuineGame") is not True or proof.get("controlledProvider") is not False or \
            proof.get("systemClockInjected") is not False or proof.get("runNowCalls") != 0 or \
            proof.get("manualStartCalls") != 0 or proof.get("intervalMinutes") != 20 or \
            proof.get("originalServer") != 2212 or proof.get("targetServer") != 2211 or \
            proof.get("profileId") != restoration.get("profileId") or \
            proof.get("sessionId") != restoration.get("sessionId") or \
            proof.get("gamePid") != restoration.get("gamePid") or \
            proof.get("observedScheduledRun") is not True or \
            proof.get("observedTargetServer") is not True or \
            proof.get("observedHomeReturn") is not True or \
            proof.get("failure") is not None or proof.get("cleanupFailure") is not None:
        raise ValueError("MAP016 genuine 20-minute Auto scheduler ownership or result is unproven")
    elapsed = proof.get("firstFireElapsedMilliseconds")
    idle = proof.get("lastIdleElapsedMilliseconds")
    deadline = proof.get("deadlineUtcMilliseconds")
    samples = proof.get("samples")
    events = proof.get("events")
    if (type(elapsed) is not int or elapsed < 1_200_000 or
            type(idle) is not int or idle < 1_196_500 or idle >= elapsed or
            type(deadline) is not int or deadline <= 0 or
            not isinstance(samples, list) or len(samples) < 1150 or
            not isinstance(events, list)):
        raise ValueError("Auto scheduler has no complete observed 20-minute idle interval")
    def parse_iso(name):
        value = proof.get(name)
        try:
            dt = datetime.fromisoformat(value.replace("Z", "+00:00"))
        except (TypeError, AttributeError, ValueError) as exc:
            raise ValueError("Auto interval missing observed timestamp: " + name) from exc
        if dt.tzinfo is None: raise ValueError("Auto interval timestamp lacks timezone")
        return dt
    enabled, fired = parse_iso("enabledAt"), parse_iso("firstFireAt")
    if (fired.timestamp()*1000 < deadline or
            fired.timestamp()*1000 - enabled.timestamp()*1000 < 1_196_000 or
            abs((fired-enabled).total_seconds()*1000 - elapsed) > 10_000):
        raise ValueError("Auto scheduler fired too early or with incompatible monotonic time")
    previous = -1
    consecutive_unknown = 0
    for i,item in enumerate(samples):
        if not isinstance(item, dict): raise ValueError("Auto status sample malformed")
        now = item.get("monotonicMilliseconds")
        if (item.get("index") != i or type(now) is not int or now <= previous or
                (previous >= 0 and now - previous > 3500) or
                item.get("autoEnabled") is not True or item.get("autoUiMounted") is not True or
                item.get("deadlineUtcMilliseconds") != deadline):
            raise ValueError("Auto interval has a gap, wrong owner, missing UI or deadline")
        if now < 1_196_500 and (item.get("autoRunning") is True or
                item.get("autoCycleBusy") is True or
                item.get("manualReading") is True or
                item.get("manualScanActive") is True or
                item.get("liveServerId") not in (2212, None)):
            raise ValueError("Auto scan or navigation began before 20-minute due time")
        if now < 1_196_500:
            if item.get("liveServerId") is None:
                consecutive_unknown += 1
                if consecutive_unknown > 10 or item.get("secondaryServerId") != 2212:
                    raise ValueError("Idle Auto interval lost bounded live-home attribution")
            else:
                consecutive_unknown = 0
        previous = now
    stages = [item.get("stage") for item in events if isinstance(item,dict)]
    if (not {"scheduler_armed", "first_real_scheduled_admission", "scheduler_cycle_settled",
             "native_sqlite_railway_verified", "cleanup_verified"}.issubset(stages) or
            "cleanup_failed" in stages or "source_owned_server_return_recovery" in stages):
        raise ValueError("Auto interval lacks native completed run or natural owned return")
    run_id = proof.get("runId")
    if not isinstance(run_id,str) or not run_id:
        raise ValueError("Auto interval missing its native owner run ID")
    with closing(sqlite3.connect((destination / "profiles" / restoration["profileId"] /
                                  "map-data/map-data.db").resolve().as_uri()+"?mode=ro",uri=True)) as db:
        runs=db.execute("SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at FROM scan_runs").fetchall()
        count=db.execute("SELECT count(*) FROM map_records WHERE server_id=2211 AND kind='railway'").fetchone()[0]
    if (len(runs) != 1 or runs[0][:2] != (run_id,2211) or
            json.loads(runs[0][2]) != ["railway"] or
            runs[0][3:7] != ("completed",2500,2500,0) or
            runs[0][7] < deadline or count != proof.get("savedRailwayRecords") or
            type(proof.get("nativeRailwayRecords")) is not int or
            (proof["nativeRailwayRecords"] == 0) != (count == 0)):
        raise ValueError("Real Auto interval lacks exact durable 2211 Railway publication")
    native=json.loads((destination/"live-resource/fast-truck-scan-result.json").read_text(encoding="utf-8"))
    helper=json.loads((destination/"overview-evidence"/restoration["sessionId"]/
                       "helper-start.json").read_text(encoding="utf-8"))
    metrics=json.loads((destination/"live-resource/map-engine-stage-metrics.json").read_text(encoding="utf-8"))
    if (native.get("state") != "proven" or native.get("scanRunId") != run_id or
            native.get("serverId") != 2211 or native.get("profileId") != restoration["profileId"] or
            native.get("launchSessionId") != restoration["sessionId"] or
            native.get("gamePid") != restoration["gamePid"] or
            hashlib.sha256(str(native.get("challenge","")).encode()).hexdigest() != helper.get("challengeSha256") or
            native.get("rectanglesCompleted") != 625 or native.get("initialRequestsSent") != 625 or
            native.get("unprovenRectangles") != 0 or native.get("cameraStable") is not True or
            native.get("curViewRestored") is not True or native.get("railwayOnly") is not True or
            native.get("railwayIncluded") is not True or
            native.get("railwayRecordCount") != proof["nativeRailwayRecords"] or
            metrics.get("runId") != run_id or metrics.get("serverId") != 2211 or
            metrics.get("scanMode") != "fast" or
            type(metrics.get("wholeEngineMilliseconds")) not in (int,float) or
            not 0 < metrics["wholeEngineMilliseconds"] < 300_000):
        raise ValueError("Scheduled Railway native sweep, camera state, or timings unproven")
    for name in ("map016-auto-interval-armed.png","map016-auto-interval-returned.png"):
        screenshot=destination/name
        if not screenshot.is_file() or screenshot.stat().st_size < 1000:
            raise ValueError("Scheduled Auto interval missing genuine Map screenshot: "+name)
        with screenshot.open("rb") as stream:
            if stream.read(8) != b"\x89PNG\r\n\x1a\n":
                raise ValueError("Scheduled Auto interval screenshot is not PNG: "+name)
    return dict(source="genuine_real_time_auto_interval",idleMonotonicMilliseconds=idle,
                firstFireMonotonicMilliseconds=elapsed,serverId=2211,restoredServerId=2212,
                runId=run_id,rectanglesVerified=625,railwayCount=count,
                wholeEngineMilliseconds=metrics["wholeEngineMilliseconds"],
                noRunNow=True,noManualStart=True)


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
            if not (mode in ("city", "other", "railway", "all-nontruck", "truck", "all-nine", "all-ten", "auto-all-nine", "auto-all-ten") and proof.get("selectedTypes") == config["selected"]
                    and proof.get("completedStatus", {}).get("phase") == "completed"):
                raise ValueError("Packaged proof does not certify the requested completed selection")
    if mode in ("truck", "all-nine", "all-ten", "auto-all-nine", "auto-all-ten"):
        browse = proof.get("browse", {})
        native = browse.get("native") if mode == "truck" else browse.get("fastTruck")
        require_qualified_fast_truck_proof(native)
        if mode == "truck":
            filter_result = browse.get("filter", {})
        else:
            filter_result = browse.get("truckFilter")
        if (not isinstance(filter_result, dict) or filter_result.get("filteredTotal", 0) < 1 or
                not Path(filter_result.get("screenshot", "")).is_file()):
            raise ValueError("Packaged fast Truck positive combined filter is missing")
        receipt = read("live-resource/fast-truck-scan-result.json")
        scan_run = (proof.get("completedStatus") or {}).get("scanRunId") or browse.get("scanRunId")
        if (receipt.get("state") != "proven" or receipt.get("scanRunId") != scan_run or
                receipt.get("profileId") != restoration.get("profileId") or
                receipt.get("launchSessionId") != restoration.get("sessionId") or
                receipt.get("gamePid") != restoration.get("gamePid") or
                receipt.get("rectanglesCompleted") != 625 or
                receipt.get("initialRequestsSent") != 625 or
                receipt.get("unprovenRectangles") != 0 or
                receipt.get("requestsPerSecond") != 20 or
                receipt.get("responseFenceSize") != 25 or
                receipt.get("responseAttribution") != "bounded_sequential_fences" or
                type(receipt.get("repairPasses")) is not int or
                not 0 <= receipt["repairPasses"] <= 2 or
                receipt.get("repairRequestsSent") != receipt.get("requestsSent", -1) - 625):
            raise ValueError("Fast Truck source has unverified rectangles or lacks bounded native repair evidence")
        db_path = destination / "profiles" / restoration["profileId"] / "map-data" / "map-data.db"
        audit_published_fast_truck_rows(db_path, receipt["serverId"])
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
    if mode == "remote-clear":
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
    direct = audit_city_camera_free(destination, proof, restoration, mode) if mode in ("city", "all-nontruck", "all-nine", "all-ten", "auto-all-nine", "auto-all-ten") else None
    truck = audit_fresh_truck_filter(destination, proof, restoration) if mode == "mixed-eight" else None
    railway = audit_fast_railway(destination, proof, restoration) if mode == "railway" else None
    remote_railway = audit_remote_railway(destination, proof, restoration) if mode == "remote-railway" else None
    auto_all_kinds = audit_auto_all_nine(destination, proof, restoration, mode) if mode in ("auto-all-nine", "auto-all-ten") else None
    saved_clear = audit_saved_clear_reopen(destination, proof, restoration) if mode == "saved-clear-reopen" else None
    interval = audit_auto_interval(destination, proof, restoration) if mode == "auto-interval-20m" else None
    no_updater = audit_no_official_updater(destination, restoration) if mode in ("auto-all-nine", "auto-all-ten", "saved-clear-reopen", "auto-interval-20m") else None
    return dict(mode=mode, state="proven", proof=str(destination / "native-ui-map.json"),
                ownedExit=str(destination / "owned-exit.json"), hostPid=restoration["hostPid"],
                gamePid=restoration["gamePid"], sessionId=restoration["sessionId"],
                **({"cameraFree": direct} if direct else {}),
                **({"freshTruckFilter": truck} if truck else {}),
                **({"fastRailway": railway} if railway else {}),
                **({"remoteRailway": remote_railway} if remote_railway else {}),
                **({("autoAllTen" if mode == "auto-all-ten" else "autoAllNine"): auto_all_kinds} if auto_all_kinds else {}),
                **({"savedClearReopen": saved_clear} if saved_clear else {}),
                **({"autoInterval20m": interval} if interval else {}),
                **({"officialSettlement": no_updater} if no_updater else {}))


def owned_runner_environment(executable: str, include_new_kinds: bool) -> dict[str, str]:
    """Prevent packaged Python helpers from writing into source-verified extraction.

    Runtime __pycache__ files were observed after a genuine first packaged
    attempt. A subsequent strict ZIP/extraction identity check correctly fails
    on those extra files. Keep the identity gate strict: instead prevent the
    *owned child* from creating bytecode files in the audited application tree.
    This does not alter the user's global Python environment or remove files.
    """
    environment = os.environ.copy()
    environment["LWBRIDGE_MAP014_UI_EXE"] = executable
    environment["LWBRIDGE_MAP014_NEW_KINDS_UI"] = "1" if include_new_kinds else "0"
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    return environment


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--zip", required=True, help="Final source-pinned Windows release ZIP under artifacts")
    parser.add_argument("--extracted", required=True, help="Extracted LW-Control-UI directory under artifacts")
    parser.add_argument("--commit", required=True, help="Exact 40-character source commit used in the release")
    parser.add_argument("--mode", required=True, choices=sorted(MODES))
    parser.add_argument("--saved-db", help="Existing genuine completed scan DB for browse-new-kinds")
    parser.add_argument("--allow-remote", action="store_true",
                        help="Explicitly allow owned 2212->2213->2212 Resource or 2212->2211->2212 Railway / combined Auto proof")
    parser.add_argument("--execute", action="store_true", help="Start the exact-owned app/game proof")
    args = parser.parse_args(argv)
    package = verify_package(require_artifact(args.zip, file=True),
                             require_artifact(args.extracted, file=False), args.commit)
    mode = MODES[args.mode]
    if mode.get("remote") and not args.allow_remote:
        parser.error("remote proof requires --allow-remote")
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
    environment = owned_runner_environment(package["executable"], bool(mode.get("new_kinds")))
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
