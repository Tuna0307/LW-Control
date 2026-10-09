"""Produce a new 47-row current audit, preserving the historical inventory."""
import json
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
prefix = "origin/research/offline-controller:evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010/"
def original(path):
    return json.loads(subprocess.check_output(["git", "show", prefix + path], cwd=root))
rows = original("obligations/home-obligations.json")["rows"]
facts = {fact["id"]: fact for fact in original("c-handlers/handlers-recovery.facts.json")["facts"]}
locale = original("c-handlers/handlers-locale.json")
assert len(rows) == 47 and [r["id"] for r in rows] == ["H-%02d" % n for n in range(1, 48)]

# Each is a CURRENT outcome, an actually executed test or explicitly unrun check,
# and a distinct next action. Never copy the inherited readiness field.
current = [
("CONTROLLED", "App Home start/busy integration", "Current R1 packaged positive Launch"),
("CONTROLLED", "Native bridge no frontend timeout/extra gate", "Current R1 packaged Launch proof"),
("CONTROLLED", "Missing-root/missing-helper native inverse", "Native installed-root error cases"),
("CONTROLLED", "Selected unmanaged PID guard and process handle checks", "Actual 5-second unmanaged timeout"),
("PROTECTED_INPUT", "Original ticket/lease protected result absent", "Authorized original ticket/lease body; never fake entitlement"),
("LOCAL_GAP", "Current helper descriptor and launcher mutex adapter", "Original code/classifier versus actual helper error"),
("CONTROLLED", "First exact OFFICIAL_LAUNCHER_RESTARTED retry only", "Actual typed launcher event"),
("CLIENT_MAPPING_GAP", "No current typed launcher self-restart event producer", "Identify verified official-launcher event"),
("CLIENT_MAPPING_GAP", "No verified Lua-update/spawn-timeout equivalence", "Current-client failing launcher trace"),
("CONTROLLED", "Pipe 90s registration/250ms report/refresh source", "Real report expiry and failure"),
("CONTROLLED", "R1 real producer adoption errors[]", "Packaged launch failure error"),
("PRIOR_LIVE", "Service exact journal/owned start/stop, 002 lead witness", "Fresh R1 packaged exact restoration"),
("PROTECTED_INPUT", "Original finalizer protected future/callback missing", "Original finalizer error inputs"),
("PRIOR_LIVE", "Actual previous Close and optional-ID tests", "R1 packaged Close"),
("PRIOR_LIVE", "Accepted exact PID close/restoration", "R1 installed hashes before/after"),
("CONTROLLED", "Wrong/stale/non-owned Close native tests", "Live Stop failure/retry"),
("PRIOR_LIVE", "Existing previous installed script backup/restoration hash proof", "R1 fresh scripts and journal"),
("CONTROLLED", "Frontend repairOnClose caller", "R1 packaged actual repair button"),
("CONTROLLED", "Actual UpdateAndRestart helper success/error/cancel", "Genuine managed recovery repair"),
("CONTROLLED", "R1 actual outdated-build AdoptOrRepair -> helper across five branches", "Genuine outdated native build"),
("CONTROLLED", "Non-object startup payload default true native", "R1 enabled-profile auto startup"),
("CONTROLLED", "Registry order, enabled/locked, consumed once", "Independent simultaneous owners"),
("CONTROLLED", "Six mounted own-profile error projection cases EN/JA", "R1 packaged error localization"),
("PRIOR_LIVE", "Earlier exact same-PID host-adoption, inert 19 cases", "R1 fresh restart/adoption"),
("CONTROLLED", "Exact adoption validation, R1 wrong profile/obsolete process", "Original protected disk format unknown"),
("CONTROLLED", "Session/challenge/registration fence retained", "Native delayed adoption against successor"),
("CONTROLLED", "Pipe refresh_pending and 90s expiry", "Actual native expiry"),
("CLIENT_ADAPTATION", "5s heartbeat 1s lease is current-client adapter", "Original protected lease proof H-05"),
("CLIENT_ADAPTATION", "Worker reconnect same token/generation prior check", "Actual transport loss"),
("PACKAGED_PREVIOUS", "UI initial ON, toggled OFF, persisted reopen", "R1 enabled auto startup"),
("PACKAGED_PREVIOUS", "Native config mirror save/rollback", "Live save-error rollback"),
("PACKAGED_PREVIOUS", "Actual reconnect ON/OFF preference/reopen", "Recovery effect enabled vs disabled"),
("SOURCE_RECOVERED_PARTIAL", "Later 010 source local names autoClosePopup forced false and autoForceUpdateReload persisted; backend implements", "Forwarded game-side setAutomation and other names remain local unsupported; encrypted Lua response unknown"),
("CONTROLLED", "Original 2s/30s/60s/180s recovery policy present", "Native monitor adverse trace"),
("CONTROLLED", "Original recovery retry tables and no cap present", "Unnormalized actual native retry trace"),
("CLIENT_ADAPTATION", "Native recovery log/update/restoration adapter", "Native live recovery"),
("SOURCE_RECOVERED_MATCH", "010 c-handlers persisted game_desired_running loaded at startup; LocalConfigStore matches", "R1 host restart true flag with no tracked PID must not launch"),
("LOCAL_UNIMPLEMENTED", "Ordered SQL and service present; normal maxProfiles=1; independent simultaneous runtimes absent", "Implement true separate per-profile process/pipe owners; protected capacity separate"),
("SOURCE_RECOVERED_PARTIAL", "010 original profile_select/reorder/note; R1 note validates ID first", "Clone BRIDGE_HOST_BUSY during active session still differs from original select"),
("CLIENT_ADAPTATION", "Owner generation and profile replacement guards", "Actual A/B/A and concurrent restart"),
("SOURCE_RECOVERED_DIFFERENCE", "010 original root_select persists while running without rejection; clone stages active root", "Resolve current running-root observable monitor mismatch"),
("SOURCE_RECOVERED_PARTIAL", "010 original status null or full 12 fields and connection state; native CreateProfileInstanceStatus maps single profile", "R1 actual status polling and state ownership"),
("SOURCE_RECOVERED_MATCH", "010 original 0x154905 PROFILE_ID_REQUIRED / STATE_UNAVAILABLE / PROFILE_RUNTIME_UNAVAILABLE idle snapshot; source implements", "Poisoned-lock original failure not modeled"),
("CLIENT_ADAPTATION", "Native official updater settle bounded", "Live updater-specific conflict"),
("PACKAGED_PARTIAL", "Actual EN/light and JA/dark stopped/preference states", "R1 Connected/repair/error conditional states"),
("SOURCE_RECOVERED_MATCH", "010 handlers-locale EN/JA fallback common.actionFailed matches HomePage", "Actual R1 repair failure localization"),
("CONTROLLED", "Frontend proxyBusy/gameLaunchBusy dedupe", "Actual deferred/cancel/double-click native"),
]
assert len(current) == len(rows)
new_locators = {
    33: ("set_automation-parse", "set_automation-local-names", "set_automation-forwarded-names"),
    37: ("game_desired_running",),
    39: ("profile_select", "profile_reorder", "profile_note_set"),
    41: ("game_root_select",),
    42: ("profile_instance_status-shape", "connectionState-classifier"),
    43: ("game_recovery_status",),
}
def clean(value):
    return " ".join(str(value or "").split()).replace("|", r"\|")
