"""Read-only inspection of saved worker evidence and current artifact identities."""
import hashlib
import json
from pathlib import Path
import subprocess

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[4]
CAMPAIGN = OUT.parent
WORKER = CAMPAIGN / "R1"
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

pilots = []
for name, preflight in [("live-attempt-r1.json", "live-preflight-r1.json"),
                        ("live-attempt-stage-stop.json", "live-preflight-stage-stop.json")]:
    p, pre = load(WORKER/name), load(WORKER/preflight)
    events = p.get("events", [])
    final = next(e["details"] for e in reversed(events) if e["kind"] == "final-proof-gate")
    staged = next((e["details"] for e in events if e["kind"] == "positive-stage-stop"), None)
    current_triplet = {key: {"sha256": sha(Path(item["path"])),
        "matchesPreflight": sha(Path(item["path"])) == item["sha256"]}
        for key, item in pre["triplet"].items()}
    pilots.append(dict(file=name, sha256=sha(WORKER/name), terminal=p.get("terminal"),
        runs=[{k:r.get(k) for k in ("kind", "durableStatus", "total", "completed")} for r in p.get("runs", [])],
        preflightRootMatches=p["isolatedRoot"] == pre["isolatedRoot"],
        rootStillExists=Path(p["isolatedRoot"]).exists(), final=final,
        positiveStage=staged, currentTriplet=current_triplet,
        proofLimit="historical live receipts audited; no fresh game execution and no independently archived R1 DB/XLSX"))
derivative = load(WORKER/"obligations-r1-derivative.json")
rows = derivative["rows"]
preserved = {}
for relative in [
    "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010/lead-review-2026-10-09",
    "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010/obligations",
    "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010/R1",
]:
    p = subprocess.run(["git", "diff", "--name-only", "faec6f61", "--", relative], cwd=ROOT,
        capture_output=True, text=True, check=True)
    preserved[relative] = p.stdout.splitlines()
old_root = Path(r"C:\Users\chimw\AppData\Local\Temp\lwb317-lead010-map-f440eaf16f494633abf72bb0825f51f5")
record = dict(checkpoint="faec6f61c3fe7ddd529d5dca0155512d2c439899", pilots=pilots,
    originalExeSha256=sha(Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")),
    obligations=dict(total=len(rows), unique=len({r["id"] for r in rows}),
        home=sum(r["lane"] == "home" for r in rows), map=sum(r["lane"] == "map" for r in rows),
        limit="derivative inherits stale priorDifference/remainingWork/readiness fields; not a final validated closure matrix"),
    preservedWorkingTreeDiffs=preserved,
    oldInertAuditRoot=dict(path=str(old_root), exists=old_root.exists(),
        disposition="prior deletion rejected by automatic approval review; retained, no bypass attempted"),
    leadGameLaunches=0, leadDesktopInputOrCapture=False)
(OUT/"evidence-inspection.json").write_text(json.dumps(record, separators=(",", ":")) + "\n", encoding="utf-8")
print(json.dumps(dict(pilots=[dict(file=p["file"], terminal=p["terminal"], rootExists=p["rootStillExists"],
    finalErrors=p["final"]["errors"], restoredNow=all(v["matchesPreflight"] for v in p["currentTriplet"].values())) for p in pilots],
    obligations=record["obligations"], originalExe=record["originalExeSha256"], historicalChanged=any(preserved.values()),
    oldInertRootExists=old_root.exists())))
