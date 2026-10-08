"""Validate measured lead acceptance and preservation, without asserting complete parity."""
import json
import subprocess
from pathlib import Path
LEAD=Path(__file__).resolve().parent
REPO=next(p for p in LEAD.parents if (p/"AGENTS.md").is_file())
CHECKPOINT="9cc5bcee7a7bff19c391995c87ba8a3359e6d3aa"
def read(name):
    return json.loads((LEAD/name).read_text(encoding="utf-8-sig"))
for path in LEAD.glob("*.json"):
    json.loads(path.read_text(encoding="utf-8-sig"))
r=read("independent-results.json")
assert all(c["exit"]==0 for c in r["freshChecks"])
assert r["remainingAvailableNativeWork"] is True
assert r["leadDesktopActions"]==r["leadGameLaunches"]==0
assert r["decision"]=="BOUNDED_RECOVERY_ACCEPTED_FULL_PARITY_PARTIAL"
assert len(read("production-reconcile-inert.json")["cases"])==8
assert len(read("map-engine-stop.json")["cases"])==4
assert read("close-boundary-hypothesis.json")["proofClass"].startswith("SOURCE_BACKED_BOUNDARY_HYPOTHESIS")
assert read("closeout.json")["originalFullParity"] is False
paths=subprocess.check_output(["git","ls-tree","-r","--name-only",CHECKPOINT,"--",
    "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008",
    "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"],
    cwd=REPO,text=True).splitlines()
for path in paths:
    actual=subprocess.check_output(["git","hash-object","--path="+path,path],cwd=REPO,text=True).strip()
    expected=subprocess.check_output(["git","rev-parse",CHECKPOINT+":"+path],cwd=REPO,text=True).strip()
    assert actual==expected,path
assert "READY / PROJECT-LEAD ASSIGNED" in (REPO/"docs/work-items/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009.md").read_text(encoding="utf-8")
print("HOME008_LEAD_REVIEW_OK historicalFilesUnchanged="+str(len(paths))+" partialParity remainingWork009")