lines = [
    "# HOME-004 R1 — current 47-row Home audit", "",
    "New derivative of the immutable earlier HOME_004_CONTRACT_MATRIX.md. Original binary SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.",
    "Authority: 010 obligations/home-obligations.json; later c-handlers/handlers-recovery.facts.json and handlers-locale.json on origin/research/offline-controller; current production callers and separately classified tests.",
    "Current status never inherits the original inventory readiness marker. CONTROLLED is not actual native live proof. PRIOR_LIVE denotes proof from HOME-LAUNCH-002 only.", "",
    "| ID and input/state | Original expected outcome and locator | Current production caller/path | Current evidence/status | Remaining action |",
    "| --- | --- | --- | --- | --- |"]
for index, row in enumerate(rows):
    n = index + 1
    status, proof, next_action = current[index]
    source = str(row.get("recoveredContract", "")) + " (" + str(row.get("originalLocator", "")) + ")"
    for key in new_locators.get(n, ()):
        source += " ; LATER 010 " + key + " at " + facts[key]["locator"]
    if n == 46:
        source += " ; LATER 010 handlers-locale.json keys/codes and EN/JA fallback"
    path = row.get("currentImplementation", "")
    if n == 20:
        path = "OverviewLifecycleAdoption.cs:95-106, OverviewLifecycleService.cs:534-610; tests/HomeR1AdoptionChecks.cs"
    if n == 39:
        path = "ProfileRegistryCommandService.cs:67-123; tests/OrderedProfileReconcileChecks.cs"
    lines.append("| " + " | ".join(map(clean, [row["id"] + " " + row["control"], source, path, status + ": " + proof, next_action])) + " |")
lines += ["", "## Precisely separated blockers", "",
    "- Protected original inputs: H-05 (ticket/lease/capacity) and H-13 (finalizer callback). The game-side encrypted forwarded setAutomation outcome of H-33 is independently unknown. Excluded commercial account/login UI is not a reason to skip local Home.",
    "- Local implementation or compatibility differences: H-06/H-08/H-09, H-33 forwarded automation, H-38 independent multi-owner, H-39 running-profile select, H-41 changing root while running. These are LOCAL work, not externally unavailable inputs.",
    "- Actual packaged adversity/positive R1 proof remains a test obligation where stated per row, especially H-01/02/12/14-20/21/24/30-36/37/42/45-47.",
    "- Later static source closes earlier mistaken undecoded classifications for H-33, H-37, H-39, H-42, H-43, H-46. Recovered original facts do not automatically close current native capability or live proof.",
    "", "No whole-Home DONE assertion follows from this matrix."]
dest = root / "docs" / "HOME_004_R1_CURRENT_MATRIX.md"
dest.write_text("\n".join(lines) + "\n", encoding="utf-8")
print("HOME004_R1_MATRIX_OK rows=47 later-facts=%d locale-codes=%d" % (len(facts), len(locale["codes"])))
