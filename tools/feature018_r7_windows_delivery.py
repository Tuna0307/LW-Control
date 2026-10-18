"""Build and verify a Windows FEATURE-018 ZIP using independent approved targets.

The canonical Desktop MSBuild hook runs npm ci, whose R5 unlink of esbuild.exe
was denied. This script NEVER runs npm ci, touches node_modules, repairs ACLs,
or starts the game. It uses the repository's public build-production.mjs
entrypoint and existing SkipCanonicalProductionUiBuild C# property, then
verifies both generated outputs and copies them into the canonical layout.
The R4 recovery-marker denied rename is unrelated and is NEVER attempted.

This produces a source-matched Windows *build*. It does not establish that the
game can start or that any feature completed an authenticated game action.
"""
from __future__ import annotations

import hashlib
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parent.parent
UI = ROOT / "src/LWBridge.UI-0.3.17"
BRANCH = "codex/feature-recovery-018"
SUPPORTED_ROUNDS = ("R7", "R8", "R9", "R10", "R11", "R14")
R14_UID = "1183884595002296"
R14_SERVER = 2296
R4_SHA = "7ef40983553155ba79907640201720f3fe69a89a6eab6dcd6f749a83615c992f"
R4_ZIP = ROOT / "artifacts/feature018/delivery-r4-3c9049aa/LW-Control-FEATURE-RECOVERY-018-R4-PARTIAL-3c9049aa0662.zip"
ORIGINAL = Path(os.path.expanduser("~")) / "AppData/LocalLow/FunFly/Last War-Survival Game/lwScripts"
ORIGINAL_HASHES = {
    "LWScripts.data": "49f6249d1530b5f16aad3750388d017ac3032f4b8ef584eec6cff9ea01d19b6d",
    "LWScripts.txt": "52d5b30c486e9ff222e125eb05a13cc01a6407b0bdb40f15cbf12af0389ffae0",
    "version.txt": "5f9c4ab08cac7457e9111a30e4664920607ea2c115a1433d7be98e97e64244ca",
}
SOURCE_MATCHED_BRIDGE_FILES = (
    "run_overview_bridge.py",
    "run_overview_bridge_current.py",
    "recover_overview_pending_current.py",
    "current_overview_bridge.lua",
    "run_live_resource_probe.py",
    "current_client_compat.py",
    "current_live_resource_probe.lua",
    "feature018_game_bridge.lua",
    "feature018_city_game.lua",
    "feature018_hotkey_game.lua",
    "feature018_hotkey_actions.lua",
    "feature018_attack_actions.lua",
    "feature018_relocation_actions.lua",
    "feature018_equipment_actions.lua",
    "feature018_mini_game.lua",
    "feature018_sheep_solver.lua",
)
R14_LIVE_PREREQUISITES = {
    "Q/W/E/R": (
        "Physical key capture on the authenticated game HWND plus a source-valid "
        "current world target, staffed owned formation, one original march send, "
        "server-correlated poststate and visible march."
    ),
    "A/S/D/F": (
        "An exact owned live march from the selected formation, one original return "
        "send, and a server-applied same-march BACK_HOME transition."
    ),
    "F9": (
        "An owned usable random-relocation item, clear native warnings/input, one "
        "original ItemUse send, matching reply, item debit and authoritative city-position change."
    ),
    "F10": (
        "Native alliance membership is required. The original v26 path also needs an "
        "owner-verified eligible other-server alliance rally, owned relocation item, "
        "UIChooseRallyPoint owner selection, one observed AllianceMoveCity send/reason-240 "
        "success, item debit and authoritative destination position. If those prerequisites "
        "are absent, F10 remains unavailable/unproven."
    ),
    "CityLayout": (
        "Authoritative 2296 city state plus server-applied move/swap/cancel observations "
        "and exact restoration of the original layout; Unlock Land Cell is outside this proof."
    ),
}


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def require_clean_pushed_sha() -> str:
    if git("branch", "--show-current") != BRANCH:
        raise RuntimeError("Build requires the FEATURE-RECOVERY-018 feature branch")
    if git("status", "--porcelain=v1", "--untracked-files=all"):
        raise RuntimeError("Build requires a clean committed checkout; final ZIPs must never contain uncommitted source")
    sha = git("rev-parse", "HEAD")
    remote_fields = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()
    if not remote_fields:
        raise RuntimeError("Remote feature branch is unavailable")
    remote = remote_fields[0]
    if sha != remote:
        raise RuntimeError("Remote SHA does not match this committed source; push the exact final commit before packaging")
    return sha


def digest(path: Path) -> str:
    with path.open("rb") as inp:
        result = hashlib.sha256()
        for part in iter(lambda: inp.read(1024 * 1024), b""):
            result.update(part)
    return result.hexdigest()


def run(*args: str, cwd: Path = ROOT) -> None:
    print("VERIFY_BUILD_STEP", args[0], args[1:3], flush=True)
    subprocess.run(args, cwd=cwd, check=True)


