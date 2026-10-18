"""Package a lead-verified FEATURE-RECOVERY-018 R16 Windows owner-test build.

Run only after the final code/tests/live assessment, a fresh Windows publish,
and a pushed source commit. This script only inspects local files, verifies
source identity, and writes a new ZIP and integrity receipt. It never launches
or modifies the game or builds the application.

Usage:
  python tools/feature018_r16_package.py --publish <fresh-windows-publish> \
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
LEGACY_ACCOUNT_LITERAL = re.compile(r"(?<![0-9])(?:2212|2288|2289|2296|2553945035002288|3133546747002289|1183884595002296|4799432499002212)(?![0-9])")
TEST_ACCOUNT_UIDS = (
    "2553945035002288",
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
        raise RuntimeError("R16 release must be packaged from " + BRANCH)
    if git("status", "--porcelain", "--untracked-files=normal"):
        raise RuntimeError("Commit and review all source changes before R16 packaging")
    commit = git("rev-parse", "HEAD")
    if len(commit) != 40 or any(c not in "0123456789abcdef" for c in commit):
        raise RuntimeError("Invalid source commit")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()
    if len(remote) != 2 or remote[0] != commit:
        raise RuntimeError("Local source commit is not the pushed R16 checkpoint")
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

    The isolated proof harness now receives its account through environment
    variables, so the owner binary also stays free of a fixed proof UID.
    A source scan is a useful release tripwire, not a substitute for native
    same-account transition tests.
    """
    native = sorted((ROOT / "tools").glob("feature018_*.lua"))
    native.append(ROOT / "tools/current_overview_bridge.lua")
    managed = sorted((ROOT / "src/LWBridge.Desktop").glob("Feature018*.cs"))
    managed.extend(sorted((ROOT / "src/LWBridge.Desktop").glob("LWBridgeWindow.Feature018*.cs")))
    ui = sorted((ROOT / "src/LWBridge.UI-0.3.17/src").glob("*.jsx"))
    for source in native + managed + ui:
        if not source.is_file():
            raise RuntimeError("R16 product source missing: " + str(source))
        comment_prefix = "--" if source.suffix == ".lua" else "//"
        for line_number, line in enumerate(source.read_text(encoding="utf-8").splitlines(), 1):
            # Historical source comments can describe old proof accounts.
            executable = line.split(comment_prefix, 1)[0]
            if LEGACY_ACCOUNT_LITERAL.search(executable):
                raise RuntimeError(
                    f"R16 shipped product still references a fixed test account/server: "
                    f"{source.relative_to(ROOT)}:{line_number}"
                )


def verify_no_test_account_bytes(name: str, data: bytes) -> None:
    for uid in TEST_ACCOUNT_UIDS:
        if uid.encode("ascii") in data or uid.encode("utf-16le") in data:
            raise RuntimeError(f"R16 package entry contains a test account UID: {name}")


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
         "[Diagnostics.FileVersionInfo]::GetVersionInfo($env:LWBRIDGE_R16_EXE).ProductVersion"],
        env=dict(os.environ, LWBRIDGE_R16_EXE=str(exe)), text=True
    ).strip()
    if not version.endswith("+" + commit):
        raise RuntimeError("Windows executable source-commit mismatch: " + version)
    subprocess.run(["node", str(UI_CHECK), str(publish / "ProductionUi")],
                   cwd=ROOT, check=True)
    return version, verify_bridge_sources(publish)


