"""Build a read-only derivative over the immutable 47+86 Completion010 inventory.

Never upgrades original equivalence from a current-client test. Evidence fields
remain explicitly partitioned into source recovery, implementation, live and
outstanding dependency. Re-run after adding new proof, do not mutate 010 lists.
"""
from __future__ import annotations
import collections
import datetime as dt
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = root / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010/obligations"
target = root / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010/R1"
target.mkdir(parents=True, exist_ok=True)

# Bounded new implementation evidence, never replacement of original statuses.
updates = {
    "H-12": ("CONTROLLED_FIXED_R1", "Actual late-readiness !IsReady now invokes captured owned Stop before surfacing BRIDGE_START_TIMEOUT; --home-campaign-lifecycle-check."),
    "H-22": ("PARTIAL_LOCAL_LOOP_R1", "Ordered local enabled-profile visits and per-owner errors, with inert per-profile delegate; unknown protected entitlement and inactive runtime launch remain unavailable."),
    "H-38": ("PARTIAL_LOCAL_LOOP_R1", "Display-order, created_at, id loop implemented and owner-scoped with --ordered-profile-reconcile-check; non-selected runtime not manufactured."),
    "H-41": ("CONTROLLED_FIXED_R1", "Configured root persists during a running game, its captured old active root remains for Stop, and next eligible launch picks the selected root; --game-root-select-check."),
    "H-45": ("LIVE_UI_CAPTURE_FAILED_R1", "Packaged canonical WinForms/WebView2 overview EN/light screenshot crashed twice with c0000005 (KERNELBASE.dll), no PNG. JA/dark desktop proof remains absent; negative packet R1/ui/capture-negative.json."),
    "M-07": ("CONTROLLED_FIXED_R1", "Enter-world request 5s and 500ms bounded polling up to 10s added to original-backed scan machine; --map317 scan-state checks."),
    "M-20": ("CONTROLLED_FIXED_R1", "Captured run+server checked under FailAndStop gate and status poll serial; actual command/control/provider/SQLite inverse 5/5 including true same-run server failure."),
    "M-24": ("PARTIAL_CONTROLLED_R1", "Held old reply cannot mutate successor run, lease or provider; profile replacement and actual mounted App comparisons remain separate."),
    "M-28": ("CONTROLLED_FIXED_R1", "MapSearchResult includes native page/pageSize for positive and unknown-kind empty pages; actual --map317-native-boundary-check."),
    "M-29": ("CONTROLLED_FIXED_R1", "Loose non-page coercion, bool/sort handling, f64 min/max level and power bounds with inversion; tested on all eight kind handlers."),
    "M-37": ("LIVE_CURRENT_CLIENT_R1", "Two native City workbooks checked against reopened SQLite, all twelve column values; bounded current-client only, not original writer oracle."),
    "M-38": ("CONTROLLED_FIXED_R1", "Non-string City headers are skipped before exact 12-count check; query/server/labels reused; actual command boundary."),
    "M-40": ("LIVE_CURRENT_CLIENT_R1", "Manual City run completed and published 6780 records in first live pilot; separate second pilot subject to final proof."),
    "M-42": ("LIVE_CURRENT_CLIENT_R1", "Manual Resource run completed and published 8010 records in first live pilot; no original game-universe completeness claim."),
    "M-69": ("CONTROLLED_FIXED_R1", "Player mark value other than literal true (false/number/string/null/missing) now deletes; actual native Map store inverse."),
    "M-84": ("CONTROLLED_FIXED_R1", "Dispatch-only daily-limit fan-out, with distinguishing Ghost-result inverse under worker."),
}
# Preserve unchanged accepted code/negative history while recording genuinely new A/B evidence.
for code in ("H-26", "M-03", "M-58", "M-59", "M-60", "M-67", "M-73", "M-82"):
    updates.setdefault(code, ("PRESERVED_PRIOR_010_FIX", "Prior 010 correction kept; new R1 integration still required."))
pilot_names = ("live-attempt-r1.json", "live-attempt-stage-stop.json")
pilots = []
for name in pilot_names:
    path = target / name
    if not path.exists():
        continue
    try:
        observed = json.loads(path.read_text(encoding="utf-8"))
        pilots.append({
            "evidence": name,
            "terminal": observed.get("terminal", "IN_PROGRESS"),
            "proofErrors": observed.get("finalProofErrors"),
            "runs": [
                {"kind": x.get("kind"), "durableStatus": x.get("durableStatus"),
                 "total": x.get("total")} for x in observed.get("runs", [])],
            "positiveStageStop": next((e.get("details") for e in reversed(observed.get("events", []))
                                       if e.get("kind") == "positive-stage-stop"), None),
            "exactFinalProof": next((e.get("details") for e in reversed(observed.get("events", []))
                                      if e.get("kind") == "final-proof-gate"), None),
        })
    except (ValueError, OSError) as exc:
        pilots.append({"evidence": name, "parseError": str(exc)})

