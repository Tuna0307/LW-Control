"""Build one source-pinned, unpublished FEATURE-RECOVERY-018 R4 Windows ZIP.

This must run AFTER the final source commit is pushed and a fresh Windows
publish has been compiled from that commit. All game receipts are read-only
inputs; packaging never starts a game, launcher or helper.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile


ROOT = Path(__file__).resolve().parent.parent
EXPECTED_UID = "2553945035002288"
EXPECTED_SERVER = 2288
ORIGINAL_SHA = {
    "LWScripts.data": "49f6249d1530b5f16aad3750388d017ac3032f4b8ef584eec6cff9ea01d19b6d",
    "LWScripts.txt": "52d5b30c486e9ff222e125eb05a13cc01a6407b0bdb40f15cbf12af0389ffae0",
    "version.txt": "5f9c4ab08cac7457e9111a30e4664920607ea2c115a1433d7be98e97e64244ca",
}


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def validate_receipt(directory: Path) -> None:
    admission = json.loads((directory / "feature018-account-admission.json").read_text("utf-8"))
    exit_report = json.loads((directory / "owned-exit.json").read_text("utf-8"))
    if (str(admission.get("uid")) != EXPECTED_UID or
            admission.get("serverId") != EXPECTED_SERVER or
            admission.get("loggedIn") is not True or
            admission.get("connected") is not True):
        raise RuntimeError(f"Unapproved game receipt: {directory}")
    audit = exit_report.get("restorationAudit") or {}
    if (exit_report.get("exitCode") != 0 or
            exit_report.get("proof") is not True or
            audit.get("state") != "proven" or
            exit_report.get("recoveryJournalPresent") is not False or
            audit.get("preflightOriginalHashes") != ORIGINAL_SHA or
            audit.get("installedHashes") != ORIGINAL_SHA or
            not all(exit_report.get("originalScriptsRestored", {}).get(name) is True
                    for name in ORIGINAL_SHA)):
        raise RuntimeError(f"Game original-script restoration unverified: {directory}")
    if not (directory / "native-ui-map.json").exists():
        raise RuntimeError(f"No genuine packaged native receipt: {directory}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", type=Path, default=ROOT / "artifacts/map014-takeover/publish")
    parser.add_argument("--session", action="append", required=True,
                        help="Exact native-ui-YYYYMMDD-HHMMSS proof directory name; repeat for each session")
    parser.add_argument("--failed-session", action="append", default=[],
                        help="Existing pre-game-start failure receipt (never counted as live proof)")
    args = parser.parse_args()
    if git("branch", "--show-current") != "codex/feature-recovery-018":
        raise RuntimeError("Wrong release branch")
    if git("status", "--porcelain"):
        raise RuntimeError("Refuse to package dirty checkout; commit and verify the actual source first")
    commit = git("rev-parse", "HEAD")
    remote = git("ls-remote", "origin", "refs/heads/codex/feature-recovery-018").split()[0]
    if remote != commit:
        raise RuntimeError(f"Published branch commit does not equal source: {remote} vs {commit}")
    publish = args.publish.resolve(strict=True)
    for relative in ("LWBridge.Desktop.exe", "LWBridge.Desktop.dll",
                     "ProductionUi/index.html", "OverviewBridge/current_overview_bridge.lua",
                     "OverviewBridge/run_overview_bridge.py", "OverviewBridge/feature018_city_game.lua",
                     "OverviewBridge/feature018_hotkey_actions.lua",
                     "OverviewBridge/feature018_mini_game.lua"):
        if not (publish / relative).is_file():
            raise RuntimeError(f"Incomplete published Windows app: {relative}")
    for relative in ("current_overview_bridge.lua", "run_overview_bridge.py",
                     "feature018_city_game.lua", "feature018_hotkey_actions.lua",
                     "feature018_mini_game.lua", "feature018_attack_actions.lua"):
        packed = publish / "OverviewBridge" / relative
        if not packed.is_file() or sha(packed.read_bytes()) != sha((ROOT / "tools" / relative).read_bytes()):
            raise RuntimeError(f"Stale published game source: {relative}")

    sessions = []
    for name in args.session:
        if not name.startswith("native-ui-20261017-") or "/" in name or "\\" in name:
            raise RuntimeError(f"Invalid proof directory name: {name}")
        directory = ROOT / "artifacts/map014-takeover" / name
        validate_receipt(directory)
        if name not in sessions:
            sessions.append(name)
    failed_sessions = []
    for name in args.failed_session:
        if not name.startswith("native-ui-20261017-") or "/" in name or "\\" in name or name in sessions:
            raise RuntimeError("Invalid or already accepted failed proof session")
        directory = ROOT / "artifacts/map014-takeover" / name
        outcome = json.loads((directory / "owned-exit.json").read_text("utf-8"))
        if outcome.get("exitCode") != 1 or outcome.get("proof") is not False or \
            outcome.get("recoveryJournalPresent") is not False or \
            outcome.get("originalScriptsRestored") != {name: True for name in ORIGINAL_SHA} or \
            (directory / "feature018-account-admission.json").exists():
            raise RuntimeError("Failed receipt must be pre-admission and restoration reported")
        errors = list((directory / "overview-evidence").glob("*/helper-start-error.json"))
        if len(errors) != 1:
            raise RuntimeError("Failed session has no unique helper-start error")
        error = json.loads(errors[0].read_text("utf-8"))
        if error.get("errorType") != "PermissionError" or "WinError 5" not in error.get("error", ""):
            raise RuntimeError("Failed session does not match recorded OS denial")
        failed_sessions.append((name, directory, errors[0]))
    destination = ROOT / "artifacts/feature018" / ("delivery-r4-" + commit[:8])
    destination.mkdir(parents=True, exist_ok=True)
    target = destination / ("LW-Control-FEATURE-RECOVERY-018-R4-PARTIAL-" + commit[:12] + ".zip")
    if target.exists():
        raise RuntimeError("Refuse to overwrite an existing release package")
    files: dict[str, Path] = {}
    for path in sorted(publish.rglob("*")):
        if not path.is_file() or "runtimes" in path.relative_to(publish).parts:
            continue
        files[(Path("LW-Control-UI") / path.relative_to(publish)).as_posix()] = path
    for name in sessions:
        source = ROOT / "artifacts/map014-takeover" / name
        for path in sorted(source.iterdir()):
            if path.is_file() and (path.name in (
                    "feature018-account-admission.json", "native-ui-map.json", "owned-exit.json",
                    "feature018-live-land.json", "feature018-live-shield8.json",
                    "feature018-live-shieldUseOwned.json",
                    "feature018-live-readiness.json") or
                    path.name.startswith("feature018-city-driver-") and path.suffix == ".json" or
                    path.name.startswith("feature018-r2-") and path.suffix == ".png"):
                files[(Path("Proof-R3") / name / path.name).as_posix()] = path
    for name, directory, error in failed_sessions:
        # The R4 launch did not reach the game and cannot masquerade as a
        # successful admitted account/session. Keep denial and cleanup proof
        # separate from the positive R3 game observations.
        files[(Path("Proof-R4-Launch-Denied") / name / "owned-exit.json").as_posix()] = directory / "owned-exit.json"
        files[(Path("Proof-R4-Launch-Denied") / name / "helper-start-error.json").as_posix()] = error
        cleanup = error.with_name("helper-start-cleanup.json")
        if cleanup.is_file():
            files[(Path("Proof-R4-Launch-Denied") / name / "helper-start-cleanup.json").as_posix()] = cleanup
    for dest, source in {
        "WORKER-REPORT.md": "docs/FEATURE_RECOVERY_018_WORKER_REPORT.md",
        "ORIGINAL-INVENTORY.md": "docs/FEATURE_RECOVERY_018_INVENTORY.md",
        "LEAD-AUDIT.md": "docs/reviews/2026-10-17-FEATURE-RECOVERY-018-LEAD-AUDIT.md",
        "OWNER-SUB-ACCOUNT-DIRECTION.md": "docs/OWNER_DIRECTION_SUB_ACCOUNT_ACTIONS_2026-10-17.md",
    }.items():
        files[dest] = ROOT / source

    manifest = {
        "task": "FEATURE-RECOVERY-018", "status": "PARTIAL", "commit": commit,
        "branch": "codex/feature-recovery-018", "pr": "https://github.com/Tuna0307/LW-Control/pull/14",
        "liveProofSessionIds": sessions, "failedPreAdmissionSessions": [name for name, _, _ in failed_sessions],
        "verifiedUid": EXPECTED_UID,
        "verifiedServer": EXPECTED_SERVER, "originalScriptsRestored": True,
        "files": {},
    }
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
        for arc, path in sorted(files.items()):
            data = path.read_bytes()
            manifest["files"][arc] = {"sha256": sha(data), "size": len(data)}
            archive.writestr(arc, data)
        notice = (
            "FEATURE-RECOVERY-018 R4 source-pinned Windows review build.\n"
            f"Commit: {commit}\nStatus: PARTIAL, NOT fully recovered.\n"
            "No real-money purchases; original scripts restored in all included game sessions.\n"
            "No inferred server ACK from a local send, native read or UI event.\n"
            "R4 failed startup receipt is pre-admission, not an additional game proof.\n"
            "READ WORKER-REPORT.md and LEAD-AUDIT.md before interpreting proof.\n"
            "No source is merged, published or installed by this ZIP.\n"
        ).encode()
        manifest["files"]["README-RELEASE.txt"] = {"sha256": sha(notice), "size": len(notice)}
        archive.writestr("README-RELEASE.txt", notice)
        archive.writestr("RELEASE-INTEGRITY.json", json.dumps(manifest, indent=2).encode())
    with zipfile.ZipFile(target) as check:
        if check.testzip() is not None:
            raise RuntimeError("Release ZIP CRC failed")
        for arc, expected in manifest["files"].items():
            if sha(check.read(arc)) != expected["sha256"]:
                raise RuntimeError(f"Release ZIP integrity mismatch: {arc}")
    digest = sha(target.read_bytes())
    (destination / "release-integrity.json").write_text(json.dumps({
        "zip": target.name, "sha256": digest, "size": target.stat().st_size,
        "entries": len(manifest["files"]) + 1, "commit": commit,
        "liveProofSessionIds": sessions,
    }, indent=2), encoding="utf-8")
    print(f"FEATURE018_R4_RELEASE_OK {target}\nSHA256={digest} "
          f"BYTES={target.stat().st_size} ENTRIES={len(manifest['files'])+1} COMMIT={commit}")


if __name__ == "__main__":
    main()
