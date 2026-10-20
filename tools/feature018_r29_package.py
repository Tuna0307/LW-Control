"""Source-verified R29 owner-test ZIP; never launches the app/game or builds it.

Run only after final checks, live assessment, a pushed source checkpoint and a
fresh Windows publish. Reuses R28's source, frontend, bridge and account-byte
gates. The final assessment retains the R28 fields and adds partA, tabSweep,
and performance objects, each with status PROVEN/PARTIAL/UNKNOWN, summary,
limitations (explicit list) and evidence (nonempty list). performance also
requires measuredSmoothnessFixes (nonempty plain-language list). Every tab in
TAB_KEYS needs a tabSweep.tabs entry with the same evidence fields. No proof
account numbers or credentials belong in the assessment or owner ZIP.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import zipfile

import feature018_r28_package as previous


ROOT = previous.ROOT
BRANCH = previous.BRANCH
APP_FOLDER = previous.APP_FOLDER
TEST_ACCOUNT_UIDS = previous.TEST_ACCOUNT_UIDS
verify_no_test_account_bytes = previous.verify_no_test_account_bytes
sha256 = previous.sha256
TAB_KEYS = ("home", "mapData", "afkTasks", "equipment", "cityLayout", "hotkeys",
            "miniGames", "settings", "topBar", "accountSidebar")
EXCLUDED_LIVE_ACTIONS = (
    "Unlock Land Cell", "F6/F7/F8 shield use", "Treasure claim",
    "Food House", "Black Market", "Alliance Drill rally",
)
RETIRED_FEATURES = (
    "Space countdown", "march speed-ups", "Auto Garrison", "Zombie Bus Garrison",
    "worker test panels", "two-games-at-once", "remote no-switch scanning",
)


def clean_pushed_commit() -> tuple[str, str]:
    # R28 checks tracked/index cleanliness and an exact ls-remote origin branch
    # match, including the packaging helpers themselves. Never silently package
    # an unpushed source or a local source modification.
    git = previous.git
    if git("branch", "--show-current") != BRANCH:
        raise RuntimeError("R29 release must be packaged from " + BRANCH)
    if git("status", "--porcelain", "--untracked-files=no"):
        raise RuntimeError("Commit and review all tracked source changes before R29 packaging")
    commit = git("rev-parse", "HEAD")
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise RuntimeError("Invalid source commit")
    remote = git("ls-remote", "origin", "refs/heads/" + BRANCH).split()
    if len(remote) != 2 or remote[0] != commit:
        raise RuntimeError("Local source commit is not the pushed R29 checkpoint")
    return commit, git("rev-parse", "HEAD^{tree}")


def evidence_section(value: object, label: str) -> None:
    if not isinstance(value, dict) or value.get("status") not in ("PROVEN", "PARTIAL", "UNKNOWN"):
        raise RuntimeError("R29 " + label + " requires PROVEN, PARTIAL or UNKNOWN")
    if not isinstance(value.get("summary"), str) or not value["summary"].strip():
        raise RuntimeError("R29 " + label + " requires a summary")
    for key in ("limitations", "evidence"):
        items = value.get(key)
        if not isinstance(items, list) or any(not isinstance(item, str) or not item.strip() for item in items):
            raise RuntimeError("R29 " + label + "." + key + " requires an explicit text list")
    if not value["evidence"]:
        raise RuntimeError("R29 " + label + " requires evidence or an explicit unavailable-evidence explanation")
    if value["status"] != "PROVEN" and not value["limitations"]:
        raise RuntimeError("R29 " + label + " must describe its unproved outcomes")


def read_final_assessment(path: Path) -> dict:
    assessment = previous.read_final_assessment(path)
    for key in ("partA", "tabSweep", "performance"):
        evidence_section(assessment.get(key), key)
    tabs = assessment["tabSweep"].get("tabs")
    if not isinstance(tabs, dict) or set(tabs) != set(TAB_KEYS):
        raise RuntimeError("R29 tabSweep.tabs requires exactly: " + ", ".join(TAB_KEYS))
    for key in TAB_KEYS:
        evidence_section(tabs[key], "tabSweep.tabs." + key)
    if assessment["tabSweep"]["status"] == "PROVEN" and any(tabs[key]["status"] != "PROVEN" for key in TAB_KEYS):
        raise RuntimeError("R29 tab sweep cannot be PROVEN with unproved tabs")
    fixes = assessment["performance"].get("measuredSmoothnessFixes")
    if not isinstance(fixes, list) or not fixes or any(not isinstance(item, str) or not item.strip() for item in fixes):
        raise RuntimeError("R29 performance.measuredSmoothnessFixes requires explicit measured fixes or limitations")
    if not isinstance(assessment["checks"], list) or any(
            not isinstance(item, str) or not item.strip() for item in assessment["checks"]):
        raise RuntimeError("R29 checks must name the completed checks as nonempty text")
    return assessment


def verify_r29_feature_sources() -> None:
    # Retain all owner-directed removals and source-supported R28 bug fixes.
    previous.verify_account_agnostic_sources()
    previous.verify_r28_retirements()
    previous.verify_r28_engine_sources()
    previous.verify_r28_feature_sources()
    required = {
        "tools/feature018_equipment_actions.lua": (
            "observe_final_burst", "tx.burstComplete=true", 'fields.queryOnly=="1"',
        ),
        "tools/current_overview_bridge.lua": (
            "function M.ConfigureVisualMetrics", "smoothDeltaTime", "GetPing", "visual_metrics_session",
        ),
        "src/LWBridge.UI-0.3.17/src/App.jsx": (
            "profileSidebarHidden", 'className="theme-toggle profile-sidebar-restore"',
            'feature018_visual_metrics_configure',
        ),
        "src/LWBridge.UI-0.3.17/src/SettingsPage.jsx": (
            'visual_metrics_config_get', 'visual_metrics_config_save', 'feature018_visual_metrics_configure',
        ),
    }
    for relative, markers in required.items():
        text = (ROOT / relative).read_text(encoding="utf-8")
        missing = [marker for marker in markers if marker not in text]
        if missing:
            raise RuntimeError("R29 feature source contract missing from " + relative + ": " + ", ".join(missing))
    contracts = (ROOT / "src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js").read_text(encoding="utf-8")
    if any(marker in contracts for marker in ('binding: "Z / X / C / V"', 'key: "marchSpeedUp"', 'key: "speedUp"')):
        raise RuntimeError("R29 retired march speed-up Hotkeys contract remains")


def owner_readme(commit: str, assessment: dict) -> str:
    def section(title: str, key: str) -> str:
        value = assessment[key]
        lines = [title + " [" + value["status"] + "]", value["summary"]]
        lines.extend("- Limit: " + item for item in value["limitations"])
        lines.extend("- Evidence: " + item for item in value["evidence"])
        return "\n".join(lines)

    prior_readme = previous.owner_readme(commit, assessment)
    inherited = prior_readme[prior_readme.index("HOW TO USE EQUIPMENT AND PRESETS"):]
    inherited = inherited.replace("R28-FINAL-ASSESSMENT.json", "R29-FINAL-ASSESSMENT.json")
    tab_lines = []
    for key in TAB_KEYS:
        item = assessment["tabSweep"]["tabs"][key]
        tab_lines.append(f"- {key} [{item['status']}]: {item['summary']}")
        tab_lines.extend("  Limit: " + limit for limit in item["limitations"])
    smoothness = "\n".join("- " + item for item in assessment["performance"]["measuredSmoothnessFixes"])
    return f"""LW-Control FEATURE-RECOVERY-018 R29 owner-test build