def owner_readme(commit: str) -> str:
    lines = [
        "LW-Control FEATURE-RECOVERY-018 R16 owner-test build",
        f"Built from commit: {commit}",
        "",
        "HOW TO RUN: extract the whole LW-Control-UI folder and run",
        "LWBridge.Desktop.exe. Keep ProductionUi and OverviewBridge beside it.",
        "Needs .NET 10 Desktop runtime, Edge WebView2 and Python. Connect your",
        "Last War game as usual. Hotkeys only work while the game window is",
        "active and you are not typing in a game text box.",
        "",
        "ACCOUNT: works on any account. No fixed UID/server. If the account or",
        "server changes during an action, that action stops.",
        "",
        "TESTED WORKING IN THE REAL GAME:",
        "- G (Mini Games): adds soldiers in Frontline/normal stages. Soldiers",
        "  per G press is configurable 1-50 (default 5). Confirmed by owner.",
        "- Q/W/E/R (Hotkeys): hover the mouse over a target on the world map and",
        "  press Q/W/E/R to send squad 1/2/3/4. Proven: Q on a normal monster,",
        "  W on an empty resource field. The game's own squad chooser is used.",
        "  E/R use the same code (squads 3/4) but were not separately tested.",
        "- Repeat use: three Q -> A -> home cycles in one session, then",
        "  Q + W for two squads -> A + S. Every send/recall confirmed live.",
        "- Player cities: hover a guildless city + Q, then A to recall.",
        "  Server-confirmed ATTACK_CITY and exact-march recall proven live.",
        "  Other-alliance/unknown cities are never attacked. Same-alliance cities",
        "  are classified for reinforcement instead of attack.",
        "- R16 regression: one fresh guildless-city Q -> A was confirmed after",
        "  speed-up removal. Native speed-up inventory and diamond balance were",
        "  unchanged before/after that pair.",
        "- Rejected hotkeys display a clear status message in the app.",
        "- A/S/D/F (Hotkeys): recall squad 1/2/3/4. A and S proven live; D/F use",
        "  the same code but were not separately tested.",
        "",
        "PARTLY WORKING:",
        "- City Layout: reads your real city, moves and swaps buildings, and",
        "  Cancel stops a running job. It may sometimes show 'result uncertain'",
        "  even though the move went through; check the city in game and move",
        "  buildings back yourself if needed.",
        "",
        "NOT YET TESTED IN THE GAME (may not work):",
        "- Ally city reinforcement is built from the game's own AssistanceCity /",
        "  ASSISTANCE_CITY route, but has NOT been sent live by this R16 worker.",
        "  Owner: please hover a same-alliance city and test Q/W/E/R personally.",
        "- Destroyed city ruin hover is built from the game's own",
        "  ATTACK_PLAYER_RUIN_BUILDING route and native mastery/alliance gates,",
        "  but has NOT been sent live by this R16 worker. Owner: please test it.",
        "- Unlock Land Cell is not part of this R16 owner test; do not press it.",
        "- F9 random relocation, F10 alliance relocation.",
        "- Space shield countdown, F6/F7/F8 shields, Alt+1-4 equipment.",
        "- Food House auto-solve and Black Market chest marker (events off).",
        "",
        "REMOVED BY OWNER DECISION:",
        "- March speed-ups are removed. There is no inventory-item speed-up,",
        "  diamond speed-up, rapid command, or automatic speed-up after Q/W/E/R.",
        "  Old saved speed-up settings are ignored safely.",
        "",
        "A 'Submitted' result means completion is not confirmed. The native",
        "chooser may still be open, or a request may already have been sent.",
        "Check the game before pressing again and report what you see.",
    ]
    return "\n".join(lines) + "\n"


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
        "task": "FEATURE-RECOVERY-018", "round": "R16", "status": "OWNER_TEST",
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
                            "nativeHoveredKinds": ["ordinary_monster", "ordinary_resource",
                                                   "guildless_player_city", "same_alliance_player_city",
                                                   "destroyed_city_ruin"],
                            "cityHoverImplemented": True,
                            "guildlessPlayersOnly": True,
                            "sameAllianceCityReinforcementImplemented": True,
                            "cityRuinImplemented": True,
                            "alliedAndRuinHoverLiveProven": False,
                            "releaseEvidence": "R16_LIVE_Q_A_NO_SPEEDUP_SPEND_PLUS_R15_REPEAT_AND_CITY_PROOFS"},
            "recall": {"keys": "A/S/D/F", "slots": [1, 2, 3, 4],
                       "releaseEvidence": "LIVE_CONFIRMED_A_S"},
            "marchSpeedups": {"implemented": False, "removedByOwnerDirection": True},
        },
        "files": {},
    }
    for path in files:
        verify_no_test_account_bytes(path.relative_to(publish).as_posix(), path.read_bytes())
        name = (Path(APP_FOLDER) / path.relative_to(publish)).as_posix()
        manifest["files"][name] = {"sha256": sha256(path), "size": path.stat().st_size}
    readme = owner_readme(commit).encode("utf-8")
    readme_name = f"{APP_FOLDER}/README-R16.txt"
    manifest["files"][readme_name] = {
        "sha256": hashlib.sha256(readme).hexdigest(), "size": len(readme)
    }
    source_name = f"{APP_FOLDER}/SOURCE-COMMIT.txt"
    source_note = (commit + "\n" + tree + "\n" + source_digest + "\n").encode("ascii")
    manifest["files"][source_name] = {
        "sha256": hashlib.sha256(source_note).hexdigest(), "size": len(source_note)
    }
    manifest_name = f"{APP_FOLDER}/R16-RELEASE-INTEGRITY.json"
    target_name = f"LW-Control-FEATURE-018-R16-OWNER-TEST-{commit[:12]}.zip"
    output.mkdir(parents=True, exist_ok=True)
    target = output / target_name
    receipt = output / "r16-package-receipt.json"
    if target.exists() or receipt.exists():
        raise RuntimeError("Existing R16 artifact preserved; choose a fresh output directory")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
        for path in files:
            archive.write(path, (Path(APP_FOLDER) / path.relative_to(publish)).as_posix())
        archive.writestr(readme_name, readme)
        archive.writestr(source_name, source_note)
        archive.writestr(manifest_name, json.dumps(manifest, indent=2).encode("utf-8"))
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise RuntimeError("R16 ZIP CRC verification failed")
        if set(archive.namelist()) != set(manifest["files"]) | {manifest_name}:
            raise RuntimeError("R16 ZIP entry set differs from release integrity manifest")
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            verify_no_test_account_bytes(name, data)
            if (len(data) != expected["size"] or
                    hashlib.sha256(data).hexdigest() != expected["sha256"]):
                raise RuntimeError("R16 ZIP payload differs from source: " + name)
        if json.loads(archive.read(manifest_name)) != manifest:
            raise RuntimeError("R16 embedded manifest mismatch")
        verify_no_test_account_bytes(manifest_name, archive.read(manifest_name))
    result = {
        "zip": str(target), "sha256": sha256(target), "bytes": target.stat().st_size,
        "zipEntries": len(manifest["files"]) + 1, "sourceCommit": commit,
        "sourceInputSha256": source_digest, "sourceInputFileCount": source_count,
        "bridgesVerified": len(bridges), "status": "OWNER_TEST",
        "testAccountUidScan": "PASS_ASCII_AND_UTF16LE_ALL_ENTRIES",
    }
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("FEATURE018_R16_PACKAGE_SOURCE_MATCHED", json.dumps(result))


if __name__ == "__main__":
    main()
