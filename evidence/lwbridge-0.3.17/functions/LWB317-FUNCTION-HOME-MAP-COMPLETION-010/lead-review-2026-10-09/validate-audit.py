"""Validate saved audit evidence only; no game, desktop, cleanup or product mutation."""
import hashlib
import json
from pathlib import Path

p = Path(__file__).resolve().parent
def read(name):
    return json.loads((p / name).read_text(encoding="utf-8-sig"))

summary = read("audit-summary.json")
assert summary["reviewedSource"] == "76018780d017321cd7fa2a819392c97c1e2888f0"
assert summary["leadRealGameLaunches"] == 0 and summary["leadProductChanges"] == 0
assert summary["decision"] == "PARTIAL_CHANGES_REQUIRED"
checks = read("replay/final-checks.json")
assert len(checks) == 38 and all(x["exitCode"] == 0 for x in checks)
assert all((p / "replay" / x["log"]).exists() for x in checks)
comparison = read("replay/recovery-comparison.json")
assert len(comparison["rows"]) == 230 and comparison["mismatches"] == 0
assert len(read("adoption-current.json")["cases"]) == 2
assert not any(x["mismatch"] for x in read("adoption-current.json")["cases"])
negative = read("map-inverse-negative.json")
assert negative["realGameLaunches"] == 0 and negative["isolatedRootRemoved"]
assert len(negative["cases"]) == 3 and all(x["mismatch"] for x in negative["cases"])
assert hashlib.sha256((p / "map-inverse-negative.json").read_bytes()).hexdigest() == summary["negativeSha256"]
assert len(read("obligation-reconciliation.json")["rows"]) == 133
assert all(x["matches"] for x in read("current-triplet.json").values())
for result in read("current-handler-static.json"):
    assert result["exitCode"] == 0 and "OK" in result["output"] and "DEV:" not in result["output"]
reference = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
assert hashlib.sha256(reference.read_bytes()).hexdigest().upper() == "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783"
json_count = 0
for path in p.rglob("*.json"):
    if {"bin", "obj"}.intersection(path.relative_to(p).parts):
        continue
    json.loads(path.read_text(encoding="utf-8-sig"))
    json_count += 1
print(f"LWB317_COMPLETION010_PARTIAL_LEAD_AUDIT_OK commands=38 adoption=2 newFailures=3 json={json_count}")
