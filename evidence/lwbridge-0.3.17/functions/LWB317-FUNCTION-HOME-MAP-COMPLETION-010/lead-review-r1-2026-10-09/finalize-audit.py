"""Compact only new lead JSON and verify current checkpoint evidence."""
import hashlib
import json
from pathlib import Path
import subprocess

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[4]
def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
results = read(OUT/"replay/results.json")
assert len(results) == 44 and all(r["exitCode"] == 0 for r in results), "fresh command run incomplete/failed"
probe = read(OUT/"map-independent.json")
assert len(probe["cases"]) == 7 and sum(c["mismatch"] for c in probe["cases"]) == 4
assert probe["isolatedRootRemoved"] and probe["gameLaunches"] == 0
inspection = read(OUT/"evidence-inspection.json")
assert not any(inspection["preservedWorkingTreeDiffs"].values())
assert all(not p["rootStillExists"] and not p["final"]["errors"]
           and all(v["matchesPreflight"] for v in p["currentTriplet"].values())
           for p in inspection["pilots"])
assert inspection["obligations"]["total"] == inspection["obligations"]["unique"] == 133
assert inspection["originalExeSha256"].upper() == "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783"

native = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe").read_bytes()
literals = []
for value in (b"level >= ?", b"level <= ?"):
    at = native.find(value)
    assert at >= 0
    literals.append(dict(fileOffset=hex(at), value=value.decode("ascii")))
changed = subprocess.run(["git", "diff", "--name-only", "86ac31ef", "faec6f61", "--", "src", "tests"],
    cwd=ROOT, capture_output=True, text=True, check=True).stdout.splitlines()
sources = {name: sha(ROOT/name) for name in changed}
snapshot = subprocess.run(["pwsh", "-NoProfile", "-Command",
    "@((Get-CimInstance Win32_Process | Where-Object { $_.Name -match '^(LastWar|LastWarLauncher|LWBridge|LWBridge.Desktop)[.]exe$' } | Select-Object Name,ProcessId,ExecutablePath,CreationDate)) | ConvertTo-Json -Compress"],
    cwd=ROOT, capture_output=True, text=True, check=True)
processes = json.loads(snapshot.stdout.strip() or "[]")
assert processes == [], "a game/clone process appeared; preserve and report instead of stopping it"
delta = {
    "H-12": "bounded actual-service readiness cleanup accepted; live adversity unfinished",
    "H-22": "partial local ordering; actual unavailable nonselected owners not implemented",
    "H-38": "partial ordering/error isolation; not simultaneous multi-profile runtime",
    "H-41": "bounded configured/active root checks pass",
    "H-45": "packaged capture crash preserved; genuine native EN/JA control captures absent",
    "M-20": "LEAD010-01 controlled fix accepted; outside-world status difference LEAD010R1-02 remains",
    "M-28": "page envelope partial; LEAD010R1-03 i64 clamp mismatch",
    "M-29": "positive ranges pass; LEAD010R1-01 zero/negative level boundaries fail",
    "M-37": "historical current-client workbook comparison receipt; original writer/full A-to-A unproved",
    "M-40": "historical City 6780 receipts credited; encrypted original extractor unproved",
    "M-42": "historical Resource 8010/8011 receipts credited; encrypted original extractor unproved",
    "M-24": "controlled ownership fix accepted; Stop staging proof credited; LEAD010R1-04 publication invariant not measured",
}
(OUT/"obligation-lead-delta.json").write_text(json.dumps(delta), encoding="utf-8")
summary = dict(checkpoint="faec6f61c3fe7ddd529d5dca0155512d2c439899",
    disposition="PARTIAL / CHANGES_REQUIRED", worker="STOPPED_BY_OWNER",
    freshCommands=len(results), failedCommands=[], originalMapByteChecks=62,
    independentCases=7, independentMismatches=4,
    findings=["LEAD010R1-01", "LEAD010R1-02", "LEAD010R1-03", "LEAD010R1-04"],
    sources=sources, originalSqlLiterals=literals, passiveProcesses=processes,
    auditGameLaunches=0, auditDesktopInputOrCapture=False, productionCodeEdited=False,
    oldInertRoot=inspection["oldInertAuditRoot"],
    warning="general green commands do not close distinguishing negatives or original parity")
(OUT/"audit-summary.json").write_text(json.dumps(summary), encoding="utf-8")
for path in OUT.rglob("*.json"):
    if "bin" in path.parts or "obj" in path.parts: continue
    path.write_text(json.dumps(read(path), ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
files = {str(p.relative_to(OUT)).replace("\\", "/"): sha(p) for p in OUT.rglob("*")
    if p.is_file() and "bin" not in p.parts and "obj" not in p.parts and p.name != "manifest.json"}
(OUT/"manifest.json").write_text(json.dumps(files, separators=(",", ":")) + "\n", encoding="utf-8")
print("LEAD010_R1_AUDIT_VALIDATION_OK commands=44 cases=7 mismatches=4 historical_unchanged=true game_launches=0")