rows = []
counts = {}
for lane, expected in (("home", 47), ("map", 86)):
    data = json.loads((source / f"{lane}-obligations.json").read_text(encoding="utf-8"))
    if len(data["rows"]) != expected:
        raise SystemExit(f"WRONG {lane} inventory count")
    count = collections.Counter()
    for original in data["rows"]:
        key = original["id"]
        count[original["status"]] += 1
        class_name = ("PROTECTED_OR_MISSING_SOURCE" if original["status"] == "BLOCKED"
                      else "CURRENT_CLIENT_ONLY" if original["status"] == "LIVE_CURRENT_CLIENT_ONLY"
                      else "RECOVERED_SOURCE_NOT_FULL_EQUIVALENCE" if original["status"] == "ORIGINAL_PROVED"
                      else "NEEDS_INDEPENDENT_PROOF")
        change, new_proof = updates.get(key, ("PRESERVED_BASELINE_UNREASSESSED_R1", "No additional distinguishing R1 evidence; prior inventory evidence retained, not upgraded."))
        rows.append({
            "id": key,
            "lane": lane,
            "priorOriginalStatus": original["status"],
            "prior010Status": original.get("status010"),
            "provenanceClass": class_name,
            "originalLocator": original.get("originalLocator"),
            "recoveredContract": original.get("recoveredContract"),
            "implementation": original.get("currentImplementation"),
            "priorDifference": original.get("demonstratedDifference"),
            "previousProof": original.get("proofAvailable"),
            "r1Assessment": change,
            "newR1Evidence": new_proof,
            "remainingWork": original.get("requiredCorrection"),
            "dependency": original.get("remainingDependency"),
            "readiness": original.get("readiness"),
        })
    counts[lane] = dict(count)
assert len(rows) == 133 and len({x["id"] for x in rows}) == 133
result = {
    "workItem": "LWB317-FUNCTION-HOME-MAP-COMPLETION-010-R1",
    "state": "PARTIAL",
    "baseline": "010 47 Home + 86 Map; source evidence retained without mutation",
    "countsByPreviousStatus": counts,
    "total": 133,
    "changedRows": sorted(updates),
    "originalEquivalence": "UNPROVEN_FULL",
    "pilotEvidence": pilots,
    "rows": rows,
}
(target / "obligations-r1-derivative.json").write_text(
    json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
text = [
    "# Completion010-R1 obligation reconciliation (derivative)",
    "",
    f"Original source inventory: **47 Home + 86 Map = {len(rows)}** unchanged. No old lead failures or original locators overwritten.",
    "",
    "R1 proof never upgrades source-only ORIGINAL_PROVED, working current-client scans, or clone-only tests to full original 0.3.17 A-to-A.",
    "",
    "## R1 modifications",
    "",
    "| ID | R1 assessment | Evidence |",
    "|---|---|---|",
]
for key in sorted(updates):
    status, evidence = updates[key]
    text.append(f"| {key} | {status} | {evidence.replace('|', ' / ')} |")
text.extend(["", "## Bounded live witnesses", ""])
for p in pilots:
    evidence = p["evidence"]
    text.append(f"- {evidence}: terminal **{p.get('terminal')}**, runs " +
        ", ".join(f"{q.get('kind')}={q.get('total')} ({q.get('durableStatus')})"
                  for q in p.get("runs", [])) +
        f"; final errors={p.get('proofErrors')}; positive-stage={p.get('positiveStageStop') is not None}")
text.extend([
    "",
    "## Separated remaining dependencies",
    "",
    "- Protected original lease/capacity services are unavailable; no local stub proves a license entitlement.",
    "- Protected Map Lua controller bodies and Treasure/Ghost raw correlation schema still unavailable. No FIFO/fuid guessing.",
    "- Native Home launcher retry event producer, original callback mapping and some start/Stop details still require source-backed tracing.",
    "- Multi-profile *local ordering* exists; full product multi-owner simultaneous launch remains an implementation gap separate from original entitlement.",
    "- Non-current server movement, live protected actions, spending, recurring Auto and updater were not exercised.",
    "- Canonical real-app EN/light and genuine JA/dark captures, repair/restart and retained-game adverse reconnect are still pending live proofs.",
    "- A current-client live positive witness is not a complete original-provider acquisition oracle.",
    "",
    "See obligations-r1-derivative.json for all 133 individual original locators, previous proof, source status, new R1 assessment, explicit dependency and next action.",
])
(target / "obligations-r1-summary.md").write_text("\n".join(text) + "\n", encoding="utf-8")
print("R1_RECONCILE", len(rows), "rows,", len(updates), "R1 updates,", len(pilots), "live evidence records")
