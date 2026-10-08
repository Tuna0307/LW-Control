"""Finite lead observations against retained evidence and committed production source."""
import hashlib
import json
import subprocess
from datetime import datetime
from pathlib import Path

LEAD = Path(__file__).resolve().parent
REPO = next(p for p in LEAD.parents if (p / "AGENTS.md").is_file())
PACKET = LEAD.parent
def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))
def event(report, name):
    rows = [e for e in report["events"] if e["kind"] == name]
    assert len(rows) == 1, (name, len(rows))
    return rows[0]
def delta(a, b):
    return (datetime.fromisoformat(b) - datetime.fromisoformat(a)).total_seconds()
old_path = REPO / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-world-resource-004/live-attempt-001.json"
old = read(old_path)
current = read(PACKET / "live-positive-staged-stop-001.json")
completed = [e for e in old["events"] if e["kind"] == "resource-natural-terminal"]
start = event(old, "resource-start")
# Prefer the completion evidence carrying the published result, never the failed later command.
finish = next(e for e in completed if e["atUtc"] > start["atUtc"])
new_start = event(current, "resource-start")
new_stop = event(current, "production-active-stop-before")
queue = read(PACKET / "queue.json")
matrix = read(PACKET / "reference-contract-matrix.json")
body = read(LEAD / "home-native-body-availability.json")
assert len(body["commands"]) == 5
source_paths = [
    "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs",
    "src/LWBridge.Desktop/MapScanEngine.cs",
    "src/LWBridge.Desktop/CurrentClientMap317ScanProvider.cs",
    "tests/LWBridge.Desktop.Checks/BackgroundHomeMapWitness002.cs",
]
source_hashes = {p: hashlib.sha256((REPO / p).read_bytes()).hexdigest() for p in source_paths}
report = {
    "workerCheckpoint": "4442b0486125420f11e92020e0a900361aecef4b",
    "status": "CHANGES_REQUIRED_FOR_CAMPAIGN_CLOSEOUT",
    "originalHomeNativeBodiesAvailable": len(body["commands"]),
    "originalHomeSemanticParityProvedByThisReview": False,
    "prior004ScanElapsedSeconds": delta(start["atUtc"], finish["atUtc"]),
    "prior004FinishEvent": finish["kind"],
    "campaign007ElapsedBeforeStopSeconds": delta(new_start["atUtc"], new_stop["atUtc"]),
    "campaign007StopStagedRows": current["positiveStagingCountBeforeStop"],
    "timingConclusion": "007 stopped earlier than the retained successful 004 completion; this does not prove the cause of 007 zero-stage.",
    "batchConclusion": "For a fresh 1000x1000 Resource run, CaptureBatchAsync takes the 2500-pending full-world route. MapScanEngine checkpoints only after that batch returns. Provider acquisition progress and durable staging are distinct.",
    "sourceHashes": source_hashes,
    "unfinishedQueueEntries": [r for r in queue["obligations"] if r["id"] in ("B01", "C02", "G02")],
    "M33": [r for r in matrix["observations"] if r.get("id") == "M33"],
    "preservationLaunchCountFieldRejected": True,
    "leadDesktopActions": 0,
    "leadGameLaunches": 0,
    "proofLimit": "Read-only static/source/evidence analysis, inert production tests and headless React tests. No original runtime or live parity claim.",
}
assert report["campaign007ElapsedBeforeStopSeconds"] < report["prior004ScanElapsedSeconds"]
(LEAD / "review-observations.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
# Verify every pre-existing worker packet file still matches the reviewed commit.
tracked = subprocess.check_output(["git", "ls-tree", "-r", "--name-only", report["workerCheckpoint"], "--", str(PACKET.relative_to(REPO)).replace("\\", "/")], cwd=REPO, text=True).splitlines()
changed = []
for path in tracked:
    # Windows may check out CRLF. Compare Git-normalized content without updating the index.
    blob = subprocess.check_output(["git", "hash-object", "--path=" + path, path], cwd=REPO, text=True).strip()
    expected = subprocess.check_output(["git", "rev-parse", report["workerCheckpoint"] + ":" + path], cwd=REPO, text=True).strip()
    if blob != expected:
        changed.append(path)
assert not changed, changed
(LEAD / "historical-packet-preservation.json").write_text(json.dumps({
    "checkpoint": report["workerCheckpoint"], "filesVerified": len(tracked),
    "changed": changed, "comparison": "Git-normalized content; historical packet never repinned."
}, indent=2) + "\n", encoding="utf-8")
print("LEAD007_REVIEW_OBSERVATIONS_OK", report["prior004ScanElapsedSeconds"], report["campaign007ElapsedBeforeStopSeconds"], "historical", len(tracked))
