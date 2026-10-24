"""Build the integrity ZIP for the LwTool full-improvement programme.

The Windows app must already have been freshly published from the exact pushed
feature-branch commit. This script does not build or launch the product; it
verifies source/artifact identity, rejects private/test payloads, writes a
source-pinned manifest, creates a new ZIP, reopens it, and verifies every entry.

Usage:
  python tools/lwtool_full_improvement_package.py \
      --publish <fresh-publish-directory> --output <new-package-directory>
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile

import feature018_r28_package as proven


ROOT = Path(__file__).resolve().parent.parent
BRANCH = "codex/lwtool-full-improvement"
APP_FOLDER = proven.APP_FOLDER
MANIFEST_NAME = "PROGRAMME-RELEASE-INTEGRITY.json"
SOURCE_NAME = "SOURCE-COMMIT.txt"

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


def clean_pushed_commit() -> tuple[str, str]:
    if git("branch", "--show-current") != BRANCH:
        raise RuntimeError("Programme package must be created from " + BRANCH)
    tracked = git("status", "--porcelain", "--untracked-files=no")
    if tracked:
        raise RuntimeError("Commit all tracked source changes before programme packaging")
    status = git("status", "--porcelain", "--untracked-files=all")
    for line in status.splitlines():
        if not line:
            continue
        if line.startswith("?? .claude/") or line == "?? .claude":
            continue
        raise RuntimeError("Unexpected untracked workspace content before packaging: " + line)
    commit = git("rev-parse", "HEAD")
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise RuntimeError("Invalid source commit")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()
    if len(remote) != 2 or remote[0] != commit:
        raise RuntimeError("Local source commit is not the pushed programme checkpoint")
    return commit, git("rev-parse", "HEAD^{tree}")


def payload_files(publish: Path) -> list[Path]:
    files = sorted(
        (path for path in publish.rglob("*") if path.is_file() and path.suffix.lower() != ".pdb"),
        key=lambda path: path.relative_to(publish).as_posix(),
    )
    if len(files) < 25:
        raise RuntimeError("Packaged Windows app has too few runtime files")
    for path in files:
        relative = path.relative_to(publish)
        lower_parts = {part.lower() for part in relative.parts}
        if path.is_symlink():
            raise RuntimeError("Publish must not contain symbolic-link payloads: " + str(relative))
        if lower_parts & PRIVATE_PARTS:
            raise RuntimeError("Publish contains research/account/private directory: " + str(relative))
        if path.name.lower() in PRIVATE_NAMES:
            raise RuntimeError("Publish contains private/competitor payload: " + str(relative))
        if path.suffix.lower() in PRIVATE_SUFFIXES:
            raise RuntimeError("Publish contains state/secret/capture payload: " + str(relative))
        proven.verify_no_test_account_bytes(relative.as_posix(), path.read_bytes())
    return files


def write_package(publish: Path, output: Path, commit: str, tree: str) -> dict:
    product_version, bridge_hashes = proven.verify_published_app(publish, commit)
    source_digest, source_count = proven.source_inputs_digest()
    files = payload_files(publish)
    manifest = {
        "task": "LWTOOL-FULL-COMPARISON-AND-IMPROVEMENT",
        "status": "INDEPENDENT_REVIEW_REQUIRED",
        "branch": BRANCH,
        "sourceCommit": commit,
        "sourceGitTree": tree,
        "sourceInputSha256": source_digest,
        "sourceInputFileCount": source_count,
        "sourceInputHashMethod": "SHA256(sorted git-tracked src/ and tools/: UTF8 path + NUL + file bytes + NUL)",
        "windowsProductVersion": product_version,
        "productionUiSourceAndArtifactFingerprintVerified": True,
        "desktopLinkedBridgeSha256": bridge_hashes,
        "privacyGates": [
            "fixed proof/test UIDs rejected in ASCII/UTF16LE/UTF16BE",
            "research/evidence/artifacts/profile/userdata/credential directories rejected",
            "competitor/recovered/private-key/session filenames rejected",
            "db/sqlite/dump/capture/key/certificate payload suffixes rejected",
            "PDB files omitted",
        ],
        "liveGameplayValidatedByPackaging": False,
        "independentLeadAcceptance": "SEPARATE",
        "files": {},
    }
    for path in files:
        name = (Path(APP_FOLDER) / path.relative_to(publish)).as_posix()
        manifest["files"][name] = {"sha256": sha256(path), "size": path.stat().st_size}

    source_bytes = (commit + "\n").encode("ascii")
    source_entry = f"{APP_FOLDER}/{SOURCE_NAME}"
    manifest["files"][source_entry] = {
        "sha256": hashlib.sha256(source_bytes).hexdigest(), "size": len(source_bytes)
    }
    manifest = json.loads(json.dumps(manifest))
    manifest_bytes = (json.dumps(manifest, indent=2, sort_keys=True) + "\n").encode("utf-8")
    proven.verify_no_test_account_bytes(MANIFEST_NAME, manifest_bytes)

    output.mkdir(parents=True, exist_ok=False)
    target = output / f"LW-Control-lwtool-improvement-{commit[:12]}.zip"
    receipt = output / "package-receipt.json"
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in files:
            archive.write(path, (Path(APP_FOLDER) / path.relative_to(publish)).as_posix())
        archive.writestr(source_entry, source_bytes)
        archive.writestr(MANIFEST_NAME, manifest_bytes)

    expected_entries = set(manifest["files"]) | {MANIFEST_NAME}
    with zipfile.ZipFile(target, "r") as archive:
        bad = archive.testzip()
        if bad is not None:
            raise RuntimeError("ZIP CRC failed: " + bad)
        actual_entries = set(archive.namelist())
        if actual_entries != expected_entries:
            raise RuntimeError("ZIP entry set differs from integrity manifest")
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            proven.verify_no_test_account_bytes(name, data)
            if len(data) != expected["size"] or hashlib.sha256(data).hexdigest() != expected["sha256"]:
                raise RuntimeError("ZIP payload hash mismatch: " + name)
        embedded = archive.read(MANIFEST_NAME)
        proven.verify_no_test_account_bytes(MANIFEST_NAME, embedded)
        if json.loads(embedded) != manifest:
            raise RuntimeError("Embedded programme manifest mismatch")

    result = {
        "zip": str(target.resolve()),
        "sha256": sha256(target),
        "bytes": target.stat().st_size,
        "zipEntries": len(expected_entries),
        "sourceCommit": commit,
        "sourceGitTree": tree,
        "sourceInputSha256": source_digest,
        "sourceInputFileCount": source_count,
        "bridgesVerified": len(bridge_hashes),
        "status": "INDEPENDENT_REVIEW_REQUIRED",
    }
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("LWTOOL_FULL_IMPROVEMENT_PACKAGE_OK", json.dumps(result))
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    commit, tree = clean_pushed_commit()
    publish = args.publish.resolve(strict=True)
    output = args.output.resolve()
    if output == publish or publish in output.parents or output in publish.parents:
        raise RuntimeError("Package output must not overlap the Windows publish directory")
    write_package(publish, output, commit, tree)


if __name__ == "__main__":
    main()
