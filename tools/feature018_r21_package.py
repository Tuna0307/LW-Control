"""Package a lead-verified FEATURE-RECOVERY-018 R21 Windows owner-test build.

Run only after the final code/tests/live assessment, a fresh Windows publish,
and a pushed source commit. This script only inspects local files, verifies
source identity, and writes a new ZIP and integrity receipt. It never launches
or modifies the game or builds the application.

Usage:
  python tools/feature018_r21_package.py --publish <fresh-windows-publish> \
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
        raise RuntimeError("R21 release must be packaged from " + BRANCH)
    # Package identity is defined by tracked source. Preserve unrelated local
    # workspace metadata (for example an owner's untracked .claude/ folder)
    # while still refusing any tracked source/index difference from HEAD.
    if git("status", "--porcelain", "--untracked-files=no"):
        raise RuntimeError("Commit and review all tracked source changes before R21 packaging")
    commit = git("rev-parse", "HEAD")
    if len(commit) != 40 or any(c not in "0123456789abcdef" for c in commit):
        raise RuntimeError("Invalid source commit")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()
    if len(remote) != 2 or remote[0] != commit:
        raise RuntimeError("Local source commit is not the pushed R21 checkpoint")
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
            raise RuntimeError("R21 product source missing: " + str(source))
        comment_prefix = "--" if source.suffix == ".lua" else "//"
        for line_number, line in enumerate(source.read_text(encoding="utf-8").splitlines(), 1):
            executable = line.split(comment_prefix, 1)[0]
            if LEGACY_ACCOUNT_LITERAL.search(executable):
                raise RuntimeError(
                    f"R21 shipped product still references a fixed test account/server: "
                    f"{source.relative_to(ROOT)}:{line_number}"
                )


def verify_r21_retirements() -> None:
    canonical = ROOT / "src/LWBridge.UI-0.3.17/src"
    squads = (canonical / "SquadsPage.jsx").read_text(encoding="utf-8")
    if any(key in squads for key in ("garrison.", "zombieBus.", "allianceGarrison")):
        raise RuntimeError("R21 retired canonical card remains")
    for key in ("squad.afkMaster", "squad.afkAllianceDrill", "automation.autoUsePotion"):
        if key not in squads:
            raise RuntimeError("R21 retained control missing: " + key)
    if (ROOT / "src/LWBridge.Desktop/AllianceGarrisonConfigCommandService.cs").exists():
        raise RuntimeError("R21 retired command service remains")
    for code in ("en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"):
        text = (canonical / "locales" / (code + ".js")).read_text(encoding="utf-8")
        if any(key in text for key in ('"garrison.', '"zombieBus.')):
            raise RuntimeError("R21 retired canonical locale remains: " + code)
    legacy = ROOT / "src/LWBridge.Desktop/WebUi/assets"
    for source in legacy.glob("*.js"):
        if source.name.startswith(("SquadPanel-", "en-", "zh-CN-", "zh-TW-", "ja-", "ko-", "vi-", "id-", "ru-", "pt-")):
            text = source.read_text(encoding="utf-8")
            if any(key in text for key in ("garrison.", "zombieBus.")):
                raise RuntimeError("R21 retired legacy card/locale remains: " + source.name)


def verify_r21_engine_sources() -> None:
    required = {
        "feature018_afk_engine.lua": ("function M.Start", "function M.Stop", "api.Owned", "t.player==true"),
        "feature018_afk_native.lua": ("GetSpecialResWay", "GetGoodsReturnItemCount", "function M.StartDrill", "AttackStage"),
        "feature018_stamina_engine.lua": ("countAfter", "staminaAfter", "function M.Stop"),
        "feature018_drill_engine.lua": ("same_target", "api.CancelUnsent", "target.player==true"),
    }
    for name, markers in required.items():
        text = (ROOT / "tools" / name).read_text(encoding="utf-8")
        if any(marker not in text for marker in markers):
            raise RuntimeError("R21 engine source contract missing: " + name)


def verify_r21_feature_sources() -> None:
    required = {
        ROOT / "tools/feature018_attack_actions.lua": (
            "hotkeyRetarget=existing~=nil",
            "WorldMarchChange",
            "native_owned_formation_attack_target_transition",
        ),
        ROOT / "src/LWBridge.UI-0.3.17/src/HotkeyFeedback.jsx": (
            "window.setTimeout(() => setOutcome(null), 6000)",
            'aria-label={t("common.close")}',
        ),
        ROOT / "src/LWBridge.UI-0.3.17/src/NativeEquipmentContent.jsx": (
            'data-native-equipment="true"',
            "equipmentPresets",
            'data-equipment-action="apply"',
            'data-equipment-action="restore"',
        ),
        ROOT / "src/LWBridge.UI-0.3.17/src/CityLayoutPage.jsx": (
            "lastwar_map_localize",
            "city-layout-building-name",
            "city-layout-building-level",
        ),
        ROOT / "tools/feature018_city_game.lua": (
            "nameKey = building_name_key",
        ),
        ROOT / "tools/feature018_relocation_actions.lua": (
            "selectionConsumed",
            "selectionDispatchRefused",
            "AllianceMoveCityToSourceRallyPoint",
            "serverAckAttributableByOneSendFence",
        ),
    }
    for source, markers in required.items():
        if not source.is_file():
            raise RuntimeError("R21 feature source missing: " + str(source))
        text = source.read_text(encoding="utf-8")
        missing = [marker for marker in markers if marker not in text]
        if missing:
            raise RuntimeError(
                "R21 feature source contract missing from "
                + str(source.relative_to(ROOT)) + ": " + ", ".join(missing)
            )


def verify_no_test_account_bytes(name: str, data: bytes) -> None:
    for uid in TEST_ACCOUNT_UIDS:
        if any(uid.encode(encoding) in data for encoding in ("ascii", "utf-16le", "utf-16be")):
            raise RuntimeError(f"R21 package entry contains a test account UID: {name}")


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
         "[Diagnostics.FileVersionInfo]::GetVersionInfo($env:LWBRIDGE_R21_EXE).ProductVersion"],
        env=dict(os.environ, LWBRIDGE_R21_EXE=str(exe)), text=True
    ).strip()
    if not version.endswith("+" + commit):
        raise RuntimeError("Windows executable source-commit mismatch: " + version)
    subprocess.run(["node", str(UI_CHECK), str(publish / "ProductionUi")],
                   cwd=ROOT, check=True)
    return version, verify_bridge_sources(publish)


def owner_readme(commit: str) -> str:
    return f"""LW-Control FEATURE-RECOVERY-018 R21 owner-test build
