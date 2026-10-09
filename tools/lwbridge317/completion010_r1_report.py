"""Compact self-contained R1 worker report with independently inspectable links."""
import json
from pathlib import Path
import subprocess

root=Path(__file__).resolve().parents[2]
base=Path("evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010/R1")
folder=root/base
a=json.loads((folder/"live-attempt-r1.json").read_text(encoding="utf-8"))
b=json.loads((folder/"live-attempt-stage-stop.json").read_text(encoding="utf-8"))
assert a.get("terminal")==b.get("terminal")=="PILOT_COMPLETED"
def last(run, event):
    return next((x["details"] for x in reversed(run.get("events",[])) if x["kind"]==event),{})
stage=last(b,"positive-stage-stop")
assert stage.get("requestedStageStopVerified") is True
for item in (a,b):
    proof=last(item,"final-proof-gate")
    assert not proof.get("errors") and all(proof["proof"][k] for k in [
        "PreflightRootAndBackupsVerified","OwnedSessionStarted",
        "OwnedHomeStopSucceeded","ConfirmedExactProcessExit",
        "OriginalHashesRestored","RecoveryJournalAbsent",
        "IsolatedRootRemoved","NoTaskOwnedGameProcesses","AllRequiredRunsPositive"])
checks_path=folder/"final-sweep/final-checks-r1.json"
checks=json.loads(checks_path.read_text(encoding="utf-8")) if checks_path.exists() else []
initial_passed=sum(x["exitCode"]==0 for x in checks)
initial_failed=[x["name"] for x in checks if x["exitCode"]!=0]
corrected_path=folder/"final-sweep/corrected-checks-r1.json"
correction=json.loads(corrected_path.read_text(encoding="utf-8")) if corrected_path.exists() else {}
passed=correction.get("effectivePassed",initial_passed)
failed=[] if correction.get("allZero") else initial_failed
git_head=subprocess.run(["git","rev-parse","HEAD"],cwd=root,capture_output=True,
 text=True).stdout.strip()
lines=[
 "# COMPLETION-010-R1 worker report — PARTIAL",
 "",
 "**Scope:** Home and Map 0.3.17 original-source recovery plus verified current-client adaptation. "
 "This is not project-lead acceptance and does not establish full Home/Map original equivalence.",
 "",
 f"Starting lead: `86ac31ef957c4a38ef8e966548b4ba69559d42c5`. First R1 A/B commit: `d6dd7618`. Latest local HEAD at generation: `{git_head}`. "
 "Historical lead failure evidence and 010 inventories unchanged.",
 "",
 "## A — LEAD010-01 corrected",
 "",
 "Captured scan run/server are checked while holding the serialized Start/Stop operation gate, "
 "before failure or provider Stop. The production status poll rejects late predecessor replies and "
 "overlapping out-of-order observations. Actual command/control/provider/SQLite held inverse: 5/5; "
 "true same-run server-change still durably fails and calls provider Stop once. "
 "See tests/LWBridge.Desktop.Checks/Completion010StatusGuardChecks.cs.",
 "",
 "## B — LEAD010-02 corrected and bounded live",
 "",
 "Runner uses exact preflight root and three verified reversible originals. Required City and "
 "Resource statuses must be durably completed, nonempty, and internally consistent across "
 "native query, filters, pages, options, summary, independent workbook cell values, reopened SQLite "
 "and foreign-profile isolation. Failed/cancelled/timeout/empty cannot satisfy positive completion; "
 "Stop, exact PID exit, restoration, journal removal and root cleanup are fatal success gates. "
 "21/21 inert inverse/positive gates passed; no game launches during those gates.",
 "",
 "## E — production current-client pilot evidence",
 "",
 "| Pilot | Durable City | Durable Resource | XLSX | Owned Stop / exact exit |",
 "|---|---:|---:|---|---|",
]
for x, label in [(a,"first"),(b,"positive-stage Stop")]:
    counts={r["kind"]:r.get("total") for r in x["runs"]}
    export=next(r.get("export") for r in x["runs"] if r["kind"]=="city")
    final=last(x,"final-proof-gate")["proof"]
    lines.append(f"| {label} | {counts['city']:,} | {counts['resource']:,} | "
        f"{export['exportedCount']:,} rows, 12 ordered headers, cell-matched | "
        f"{final['OwnedHomeStopSucceeded']} / {final['ConfirmedExactProcessExit']} |")
