"""Package a lead-verified FEATURE-RECOVERY-018 R19 Windows owner-test build.

Run only after the final code/tests/live assessment, a fresh Windows publish,
and a pushed source commit. This script only inspects local files, verifies
source identity, and writes a new ZIP and integrity receipt. It never launches
or modifies the game or builds the application.

Usage:
  python tools/feature018_r19_package.py --publish <fresh-windows-publish> \
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
LEGACY_ACCOUNT_LITERAL = re.compile(r"(?<![0-9])(?:2212|2288|2289|2296|2553945035002288|3133546747002212|3133546747002289|1183884595002296|4799432499002212)(?![0-9])")
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
        raise RuntimeError("R19 release must be packaged from " + BRANCH)
    # Package identity is defined by tracked source. Preserve unrelated local
    # workspace metadata (for example an owner's untracked .claude/ folder)
    # while still refusing any tracked source/index difference from HEAD.
    if git("status", "--porcelain", "--untracked-files=no"):
        raise RuntimeError("Commit and review all tracked source changes before R19 packaging")
    commit = git("rev-parse", "HEAD")
    if len(commit) != 40 or any(c not in "0123456789abcdef" for c in commit):
        raise RuntimeError("Invalid source commit")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()
    if len(remote) != 2 or remote[0] != commit:
        raise RuntimeError("Local source commit is not the pushed R19 checkpoint")
    return commit, git("rev-parse", "HEAD^{tree}")


def source_inputs_digest() -> tuple[str, int]:
    """SHA-256(path + NUL + bytes + NUL) for tracked src/ and tools/ files."""
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
    """Keep fixed proof-account allowlists out of the shipped product sources."""
    native = sorted((ROOT / "tools").glob("feature018_*.lua"))
    native.append(ROOT / "tools/current_overview_bridge.lua")
    managed = sorted((ROOT / "src/LWBridge.Desktop").glob("Feature018*.cs"))
    managed.extend(sorted((ROOT / "src/LWBridge.Desktop").glob("LWBridgeWindow.Feature018*.cs")))
    ui = sorted((ROOT / "src/LWBridge.UI-0.3.17/src").glob("*.jsx"))
    for source in native + managed + ui:
        if not source.is_file():
            raise RuntimeError("R19 product source missing: " + str(source))
        comment_prefix = "--" if source.suffix == ".lua" else "//"
        for line_number, line in enumerate(source.read_text(encoding="utf-8").splitlines(), 1):
            executable = line.split(comment_prefix, 1)[0]
            if LEGACY_ACCOUNT_LITERAL.search(executable):
                raise RuntimeError(
                    f"R19 shipped product still references a fixed test account/server: "
                    f"{source.relative_to(ROOT)}:{line_number}"
                )


def verify_r19_retirements() -> None:
    canonical = ROOT / "src/LWBridge.UI-0.3.17/src"
    squads = (canonical / "SquadsPage.jsx").read_text(encoding="utf-8")
    if any(key in squads for key in ("garrison.", "zombieBus.", "allianceGarrison")):
        raise RuntimeError("R19 retired canonical card remains")
    for key in ("squad.afkMaster", "squad.afkAllianceDrill", "automation.autoUsePotion"):
        if key not in squads:
            raise RuntimeError("R19 retained control missing: " + key)
    if (ROOT / "src/LWBridge.Desktop/AllianceGarrisonConfigCommandService.cs").exists():
        raise RuntimeError("R19 retired command service remains")
    for code in ("en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"):
        text = (canonical / "locales" / (code + ".js")).read_text(encoding="utf-8")
        if any(key in text for key in ('"garrison.', '"zombieBus.')):
            raise RuntimeError("R19 retired canonical locale remains: " + code)
    legacy = ROOT / "src/LWBridge.Desktop/WebUi/assets"
    for source in legacy.glob("*.js"):
        if source.name.startswith(("SquadPanel-", "en-", "zh-CN-", "zh-TW-", "ja-", "ko-", "vi-", "id-", "ru-", "pt-")):
            text = source.read_text(encoding="utf-8")
            if any(key in text for key in ("garrison.", "zombieBus.")):
                raise RuntimeError("R19 retired legacy card/locale remains: " + source.name)


def verify_r19_responsive_source() -> None:
    """Require the responsive source contract that R19 qualifies headlessly."""
    canonical = ROOT / "src/LWBridge.UI-0.3.17"
    styles = (canonical / "src/styles.css").read_text(encoding="utf-8")
    required_styles = (
        "FEATURE-RECOVERY-018 R19 responsive layout qualification",
        ".monster-afk-toolbar{grid-template-columns:repeat(auto-fit",
        ".equipment-preset-positions{",
        ".automation-grid{",
        'data-hotkey-category="miniGames"',
        ".map-table--dispatch,.map-table--ghost{min-width:1480px!important}",
        "@media (min-width:1100px)",
        "@media (max-width:1099px)",
        ".city-layout-workbench{grid-template-columns:minmax(0,1fr) minmax(250px,280px)}",
    )
    missing = [marker for marker in required_styles if marker not in styles]
    if missing:
        raise RuntimeError("R19 responsive CSS contract missing: " + ", ".join(missing))
    evidence = canonical / "scripts/r19-responsive-evidence.mjs"
    if not evidence.is_file():
        raise RuntimeError("R19 responsive evidence harness is missing")
    evidence_text = evidence.read_text(encoding="utf-8")
    for marker in ("[900, 1100, 1400, 1600]", '["en", "dark"]', '["zh-CN", "dark"]', "R19 responsive acceptance PASS"):
        if marker not in evidence_text:
            raise RuntimeError("R19 responsive evidence contract missing: " + marker)


def verify_no_test_account_bytes(name: str, data: bytes) -> None:
    for uid in TEST_ACCOUNT_UIDS:
        if any(uid.encode(encoding) in data for encoding in ("ascii", "utf-16le", "utf-16be")):
            raise RuntimeError(f"R19 package entry contains a test account UID: {name}")


def verify_bridge_sources(publish: Path) -> dict[str, str]:
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
                continue
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
         "[Diagnostics.FileVersionInfo]::GetVersionInfo($env:LWBRIDGE_R19_EXE).ProductVersion"],
        env=dict(os.environ, LWBRIDGE_R19_EXE=str(exe)), text=True
    ).strip()
    if not version.endswith("+" + commit):
        raise RuntimeError("Windows executable source-commit mismatch: " + version)
    subprocess.run(["node", str(UI_CHECK), str(publish / "ProductionUi")],
                   cwd=ROOT, check=True)
    return version, verify_bridge_sources(publish)


def owner_readme(commit: str) -> str:
    return f"""LW-Control FEATURE-RECOVERY-018 R19 owner-test build