Built from commit: {commit}

RUN
Extract the complete LW-Control-UI folder and run LWBridge.Desktop.exe.
Keep ProductionUi and OverviewBridge beside it. Requires .NET 10 Desktop
runtime, Edge WebView2 and Python. Leave the official game in Chinese.
No fixed test UID/server is embedded. Every action captures the authenticated
profile account/session; changing account pauses the engines.

Q/W/E/R RE-TARGET
If the selected owned squad already has a native march, Q/W/E/R uses the game's
original WorldMarchChange route instead of launching a second march. The app
rechecks the exact account, formation, existing march, target and alliance at
the send edge, then confirms the same march changed target from native poststate.
Moving and returning owned marches are supported; a broken or changed march is
refused instead of silently starting another one.

HOTKEY ERROR BANNER
Failed hotkey actions show a dismissible in-app error banner for about six
seconds. The banner can be closed immediately and is retired when its page/tab
or owner route changes. Successful hotkey actions remain silent.

EQUIPMENT SCHEMES
Open Squads > Equipment Schemes. The tab reads current native hero positions and
equipment, supports four saved profile-scoped ordinals, can overwrite/save a
scheme, apply it to the configured squads, and restore the trusted initial
equipment snapshot. Alt+1 through Alt+4 apply the corresponding saved ordinal.
Apply/restore uses the persisted owner configuration and native confirmation;
caller-supplied gear UUIDs are not trusted as authority.

CITY LAYOUT NAMES
City Layout reads each building's localization key from the native game snapshot
and resolves it against the installed game text for the app's selected language.
At 100% zoom, buildings show readable numeric levels and names where space allows;
the full localized name plus level remains in the building tooltip/accessibility
label. If installed text cannot be read, the app reports the name-source error
and offers retry instead of treating an item number as a translated name.

F10 ALLIANCE RELOCATION
F10 follows the recovered original alliance-relocation route. For a same-home
alliance rally it sends the original AllianceMoveCity request with cost type 4;
the prior inverted equality-branch recovery that suppressed this valid route is
fixed. Before the send and again at the network edge, the adapter checks the
same authenticated owner, source/self/current server, alliance, city position,
exact rally target and owned relocation item. Native warning/cooldown/item
preconditions still apply. The chooser and direct paths keep a one-send tombstone,
reject foreign/cross-server route changes, and require the correlated reason-240
reply plus independent world-position/item-debit poststate before confirmation.
If any identity, target or observer changes, the result stays unconfirmed and
there is no automatic retry.

PROOF AND LIMITS
R21 Tasks 1-3 have lead-recorded native proof for re-target, hotkey error
feedback and Equipment Schemes. City-name and F10 code is source-backed and
covered by the shipped offline checks; live acceptance is recorded separately
in the lead audit because packaging itself does not run gameplay. Inspect the
external package receipt for final source identity. A Submitted/uncertain action
is not confirmation.

