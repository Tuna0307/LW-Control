"""Package a source-verified FEATURE-RECOVERY-018 R28 Windows owner-test build.

Run only after the final code/tests/live assessment, a fresh Windows publish,
and a pushed source commit. This script only inspects local files, verifies
source identity, and writes a new ZIP and integrity receipt. It never launches
or modifies the game or builds the application.

Usage:
  python tools/feature018_r28_package.py --publish <fresh-windows-publish> \
      --output <new-release-directory> --assessment <final-assessment.json>

The final assessment contains sourceCommit, checks, qRedirect, relocation,
equipment, spaceCountdown, priorR24, limitations and ownerTests. Each feature
is an object with an explicit evidence status, summary and optional evidence.
Limitations and ownerTests are explicit lists. Do not include proof-account
numbers in owner-facing summaries or copied native evidence payloads.
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
    "OverviewBridge/feature018_chat_engine.lua",
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
        raise RuntimeError("R28 release must be packaged from " + BRANCH)
    # Package identity is defined by tracked source. Preserve unrelated local
    # workspace metadata (for example an owner's untracked .claude/ folder)
    # while still refusing any tracked source/index difference from HEAD.
    if git("status", "--porcelain", "--untracked-files=no"):
        raise RuntimeError("Commit and review all tracked source changes before R28 packaging")
    commit = git("rev-parse", "HEAD")
    if len(commit) != 40 or any(c not in "0123456789abcdef" for c in commit):
        raise RuntimeError("Invalid source commit")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()
    if len(remote) != 2 or remote[0] != commit:
        raise RuntimeError("Local source commit is not the pushed R28 checkpoint")
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
            raise RuntimeError("R28 product source missing: " + str(source))
        comment_prefix = "--" if source.suffix == ".lua" else "//"
        for line_number, line in enumerate(source.read_text(encoding="utf-8").splitlines(), 1):
            executable = line.split(comment_prefix, 1)[0]
            if LEGACY_ACCOUNT_LITERAL.search(executable):
                raise RuntimeError(
                    f"R28 shipped product still references a fixed test account/server: "
                    f"{source.relative_to(ROOT)}:{line_number}"
                )


def verify_r28_retirements() -> None:
    canonical = ROOT / "src/LWBridge.UI-0.3.17/src"
    live_equipment = (canonical / "NativeEquipmentContent.jsx").read_text(encoding="utf-8")
    for retired in ("equipment_config_get", "equipment_config_save", "equipment-plan-banner",
                    'data-equipment-action="capture"', 'data-equipment-action="save"',
                    'data-equipment-action="apply"', 'data-equipment-action="restore"'):
        if retired in live_equipment:
            raise RuntimeError("R28 live Equipment still contains plan workflow: " + retired)
    squads = (canonical / "SquadsPage.jsx").read_text(encoding="utf-8")
    if any(key in squads for key in ("garrison.", "zombieBus.", "allianceGarrison")):
        raise RuntimeError("R28 retired canonical card remains")
    for key in ("squad.afkMaster", "squad.afkAllianceDrill", "automation.autoUsePotion"):
        if key not in squads:
            raise RuntimeError("R28 retained control missing: " + key)
    if (ROOT / "src/LWBridge.Desktop/AllianceGarrisonConfigCommandService.cs").exists():
        raise RuntimeError("R28 retired command service remains")
    if (ROOT / "src/LWBridge.Desktop/Feature018ShieldStatusOverlay.cs").exists():
        raise RuntimeError("R28 retired Space shield overlay source remains")
    if (ROOT / "tools/feature018_visible_shield_checks.py").exists():
        raise RuntimeError("R28 retired visible-city shield check remains")
    hotkeys = (canonical / "previewRemainingPagesContracts.js").read_text(encoding="utf-8")
    for retired in ("shieldOverlay", 'binding: "Space"', "hotkeys.shield.title", "hotkeys.shield.description"):
        if retired in hotkeys:
            raise RuntimeError("R28 retired Space hotkey contract remains: " + retired)
    for retained in ('key: "shieldUse"', 'binding: "F6 / F7 / F8"'):
        if retained not in hotkeys:
            raise RuntimeError("R28 retained F6/F7/F8 shield-use card missing: " + retained)
    game_command = (ROOT / "src/LWBridge.Desktop/Feature018GameCommandService.cs").read_text(encoding="utf-8")
    native_hotkey = (ROOT / "tools/feature018_hotkey_game.lua").read_text(encoding="utf-8")
    config = (ROOT / "src/LWBridge.Desktop/HotkeyConfigCommandService.cs").read_text(encoding="utf-8")
    for name, text in (("game command", game_command), ("hotkey Lua", native_hotkey), ("config", config)):
        if any(retired in text for retired in
               ("shieldOverlay", "ShieldRemaining", "VisibleCityShieldSnapshot", "feature018_visible_city_shields")):
            raise RuntimeError("R28 retired Space countdown remains in " + name)
    for retained in ("shieldUse", "ShieldStatusSnapshot"):
        if retained not in native_hotkey and retained not in game_command:
            raise RuntimeError("R28 retained shield-use contract missing: " + retained)
    for code in ("en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"):
        text = (canonical / "locales" / (code + ".js")).read_text(encoding="utf-8")
        if any(key in text for key in ('"garrison.', '"zombieBus.', '"hotkeys.shield.title"',
                                       '"hotkeys.shield.description"')):
            raise RuntimeError("R28 retired canonical locale remains: " + code)
    legacy = ROOT / "src/LWBridge.Desktop/WebUi/assets"
    for source in legacy.glob("*.js"):
        if source.name.startswith(("SquadPanel-", "en-", "zh-CN-", "zh-TW-", "ja-", "ko-", "vi-", "id-", "ru-", "pt-")):
            text = source.read_text(encoding="utf-8")
            if any(key in text for key in ("garrison.", "zombieBus.", "hotkeys.shield.title",
                                           "hotkeys.shield.description", "shieldOverlay")):
                raise RuntimeError("R28 retired legacy card/locale remains: " + source.name)


def verify_r28_engine_sources() -> None:
    required = {
        "feature018_afk_engine.lua": ("function M.Start", "function M.Stop", "api.Owned", "t.player==true"),
        "feature018_afk_native.lua": ("GetSpecialResWay", "GetGoodsReturnItemCount", "function M.StartDrill", "AttackStage"),
        "feature018_stamina_engine.lua": ("countAfter", "staminaAfter", "function M.Stop"),
        "feature018_drill_engine.lua": ("same_target", "api.CancelUnsent", "target.player==true"),
        "feature018_chat_engine.lua": (
            "function M.Configure", "function M.Run", "function M.Read",
            "function M.CleanupAfterLeaseLoss", "Feature018OwnerToken",
            "CHAT_RECIEVE_ROOM_MSG", "OpenRedPacket", "GetFireworksGift",
            "DetectEventClaimTreasure", "job.sendCount=1",
            "feature018_chat_configure", "feature018_chat_run", "feature018_chat_status",
        ),
    }
    for name, markers in required.items():
        text = (ROOT / "tools" / name).read_text(encoding="utf-8")
        if any(marker not in text for marker in markers):
            raise RuntimeError("R28 engine source contract missing: " + name)


def verify_r28_feature_sources() -> None:
    required = {
        ROOT / "tools/feature018_attack_actions.lua": (
            "hotkeyRetarget=existing~=nil",
            "WorldMarchChange",
            "native_owned_formation_attack_target_transition",
            "settle_previous_attack_for_fresh_order",
            "native_redirect_target_unchanged",
            "IsInBlackRange",
            "elapsed>=3",
            "native_owned_march_redirect_unchanged_after_wait",
            "The game refused the previous redirect; no new order was sent. Press the hotkey again.",
        ),
        ROOT / "src/LWBridge.Desktop/Feature018OwnedGameExchange.cs": (
            "GAME_COMMAND_NOT_PUBLISHED", "requestDispatched = false", "attempt >= 5",
            "catch (UnauthorizedAccessException) { }",
        ),
        ROOT / "src/LWBridge.Desktop/Feature018GameCommandService.cs": (
            'case "feature018_equipment_snapshot":',
            "Equipment snapshot accepts owner routing profile only.",
        ),
        ROOT / "src/LWBridge.UI-0.3.17/scripts/check-r21-equipment.mjs": (
            "createBackendBridge",
            "mounted equipment snapshot must carry only the real profile routing field",
        ),
        ROOT / "src/LWBridge.UI-0.3.17/src/HotkeyFeedback.jsx": (
            "window.setTimeout(() => setOutcome(null), 6000)",
            'aria-label={t("common.close")}',
        ),
        ROOT / "src/LWBridge.UI-0.3.17/src/NativeEquipmentContent.jsx": (
            'data-native-equipment="true"', "createLiveEquipmentRuntime", "runtime.edit", "runtime.resume", "runtime.savePreset", "runtime.applyPreset",
            "data-equipment-level", "data-equipment-preset", "liveEquipmentPending", "liveEquipmentFileBusy",
            "window.setInterval(read, 1500)", "mutationGeneration", 'data-equipment-sync=', "data-inventory-equip",
        ),
        ROOT / "src/LWBridge.UI-0.3.17/src/equipmentRuntime.js": (
            "createLiveEquipmentRuntime", '"feature018_equipment_edit"', "queryOnly: true", "expectedWearerUuid",
            "expectedGameUid: displayed.ownerUid", "expectedServerId: displayed.serverId",
            "definitelyNotSent", "GAME_COMMAND_NOT_PUBLISHED", "GAME_RESPONSE_TIMEOUT", "readPresets", "savePreset", "applyPreset", 'pending.command', 'pending.query',
        ),
        ROOT / "src/LWBridge.Desktop/Feature018OwnedGameExchange.cs": (
            "GAME_COMMAND_NOT_PUBLISHED", "requestDispatched = false", "attempt >= 5",
            "catch (UnauthorizedAccessException) { }",
        ),
        ROOT / "src/LWBridge.Desktop/Feature018GameCommandService.cs": (
            'case "feature018_equipment_edit":', "expectedWearerUuid", "only a single explicit slot edit",
            "equipmentPreflightRoute", "profile routing only.",
        ),
        ROOT / "src/LWBridge.UI-0.3.17/src/locales/feature018-equipment-live.js": (
            '"zh-CN":', '"zh-TW":', "ja:", "ko:", "vi:", "id:", "ru:", "pt:", "en:",
            "liveEquipmentHelp", "liveEquipmentSynced", "liveEquipmentInventory",
        ),
        ROOT / "tools/feature018_equipment_actions.lua": (
            "EQUIPMENT_STATE_PENDING", "awaitingNativeEquipmentSync", "nextStepNotDispatched=true",
            "after fresh snapshot read",
            "Native hero changed during fresh equipment snapshot read",
        ),
        ROOT / "src/LWBridge.UI-0.3.17/src/CityLayoutPage.jsx": (
            "lastwar_map_localize",
            "city-layout-building-name",
            "city-layout-building-level",
            "assetPath={building.iconPath}",
            "city-layout-selected-icon",
        ),
        ROOT / "tools/feature018_city_game.lua": (
            "nameKey = building_name_key",
            "GetBuildIconPath",
            "iconPath = icon_path",
        ),
        ROOT / "tools/feature018_relocation_actions.lua": (
            "selectionConsumed",
            "selectionDispatchRefused",
            "AllianceMoveCityToSourceRallyPoint",
            "native_relocation_item_and_world_position_poststate",
            "native_alliance_relocation_item_and_world_position_poststate",
            "itemDebitedVerified=confirmed",
            "owner_alliance = alliance_id(owner_alliance)",
            'alliance_id(member(mark, "allianceId"))',
        ),
        ROOT / "src/LWBridge.Desktop/Feature018HotkeyNativeProviders.cs": (
            "native_relocation_item_and_world_position_poststate",
            "native_alliance_relocation_item_and_world_position_poststate",
            'TryGetProperty("itemDebitedVerified"',
        ),
        ROOT / "src/LWBridge.Desktop/OverviewLifecycleRecovery.cs": (
            'WriteRecoveryAudit("trigger"',
            '"bridge_offline" : "game_unhealthy"',
            "heartbeat.HeartbeatFresh",
            "heartbeat.AuthenticatedRouteConnected",
        ),
        ROOT / "src/LWBridge.Desktop/OverviewRecoveryAuditWriter.cs": (
            "MaximumFileBytes = 128 * 1024",
            '"recovery-events.previous.jsonl"',
        ),
    }
    for source, markers in required.items():
        if not source.is_file():
            raise RuntimeError("R28 feature source missing: " + str(source))
        text = source.read_text(encoding="utf-8")
        missing = [marker for marker in markers if marker not in text]
        if missing:
            raise RuntimeError(
                "R28 feature source contract missing from "
                + str(source.relative_to(ROOT)) + ": " + ", ".join(missing)
            )


def verify_no_test_account_bytes(name: str, data: bytes) -> None:
    for uid in TEST_ACCOUNT_UIDS:
        if any(uid.encode(encoding) in data for encoding in ("ascii", "utf-16le", "utf-16be")):
            raise RuntimeError(f"R28 package entry contains a test account UID: {name}")


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
         "[Diagnostics.FileVersionInfo]::GetVersionInfo($env:LWBRIDGE_R28_EXE).ProductVersion"],
        env=dict(os.environ, LWBRIDGE_R28_EXE=str(exe)), text=True
    ).strip()
    if not version.endswith("+" + commit):
        raise RuntimeError("Windows executable source-commit mismatch: " + version)
    subprocess.run(["node", str(UI_CHECK), str(publish / "ProductionUi")],
                   cwd=ROOT, check=True)
    retired_ui_markers = (b"shieldOverlay", b"hotkeys.shield.title", b"hotkeys.shield.description")
    for path in publish.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in (".js", ".html"):
            continue
        data = path.read_bytes()
        if any(marker in data for marker in retired_ui_markers):
            raise RuntimeError("Published UI still contains retired Space shield countdown: " +
                               path.relative_to(publish).as_posix())
    return version, verify_bridge_sources(publish)


def read_final_assessment(path: Path) -> dict:
    """Embed final assessed evidence, never infer native proof from packaging."""
    raw = path.read_bytes()
    verify_no_test_account_bytes("R28 final assessment", raw)
    assessment = json.loads(raw.decode("utf-8"))
    required = ("sourceCommit", "checks", "qRedirect", "relocation", "equipment",
                "spaceCountdown", "priorR24", "limitations", "ownerTests")
    if not isinstance(assessment, dict) or any(key not in assessment for key in required):
        raise RuntimeError("R28 assessment must contain: " + ", ".join(required))
    if not all(isinstance(assessment[key], (str, list, dict)) for key in required):
        raise RuntimeError("R28 assessment fields must be descriptive text, lists, or objects")
    if not isinstance(assessment["sourceCommit"], str) or not re.fullmatch(r"[0-9a-f]{40}", assessment["sourceCommit"]):
        raise RuntimeError("R28 assessment sourceCommit must be a full lowercase commit SHA")
    if not isinstance(assessment["checks"], (list, dict)) or not assessment["checks"]:
        raise RuntimeError("R28 assessment requires explicit completed checks")
    if not isinstance(assessment["limitations"], list) or not isinstance(assessment["ownerTests"], list):
        raise RuntimeError("R28 limitations and ownerTests must be explicit lists")
    if any(not isinstance(item, str) or not item.strip()
           for key in ("limitations", "ownerTests") for item in assessment[key]):
        raise RuntimeError("R28 limitations and ownerTests entries must be nonempty text")
    for key in ("qRedirect", "relocation", "equipment", "spaceCountdown", "priorR24"):
        value = assessment[key]
        if not isinstance(value, dict) or value.get("status") not in (
                "PROVEN", "RECOVERED", "REMOVED", "UNKNOWN", "PARTIAL"):
            raise RuntimeError("R28 " + key + " requires an explicit evidence status")
        if not isinstance(value.get("summary"), str) or not value["summary"].strip():
            raise RuntimeError("R28 " + key + " requires a plain-language summary")
    return assessment


def owner_readme(commit: str, assessment: dict) -> str:
    sections = []
    for title, key in (("Q/W/E/R REDIRECT RECOVERY", "qRedirect"),
                       ("F9/F10 RELOCATION CONFIRMATION", "relocation"),
                       ("EQUIPMENT SCHEMES", "equipment"),
                       ("SPACE SHIELD COUNTDOWN", "spaceCountdown"),
                       ("PRIOR R24 FEATURES", "priorR24")):
        item = assessment[key]
        sections.append(title + " [" + item["status"] + "]\n" + item["summary"])
    limitations = "\n".join("- " + str(item) for item in assessment["limitations"])
    owner_tests = "\n".join("- " + str(item) for item in assessment["ownerTests"])
    return f"""LW-Control FEATURE-RECOVERY-018 R28 owner-test build
