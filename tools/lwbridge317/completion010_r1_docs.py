"""Prepend current R1 state to active Home/Map masters; older evidence stays verbatim."""
import json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
base = "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010/R1"
r1 = root / base
pilots = []
for filename in ["live-attempt-r1.json", "live-attempt-stage-stop.json"]:
    p = r1 / filename
    if p.exists():
        d = json.loads(p.read_text(encoding="utf-8"))
        rows = ", ".join(f"{x.get('kind')}={x.get('total')}" for x in d.get("runs",[]))
        events = d.get("events", [])
        positive = next((x["details"] for x in reversed(events)
                          if x["kind"]=="positive-stage-stop"), None)
        pilots.append(f"{filename}: {d.get('terminal','IN_PROGRESS')}, {rows}; staged Stop: "+
            (str(positive.get("requestedStageStopVerified")) if positive else "not attempted/unknown"))
text = "; ".join(pilots)
update = (
 "**2026-10-09 COMPLETION-010-R1 WORKER CONTINUATION — PARTIAL; NOT ORIGINAL 0.3.17 A→A OR LEAD ACCEPTANCE.** "
 "LEAD010-01 delayed Map status ownership corrected at actual production command/control/provider/SQLite boundaries "
 "(5/5 held tests, true same-run server change preserved); LEAD010-02 repaired City/Resource live-runner proof "
 "(21/21 inert positive/inverses, twelve-column XLSX, durable publication, exact owned exit/restoration/root cleanup). "
 "Ready Home/Map source-backed work adds post-publication Home readiness cleanup, configured-versus-active root "
 "lifetime, ordered local profile reconciliation without faking entitlement, enter-world polling, all-eight-kind "
 "numeric coercion/page envelope, mark deletion and Dispatch-only daily-limit fan-out. "
 f"Current-client live evidence: {text}. "
 f"Full 47 Home + 86 Map reconciled in `{base}/obligations-r1-derivative.json`; "
 "previous lead/worker inventories remain immutable. Protected Lua/lease, remaining native mapping, actual "
 "multi-owner startup, native EN/light capture twice crashed with 0xc0000005; JA/dark desktop "
 "and adverse Home repair/reconnect remain unresolved; "
 "no full original parity or protected-service claim. See dated R1 worker report."
)
rootnotes = {
 "docs/tabs/home.md": "Home current state. ",
 "docs/tabs/map-data.md": "Map Data current state. ",
 "docs/lwbridge-map-scan.md": "Map scan current contract state. ",
 "docs/lwbridge-feature-ledger.md": "Latest feature ledger state. ",
 "docs/lwbridge-parity-matrix.md": "Latest parity-matrix status. ",
 "docs/implementation-handoff.md": "Latest implementation handoff. ",
 "docs/live-test-handoff.md": "Latest live-test handoff. ",
 "docs/LOOP_QUEUE.md": "Current queue worker state. ",
 "docs/strict-parity-recovery.md": "Current recovery authority. ",
}
for filename, heading in rootnotes.items():
    path = root / filename
    if not path.exists():
        raise SystemExit("Expected current master missing: " + str(path))
    contents = path.read_text(encoding="utf-8")
    marker = "**2026-10-09 COMPLETION-010-R1 WORKER CONTINUATION"
    if contents.startswith(marker):
        contents = contents.split("\n\n",1)[1]
    path.write_text(update + "\n\n" + contents, encoding="utf-8")
print("R1_DOCS", len(rootnotes), "updated, summaries", len(pilots))
