"""Check bounded lead acceptance and ensure unfinished Home recovery stays open."""
import json
from pathlib import Path
LEAD = Path(__file__).resolve().parent
REPO = next(p for p in LEAD.parents if (p / "AGENTS.md").is_file())
def read(name):
    return json.loads((LEAD / name).read_text(encoding="utf-8-sig"))
for path in LEAD.glob("*.json"):
    json.loads(path.read_text(encoding="utf-8-sig"))
result = read("independent-results.json")
assert result["decision"] == "PARTIALLY_ACCEPTED"
assert result["findingDispositions"]["R007-01"] == "OPEN_READY_HOME_NATIVE_SEMANTICS_008"
assert all(x["exit"] == 0 for x in result["freshChecks"])
assert result["leadGameLaunches"] == result["leadDesktopActions"] == 0
assert result["homeMapParity"] == "PARTIAL" and result["livePositiveStagedStop"] is False
engine = read("engine-sink-stop-proof.json")
assert len(engine["cases"]) == 4 and engine["livePositiveStageStop"] is False
assert all(x["stagedAfterStop"] == 0 and x["publishedAfterStop"] == 1 for x in engine["cases"])
mounted = read("mounted-app-profiles.json")
assert mounted["status"] == "PASS" and mounted["cases"] == 20
assert mounted["originalParity"] == "UNPROVEN" and mounted["externalGameActions"] == 0
integrity = read("native-and-history-integrity.json")
assert integrity["verifiedNativeSlices"] == 129 and integrity["historicalFilesUnchanged"] == 77
assert integrity["remainingHomeStaticWork"] is True
assert len(read("closeout.json")["validatorMutationProbes"]) == 6
for relative in ("docs/tabs/home.md", "docs/GOAL_CAMPAIGN_PHASE2_MAP.md",
                 "docs/lwbridge-parity-matrix.md", "docs/lwbridge-feature-ledger.md",
                 "docs/implementation-handoff.md", "docs/live-test-handoff.md"):
    assert (REPO / relative).read_text(encoding="utf-8").startswith(
        "**2026-10-08 R1 PROJECT-LEAD DECISION: PARTIALLY_ACCEPTED.")
assert "READY / PROJECT-LEAD ASSIGNED" in (REPO / "docs/work-items/LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008.md").read_text(encoding="utf-8")
print("LEAD_R1_DECISION_OK cancellation/integration accepted; Home native semantics OPEN; full parity PARTIAL")