Built from commit: {commit}

RUN
Extract the complete LW-Control-UI folder and run LWBridge.Desktop.exe.
Keep ProductionUi and OverviewBridge beside it. Requires .NET 10 Desktop
runtime, Edge WebView2 and Python. Leave the official game in Chinese.
No fixed test UID/server is embedded. Actions use the authenticated profile's
captured owner/session; changing account pauses running engines.

{"\n\n".join(sections)}

HOW TO USE EQUIPMENT AND PRESETS
1. Open Squads / AFK > Equipment Schemes. Gear slots and inventory show native
   equipment levels. One page Refresh remains; game changes also refresh automatically.
2. Choose gear or Empty to equip/remove immediately in the game. Drag gear to
   another hero's matching slot to move it; displaced gear returns to inventory.
3. Select preset 1, 2, 3 or 4 and enter its name. Save current gear to preset
   captures the gear actually worn by every squad. Saving does not change gear.
4. Equip preset recalls that saved gear immediately. Alt+1, Alt+2, Alt+3 and Alt+4
   use the same saved preset ordinals when the Equipment hotkey toggle is enabled.
5. Continue live gear editing as usual. Presets change only when explicitly saved;
   normal gear edits never overwrite them. Pending changes must confirm before another edit.

FILE LOCK RECOVERY
A brief game command file lock is retried before sending. If it remains busy,
no change is sent and editing remains available. After an uncertain send, use
Resume confirmation beside the waiting status; the original change is never replayed.

