"""Build the source-pinned Windows release ZIP from a fresh `dotnet publish`.

This script never builds or launches the app or the game. It checks that the
working tree is a clean, pushed commit, that the publish folder was built from
exactly that commit, that every shipped game script matches the source tree,
and that no private/test payload is included. It then writes a new ZIP with an
integrity manifest, reopens it and verifies every entry.

Usage (Windows, from the repository root, after committing and pushing):
  dotnet publish src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release -o artifacts/publish
  python tools/release/package_release.py --publish artifacts/publish --output artifacts/release-<date>

Replaces the older per-round packagers (feature018_r28_package.py,
lwtool_full_improvement_package.py, lwtool_remaining_tabs_package.py), which
used the same gates but were pinned to one feature branch each.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[2]
DESKTOP_PROJECT = ROOT / "src/LWBridge.Desktop/LWBridge.Desktop.csproj"
GAME_SCRIPTS = ROOT / "src/LWBridge.GameScripts"
UI_CHECK = ROOT / "src/LWBridge.UI-0.3.17/scripts/check-production-build.mjs"
APP_FOLDER = "LW-Control-UI"
MANIFEST_NAME = "PROGRAMME-RELEASE-INTEGRITY.json"
SOURCE_NAME = "SOURCE-COMMIT.txt"

# Proof/test account UIDs that must never ship (see docs/LIVE_TESTING.md).
TEST_ACCOUNT_UIDS = (
    "2553945035002288",
    "3133546747002212",
    "3133546747002289",
    "1183884595002296",
    "4799432499002212",
)
REQUIRED_PUBLISH = (
    "LWBridge.Desktop.exe",
    "LWBridge.Desktop.dll",
    "LWBridge.Desktop.runtimeconfig.json",
    "ProductionUi/index.html",
    "ProductionUi/lwbridge-ui-build.json",
    "OverviewBridge/LWBridge.GamePipeAdapter.dll",
    "OverviewBridge/current_overview_bridge.lua",
    "OverviewBridge/run_overview_bridge.py",
    "OverviewBridge/feature018_game_bridge.lua",
)
PRIVATE_PARTS = {
    "artifacts", "evidence", "research", "profiles", "userdata", "credentials",
    ".claude", ".codex",
}
PRIVATE_NAMES = {
    "credentials.json", "accounts.json", "profiles.json", "launch.json",
    "session.json", "private-runtime-key.bin", "recovered-core.py",
    "recovered-ui.html", "lwtool.exe", "lwtool.dll", "lastwarplugin.exe",
}
PRIVATE_SUFFIXES = {".db", ".sqlite", ".dmp", ".har", ".pcap", ".key", ".pem", ".pfx"}


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def clean_pushed_commit() -> tuple[str, str, str]:
    branch = git("branch", "--show-current")
    if not branch:
        raise RuntimeError("Package from a branch, not a detached HEAD")
    if git("status", "--porcelain", "--untracked-files=no"):
        raise RuntimeError("Commit all tracked source changes before packaging")
    for line in git("status", "--porcelain", "--untracked-files=all").splitlines():
        if line and not (line.startswith("?? .claude/") or line == "?? .claude"):
            raise RuntimeError("Unexpected untracked workspace content before packaging: " + line)
    commit = git("rev-parse", "HEAD")
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise RuntimeError("Invalid source commit")
    remote = git("ls-remote", "origin", "refs/heads/" + branch).split()
    if len(remote) != 2 or remote[0] != commit:
        raise RuntimeError("Local commit is not the pushed head of origin/" + branch)
    return branch, commit, git("rev-parse", "HEAD^{tree}")


def source_inputs_digest() -> tuple[str, int]:
    """SHA-256(path + NUL + bytes + NUL) over tracked src/ files."""
    names = subprocess.check_output(["git", "ls-files", "-z", "--", "src"], cwd=ROOT).split(b"\0")
    paths = sorted(name.decode("utf-8") for name in names if name)
    if not paths:
        raise RuntimeError("Missing tracked application source inputs")
    digest = hashlib.sha256()
    for relative in paths:
        source = ROOT / relative
        if not source.is_file():
            raise RuntimeError("Tracked application source is missing: " + relative)
        digest.update(relative.encode("utf-8") + b"\0")
        digest.update(source.read_bytes())
        digest.update(b"\0")
    return digest.hexdigest(), len(paths)


def verify_no_test_account_bytes(name: str, data: bytes) -> None:
    for uid in TEST_ACCOUNT_UIDS:
        if any(uid.encode(encoding) in data for encoding in ("ascii", "utf-16le", "utf-16be")):
            raise RuntimeError("Package entry contains a test account UID: " + name)


def declared_game_scripts() -> dict[str, Path]:
    """Map OverviewBridge/LiveResourceProbe publish paths to their source files."""
    declared: dict[str, Path] = {}
    for content in ET.parse(DESKTOP_PROJECT).iter("Content"):
        folder = (content.get("Link") or "").split("\\", 1)[0]
        if folder not in ("OverviewBridge", "LiveResourceProbe"):
            continue
        for entry in (content.get("Include") or "").split(";"):
            source = (DESKTOP_PROJECT.parent / entry.replace("\\", "/")).resolve()
            if source.parent != GAME_SCRIPTS:
                continue
            if not source.is_file():
                raise RuntimeError("Desktop project links a missing game script: " + entry)
            declared[f"{folder}/{source.name}"] = source
    if len(declared) < 12:
        raise RuntimeError("Desktop project declares too few game scripts")
    return declared


def verify_published_app(publish: Path, commit: str) -> tuple[str, dict[str, str]]:
    for relative in REQUIRED_PUBLISH:
        if not (publish / relative).is_file():
            raise RuntimeError("Incomplete Windows publish: " + relative)
    exe = publish / "LWBridge.Desktop.exe"
    with exe.open("rb") as stream:
        if stream.read(2) != b"MZ":
            raise RuntimeError("Published executable is not a Windows PE image")
    version = subprocess.check_output(
        ["powershell", "-NoProfile", "-Command",
         "[Diagnostics.FileVersionInfo]::GetVersionInfo($env:LWBRIDGE_EXE).ProductVersion"],
        env=dict(os.environ, LWBRIDGE_EXE=str(exe)), text=True,
    ).strip()
    if not version.endswith("+" + commit):
        raise RuntimeError("Windows executable source-commit mismatch: " + version)
    subprocess.run(["node", str(UI_CHECK), str(publish / "ProductionUi")], cwd=ROOT, check=True)
    hashes: dict[str, str] = {}
    for destination, source in sorted(declared_game_scripts().items()):
        expected = sha256(source)
        packaged = publish / destination
        if not packaged.is_file() or sha256(packaged) != expected:
            raise RuntimeError("Missing/stale packaged game script: " + destination)
        hashes[destination] = expected
    return version, hashes


def payload_files(publish: Path) -> list[Path]:
    files = sorted(
        (path for path in publish.rglob("*") if path.is_file() and path.suffix.lower() != ".pdb"),
        key=lambda path: path.relative_to(publish).as_posix(),
    )
    if len(files) < 25:
        raise RuntimeError("Packaged Windows app has too few runtime files")
    for path in files:
        relative = path.relative_to(publish)
        if path.is_symlink():
            raise RuntimeError("Publish must not contain symbolic links: " + str(relative))
        if {part.lower() for part in relative.parts} & PRIVATE_PARTS:
            raise RuntimeError("Publish contains research/account/private directory: " + str(relative))
        if path.name.lower() in PRIVATE_NAMES:
            raise RuntimeError("Publish contains private/competitor payload: " + str(relative))
        if path.suffix.lower() in PRIVATE_SUFFIXES:
            raise RuntimeError("Publish contains state/secret/capture payload: " + str(relative))
        verify_no_test_account_bytes(relative.as_posix(), path.read_bytes())
    return files


def write_package(publish: Path, output: Path, branch: str, commit: str, tree: str) -> dict:
    product_version, script_hashes = verify_published_app(publish, commit)
    source_digest, source_count = source_inputs_digest()
    files = payload_files(publish)
    manifest = {
        "branch": branch,
        "sourceCommit": commit,
        "sourceGitTree": tree,
        "sourceInputSha256": source_digest,
        "sourceInputFileCount": source_count,
        "sourceInputHashMethod": "SHA256(sorted git-tracked src/: UTF8 path + NUL + file bytes + NUL)",
        "windowsProductVersion": product_version,
        "productionUiSourceAndArtifactFingerprintVerified": True,
        "desktopLinkedBridgeSha256": script_hashes,
        "privacyGates": [
            "fixed proof/test UIDs rejected in ASCII/UTF16LE/UTF16BE",
            "research/evidence/artifacts/profile/userdata/credential directories rejected",
            "competitor/recovered/private-key/session filenames rejected",
            "db/sqlite/dump/capture/key/certificate payload suffixes rejected",
            "PDB files omitted",
        ],
        "liveGameplayValidatedByPackaging": False,
        "files": {},
    }
    for path in files:
        name = (Path(APP_FOLDER) / path.relative_to(publish)).as_posix()
        manifest["files"][name] = {"sha256": sha256(path), "size": path.stat().st_size}
    source_bytes = (commit + "\n").encode("ascii")
    source_entry = f"{APP_FOLDER}/{SOURCE_NAME}"
    manifest["files"][source_entry] = {"sha256": hashlib.sha256(source_bytes).hexdigest(),
                                       "size": len(source_bytes)}
    manifest_bytes = (json.dumps(manifest, indent=2, sort_keys=True) + "\n").encode("utf-8")
    verify_no_test_account_bytes(MANIFEST_NAME, manifest_bytes)

    output.mkdir(parents=True, exist_ok=False)
    target = output / f"LW-Control-{commit[:12]}.zip"
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in files:
            archive.write(path, (Path(APP_FOLDER) / path.relative_to(publish)).as_posix())
        archive.writestr(source_entry, source_bytes)
        archive.writestr(MANIFEST_NAME, manifest_bytes)

    with zipfile.ZipFile(target, "r") as archive:
        bad = archive.testzip()
        if bad is not None:
            raise RuntimeError("ZIP CRC failed: " + bad)
        if set(archive.namelist()) != set(manifest["files"]) | {MANIFEST_NAME}:
            raise RuntimeError("ZIP entry set differs from integrity manifest")
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            verify_no_test_account_bytes(name, data)
            if len(data) != expected["size"] or hashlib.sha256(data).hexdigest() != expected["sha256"]:
                raise RuntimeError("ZIP payload hash mismatch: " + name)
        if json.loads(archive.read(MANIFEST_NAME)) != manifest:
            raise RuntimeError("Embedded manifest mismatch")

    result = {
        "zip": str(target.resolve()),
        "sha256": sha256(target),
        "bytes": target.stat().st_size,
        "zipEntries": len(manifest["files"]) + 1,
        "branch": branch,
        "sourceCommit": commit,
        "sourceGitTree": tree,
        "sourceInputSha256": source_digest,
        "gameScriptsVerified": len(script_hashes),
    }
    (output / "package-receipt.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("LW_CONTROL_PACKAGE_OK", json.dumps(result))
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--publish", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    branch, commit, tree = clean_pushed_commit()
    publish = args.publish.resolve(strict=True)
    output = args.output.resolve()
    if output == publish or publish in output.parents or output in publish.parents:
        raise RuntimeError("Package output must not overlap the publish directory")
    write_package(publish, output, branch, commit, tree)


if __name__ == "__main__":
    main()