Built from commit: {commit}

HOW TO RUN
Extract the whole LW-Control-UI folder and run LWBridge.Desktop.exe.
Keep ProductionUi and OverviewBridge beside it. Needs .NET 10 Desktop
runtime, Edge WebView2 and Python. Keep the official game in Chinese.
Works on any authenticated account; no fixed test UID/server is included.
Hotkeys need the active game window and no game text box selected.

R19 CHANGES AND VERIFIED SCOPE
- R19 is a UI/CSS-only responsive-layout round. No game or game session was
  used for the R19 work and no feature wording or functional behavior changed.
- Squads / AFK cards wrap cleanly and the shared-profile list/editor stack at
  narrower widths. Equipment preset lists and squad positions reflow instead
  of clipping names or running off the page.
- Automation cards pack by their own height, category tabs wrap, and Mini Games
  fills an odd final row without leaving a half-row hole.
- The top bar remains one row at 1100 px and wider and wraps into a deliberate
  two-row layout below that width. Map Data keeps readable table minimums with
  horizontal scrolling inside the table container only. City Layout reserves
  more room for the properties panel around 1100 px.
- Headless preview qualification passed 144 combinations: nine assigned pages,
  900/1100/1400/1600 px, English/zh-CN, light/dark. The acceptance checker found
  no horizontal page overflow, out-of-viewport content or untitled clipped text.