EVIDENCE AND LIMITS
PROVEN means the cited behavior has actual evidence. RECOVERED means the
    implementation follows recovered original/current-game behavior. REMOVED means
    the owner directed that feature out of the product and removal checks passed.
    UNKNOWN and PARTIAL identify unproved outcomes. Packaging checks file/source
    identity and account-literal absence; it does not run gameplay or establish live acceptance.
See R28-FINAL-ASSESSMENT.json and the lead audit for exact evidence and checks.
{limitations or "- No additional limitations declared in the final assessment."}

OWNER TESTS
{owner_tests or "- No additional owner tests declared in the final assessment."}

PRESERVED LIMITS
Auto Garrison and Zombie Bus Garrison stay removed. Startup clears map scan rows
but retains profiles/settings/G count. Prior R20 Monster AFK, stamina-item and
Alliance Drill behavior is preserved. Event availability controls live proof;
submission or an uncertain outcome is never confirmation. No real money or
gem purchase route is used by the Chat engines. Space no longer has a Hotkeys
action; F6/F7/F8 shield item use remains. NEVER Unlock Land Cell.
This is an owner-test package; independent lead acceptance is separate.
"""


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", required=True, type=Path,
                        help="Source-verified, freshly compiled Windows publish directory")
    parser.add_argument("--assessment", required=True, type=Path,
                        help="Final evidence assessment JSON pinned to the pushed source commit")
    parser.add_argument("--output", required=True, type=Path,
                        help="New release directory outside the Windows publish folder")
    args = parser.parse_args()
    commit, tree = clean_pushed_commit()
    assessment = read_final_assessment(args.assessment)
    if assessment["sourceCommit"] != commit:
        raise RuntimeError("R28 assessment source commit differs from the pushed checkpoint")
    publish = args.publish.resolve(strict=True)
    output = args.output.resolve()
    if output == publish or publish in output.parents or output in publish.parents:
        raise RuntimeError("Release output must not overlap the Windows publish folder")
    verify_account_agnostic_sources()
    verify_r28_retirements()
    verify_r28_engine_sources()
    verify_r28_feature_sources()
    product_version, bridges = verify_published_app(publish, commit)
    source_digest, source_count = source_inputs_digest()
    files = sorted(
        (p for p in publish.rglob("*") if p.is_file() and p.suffix.lower() != ".pdb"),
        key=lambda p: p.relative_to(publish).as_posix(),
    )
    if len(files) < 25:
        raise RuntimeError("Packaged Windows app has too few runtime files")
    manifest = {
        "task": "FEATURE-RECOVERY-018", "round": "R28", "status": "OWNER_TEST",
        "branch": BRANCH, "sourceCommit": commit, "sourceGitTree": tree,
        "sourceInputSha256": source_digest, "sourceInputFileCount": source_count,
        "sourceInputHashMethod": "SHA256(sorted git-tracked src/ and tools/: UTF8 path + NUL + file bytes + NUL)",
        "windowsProductVersion": product_version,
        "productionUiSourceAndArtifactFingerprintVerified": True,
        "desktopLinkedBridgeSha256": bridges,
        "supportedAccountScope": "any authenticated account; same UID/server throughout action",
        "fixedProofAccountLiteralPreflight": True,
        "liveGameplayValidatedByPackaging": False,
        "finalAssessment": assessment,
        "features": {
            "hotkeyRetarget": {"nativeWorldMarchChange": True, "blackTilePreflight": True,
                               "unchangedRedirectRetiresAfterSeconds": 3, "automaticReplay": False},
            "equipmentSchemes": {"liveGameSync": True, "immediateEquipAndRemove": True,
                "dragMovesInsteadOfSwaps": True, "autoReadIntervalMs": 1500,
                "liveEditsDoNotOverwritePresets": True, "savedAltOrdinals": 4,
                "nativeGearLevels": True, "onePageRefresh": True, "singleOperationConfirmationFence": True},
            "relocation": {"f9AndF10AfterState": "city point changed + exact item debit",
                           "oneSendFence": True},
            "spaceShieldCountdown": {"removedByOwnerDirection": True, "f6F7F8ShieldUseRetained": True},
            "priorR24Evidence": "See the audit; inherited evidence is not new R28 live proof",
            "evidenceLabels": {"scheduling": "OWN_DESIGN", "nativeRoutes": "RECOVERED"},
        },
        "files": {},
    }
    for path in files:
        verify_no_test_account_bytes(path.relative_to(publish).as_posix(), path.read_bytes())
        name = (Path(APP_FOLDER) / path.relative_to(publish)).as_posix()
        manifest["files"][name] = {"sha256": sha256(path), "size": path.stat().st_size}
    readme = owner_readme(commit, assessment).encode("utf-8")
    assessment_bytes = (json.dumps(assessment, indent=2) + "\n").encode("utf-8")
    assessment_name = f"{APP_FOLDER}/R28-FINAL-ASSESSMENT.json"
    verify_no_test_account_bytes(assessment_name, assessment_bytes)
    manifest["files"][assessment_name] = {
        "sha256": hashlib.sha256(assessment_bytes).hexdigest(), "size": len(assessment_bytes)
    }
    readme_name = f"{APP_FOLDER}/README-R28.txt"
    manifest["files"][readme_name] = {
        "sha256": hashlib.sha256(readme).hexdigest(), "size": len(readme)
    }
    source_name = f"{APP_FOLDER}/SOURCE-COMMIT.txt"
    source_note = (commit + "\n" + tree + "\n" + source_digest + "\n").encode("ascii")
    manifest["files"][source_name] = {
        "sha256": hashlib.sha256(source_note).hexdigest(), "size": len(source_note)
    }
    manifest_name = f"{APP_FOLDER}/R28-RELEASE-INTEGRITY.json"
    target_name = f"LW-Control-FEATURE-018-R28-OWNER-TEST-{commit[:12]}.zip"
    output.mkdir(parents=True, exist_ok=True)
    target = output / target_name
    receipt = output / "r28-package-receipt.json"
    if target.exists() or receipt.exists():
        raise RuntimeError("Existing R28 artifact preserved; choose a fresh output directory")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
        for path in files:
            archive.write(path, (Path(APP_FOLDER) / path.relative_to(publish)).as_posix())
        archive.writestr(readme_name, readme)
        archive.writestr(assessment_name, assessment_bytes)
        archive.writestr(source_name, source_note)
        archive.writestr(manifest_name, json.dumps(manifest, indent=2).encode("utf-8"))
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise RuntimeError("R28 ZIP CRC verification failed")
        if set(archive.namelist()) != set(manifest["files"]) | {manifest_name}:
            raise RuntimeError("R28 ZIP entry set differs from release integrity manifest")
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            verify_no_test_account_bytes(name, data)
            if (len(data) != expected["size"] or
                    hashlib.sha256(data).hexdigest() != expected["sha256"]):
                raise RuntimeError("R28 ZIP payload differs from source: " + name)
        if json.loads(archive.read(manifest_name)) != manifest:
            raise RuntimeError("R28 embedded manifest mismatch")
        verify_no_test_account_bytes(manifest_name, archive.read(manifest_name))
    result = {
        "zip": str(target), "sha256": sha256(target), "bytes": target.stat().st_size,
        "zipEntries": len(manifest["files"]) + 1, "sourceCommit": commit,
        "sourceInputSha256": source_digest, "sourceInputFileCount": source_count,
        "bridgesVerified": len(bridges), "status": "OWNER_TEST",
        "testAccountUidScan": "PASS_ASCII_UTF16LE_UTF16BE_ALL_ENTRIES",
    }
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("FEATURE018_R28_PACKAGE_SOURCE_MATCHED", json.dumps(result))


if __name__ == "__main__":
    main()