Built from commit: {commit}

RUN
Extract the complete LW-Control-UI folder and run LWBridge.Desktop.exe.
Keep ProductionUi and OverviewBridge beside it. Requires .NET 10 Desktop
runtime, Edge WebView2 and Python. Leave the official game in Chinese.
Actions use the authenticated profile's captured owner/session. One game at a time.

{section("PART A - PRESETS, ACCOUNT BAR AND FPS/PING", "partA")}

FAST PRESETS
Equip preset and Alt+1-4 use the same saved ordinals. Required native slot
changes are sent back-to-back, then checked against one fresh complete native
state. An uncertain send is never replayed. Saving a preset only records gear.
The timing scope and account limitations are recorded in the final assessment;
the earlier speed comparison does not establish a four-populated-squad timing.

ACCOUNT BAR AND FPS/PING
Hide the account side bar with its hide control; a small restore button remains
in the top bar. The choice survives restart. In Settings, Show FPS and Show ping
control the session-owned overlay in the game; saved settings reapply on reconnect.

{section("PART B - TAB SWEEP (AUTOMATION EXCLUDED)", "tabSweep")}
{"\n".join(tab_lines)}

{section("PART C - PERFORMANCE AND SMOOTHNESS", "performance")}
Measured fixes and their scope:
{smoothness}
Read the limitations before interpreting the measurements. Packaging does not
establish the idle FPS target, hotkey smoothness, tab latency or memory stability.

