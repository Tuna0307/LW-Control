"""Package a lead-verified FEATURE-RECOVERY-018 R20 Windows owner-test build.

Run only after the final code/tests/live assessment, a fresh Windows publish,
and a pushed source commit. This script only inspects local files, verifies
source identity, and writes a new ZIP and integrity receipt. It never launches
or modifies the game or builds the application.

Usage:
  python tools/feature018_r20_package.py --publish <fresh-windows-publish> \
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
    "OverviewBridge/feature018_afk_engine.lua",
    "OverviewBridge/feature018_afk_native.lua",
    "OverviewBridge/feature018_stamina_engine.lua",
    "OverviewBridge/feature018_drill_engine.lua",
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
        raise RuntimeError("R20 release must be packaged from " + BRANCH)
    # Package identity is defined by tracked source. Preserve unrelated local
    # workspace metadata (for example an owner's untracked .claude/ folder)
    # while still refusing any tracked source/index difference from HEAD.
    if git("status", "--porcelain", "--untracked-files=no"):
        raise RuntimeError("Commit and review all tracked source changes before R20 packaging")
    commit = git("rev-parse", "HEAD")
    if len(commit) != 40 or any(c not in "0123456789abcdef" for c in commit):
        raise RuntimeError("Invalid source commit")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()
    if len(remote) != 2 or remote[0] != commit:
        raise RuntimeError("Local source commit is not the pushed R20 checkpoint")
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
            raise RuntimeError("R20 product source missing: " + str(source))
        comment_prefix = "--" if source.suffix == ".lua" else "//"
        for line_number, line in enumerate(source.read_text(encoding="utf-8").splitlines(), 1):
            executable = line.split(comment_prefix, 1)[0]
            if LEGACY_ACCOUNT_LITERAL.search(executable):
                raise RuntimeError(
                    f"R20 shipped product still references a fixed test account/server: "
                    f"{source.relative_to(ROOT)}:{line_number}"
                )


def verify_r20_retirements() -> None:
    canonical = ROOT / "src/LWBridge.UI-0.3.17/src"
    squads = (canonical / "SquadsPage.jsx").read_text(encoding="utf-8")
    if any(key in squads for key in ("garrison.", "zombieBus.", "allianceGarrison")):
        raise RuntimeError("R20 retired canonical card remains")
    for key in ("squad.afkMaster", "squad.afkAllianceDrill", "automation.autoUsePotion"):
        if key not in squads:
            raise RuntimeError("R20 retained control missing: " + key)
    if (ROOT / "src/LWBridge.Desktop/AllianceGarrisonConfigCommandService.cs").exists():
        raise RuntimeError("R20 retired command service remains")
    for code in ("en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"):
        text = (canonical / "locales" / (code + ".js")).read_text(encoding="utf-8")
        if any(key in text for key in ('"garrison.', '"zombieBus.')):
            raise RuntimeError("R20 retired canonical locale remains: " + code)
    legacy = ROOT / "src/LWBridge.Desktop/WebUi/assets"
    for source in legacy.glob("*.js"):
        if source.name.startswith(("SquadPanel-", "en-", "zh-CN-", "zh-TW-", "ja-", "ko-", "vi-", "id-", "ru-", "pt-")):
            text = source.read_text(encoding="utf-8")
            if any(key in text for key in ("garrison.", "zombieBus.")):
                raise RuntimeError("R20 retired legacy card/locale remains: " + source.name)


def verify_r20_engine_sources() -> None:
    required = {
        "feature018_afk_engine.lua": ("function M.Start", "function M.Stop", "api.Owned", "t.player==true"),
        "feature018_afk_native.lua": ("GetSpecialResWay", "GetGoodsReturnItemCount", "function M.StartDrill", "AttackStage"),
        "feature018_stamina_engine.lua": ("countAfter", "staminaAfter", "function M.Stop"),
        "feature018_drill_engine.lua": ("same_target", "api.CancelUnsent", "target.player==true"),
    }
    for name, markers in required.items():
        text = (ROOT / "tools" / name).read_text(encoding="utf-8")
        if any(marker not in text for marker in markers):
            raise RuntimeError("R20 engine source contract missing: " + name)


def verify_no_test_account_bytes(name: str, data: bytes) -> None:
    for uid in TEST_ACCOUNT_UIDS:
        if any(uid.encode(encoding) in data for encoding in ("ascii", "utf-16le", "utf-16be")):
            raise RuntimeError(f"R20 package entry contains a test account UID: {name}")


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
         "[Diagnostics.FileVersionInfo]::GetVersionInfo($env:LWBRIDGE_R20_EXE).ProductVersion"],
        env=dict(os.environ, LWBRIDGE_R20_EXE=str(exe)), text=True
    ).strip()
    if not version.endswith("+" + commit):
        raise RuntimeError("Windows executable source-commit mismatch: " + version)
    subprocess.run(["node", str(UI_CHECK), str(publish / "ProductionUi")],
                   cwd=ROOT, check=True)
    return version, verify_bridge_sources(publish)


def owner_readme(commit: str) -> str:
    return f"""LW-Control FEATURE-RECOVERY-018 R20 owner-test build