lines.extend([
 "",
 f"Positive-stage Stop on City: **{stage['stagedBeforeStop']} staged rows before Stop**, "
 f"durable {stage['runRow']['Status']}, **{stage['remainingStaged']} after Stop**, "
 f"cancelled-run published **{stage['publishedForRun']}**; kind-specific witness verified "
 f"`{stage['requestedStageStopVerified']}`. No claims, plunder, shares, marches, spending, server moves, Auto or updater.",
 "",
 "Both pilots: authenticated host, production getStatus, game server 2212, native City/Resource "
 "queries and pages, City full XLSX content, reconnect-independent data reopened, profile isolation. "
 "**Exactly 2 R1 LastWar launches** via the isolated production pilot, **0 remaining** LastWar/launcher "
 "processes after each completed proof, original 3-file hashes restored, journal absent, both task-owned roots removed. "
 "Full attempt timelines remain in the two live-attempt JSON files; preflight records kept even after root cleanup.",
 "",
 "## C / D — further source-backed implementation",
 "",
 "- Home: post-publication readiness failure calls exact owned cleanup (held actual service test); "
 "persisted configured root and active captured game root separated (old Stop vs next Start test); "
 "ordered local enabled/unlocked multi-profile reconciliation follows display_order/created_at/id "
 "and keeps per-profile errors (5/5 inert). Protected multi-profile entitlement/capacity is not simulated; "
 "inactive real profile owner startup remains unavailable.",
 "- Map: 5-second enter-world request + 500 ms/10-second bounded world polling; non-page loose "
 "query coercion and min/max level and power ranges for eight kinds; native empty-page response "
 "includes page/pageSize; 12-column City header collector ignores nonstrings; nontrue player mark "
 "deletes; Dispatch daily-limit fanout only on Dispatch terminal result. "
 "All source-supported explicit-target, timeout-no-Stop and between-targets disable Auto corrections retained.",
 "- Earlier accepted lease-timer/session+challenge and registration-serial fix untouched. "
 "Original recovery record, desired-running persistence, error-code projection and finalizer source work credited "
 "rather than reclassified unknown.",
 "",
 "## F — integration and reconciliation",
 "",
 f"Fresh integrated offline suite: **{passed}/{len(checks)} effective exit zero**, "
 f"from immutable first **{initial_passed}/{len(checks)}** and separately recorded corrective reruns. "
 f"Initial failures: {', '.join(initial_failed) if initial_failed else 'none'}; "
 + (f"still failing: {', '.join(failed)}." if failed else "all initial failures corrected by distinguishing tests.") +
 " See R1/final-sweep/final-checks-r1.json, corrected-checks-r1.json and separate raw logs.",
 "",
 "All **47 Home + 86 Map = 133** obligations reconciled into the new derivative "
 "R1/obligations-r1-derivative.json, preserving all baseline source locators, previous proofs, "
 "differences, and missing dependencies. R1/obligations-r1-summary.md tracks targeted changes.",
 "",
 "## Exact remaining work — not falsely closed",
 "",
 "- Full independent comparison against original 0.3.17 Home/Map runtime, licensed lease/ticket/capacity "
 "service responses and encrypted Map Lua controllers requires unavailable approved inputs. "
 "No guessed Ghost result correlation or Treasure claim protocol.",
 "- Available original Home launch reservation/descriptor and 27 launch-failure producer mappings, "
 "additional finalizer callback, recovery/restart/connect adversity and actual simultaneous local "
 "multi-owner runtime require further source recovery and implementation.",
 "- Remaining original Map scheduler atomicity/day-boundary/history, Truck edge rules, original "
 "per-kind capture order and native Auto producer lifetime need additional distinguishing checks.",
 "- Current packaged WinForms/WebView2 EN/light Home capture **crashed twice** with "
 "native 0xc0000005/KERNELBASE.dll, first hidden and then normal-window. No PNG generated; "
 "record R1/ui/capture-negative.json includes exact action and WER details. Genuine JA/dark "
 "and EN/light screenshot parity remain absent. Headless mounted App tests are not equivalent.",
 "- Real repair/restart/retained-game adverse reconnect remain separate from the bounded pilot.",
 "- Original parity remains PARTIAL pending independent lead review. Work item status is PARTIAL, "
 "not AWAITING_REVIEW (ready independent branches are still open).",
 "",
 "## Pointers",
 "",
 "- `R1/a-b-proof.md` and `R1/obligations-r1-summary.md`",
 "- `R1/live-attempt-r1.json`, `R1/live-attempt-stage-stop.json`",
 "- `R1/live-preflight-r1.json`, `R1/live-preflight-stage-stop.json`",
 "- `R1/final-sweep/final-checks-r1.json`",
 "",
])
while lines and lines[-1] == "":
    lines.pop()
content="\n".join(lines)+"\n"
content=content.replace(chr(92)+chr(96),chr(96))
(root/"docs/reviews/2026-10-09-LWB317-FUNCTION-HOME-MAP-COMPLETION-010-R1-WORKER.md").write_text(content,encoding="utf-8")
(folder/"continuation.md").write_text(content,encoding="utf-8")
print("R1_REPORT",passed,len(checks),"failed",len(failed),"two pilots verified")
