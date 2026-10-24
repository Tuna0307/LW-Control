"""Read-only MAP016 Saved Clear source/clone provenance and startup audit.

Usage (NO game/app launch, no fixture rewrite):
    python tools/map_019_review/audit_saved_clear_source.py `
        --source-run artifacts/map014-takeover/native-ui-20261016-172917 `
        --failed-run artifacts/map014-takeover/native-ui-20261024-080957

Never reimport historical Map rows *after* normal startup: R18 intentionally
expires every saved map row at the process boundary. A valid source database
cannot by itself make the present Saved Clear proof satisfiable.
"""

from __future__ import annotations

import argparse
from contextlib import closing
import hashlib
import json
from pathlib import Path
import sqlite3

REPO = Path(__file__).resolve().parents[2]
ARTIFACTS = (REPO / "artifacts").resolve()
PROFILE = "map014-native-ui"
HOME = 2212
REMOTE = 2213


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def static_startup_contract(root: Path = REPO) -> dict:
    program = (root / "src/LWBridge.Desktop/Program.cs").read_text(encoding="utf8")
    cleanup = (root / "src/LWBridge.Desktop/StartupMapScanCleanup.cs").read_text(encoding="utf8")
    store = (root / "src/LWBridge.Map-0.3.17/MapStore.cs").read_text(encoding="utf8")
    proof = (root / "src/LWBridge.Desktop/LWBridgeWindow.Map016AcceptanceProof.cs").read_text(encoding="utf8")
    runner = (root / "tools/map_014_lead_native/run_ui.py").read_text(encoding="utf8")
    required = {
        "normal startup cleanup": "StartupMapScanCleanup.Clear(DesktopApplicationPaths.Create(isolatedRootPath).Root)" in program,
        "profile Map317 database enumerated": 'Path.Combine(profile, "map-data", "map-data.db")' in cleanup,
        "legacy and current MapStore cleanup": "store.ClearAllScanData();" in cleanup,
        "full published map data removed": ("DELETE FROM scan_records; DELETE FROM scan_blocks; DELETE FROM scan_runs; DELETE FROM map_records; DELETE FROM treasure_claim_states;" in store),
        "proof demands saved home before owned remote scan": (
            "int original = await ResourceTotal(home);" in proof and
            "if (original <= 0 || priorRemote != 0 || !beforeServers.Contains(home) ||" in proof and
            "await mapAutoScanService.RunNowAsync(token);" in proof),
        "runner clones source database before app start": (
            "readonly.backup(snapshot)" in runner and
            '"saved-clear-reopen"):' in runner and
            "owned = subprocess.Popen([" in runner),
    }
    if not all(required.values()):
        raise ValueError("Saved Clear startup/proof source contract changed: " +
                         ", ".join(name for name, ok in required.items() if not ok))
    return {"normalStartupClearsMapRows": True,
            "savedClearRequiresHomeRowsBeforeOwnedRemoteScan": True,
            "historicalCloneAloneCanSatisfyProof": False}


def db_file(run_dir: Path) -> Path:
    return run_dir / "profiles" / PROFILE / "map-data" / "map-data.db"


def inspect_db(path: Path) -> dict:
    """No mutation and no WAL shadow-reads; fail closed if snapshot is active."""
    path = path.resolve(strict=True)
    if not path.is_file():
        raise ValueError("Saved source is not a regular file")
    wal = Path(str(path) + "-wal")
    if wal.exists() and wal.stat().st_size != 0:
        raise ValueError("Source has nonempty WAL; refuse immutable/stale snapshot")
    journal = Path(str(path) + "-journal")
    if journal.exists() and journal.stat().st_size != 0:
        raise ValueError("Source has a rollback journal; refuse untrusted snapshot")
    original_sha = digest(path)
    with closing(sqlite3.connect(path.as_uri() + "?mode=ro&immutable=1", uri=True)) as db:
        if db.execute("PRAGMA quick_check").fetchone() != ("ok",):
            raise ValueError("SQLite quick_check failed")
        tables = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        if not {"map_records", "scan_runs", "scan_blocks", "scan_records"}.issubset(tables):
            raise ValueError("Missing Map317 published SQLite schema")
        runs = db.execute(
            "SELECT id,server_id,status,total_blocks,completed_blocks,failed_blocks,selected_types "
            "FROM scan_runs ORDER BY created_at DESC").fetchall()
        servers = [row[0] for row in db.execute(
            "SELECT server_id FROM map_records WHERE server_id>0 GROUP BY server_id "
            "ORDER BY MAX(updated_at) DESC, server_id ASC")]
        counts = db.execute(
            "SELECT server_id,kind,COUNT(*) FROM map_records GROUP BY server_id,kind").fetchall()
        table_totals = {name: db.execute(f"SELECT COUNT(*) FROM {name}").fetchone()[0]
                        for name in ("map_records", "scan_runs", "scan_blocks", "scan_records")}
    if digest(path) != original_sha:
        raise ValueError("Saved SQLite file changed while being inspected")
    by_kind = {f"{server}:{kind}": n for server, kind, n in counts}
    return {"sha256": original_sha, "bytes": path.stat().st_size,
            "homeResourceRows": by_kind.get(f"{HOME}:resource", 0),
            "remoteResourceRows": by_kind.get(f"{REMOTE}:resource", 0),
            "remoteAnyRows": sum(n for server, _, n in counts if server == REMOTE),
            "savedServers": servers, "totalRows": table_totals["map_records"],
            "tableTotals": table_totals, "runs": runs}