PRESERVED R18 FUNCTIONAL STATUS
- R19 did not re-test gameplay. The accepted R18 native/functional evidence and
  owner-only physical-key/event reservations remain the applicable baseline.
- Auto Garrison and Zombie Bus Garrison remain removed by owner decision.
- City Layout native move/swap/cancel/restore and command-path Equipment Scheme
  apply/restore were previously proven and restored to their original states.
- The canonical Equipment Schemes tab remains fixture/offline rather than wired
  to the native snapshot/apply commands; R19 only fixes its responsive layout.

MONSTER AFK STATUS: NOT WORKING (UNCHANGED FROM R18)
Shared AFK profile settings persist, but the previous native status check found
no Monster AFK runtime producer: automation_start monsterSweep was rejected as
unsupported and no Monster AFK battle was performed. R19 does not change this.

PRESERVED EARLIER VERIFIED FEATURES
- G adds soldiers in Frontline/normal stages; count 1-50, default 5.
  Earlier predecessor native regression: team 4 -> 9.
- Q/W/E/R sends squads 1/2/3/4 via the game's chooser to ordinary monsters,
  empty resource fields or explicitly guildless player cities. Repeat Q/A
  cycles and paired Q/W -> A/S passed earlier live tests.
- Player attacks remain guildless-only; unknown/other-alliance cities refuse.
  Same-alliance cities use reinforcement, previously confirmed by the owner.
- A/S recall passed live. D/F use the same provider but are not separately proven.
- March speed-ups and the worker diagnostic panels remain removed.

RESERVED FOR OWNER TESTING (NOT LIVE-TESTED BY THE R19 WORKER)
- Space visual hold/release, F6/F7/F8 shields, Alt+1-4 physical keys.
- F9/F10 relocation, destroyed city ruins (requires Siege Mastery skill).
- Food House and Black Market marker when their events return.
- NEVER press Unlock Land Cell during this owner test.