PRESERVED LIMITS
Auto Garrison and Zombie Bus Garrison stay removed. Startup clears map scan rows
but retains profiles/settings/G count. Existing R20 Monster AFK, stamina-item and
Alliance Drill behavior is preserved. F6/F7/F8, F9, city ruins, Food House and
Black Market event positives retain their existing owner-test reservations.
NEVER Unlock Land Cell. This is an owner-test package; lead acceptance is separate.
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
    verify_r21_retirements()
    verify_r21_engine_sources()
    verify_r21_feature_sources()
    product_version, bridges = verify_published_app(publish, commit)
    source_digest, source_count = source_inputs_digest()
    files = sorted(
        (p for p in publish.rglob("*") if p.is_file() and p.suffix.lower() != ".pdb"),
        key=lambda p: p.relative_to(publish).as_posix(),
    )
    if len(files) < 25:
        raise RuntimeError("Packaged Windows app has too few runtime files")
    manifest = {
        "task": "FEATURE-RECOVERY-018", "round": "R21", "status": "OWNER_TEST",
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
            "hotkeyRetarget": {"runtimeAvailable": True, "nativeWorldMarchChange": True,
                               "sameMarchPoststateConfirmation": True},
            "hotkeyErrorBanner": {"runtimeAvailable": True, "expiryMilliseconds": 6000,
                                  "dismissible": True, "successSilent": True},
            "equipmentSchemes": {"runtimeAvailable": True, "savedOrdinals": 4,
                                 "trustedInitialRestore": True, "profileScoped": True},
            "cityLayoutNames": {"runtimeAvailable": True, "installedGameText": True,
                                "appLanguage": True, "fullTooltip": True},
            "allianceRelocateF10": {"runtimeAvailable": True, "sameHomeNativeRoute": True,
                                    "oneSendFence": True, "reason240AndPoststateConfirmation": True,
                                    "foreignRouteFailsClosed": True},
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
    readme_name = f"{APP_FOLDER}/README-R21.txt"
    manifest["files"][readme_name] = {
        "sha256": hashlib.sha256(readme).hexdigest(), "size": len(readme)
    }
    source_name = f"{APP_FOLDER}/SOURCE-COMMIT.txt"
    source_note = (commit + "\n" + tree + "\n" + source_digest + "\n").encode("ascii")
    manifest["files"][source_name] = {
        "sha256": hashlib.sha256(source_note).hexdigest(), "size": len(source_note)
    }
    manifest_name = f"{APP_FOLDER}/R21-RELEASE-INTEGRITY.json"
    target_name = f"LW-Control-FEATURE-018-R21-OWNER-TEST-{commit[:12]}.zip"
    output.mkdir(parents=True, exist_ok=True)
    target = output / target_name
    receipt = output / "r21-package-receipt.json"
    if target.exists() or receipt.exists():
        raise RuntimeError("Existing R21 artifact preserved; choose a fresh output directory")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
        for path in files:
            archive.write(path, (Path(APP_FOLDER) / path.relative_to(publish)).as_posix())
        archive.writestr(readme_name, readme)
        archive.writestr(source_name, source_note)
        archive.writestr(manifest_name, json.dumps(manifest, indent=2).encode("utf-8"))
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise RuntimeError("R21 ZIP CRC verification failed")
        if set(archive.namelist()) != set(manifest["files"]) | {manifest_name}:
            raise RuntimeError("R21 ZIP entry set differs from release integrity manifest")
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            verify_no_test_account_bytes(name, data)
            if (len(data) != expected["size"] or
                    hashlib.sha256(data).hexdigest() != expected["sha256"]):
                raise RuntimeError("R21 ZIP payload differs from source: " + name)
        if json.loads(archive.read(manifest_name)) != manifest:
            raise RuntimeError("R21 embedded manifest mismatch")
        verify_no_test_account_bytes(manifest_name, archive.read(manifest_name))
    result = {
        "zip": str(target), "sha256": sha256(target), "bytes": target.stat().st_size,
        "zipEntries": len(manifest["files"]) + 1, "sourceCommit": commit,
        "sourceInputSha256": source_digest, "sourceInputFileCount": source_count,
        "bridgesVerified": len(bridges), "status": "OWNER_TEST",
        "testAccountUidScan": "PASS_ASCII_UTF16LE_UTF16BE_ALL_ENTRIES",
    }
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("FEATURE018_R21_PACKAGE_SOURCE_MATCHED", json.dumps(result))


if __name__ == "__main__":
    main()