def load_final_audit_summary(path: Path | None) -> tuple[dict | None, dict]:
    if path is None:
        return None, {"supplied": False, "source": None, "sha256": None}
    resolved = path.resolve(strict=True)
    raw = resolved.read_bytes()
    parsed = json.loads(raw)
    if not isinstance(parsed, dict):
        raise RuntimeError("Final audit summary must be a JSON object")
    return parsed, {
        "supplied": True,
        "source": resolved.name,
        "sha256": hashlib.sha256(raw).hexdigest(),
    }


def verify_source_matched_publish(executable: Path) -> dict[str, str]:
    matched: dict[str, str] = {}
    for name in SOURCE_MATCHED_BRIDGE_FILES:
        source = ROOT / "tools" / name
        packed = executable / "OverviewBridge" / name
        if not source.is_file() or not packed.is_file():
            raise RuntimeError("Source-matched Windows publish omitted native source: " + name)
        source_sha = digest(source)
        if digest(packed) != source_sha:
            raise RuntimeError("Stale Windows publish native source: " + name)
        matched["OverviewBridge/" + name] = source_sha
    return matched


def verify_archive(target: Path, records: dict[str, dict[str, object]],
                   manifest_bytes: bytes, readme_name: str, readme: bytes) -> None:
    expected_names = set(records) | {"RELEASE-MANIFEST.json", readme_name}
    with zipfile.ZipFile(target) as archive:
        names = archive.namelist()
        if archive.testzip() is not None:
            raise RuntimeError("Windows archive CRC failed")
        if len(names) != len(expected_names) or len(names) != len(set(names)) or set(names) != expected_names:
            raise RuntimeError("Windows archive entry set is incomplete or contains duplicates")
        for name, info in records.items():
            if hashlib.sha256(archive.read(name)).hexdigest() != info["sha256"]:
                raise RuntimeError("Windows archive entry SHA mismatch: " + name)
        if archive.read("RELEASE-MANIFEST.json") != manifest_bytes:
            raise RuntimeError("Windows archive release manifest mismatch")
        if archive.read(readme_name) != readme:
            raise RuntimeError("Windows archive README mismatch")


def render_readme(release: str, sha: str) -> bytes:
    r14_readme = ""
    if release == "R14":
        r14_readme = (
            "This packager does not mark any R14 task proven. If a final-audit proof\n"
            "summary is present in RELEASE-MANIFEST.json, it was supplied separately\n"
            "and is carried as audit data without promotion by the packager.\n"
            f"R14 mutable proof actions are authorized only on UID {R14_UID} / server {R14_SERVER};\n"
            "the shipped application itself remains any-account and revalidates its current owner.\n"
            "F10 alliance relocation requires native alliance membership plus an eligible\n"
            "owner-verified other-server rally, owned relocation item, and explicit owner\n"
            "selection in UIChooseRallyPoint before its one-send/reply/poststate proof.\n"
            "If those prerequisites are unavailable, F10 must remain unavailable/unproven.\n"
        )
    return (
        f"FEATURE-RECOVERY-018 {release} - COMPILED WINDOWS BUILD / PARTIAL\n"
        "This contains a compiled Windows Desktop executable and its canonical,\n"
        "source-fingerprint-verified Vite frontend plus the native Lua bridge.\n"
        "The app/game startup, user-account state and remaining game-action\n"
        "acceptance were NOT established by the build or static integrity checks.\n"
        + r14_readme +
        "The R4 recovery-marker WinError5 remains unresolved. Do not replay it.\n"
        "R5 npm ci unlink denial was not retried or bypassed; the supported\n"
        "independent frontend and C# build entrypoints were used instead.\n"
        f"Exact committed source: {sha}\n"
        "Status: PARTIAL; see docs/FEATURE_RECOVERY_018_WORKER_REPORT.md\n"
    ).encode()


