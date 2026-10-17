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
import sys
import zipfile

ROOT = Path(__file__).resolve().parent.parent
UI = ROOT / "src/LWBridge.UI-0.3.17"
BRANCH = "codex/feature-recovery-018"
R4_SHA = "7ef40983553155ba79907640201720f3fe69a89a6eab6dcd6f749a83615c992f"
R4_ZIP = ROOT / "artifacts/feature018/delivery-r4-3c9049aa/LW-Control-FEATURE-RECOVERY-018-R4-PARTIAL-3c9049aa0662.zip"
ORIGINAL = Path(os.path.expanduser("~")) / "AppData/LocalLow/FunFly/Last War-Survival Game/lwScripts"
ORIGINAL_HASHES = {
    "LWScripts.data": "49f6249d1530b5f16aad3750388d017ac3032f4b8ef584eec6cff9ea01d19b6d",
    "LWScripts.txt": "52d5b30c486e9ff222e125eb05a13cc01a6407b0bdb40f15cbf12af0389ffae0",
    "version.txt": "5f9c4ab08cac7457e9111a30e4664920607ea2c115a1433d7be98e97e64244ca",
}


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def digest(path: Path) -> str:
    with path.open("rb") as inp:
        result = hashlib.sha256()
        for part in iter(lambda: inp.read(1024 * 1024), b""):
            result.update(part)
    return result.hexdigest()


def run(*args: str, cwd: Path = ROOT) -> None:
    print("VERIFY_BUILD_STEP", args[0], args[1:3], flush=True)
    subprocess.run(args, cwd=cwd, check=True)


def build() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--round", choices=("R7", "R8", "R9"), default="R7")
    release = parser.parse_args().round
    if os.name != "nt":
        raise RuntimeError("Windows executable delivery requires an actual Windows host")
    if git("branch", "--show-current") != BRANCH or git("status", "--porcelain"):
        raise RuntimeError("Build requires a clean committed feature branch")
    sha = git("rev-parse", "HEAD")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()[0]
    if sha != remote:
        raise RuntimeError("Remote SHA does not match this committed source")
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
    if not (executable / "OverviewBridge/feature018_game_bridge.lua").is_file():
        raise RuntimeError("Native FEATURE018 bridge script was omitted from Windows publish")

    all_files = sorted((p for p in executable.rglob("*") if p.is_file()),
                       key=lambda p: p.relative_to(executable).as_posix())
    if len(all_files) < 25:
        raise RuntimeError("Production desktop/native/UI payload incomplete")
    records = {p.relative_to(executable).as_posix(): {
        "sha256": digest(p), "size": p.stat().st_size,
    } for p in all_files}
    manifest = {
        "task": "FEATURE-RECOVERY-018", "round": release,
        "branch": BRANCH, "commit": sha,
        "status": "PARTIAL", "compiledWindowsPe": True,
        "canonicalProductionUiSourceMatched": True,
        "windowsGameStartupValidated": False,
        "nativeGameplayProofNewInThisRound": False,
        "r4AcceptedWindowsPackageSha256": R4_SHA,
        "previousDenialsNotRetried": [
            "R4 WinError5 recovery.json.tmp-to-recovery.json",
            "R5 EPERM npm ci unlink esbuild.exe",
            "R7 PSSecurityException npm.ps1 execution-policy denial",
        ],
        "uiIdentity": identity, "originalGameScriptsSha256": ORIGINAL_HASHES,
        "files": records,
    }
    readme = (
        f"FEATURE-RECOVERY-018 {release} - COMPILED WINDOWS BUILD / PARTIAL\n"
        "This contains a compiled Windows Desktop executable and its canonical,\n"
        "source-fingerprint-verified Vite frontend plus the native Lua bridge.\n"
        "The app/game startup, user-account state and remaining game-action\n"
        "acceptance were NOT established by the build or static integrity checks.\n"
        "The R4 recovery-marker WinError5 remains unresolved. Do not replay it.\n"
        "R5 npm ci unlink denial was not retried or bypassed; the supported\n"
        "independent frontend and C# build entrypoints were used instead.\n"
        "Game actions MUST be restricted to UID3133546747002289/server2289.\n"
        f"Exact committed source: {sha}\n"
        "Status: PARTIAL; see docs/FEATURE_RECOVERY_018_WORKER_REPORT.md\n"
    ).encode()
    target = destination / ("LW-Control-FEATURE-RECOVERY-018-" + release + "-PARTIAL-" + sha[:12] + ".zip")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in all_files:
            archive.write(path, path.relative_to(executable).as_posix())
        archive.writestr("RELEASE-MANIFEST.json", json.dumps(manifest, indent=2).encode())
        archive.writestr("README-" + release + "-PARTIAL.txt", readme)
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise RuntimeError("Windows archive CRC failed")
        for name, info in records.items():
            if hashlib.sha256(archive.read(name)).hexdigest() != info["sha256"]:
                raise RuntimeError("Windows archive entry SHA mismatch: " + name)
    result = {
        "path": str(target), "sha256": digest(target), "fileCount": len(records),
        "zipEntryCount": len(records) + 2, "commit": sha,
        "compiledWindowsPe": True, "gameStartupValidated": False,
        "uiSourceFingerprint": identity["sourceFingerprint"],
        "uiArtifactFingerprint": identity["artifactFingerprint"],
    }
    (destination / "integrity.json").write_text(json.dumps(result, indent=2) + "\n")
    print("FEATURE018_" + release + "_COMPILED_WINDOWS_SOURCE_MATCHED_PARTIAL", json.dumps(result))


if __name__ == "__main__":
    build()
