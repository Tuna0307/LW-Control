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
("PACKAGED_R1_LIVE", "R1 actual manual Home Launch -> authenticated Connected -> Close", "Adverse late Start/cancellation still controlled only"),
("PACKAGED_R1_LIVE", "R1 native Home admitted Launch with root/busy original gates", "Long native deadline and frontend cancellation boundary"),
("CONTROLLED", "Missing-root/missing-helper native inverse", "Native installed-root error cases"),
("CONTROLLED", "Selected unmanaged PID guard and process handle checks", "Actual 5-second unmanaged timeout"),
("PROTECTED_INPUT", "Original ticket/lease protected result absent", "Authorized original ticket/lease body; never fake entitlement"),
("LOCAL_GAP", "Current helper descriptor and launcher mutex adapter", "Original code/classifier versus actual helper error"),
("CONTROLLED", "First exact OFFICIAL_LAUNCHER_RESTARTED retry only", "Actual typed launcher event"),
("CLIENT_MAPPING_GAP", "No current typed launcher self-restart event producer", "Identify verified official-launcher event"),
("CLIENT_MAPPING_GAP", "No verified Lua-update/spawn-timeout equivalence", "Current-client failing launcher trace"),
("CONTROLLED", "Pipe 90s registration/250ms report/refresh source", "Real report expiry and failure"),
("CONTROLLED", "R1 real producer adoption errors[]", "Packaged launch failure error"),
("PACKAGED_R1_LIVE", "R1 native start and stop exact PID/session/ready/recovery plus restoration", "Adverse rollback under injected real helper failure"),
("PROTECTED_INPUT", "Original finalizer protected future/callback missing", "Original finalizer error inputs"),
("PACKAGED_R1_LIVE", "R1 native Home Close on two owned sessions", "Native stale in-flight Close adverse replay"),
("PACKAGED_R1_LIVE", "R1 two exact PID exits and original script hashes matched", "Failed native Stop negative"),
("CONTROLLED", "Wrong/stale/non-owned Close native tests", "Live Stop failure/retry"),
("PACKAGED_R1_LIVE", "R1 two original backed-up script restorations and zero recovery journals", "Restoration error controlled only"),
("CONTROLLED", "Frontend repairOnClose caller", "R1 packaged actual repair button"),
("CONTROLLED", "Actual UpdateAndRestart helper success/error/cancel", "Genuine managed recovery repair"),
("CONTROLLED", "R1 actual outdated-build AdoptOrRepair -> helper across five branches", "Genuine outdated native build"),
("PACKAGED_R1_LIVE", "R1 real automatic startup on enabled profile reached Connected then closed/restored", "Independent multi-owner original capacity"),
("CONTROLLED", "Registry order, enabled/locked, consumed once", "Independent simultaneous owners"),
("CONTROLLED", "Six mounted own-profile error projection cases EN/JA", "R1 packaged error localization"),
("PACKAGED_R1_LIVE", "R1 same-build host close/reopen twice, AutoReconnect OFF/ON, same PID/session", "Hard-crash/adoption expiry adverse cases"),
("CONTROLLED", "Exact adoption validation, R1 wrong profile/obsolete process", "Original protected disk format unknown"),
("CONTROLLED", "Session/challenge/registration fence retained", "Native delayed adoption against successor"),
("CONTROLLED", "Pipe refresh_pending and 90s expiry", "Actual native expiry"),
("CLIENT_ADAPTATION", "5s heartbeat 1s lease is current-client adapter", "Original protected lease proof H-05"),
("CLIENT_ADAPTATION", "Worker reconnect same token/generation prior check", "Actual transport loss"),
("PACKAGED_R1_LIVE", "R1 actual WebView initial ON, disable for setup, enable for auto startup then restore OFF", "In-flight save failure rollback"),
("PACKAGED_PREVIOUS", "Native config mirror save/rollback", "Live save-error rollback"),
("PACKAGED_R1_LIVE_PARTIAL", "R1 reconnect OFF and ON both survived native host restart and returned Connected exact PID", "Unhealthy-state recovery/restart effects enabled vs disabled not yet live-witnessed"),
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
("PACKAGED_R1_LIVE_PARTIAL", "R1 EN/light and JA/dark genuine Connected and stopped UI, native controls", "Repair/error/busy timeout locale states still need packaged live proof"),
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
    "- R1 live positives: H-01/02/12/14/15/17/21/24/30/32/45 actual native manual Launch/Connected/Close, automatic startup, exact same-PID adoption with recovery toggle OFF and ON, two exact restorations. They do not certify unhealthy-state H-34–36 recovery or cancelled/late/failing native edges.",
    "- Later static source closes earlier mistaken undecoded classifications for H-33, H-37, H-39, H-42, H-43, H-46. Recovered original facts do not automatically close current native capability or live proof.",
    "", "No whole-Home DONE assertion follows from this matrix."]
dest = root / "docs" / "HOME_004_R1_CURRENT_MATRIX.md"
dest.write_text("\n".join(lines) + "\n", encoding="utf-8")
print("HOME004_R1_MATRIX_OK rows=47 later-facts=%d locale-codes=%d" % (len(facts), len(locale["codes"])))