def build(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--round", choices=SUPPORTED_ROUNDS, default="R7")
    parser.add_argument("--final-audit-summary", type=Path,
                        help="Optional final-audit JSON object copied into release metadata; the packager does not validate or promote its proof claims")
    args = parser.parse_args(argv)
    release = args.round
    if args.final_audit_summary is not None and release != "R14":
        raise RuntimeError("Final audit summary input is supported only for R14 delivery")
    if os.name != "nt":
        raise RuntimeError("Windows executable delivery requires an actual Windows host")
    sha = require_clean_pushed_sha()
    final_audit_summary, final_audit_metadata = load_final_audit_summary(args.final_audit_summary)
    if not R4_ZIP.is_file() or digest(R4_ZIP) != R4_SHA:
        raise RuntimeError("Accepted prior R4 Windows archive was altered")
    for name, expected in ORIGINAL_HASHES.items():
        original_file = ORIGINAL / name
        if not original_file.is_file() or digest(original_file) != expected:
            raise RuntimeError("Original installed game-script triplet integrity changed: " + name)

    destination = ROOT / "artifacts/feature018" / ("delivery-" + release.lower() + "-" + sha[:8])
    # Never overwrite an earlier attempt or replace any artifact.
    destination.mkdir(parents=True, exist_ok=False)
    frontend = destination / "fresh-ui"
    executable = destination / "windows"
    # Direct Vite build writes only to the brand-new fresh-ui folder. Unlike
    # the denied npm ci, this never unlinks the installed esbuild.exe.
    run("node", str(UI / "scripts/build-production.mjs"),
        "--outDir", str(frontend), cwd=UI)
    run("node", str(UI / "scripts/check-production-build.mjs"),
        str(frontend), cwd=UI)
    run("dotnet", "publish", str(ROOT / "src/LWBridge.Desktop/LWBridge.Desktop.csproj"),
        "--no-restore", "-c", "Release",
        "-p:SkipCanonicalProductionUiBuild=true", "-o", str(executable))

    # The Desktop program's production selector requires ProductionUi next
    # to LWBridge.Desktop.exe and independently verifies the UI artifact SHA.
    shutil.copytree(frontend, executable / "ProductionUi")
    run("node", str(UI / "scripts/check-production-build.mjs"),
        str(executable / "ProductionUi"), cwd=UI)
    exe = executable / "LWBridge.Desktop.exe"
    if not exe.is_file() or exe.open("rb").read(2) != b"MZ":
        raise RuntimeError("Windows PE application host was not published")
    identity = json.loads((executable / "ProductionUi/lwbridge-ui-build.json").read_text())
    source_matched_bridge_files = verify_source_matched_publish(executable)

    all_files = sorted((p for p in executable.rglob("*") if p.is_file()),
                       key=lambda p: p.relative_to(executable).as_posix())
    if len(all_files) < 25:
        raise RuntimeError("Production desktop/native/UI payload incomplete")
    if require_clean_pushed_sha() != sha:
        raise RuntimeError("Feature branch changed while the source-matched Windows build was being produced")
    records = {p.relative_to(executable).as_posix(): {
        "sha256": digest(p), "size": p.stat().st_size,
    } for p in all_files}
    manifest: dict[str, object] = {
        "task": "FEATURE-RECOVERY-018", "round": release,
        "branch": BRANCH, "commit": sha,
        "status": "PARTIAL", "compiledWindowsPe": True,
        "canonicalProductionUiSourceMatched": True,
        "nativeBridgeSourceMatched": True,
        "sourceMatchedBridgeFiles": source_matched_bridge_files,
        "windowsGameStartupValidated": False,
        "nativeGameplayProofClaimedByPackager": False,
        "r4AcceptedWindowsPackageSha256": R4_SHA,
        "previousDenialsNotRetried": [
            "R4 WinError5 recovery.json.tmp-to-recovery.json",
            "R5 EPERM npm ci unlink esbuild.exe",
            "R7 PSSecurityException npm.ps1 execution-policy denial",
        ],
        "uiIdentity": identity, "originalGameScriptsSha256": ORIGINAL_HASHES,
        "files": records,
    }
    if release == "R14":
        manifest.update({
            "finalAuditProofSummaryMetadata": final_audit_metadata,
            "finalAuditProofSummary": final_audit_summary,
            "authorizedMutableProofAccount": {
                "uid": R14_UID, "serverId": R14_SERVER,
                "scope": "R14 live proof only; shipped product remains any-account",
            },
            "liveProofPrerequisites": R14_LIVE_PREREQUISITES,
        })
    else:
        manifest["nativeGameplayProofNewInThisRound"] = False
    readme = render_readme(release, sha)
    target = destination / ("LW-Control-FEATURE-RECOVERY-018-" + release + "-PARTIAL-" + sha[:12] + ".zip")
    manifest_bytes = json.dumps(manifest, indent=2).encode()
    readme_name = "README-" + release + "-PARTIAL.txt"
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in all_files:
            archive.write(path, path.relative_to(executable).as_posix())
        archive.writestr("RELEASE-MANIFEST.json", manifest_bytes)
        archive.writestr(readme_name, readme)
    verify_archive(target, records, manifest_bytes, readme_name, readme)
    result = {
        "path": str(target), "sha256": digest(target), "fileCount": len(records),
        "zipEntryCount": len(records) + 2, "commit": sha,
        "compiledWindowsPe": True, "gameStartupValidated": False,
        "nativeBridgeSourceMatched": True,
        "finalAuditProofSummarySupplied": release == "R14" and final_audit_metadata["supplied"],
        "releaseManifestSha256": hashlib.sha256(manifest_bytes).hexdigest(),
        "readmeSha256": hashlib.sha256(readme).hexdigest(),
        "uiSourceFingerprint": identity["sourceFingerprint"],
        "uiArtifactFingerprint": identity["artifactFingerprint"],
    }
    (destination / "integrity.json").write_text(json.dumps(result, indent=2) + "\n")
    print("FEATURE018_" + release + "_COMPILED_WINDOWS_SOURCE_MATCHED_PARTIAL", json.dumps(result))


if __name__ == "__main__":
    build()