A Submitted/uncertain result is not confirmation. Check the game before
trying again. This is an owner-test package, not whole-feature acceptance.
"""


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
    verify_r19_retirements()
    verify_r19_responsive_source()
    product_version, bridges = verify_published_app(publish, commit)
    source_digest, source_count = source_inputs_digest()
    files = sorted(
        (p for p in publish.rglob("*") if p.is_file() and p.suffix.lower() != ".pdb"),
        key=lambda p: p.relative_to(publish).as_posix(),
    )
    if len(files) < 25:
        raise RuntimeError("Packaged Windows app has too few runtime files")
    manifest = {
        "task": "FEATURE-RECOVERY-018", "round": "R19", "status": "OWNER_TEST",
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
            "responsiveUi": {"uiCssOnly": True, "pagesQualified": 9,
                             "widths": [900, 1100, 1400, 1600],
                             "languages": ["en", "zh-CN"], "themes": ["light", "dark"],
                             "headlessVariantsPassed": 144, "liveGameUsed": False},
            "retiredGarrison": {"autoGarrison": False, "zombieBusGarrison": False, "ownerDirection": True},
            "startupMapRows": {"allProfilesAndServersCleared": True, "settingsAndGCountPreserved": True},
            "visibleCityShields": {"nativeDataComparedToMapColumn": True, "physicalSpaceOwnerTestPending": True},
            "cityLayout": {"singleSwapCancelRestoreNativeProven": True, "all109PositionsRestored": True},
            "monsterAfk": {"runtimeAvailable": False, "statusCheck": "START_REJECTED_SHEEP_GAME_ONLY"},
            "equipmentSchemes": {"commandApplyRestoreNativeProven": True, "physicalAltKeysOwnerTestPending": True, "canonicalTabNativeWiring": False},
            "originalFeatureCardsOnly": {"workerDiagnosticPanelsRemoved": True,
                                         "removedByOwnerDirection": True},
            "frontlineG": {"configurableSoldiersPerPress": [1, 50], "default": 5,
                           "releaseEvidence": "R17_LIVE_NATIVE_TEAM_4_TO_9_HISTORICAL"},
            "hoverAttack": {"keys": "Q/W/E/R", "slots": [1, 2, 3, 4],
                            "nativeHoveredKinds": ["ordinary_monster", "ordinary_resource",
                                                   "guildless_player_city", "same_alliance_player_city",
                                                   "destroyed_city_ruin"],
                            "cityHoverImplemented": True,
                            "guildlessPlayersOnly": True,
                            "sameAllianceCityReinforcementImplemented": True,
                            "sameAllianceCityReinforcementOwnerProven": True,
                            "cityRuinImplemented": True,
                            "cityRuinLiveProven": False,
                            "releaseEvidence": "R17_LIVE_GUILDLESS_Q_A_PLUS_R16_ALLY_OWNER_TEST_HISTORICAL"},
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
    readme_name = f"{APP_FOLDER}/README-R19.txt"
    manifest["files"][readme_name] = {
        "sha256": hashlib.sha256(readme).hexdigest(), "size": len(readme)
    }
    source_name = f"{APP_FOLDER}/SOURCE-COMMIT.txt"
    source_note = (commit + "\n" + tree + "\n" + source_digest + "\n").encode("ascii")
    manifest["files"][source_name] = {
        "sha256": hashlib.sha256(source_note).hexdigest(), "size": len(source_note)
    }
    manifest_name = f"{APP_FOLDER}/R19-RELEASE-INTEGRITY.json"
    target_name = f"LW-Control-FEATURE-018-R19-OWNER-TEST-{commit[:12]}.zip"
    output.mkdir(parents=True, exist_ok=True)
    target = output / target_name
    receipt = output / "r19-package-receipt.json"
    if target.exists() or receipt.exists():
        raise RuntimeError("Existing R19 artifact preserved; choose a fresh output directory")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
        for path in files:
            archive.write(path, (Path(APP_FOLDER) / path.relative_to(publish)).as_posix())
        archive.writestr(readme_name, readme)
        archive.writestr(source_name, source_note)
        archive.writestr(manifest_name, json.dumps(manifest, indent=2).encode("utf-8"))
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise RuntimeError("R19 ZIP CRC verification failed")
        if set(archive.namelist()) != set(manifest["files"]) | {manifest_name}:
            raise RuntimeError("R19 ZIP entry set differs from release integrity manifest")
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            verify_no_test_account_bytes(name, data)
            if (len(data) != expected["size"] or
                    hashlib.sha256(data).hexdigest() != expected["sha256"]):
                raise RuntimeError("R19 ZIP payload differs from source: " + name)
        if json.loads(archive.read(manifest_name)) != manifest:
            raise RuntimeError("R19 embedded manifest mismatch")
        verify_no_test_account_bytes(manifest_name, archive.read(manifest_name))
    result = {
        "zip": str(target), "sha256": sha256(target), "bytes": target.stat().st_size,
        "zipEntries": len(manifest["files"]) + 1, "sourceCommit": commit,
        "sourceInputSha256": source_digest, "sourceInputFileCount": source_count,
        "bridgesVerified": len(bridges), "status": "OWNER_TEST",
        "testAccountUidScan": "PASS_ASCII_UTF16LE_UTF16BE_ALL_ENTRIES",
    }
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("FEATURE018_R19_PACKAGE_SOURCE_MATCHED", json.dumps(result))


if __name__ == "__main__":
    main()