Built from commit: {commit}

RUN
Extract the complete LW-Control-UI folder and run LWBridge.Desktop.exe.
Keep ProductionUi and OverviewBridge beside it. Requires .NET 10 Desktop
runtime, Edge WebView2 and Python. Leave the official game in Chinese.
No fixed test UID/server is embedded. Every action captures the authenticated
profile account/session; changing account pauses the engines.

MONSTER AFK
Connect/start the selected profile's game. In Squads / AFK add a Shared AFK
Profile and select a current native target. Choose assigned squads, level and
distance limits, execution count (0 = unlimited) and continuous attack if wanted.
Enable the profile and Monster AFK master. The count is shared across that
profile's squads. Saving an already completed profile does not reset its count;
an explicit new Start starts a new run. Turn the master off to stop new sends;
already dispatched marches are allowed to return. Status shows target/count/waits.
Launch rally uses the game's formation chooser; Join Alliance rallies uses its
eligible allied monster rallies and the profile's timing/leader restrictions.
These engines never target players. Native rally permissions still apply.
Continuous attack retargets the existing march before it reaches HQ; the native
server may briefly label it Returning. It does not create a fresh home march.

AUTOMATIC STAMINA ITEMS
Enable Use Stamina items automatically and set a threshold. Below it, the engine
uses owned Energy items only, one at a time. Prefer 50-point item honors that
explicit preference; otherwise the smallest owned recovery item is selected.
There is no gem/shop purchase route. A use counts only when native count falls
by exactly one AND stamina rises. An unresolved send blocks retries. Disable
the card to stop new uses. Pending confirmation pauses new Monster/Drill sends.

AUTO ALLIANCE DRILL
Select squads in priority order, set Active Rally and join restrictions, then
enable the Drill card. It is independent of the Monster AFK master. During the
native AttackStage, the first free priority squad leads if Active Rally is on;
others join eligible allied rallies on the exact Drill monster. Join-only mode
never leads. The current game rejects joining a rally led by the same account,
so other squads wait for allied leaders instead of bypassing that restriction.
It waits when the event is inactive; event end/target change/Stop prevents new
sends and discards unsent choosers. Already sent marches are not recalled.

PROOF AND LIMITS
Monster AFK passed a background packaged three-attack level-10 ordinary zombie
run and self-stop, a same-march two-attempt continuous run, and Stop in flight
with ten later idle observations. Scripts were restored after exit.
Native rally launch/join routes and Drill lifecycle are implemented and covered
by source-shaped inert checks; a live event positive depends on availability.
Current Energy inventory matched the owner's backpack. Automatic use passed
with threshold 111: one 10-point item, native count 333 -> 332, stamina 110 -> 120;
50-point stock stayed 258 and no gem purchase occurred. The old empty-category
reader was corrected. Drill was armed against native inactive stage 1, waited
with zero sends, then stopped. Active-event rally behavior remains owner-test
pending. Inspect the external package receipt for the final source identity. This package does not
claim owner-only physical keys or events have passed new live testing.
The scheduling algorithms are OWN_DESIGN; original UI/settings and current
native routes are RECOVERED. No original provider-engine equivalence is claimed.