def inspect_accepted_source(run_dir: Path) -> dict:
    """Require independently successful owned proof, exact run and native DB."""
    proof = json.loads((run_dir / "native-ui-map.json").read_text(encoding="utf8"))
    exit_receipt = json.loads((run_dir / "owned-exit.json").read_text(encoding="utf8"))
    source_receipt = json.loads((run_dir / "source.json").read_text(encoding="utf8"))
    db = inspect_db(db_file(run_dir))
    complete = proof.get("completedStatus", {})
    if not (source_receipt.get("genuine") is True and
            source_receipt.get("fixture") is False and
            proof.get("state") == "proven" and
            proof.get("genuineGame") is True and
            proof.get("controlledProvider") is False and
            proof.get("profileId") == PROFILE and
            exit_receipt.get("restorationAudit", {}).get("profileId") == PROFILE and
            proof.get("serverId") == HOME and
            complete.get("phase") == "completed" and
            complete.get("serverId") == HOME and
            complete.get("totalBlocks") == 2500 and
            complete.get("completedBlocks") == 2500 and
            complete.get("failedBlocks") == 0 and
            complete.get("scanRunId") and
            exit_receipt.get("proof") is True and
            exit_receipt.get("exitCode") == 0 and
            exit_receipt.get("error") is None and
            exit_receipt.get("recoveryJournalPresent") is False and
            exit_receipt.get("restorationAudit", {}).get("state") == "proven" and
            all(exit_receipt.get("originalScriptsRestored", {}).get(key) is True
                for key in ("LWScripts.data", "LWScripts.txt", "version.txt"))):
        raise ValueError("Historical DB lacks accepted genuine packaged home proof and owned restoration")
    if not (len(db["runs"]) == 1 and
            db["runs"][0][:6] == (complete["scanRunId"], HOME, "completed", 2500, 2500, 0) and
            json.loads(db["runs"][0][6]) == proof.get("selectedTypes") and
            db["savedServers"] == [HOME] and
            db["homeResourceRows"] > 0 and
            db["remoteAnyRows"] == 0):
        raise ValueError("Accepted proof's home scan/source is absent or contaminated")
    return {key: value for key, value in db.items() if key != "runs"} | {
        "acceptedProof": True, "sourceNeverEdited": True}


def audit(source_run: Path, failed_run: Path | None = None, *,
          enforce_source_dir: bool = True) -> dict:
    if enforce_source_dir:
        for path in (source_run, failed_run):
            if path is None:
                continue
            full = path.resolve(strict=True)
            if ARTIFACTS not in full.parents:
                raise ValueError("Only existing task artifact directories may be inspected")
    source = inspect_accepted_source(source_run)
    contract = static_startup_contract()
    result = {"scope": "strictly_read_only_no_app_no_game_no_fixture_rewrite",
              "source": source_run.resolve().relative_to(ARTIFACTS).as_posix()
                        if enforce_source_dir else source_run.name,
              "sourceAudit": source, "startupContract": contract}
    if failed_run is not None:
        failed = inspect_db(db_file(failed_run))
        result["failedClone"] = {
            "sha256": failed["sha256"], "bytes": failed["bytes"],
            "homeResourceRows": failed["homeResourceRows"],
            "remoteResourceRows": failed["remoteResourceRows"],
            "savedServers": failed["savedServers"],
            "tableTotals": failed["tableTotals"],
        }
        result["confirmedFailureAfterStartupClear"] = (
            failed["homeResourceRows"] == 0 and failed["totalRows"] == 0
            and failed["tableTotals"]["scan_runs"] == 0)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-run", required=True, type=Path)
    parser.add_argument("--failed-run", type=Path)
    args = parser.parse_args()
    print(json.dumps(audit(args.source_run, args.failed_run), indent=2))


if __name__ == "__main__":
    main()
