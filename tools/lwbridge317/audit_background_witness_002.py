"""Read-only auditor for LWB317 background-witness-002 *actual* attempt files.

Does not open the game, send input, repair files, or infer authenticated
connections or Resource acquisition from readiness/hello_sent alone.
"""
from __future__ import annotations

import argparse
import hashlib
import sqlite3
import json
from pathlib import Path
import tempfile
from datetime import datetime, timezone

ORIGINALS = {
    "LWScripts.data": "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22",
    "LWScripts.txt": "d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a",
    "version.txt": "785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09",
}

def hash_file(path: Path) -> str:
    sha = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            sha.update(block)
    return sha.hexdigest()

def inspect(path: Path) -> dict:
    report = json.loads(path.read_text(encoding="utf-8-sig"))
    root = Path(report["isolatedRoot"]).resolve()
    temp = Path(tempfile.gettempdir()).resolve()
    if root.parent != temp or not root.name.startswith("LWB317-BACKGROUND-WITNESS-002-"):
        raise ValueError("refusing non-pilot isolated root")
    if report["mode"] != "explicit-real-launch":
        raise ValueError("expected real attempt, not inert preflight")
    preflight = root / "overview-bridge-backups" / ("preflight-originals-" + report["attemptId"])
    backups = {name: hash_file(preflight / name) for name in ORIGINALS}
    expected = all(backups[name] == value for name, value in ORIGINALS.items())

    # Reopen the actual isolated SQLite file, without app provider simulation.
    # A negative witness must not inherit pre-existing City or Resource rows.
    map_db = root / "profiles" / report["profileId"] / "map-data" / "map-data.db"
    with sqlite3.connect("file:" + map_db.as_posix() + "?mode=ro", uri=True) as conn:
        database_counts = {
            "scanRuns": conn.execute("SELECT COUNT(*) FROM scan_runs").fetchone()[0],
            "scanBlocks": conn.execute("SELECT COUNT(*) FROM scan_blocks").fetchone()[0],
            "resourceScanRecords": conn.execute(
                "SELECT COUNT(*) FROM scan_records WHERE kind='resource'").fetchone()[0],
            "resourcePublishedRows": conn.execute(
                "SELECT COUNT(*) FROM map_records WHERE kind='resource'").fetchone()[0],
        }
    installed_after = report.get("installedAfter", {})
    installed = all(installed_after.get(name) == value for name, value in ORIGINALS.items())
    journals = list((root / "overview-bridge-backups").glob("*/manifest.json"))
    manifests = []
    for manifest_file in journals:
        obj = json.loads(manifest_file.read_text(encoding="utf-8-sig"))
        manifests.append({
            "backupName": manifest_file.parent.name,
            "stage": obj.get("stage") or obj.get("state"),
            "recordedOriginalMatches": all(
                obj.get("originalFiles", {}).get(key, {}).get("sha256") == value
                for key, value in {"data": ORIGINALS["LWScripts.data"],
                                   "metadata": ORIGINALS["LWScripts.txt"],
                                   "version": ORIGINALS["version.txt"]}.items()
            ),
        })
    journal_exists = (root / "overview-bridge" / "recovery.json").exists()
    pipe_file = root / "overview-bridge" / "pipe-transport.json"
    pipe = {}
    if pipe_file.exists():
        raw = json.loads(pipe_file.read_text(encoding="utf-8-sig"))
        # Whitelist only. Never commit token, secret or raw command/handshake.
        pipe = {
            "modifiedAtUtc": datetime.fromtimestamp(
                pipe_file.stat().st_mtime, tz=timezone.utc).isoformat(),
            **{name: raw.get(name) for name in (
                "state", "clientConnected", "adapterActive", "adapterLoaded",
                "adapterReadMode", "profileIdPresent", "instanceIdPresent",
                "buildIdPresent", "pipeTokenPresent", "namedPipeClientStreamAvailable",
            )},
        }
    host_events = [x for x in report["events"]
                   if x["kind"] == "authenticated-host-observation"]
    host = host_events[-1]["details"] if host_events else {}
    trace = host.get("hostTrace") or []
    stop_events = [x for x in report["events"] if x["kind"] == "owned-home-stop"]
    resource_events = [x for x in report["events"]
                       if x["kind"] in ("resource-start", "resource-capture-or-terminal")]
    restored = (expected and installed and bool(report.get("originalHashesRestored"))
                and not journal_exists and not report.get("recoveryJournalAfter")
                and bool(report.get("ownedGameStopSucceeded"))
                and bool(stop_events)
                and len(manifests) >= 1
                and all(x["stage"] == "restored" and x["recordedOriginalMatches"]
                        for x in manifests))
    return {
        "source": str(path), "attemptId": report["attemptId"],
        "profileId": report["profileId"], "terminal": report.get("terminal"),
        "restorationVerified": restored, "originalPreflightBackupMatches": expected,
        "originalPostRunHashesMatch": installed, "recoveryJournalPresent": journal_exists,
        "manifestStages": manifests, "ownedHomeStopEvidence": bool(stop_events),
        "sqliteReopened": True, "isolatedSqliteCounts": database_counts,
        "freshNativeResourceStartEvidence": any(x["kind"] == "resource-start"
                                                for x in resource_events),
        "freshNativeResourceCaptureEvidence": any(x["kind"] == "resource-capture-or-terminal"
                                                  for x in resource_events),
        "hostAccepted": host.get("accepted", False),
        "hostAuthenticatedCount": host.get("AuthenticatedSessionCount"),
        "hostConnectedRoutes": host.get("ConnectedRouteCount"),
        "hostRejectedHandshakes": host.get("RejectedHandshakeCount"),
        "hostTraceSampleCount": len(trace),
        "hostTraceFirstAtUtc": trace[0].get("atUtc") if trace else None,
        "hostTraceLastAtUtc": trace[-1].get("atUtc") if trace else None,
        "serverInstances": trace[-1].get("listenerInstances") if trace else None,
        "serverLastConnectDisposition": trace[-1].get("listenerLastConnectDisposition")
                                         if trace else None,
        "serverLastConnectError": trace[-1].get("listenerLastConnectError")
                                  if trace else None,
        "adapterStateObserved": sorted({
            str(x.get("adapterState")) for x in trace if x.get("adapterState") is not None
        }),
        "gameSidePipeDiagnostic": pipe,
    }

def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--output", required=True, type=Path)
    p.add_argument("reports", nargs="+", type=Path)
    args = p.parse_args()
    items = [inspect(path.resolve()) for path in args.reports]
    result = {
        "workItem": "LWB317-FUNCTION-HOME-MAP-BACKGROUND-WITNESS-002",
        "auditAtUtc": datetime.now(timezone.utc).isoformat(),
        "mode": "read-only post-run audit; not a Resource or canonical UI proof",
        "allAttemptRestorationsVerified": all(item["restorationVerified"] for item in items),
        "anyFreshAuthenticatedHost": any(item["hostAccepted"] for item in items),
        "anyFreshResourceStart": any(item["freshNativeResourceStartEvidence"] for item in items),
        "anyFreshResourceCapture": any(item["freshNativeResourceCaptureEvidence"] for item in items),
        "attempts": items,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: value for k, value in result.items() if k != "attempts"}, indent=2))
    if not result["allAttemptRestorationsVerified"]:
        return 2
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
