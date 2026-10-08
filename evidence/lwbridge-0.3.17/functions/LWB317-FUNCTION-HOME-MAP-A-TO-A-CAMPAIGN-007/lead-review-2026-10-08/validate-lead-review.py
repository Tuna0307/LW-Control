"""Validate this lead decision without treating test PASS or labels as full parity."""
import json
from pathlib import Path
LEAD = Path(__file__).resolve().parent
REPO = next(p for p in LEAD.parents if (p / "AGENTS.md").is_file())
def read(name):
    return json.loads((LEAD / name).read_text(encoding="utf-8-sig"))
for path in LEAD.glob("*.json"):
    json.loads(path.read_text(encoding="utf-8-sig"))
results = read("independent-review-results.json")
assert results["decision"] == "CHANGES_REQUIRED"
assert results["homeMapOverall"] == "PARTIAL"
assert results["findings"] == ["R007-01", "R007-02", "R007-03"]
assert all(check["exit"] == 0 for check in results["freshExecutions"])
assert results["leadGameLaunches"] == results["leadDesktopActions"] == 0
command = read("real-command-proof.json")
assert command["failure"] is None
assert command["result"]["profileA"]["total"] == 8008
assert command["ownedTempCleanupSucceeded"] is True
assert read("mounted-map.json")["status"] == "PASS"
assert read("mounted-app.json")["status"] == "PASS"
assert read("frontend-adapter.json")["originalParity"] == "NOT_PROVEN"
assert read("positive-staged-inert-proof.json")["stoppedWithPositiveStaging"] is True
negative = read("live-positive-staged-stop-independent-audit.json")
assert negative["positiveStagedCancellationProved"] is False
assert negative["afterStop"]["staged"] == negative["afterStop"]["publishedResource"] == 0
assert negative["restorationVerified"] is True
assert read("historical-packet-preservation.json")["changed"] == []
assert read("audit-rerun-provenance.json")["caution"]
assert len(read("home-native-body-availability.json")["commands"]) == 5
observation = read("review-observations.json")
assert observation["originalHomeSemanticParityProvedByThisReview"] is False
assert observation["campaign007ElapsedBeforeStopSeconds"] < observation["prior004ScanElapsedSeconds"]
for relative in ("docs/tabs/home.md", "docs/GOAL_CAMPAIGN_PHASE2_MAP.md",
                 "docs/lwbridge-parity-matrix.md", "docs/lwbridge-feature-ledger.md",
                 "docs/implementation-handoff.md", "docs/live-test-handoff.md"):
    assert (REPO / relative).read_text(encoding="utf-8").startswith(
        "**2026-10-08 CAMP007 PROJECT-LEAD REVIEW: CHANGES_REQUIRED")
work = REPO / "docs/work-items/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007-R1.md"
assert "READY / PROJECT-LEAD ASSIGNED" in work.read_text(encoding="utf-8")
print("LEAD007_REVIEW_PACKET_OK bounded fixes accepted; three findings assigned; full parity PARTIAL")
