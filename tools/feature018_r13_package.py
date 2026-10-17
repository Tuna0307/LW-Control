"""Package a lead-verified FEATURE-RECOVERY-018 R13 Windows owner-test build.

Run only after the final code/tests/live assessment, a fresh Windows publish,
and a pushed source commit. This script only inspects local files, verifies
source identity, and writes a new ZIP and integrity receipt. It never launches
or modifies the game or builds the application.

Usage:
  python tools/feature018_r13_package.py --publish <fresh-windows-publish> \
      --output <new-release-directory>
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


ROOT = Path(__file__).resolve().parent.parent
BRANCH = "codex/feature-recovery-018"
DESKTOP_PROJECT = ROOT / "src/LWBridge.Desktop/LWBridge.Desktop.csproj"
UI_CHECK = ROOT / "src/LWBridge.UI-0.3.17/scripts/check-production-build.mjs"
APP_FOLDER = "LW-Control-UI"
LEGACY_ACCOUNT_LITERAL = re.compile(r"(?<![0-9])(?:2212|2288|2289|2553945035002288|3133546747002289)(?![0-9])")
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
    "OverviewBridge/feature018_city_game.lua",
    "OverviewBridge/feature018_hotkey_game.lua",
    "OverviewBridge/feature018_hotkey_actions.lua",
    "OverviewBridge/feature018_attack_actions.lua",
    "OverviewBridge/feature018_relocation_actions.lua",
    "OverviewBridge/feature018_equipment_actions.lua",
    "OverviewBridge/feature018_mini_game.lua",
    "OverviewBridge/feature018_sheep_solver.lua",
)


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for data in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(data)
    return digest.hexdigest()


def clean_pushed_commit() -> tuple[str, str]:
    if git("branch", "--show-current") != BRANCH:
        raise RuntimeError("R13 release must be packaged from " + BRANCH)
    if git("status", "--porcelain", "--untracked-files=normal"):
        raise RuntimeError("Commit and review all source changes before R13 packaging")
    commit = git("rev-parse", "HEAD")
    if len(commit) != 40 or any(c not in "0123456789abcdef" for c in commit):
        raise RuntimeError("Invalid source commit")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()
    if len(remote) != 2 or remote[0] != commit:
        raise RuntimeError("Local source commit is not the pushed R13 checkpoint")
    return commit, git("rev-parse", "HEAD^{tree}")


def source_inputs_digest() -> tuple[str, int]:
    """SHA-256(path + NUL + bytes + NUL) for tracked src/ and tools/ files.

    Git's clean checkout and pinned commit make this a source-content witness,
    alongside the commit/tree IDs and the embedded Windows ProductVersion.
    """
    names = subprocess.check_output(
        ["git", "ls-files", "-z", "--", "src", "tools"], cwd=ROOT
    ).split(b"\0")
    paths = sorted(name.decode("utf-8") for name in names if name)
    if not paths:
        raise RuntimeError("Missing tracked application source inputs")
    digest = hashlib.sha256()
    for relative in paths:
        source = ROOT / relative
        if not source.is_file():
            raise RuntimeError("Tracked application source is missing: " + relative)
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        with source.open("rb") as stream:
            for data in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(data)
        digest.update(b"\0")
    return digest.hexdigest(), len(paths)


def verify_account_agnostic_sources() -> None:
    """Keep fixed proof-account allowlists out of the *shipped product* sources.

    The dedicated live-proof harness intentionally checks only the approved
    test account; proof C# and test scripts are deliberately excluded here.
    A source scan is a useful release tripwire, not a substitute for the
    lead's native same-account transition tests.
    """
    native = sorted((ROOT / "tools").glob("feature018_*.lua"))
    native.append(ROOT / "tools/current_overview_bridge.lua")
    managed = sorted((ROOT / "src/LWBridge.Desktop").glob("Feature018*.cs"))
    managed.append(ROOT / "src/LWBridge.Desktop/LWBridgeWindow.Feature018Hotkeys.cs")
    for source in native + managed:
        if not source.is_file():
            raise RuntimeError("R13 product source missing: " + str(source))
        comment_prefix = "--" if source.suffix == ".lua" else "//"
        for line_number, line in enumerate(source.read_text(encoding="utf-8").splitlines(), 1):
            # Historical source comments can describe old proof accounts.
            executable = line.split(comment_prefix, 1)[0]
            if LEGACY_ACCOUNT_LITERAL.search(executable):
                raise RuntimeError(
                    f"R13 shipped product still references a fixed test account/server: "
                    f"{source.relative_to(ROOT)}:{line_number}"
                )


def verify_bridge_sources(publish: Path) -> dict[str, str]:
    """All tools/ scripts linked by the Desktop project must be shipped exactly.

    Checking the project declaration, rather than globbing publish, prevents a
    silently omitted feature module from passing the package integrity gate.
    """
    project = ET.parse(DESKTOP_PROJECT)
    matching: dict[str, str] = {}
    for content in project.iter("Content"):
        link = content.get("Link") or ""
        if not link.startswith("OverviewBridge\\"):
            continue
        for entry in (content.get("Include") or "").split(";"):
            resolved = (DESKTOP_PROJECT.parent / entry.replace("\\", "/")).resolve()
            try:
                relative = resolved.relative_to(ROOT / "tools")
            except ValueError:
                continue  # The packaged GamePipeAdapter binary is checked separately.
            if not resolved.is_file():
                raise RuntimeError("Desktop project links missing bridge source: " + entry)
            destination = (Path("OverviewBridge") / relative.name).as_posix()
            packaged = publish / destination
            expected = sha256(resolved)
            if not packaged.is_file() or sha256(packaged) != expected:
                raise RuntimeError("Missing/stale packaged bridge source: " + destination)
            matching[destination] = expected
    if len(matching) < 12:
        raise RuntimeError("Desktop project provided too few bridge source mappings")
    for required in REQUIRED_PUBLISH:
        if required.startswith("OverviewBridge/") and required.endswith((".lua", ".py")):
            if required not in matching:
                raise RuntimeError("Required bridge source is not declared in Desktop project: " + required)
    return dict(sorted(matching.items()))


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
         "[Diagnostics.FileVersionInfo]::GetVersionInfo($env:LWBRIDGE_R13_EXE).ProductVersion"],
        env=dict(os.environ, LWBRIDGE_R13_EXE=str(exe)), text=True
    ).strip()
    if not version.endswith("+" + commit):
        raise RuntimeError("Windows executable source-commit mismatch: " + version)
    subprocess.run(["node", str(UI_CHECK), str(publish / "ProductionUi")],
                   cwd=ROOT, check=True)
    return version, verify_bridge_sources(publish)


def owner_readme(commit: str) -> str:
    return (
        "FEATURE-RECOVERY-018 R13 Windows owner-test checkpoint (PARTIAL)\n"
        f"Committed build source: {commit}\n\n"
        "Extract the entire LW-Control-UI folder on Windows x64 and run\n"
        "LWBridge.Desktop.exe. Keep ProductionUi and OverviewBridge beside it.\n"
        "The app needs the .NET 10 Desktop runtime, Edge WebView2 runtime, and\n"
        "Python for the native bridge. Connect your Last War game as usual.\n\n"
        "ACCOUNT: The shipped features are for any authenticated game account,\n"
        "with no fixed UID/server allowlist. Each action captures the current\n"
        "UID and server; a change before completion cancels its continuation.\n"
        "Reconnecting on another account starts a new action context.\n\n"
        "G / MINI GAMES: Enable the G switch on Mini Games. Set Soldiers per G\n"
        "press to an integer from 1 to 50 (default 5) in its settings. During\n"
        "a supported active Frontline/normal stage with the game window active,\n"
        "press physical G to add that many members. No confirmation per press.\n"
        "Outside an active stage G does nothing. Native stage capacity, reserve\n"
        "and battle-end behavior still apply.\n\n"
        "Q/W/E/R ATTACK (R13 LIVE VALIDATION PENDING): The intended shortcut\n"
        "hovers an eligible world-map target and uses Q/W/E/R for squads\n"
        "1/2/3/4. Targets are ordinary monsters, resources, cities (including\n"
        "allied reinforcement where allowed), and ruins. World bosses and\n"
        "rally monsters are excluded. An open native target popup may supply\n"
        "the selected target. Hover mapping and march proof must be confirmed\n"
        "in the owner's real game before relying on this shortcut.\n"
        "A/S/D/F RECALL (R13 LIVE VALIDATION PENDING): The intended matching\n"
        "squad recall shortcuts are A/S/D/F for squads 1/2/3/4. The owner\n"
        "must independently verify native return and server result.\n\n"
        "The package integrity check never establishes a march or recall.\n"
        "A locally submitted command is not a server-confirmed action.\n"
        "Other FEATURE-RECOVERY-018 features remain under review.\n"
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", required=True, type=Path,
                        help="Lead-verified, freshly compiled Windows publish directory")
    parser.add_argument("--output", required=True, type=Path,
                        help="New release directory outside the Windows publish folder")
    args = parser.parse_args()
    commit, tree = clean_pushed_commit()
    publish = args.publish.resolve(strict=True)
    output = args.output.resolve()
    if output == publish or publish in output.parents or output in publish.parents:
        raise RuntimeError("Release output must not overlap the Windows publish folder")
    verify_account_agnostic_sources()
    product_version, bridges = verify_published_app(publish, commit)
    source_digest, source_count = source_inputs_digest()
    files = sorted(
        (p for p in publish.rglob("*") if p.is_file() and p.suffix.lower() != ".pdb"),
        key=lambda p: p.relative_to(publish).as_posix(),
    )
    if len(files) < 25:
        raise RuntimeError("Packaged Windows app has too few runtime files")
    manifest = {
        "task": "FEATURE-RECOVERY-018", "round": "R13", "status": "PARTIAL",
        "branch": BRANCH, "sourceCommit": commit, "sourceGitTree": tree,
        "sourceInputSha256": source_digest, "sourceInputFileCount": source_count,
        "sourceInputHashMethod": "SHA256(sorted git-tracked src/ and tools/: UTF8 path + NUL + file bytes + NUL)",
        "windowsProductVersion": product_version,
        "productionUiSourceAndArtifactFingerprintVerified": True,
        "desktopLinkedBridgeSha256": bridges,
        "supportedAccountScope": "any authenticated account; same UID/server throughout action",
        "fixedProofAccountLiteralPreflight": True,
        "liveGameplayValidatedByPackaging": False,
        "features": {
            "frontlineG": {"configurableSoldiersPerPress": [1, 50], "default": 5},
            "hoverAttack": {"keys": "Q/W/E/R", "slots": [1, 2, 3, 4],
                            "releaseEvidence": "PENDING_R13_LIVE_VALIDATION"},
            "recall": {"keys": "A/S/D/F", "slots": [1, 2, 3, 4],
                       "releaseEvidence": "PENDING_R13_LIVE_VALIDATION"},
        },
        "files": {},
    }
    for path in files:
        name = (Path(APP_FOLDER) / path.relative_to(publish)).as_posix()
        manifest["files"][name] = {"sha256": sha256(path), "size": path.stat().st_size}
    readme = owner_readme(commit).encode("utf-8")
    readme_name = f"{APP_FOLDER}/README-R13.txt"
    manifest["files"][readme_name] = {
        "sha256": hashlib.sha256(readme).hexdigest(), "size": len(readme)
    }
    source_name = f"{APP_FOLDER}/SOURCE-COMMIT.txt"
    source_note = (commit + "\n" + tree + "\n" + source_digest + "\n").encode("ascii")
    manifest["files"][source_name] = {
        "sha256": hashlib.sha256(source_note).hexdigest(), "size": len(source_note)
    }
    manifest_name = f"{APP_FOLDER}/R13-RELEASE-INTEGRITY.json"
    target_name = f"LW-Control-FEATURE-018-R13-PARTIAL-{commit[:12]}.zip"
    output.mkdir(parents=True, exist_ok=True)
    target = output / target_name
    receipt = output / "r13-package-receipt.json"
    if target.exists() or receipt.exists():
        raise RuntimeError("Existing R13 artifact preserved; choose a fresh output directory")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
        for path in files:
            archive.write(path, (Path(APP_FOLDER) / path.relative_to(publish)).as_posix())
        archive.writestr(readme_name, readme)
        archive.writestr(source_name, source_note)
        archive.writestr(manifest_name, json.dumps(manifest, indent=2).encode("utf-8"))
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise RuntimeError("R13 ZIP CRC verification failed")
        if set(archive.namelist()) != set(manifest["files"]) | {manifest_name}:
            raise RuntimeError("R13 ZIP entry set differs from release integrity manifest")
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            if (len(data) != expected["size"] or
                    hashlib.sha256(data).hexdigest() != expected["sha256"]):
                raise RuntimeError("R13 ZIP payload differs from source: " + name)
        if json.loads(archive.read(manifest_name)) != manifest:
            raise RuntimeError("R13 embedded manifest mismatch")
    result = {
        "zip": str(target), "sha256": sha256(target), "bytes": target.stat().st_size,
        "zipEntries": len(manifest["files"]) + 1, "sourceCommit": commit,
        "sourceInputSha256": source_digest, "sourceInputFileCount": source_count,
        "bridgesVerified": len(bridges), "status": "PARTIAL",
    }
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("FEATURE018_R13_PACKAGE_SOURCE_MATCHED", json.dumps(result))


if __name__ == "__main__":
    main()