INHERITED BUG FIXES
Retains bounded mailbox-lock retries, definite-no-send editing recovery,
query-only Resume confirmation, split native equipment-push handling, immediate
gear edits, native levels, four presets and one Refresh. Prior redirect,
relocation, equipment and R24 evidence stays explicitly labelled in
R29-FINAL-ASSESSMENT.json; inherited evidence is not new R29 live proof.

LIVE ACTIONS EXCLUDED FROM THE SWEEP
{"; ".join(EXCLUDED_LIVE_ACTIONS)}. Their screens can be checked; no live pass
is implied. Automation is outside this sweep. Gear/building/march tests must
restore the starting account state. Never Unlock Land Cell.

RETIRED FEATURES
{"; ".join(RETIRED_FEATURES)} remain removed. One profile switcher does not
permit two games at once. Remote scanning still uses supported server switching.

{inherited}"""


def payload_files(publish: Path) -> list[Path]:
    files = sorted((p for p in publish.rglob("*") if p.is_file() and p.suffix.lower() != ".pdb"),
                   key=lambda p: p.relative_to(publish).as_posix())
    if len(files) < 25:
        raise RuntimeError("Packaged Windows app has too few runtime files")
    # Never include a runtime data/profile store, test harness evidence, or
    # injected login state even if accidentally left beside a fresh publish.
    for path in files:
        relative = path.relative_to(publish)
        if path.is_symlink():
            raise RuntimeError("R29 publish must not contain symbolic-link payloads: " + str(relative))
        if any(part.lower() in ("artifacts", "profiles", "userdata", "credentials", ".claude", ".codex")
               for part in relative.parts):
            raise RuntimeError("R29 publish contains non-runtime account/test data: " + str(relative))
        if path.name.lower() in ("credentials.json", "accounts.json", "profiles.json", "launch.json"):
            raise RuntimeError("R29 publish contains account/test configuration: " + str(relative))
        verify_no_test_account_bytes(relative.as_posix(), path.read_bytes())
    return files


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", required=True, type=Path)
    parser.add_argument("--assessment", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    commit, tree = clean_pushed_commit()
    assessment = read_final_assessment(args.assessment)
    if assessment["sourceCommit"] != commit:
        raise RuntimeError("R29 assessment source commit differs from the pushed checkpoint")
    publish = args.publish.resolve(strict=True)
    output = args.output.resolve()
    if output == publish or publish in output.parents or output in publish.parents:
        raise RuntimeError("Release output must not overlap the Windows publish folder")
    verify_r29_feature_sources()
    product_version, bridges = previous.verify_published_app(publish, commit)
    source_digest, source_count = previous.source_inputs_digest()
    files = payload_files(publish)
    manifest = {
        "task": "FEATURE-RECOVERY-018", "round": "R29", "status": "OWNER_TEST",
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
            "partA": assessment["partA"], "tabSweep": assessment["tabSweep"],
            "performance": assessment["performance"],
            "excludedLiveActions": EXCLUDED_LIVE_ACTIONS,
            "retiredFeatures": RETIRED_FEATURES,
            "uncertainSendReplay": False, "independentLeadAcceptance": "SEPARATE",
        },
        "files": {},
    }
    # JSON roundtrip normalizes tuple metadata so embedded-manifest comparison
    # checks semantic identity as well as each payload's independent hash.
    manifest = json.loads(json.dumps(manifest))
    for path in files:
        name = (Path(APP_FOLDER) / path.relative_to(publish)).as_posix()
        manifest["files"][name] = {"sha256": sha256(path), "size": path.stat().st_size}
    additions = {
        f"{APP_FOLDER}/README-R29.txt": owner_readme(commit, assessment).encode("utf-8"),
        f"{APP_FOLDER}/R29-FINAL-ASSESSMENT.json": (json.dumps(assessment, indent=2) + "\n").encode("utf-8"),
        f"{APP_FOLDER}/SOURCE-COMMIT.txt": (commit + "\n" + tree + "\n" + source_digest + "\n").encode("ascii"),
    }
    for name, data in additions.items():
        if name in manifest["files"]:
            raise RuntimeError("R29 generated metadata collides with publish payload: " + name)
        verify_no_test_account_bytes(name, data)
        manifest["files"][name] = {"sha256": hashlib.sha256(data).hexdigest(), "size": len(data)}
    manifest_name = f"{APP_FOLDER}/R29-RELEASE-INTEGRITY.json"
    if manifest_name in manifest["files"]:
        raise RuntimeError("R29 publish contains stale release integrity metadata")
    manifest_bytes = json.dumps(manifest, indent=2).encode("utf-8")
    verify_no_test_account_bytes(manifest_name, manifest_bytes)
    output.mkdir(parents=True, exist_ok=True)
    target = output / f"LW-Control-FEATURE-018-R29-OWNER-TEST-{commit[:12]}.zip"
    receipt = output / "r29-package-receipt.json"
    if target.exists() or receipt.exists():
        raise RuntimeError("Existing R29 artifact preserved; choose a fresh output directory")
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=7) as archive:
        for path in files:
            archive.write(path, (Path(APP_FOLDER) / path.relative_to(publish)).as_posix())
        for name, data in additions.items():
            archive.writestr(name, data)
        archive.writestr(manifest_name, manifest_bytes)
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise RuntimeError("R29 ZIP CRC verification failed")
        if set(archive.namelist()) != set(manifest["files"]) | {manifest_name}:
            raise RuntimeError("R29 ZIP entry set differs from release integrity manifest")
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            verify_no_test_account_bytes(name, data)
            if len(data) != expected["size"] or hashlib.sha256(data).hexdigest() != expected["sha256"]:
                raise RuntimeError("R29 ZIP payload differs from source: " + name)
        verify_no_test_account_bytes(manifest_name, archive.read(manifest_name))
        if json.loads(archive.read(manifest_name)) != manifest:
            raise RuntimeError("R29 embedded manifest mismatch")
    result = {
        "zip": str(target), "sha256": sha256(target), "bytes": target.stat().st_size,
        "zipEntries": len(manifest["files"]) + 1, "sourceCommit": commit,
        "sourceInputSha256": source_digest, "sourceInputFileCount": source_count,
        "bridgesVerified": len(bridges), "status": "OWNER_TEST",
        "testAccountUidScan": "PASS_ASCII_UTF16LE_UTF16BE_ALL_ENTRIES",
    }
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("FEATURE018_R29_PACKAGE_SOURCE_MATCHED", json.dumps(result))


if __name__ == "__main__":
    main()