PRESERVED LIMITS
Auto Garrison and Zombie Bus Garrison stay removed. Startup clears map scan rows
but retains profiles/settings/G count. R18 City Layout and command-path Equipment
Scheme apply/restore proofs are preserved. The Equipment Schemes tab still needs
native UI wiring; unrelated Automation tasks are not claimed implemented.
Space physical hold/release, F6/F7/F8, Alt+1-4, F9/F10, city ruins, Food House and
Black Market events retain their owner-test reservations. NEVER Unlock Land Cell.
A Submitted/uncertain result is not confirmation. This is an owner-test package;
lead acceptance is separate.
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
    verify_r20_retirements()
    verify_r20_engine_sources()
    product_version, bridges = verify_published_app(publish, commit)
    source_digest, source_count = source_inputs_digest()
    files = sorted(
        (p for p in publish.rglob("*") if p.is_file() and p.suffix.lower() != ".pdb"),
        key=lambda p: p.relative_to(publish).as_posix(),
    )
    if len(files) < 25:
        raise RuntimeError("Packaged Windows app has too few runtime files")
    manifest = {
        "task": "FEATURE-RECOVERY-018", "round": "R20", "status": "OWNER_TEST",
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
            "monsterAfk": {"runtimeAvailable": True, "ordinaryLevel10ThreeAttackSelfStopNativeProven": True,
                           "continuousSameMarchNativeProven": True, "stopInFlightNativeProven": True,
                           "rallyRoutesInertQualified": True, "liveRallyPositive": False},
            "staminaPotion": {"runtimeAvailable": True, "ownedOnly": True, "purchaseRoute": False,
                              "confirmation": "native_count_minus_one_AND_stamina_increase",
                              "oneItemNativeProven": True, "nativeCount": [333, 332], "nativeStamina": [110, 120]},
            "allianceDrill": {"runtimeAvailable": True, "eventLifecycleInertQualified": True,
                              "respectsNativeOwnLeaderJoinRejection": True, "inactiveNativeWaitStopProven": True,
                              "activeEventLivePositive": False, "ownerEventTestPending": True},
            "evidenceLabels": {"scheduling": "OWN_DESIGN", "uiSettingsAndNativeRoutes": "RECOVERED"},
        },
        "files": {},
    }
    for path in files:
        verify_no_test_account_bytes(path.relative_to(publish).as_posix(), path.read_bytes())
        name = (Path(APP_FOLDER) / path.relative_to(publish)).as_posix()
        manifest["files"][name] = {"sha256": sha256(path), "size": path.stat().st_size}
    readme = owner_readme(commit).encode("utf-8")
    readme_name = f"{APP_FOLDER}/README-R20.txt"
    manifest["files"][readme_name] = {
        "sha256": hashlib.sha256(readme).hexdigest(), "size": len(readme)
    }
    source_name = f"{APP_FOLDER}/SOURCE-COMMIT.txt"
    source_note = (commit + "\n" + tree + "\n" + source_digest + "\n").encode("ascii")
    manifest["files"][source_name] = {
        "sha256": hashlib.sha256(source_note).hexdigest(), "size": len(source_note)
    }
    manifest_name = f"{APP_FOLDER}/R20-RELEASE-INTEGRITY.json"
    target_name = f"LW-Control-FEATURE-018-R20-OWNER-TEST-{commit[:12]}.zip"
    output.mkdir(parents=True, exist_ok=True)
    target = output / target_name
    receipt = output / "r20-package-receipt.json"
    if target.exists() or receipt.exists():
        raise RuntimeError("Existing R20 artifact preserved; choose a fresh output directory")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
        for path in files:
            archive.write(path, (Path(APP_FOLDER) / path.relative_to(publish)).as_posix())
        archive.writestr(readme_name, readme)
        archive.writestr(source_name, source_note)
        archive.writestr(manifest_name, json.dumps(manifest, indent=2).encode("utf-8"))
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise RuntimeError("R20 ZIP CRC verification failed")
        if set(archive.namelist()) != set(manifest["files"]) | {manifest_name}:
            raise RuntimeError("R20 ZIP entry set differs from release integrity manifest")
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            verify_no_test_account_bytes(name, data)
            if (len(data) != expected["size"] or
                    hashlib.sha256(data).hexdigest() != expected["sha256"]):
                raise RuntimeError("R20 ZIP payload differs from source: " + name)
        if json.loads(archive.read(manifest_name)) != manifest:
            raise RuntimeError("R20 embedded manifest mismatch")
        verify_no_test_account_bytes(manifest_name, archive.read(manifest_name))
    result = {
        "zip": str(target), "sha256": sha256(target), "bytes": target.stat().st_size,
        "zipEntries": len(manifest["files"]) + 1, "sourceCommit": commit,
        "sourceInputSha256": source_digest, "sourceInputFileCount": source_count,
        "bridgesVerified": len(bridges), "status": "OWNER_TEST",
        "testAccountUidScan": "PASS_ASCII_UTF16LE_UTF16BE_ALL_ENTRIES",
    }
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("FEATURE018_R20_PACKAGE_SOURCE_MATCHED", json.dumps(result))


if __name__ == "__main__":
    main()
