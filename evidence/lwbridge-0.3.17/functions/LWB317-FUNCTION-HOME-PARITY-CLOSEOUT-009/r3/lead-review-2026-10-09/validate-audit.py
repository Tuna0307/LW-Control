"""Validate this bounded lead packet; does not launch/control a game or change product code."""
import hashlib
import json
from pathlib import Path

packet = Path(__file__).resolve().parent
repo = packet.parents[5]

def read(name):
    return json.loads((packet / name).read_text(encoding="utf-8-sig"))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

checks = read("replay/final-checks.json")
assert len(checks) == 38 and all(x["exitCode"] == 0 for x in checks)
assert all((packet / "replay" / x["log"]).is_file() for x in checks)
comparison = read("replay/recovery-comparison.json")
assert comparison["mismatches"] == 0 and len(comparison["rows"]) == 230
mounted = read("mounted-home-current.json")
assert mounted["status"] == "PASS" and mounted["cases"] == 6
refresh = read("refresh-current.json")
assert len(refresh["cases"]) == 2 and not any(x["mismatch"] for x in refresh["cases"])
negative = read("adoption-stop-start-negative.json")
assert negative["checkpoint"] == "98b9d4f78825970590ccb57ef8e146f2d78f30ea"
assert negative["realGameLaunches"] == 0
assert len(negative["cases"]) == 2
assert [x["mismatch"] for x in negative["cases"]] == [False, True]
assert negative["cases"][1]["leaseTimerBefore"] and not negative["cases"][1]["leaseTimerAfter"]
pipe = json.loads((packet / "pipe-epoch-current.txt").read_text(encoding="utf-8-sig").strip().splitlines()[-1])
assert pipe["ok"] and pipe["successiveConnections"] == 24
summary = read("audit-summary.json")
assert summary["productSourceChanges"] == 0 and summary["leadGameLaunches"] == 0
assert summary["decision"] == "CHANGES_REQUIRED_PARTIAL_REVIEW"
assert sha(packet / "adoption-stop-start-negative.json") == summary["negativeSha256"]
reference = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
assert sha(reference).upper() == "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783"
count = 0
for path in packet.rglob("*.json"):
    if "bin" in path.relative_to(packet).parts or "obj" in path.relative_to(packet).parts:
        continue
    json.loads(path.read_text(encoding="utf-8-sig"))
    count += 1
print(f"LWB317_HOME009_R3_PARTIAL_LEAD_AUDIT_OK checks=38 inverseFailures=1 json={count}")
