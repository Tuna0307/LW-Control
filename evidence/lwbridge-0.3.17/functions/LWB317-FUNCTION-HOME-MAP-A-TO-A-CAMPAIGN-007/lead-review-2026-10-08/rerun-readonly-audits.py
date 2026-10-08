"""Rerun unchanged worker auditors, redirecting only their output to this lead packet."""
import hashlib
import json
import runpy
from pathlib import Path

LEAD = Path(__file__).resolve().parent
REPO = next(p for p in LEAD.parents if (p / "AGENTS.md").is_file())
TOOLS = REPO / "tools/lwbridge317"
sources = {}
live = TOOLS / "audit_campaign007_live_stop.py"
namespace = runpy.run_path(str(live))
namespace["audit"].__globals__["BASE"] = LEAD
namespace["audit"]()
preservation = TOOLS / "campaign007_preservation.py"
source = preservation.read_text(encoding="utf-8-sig")
old = '(ROOT/"preservation-final.json").write_text'
assert source.count(old) == 1
redirected = source.replace(old, '(LEAD/"preservation-final.json").write_text')
exec(compile(redirected, str(preservation), "exec"),
     {"__file__": str(preservation), "__name__": "__main__", "LEAD": LEAD})
for path in (live, preservation):
    sources[str(path.relative_to(REPO))] = hashlib.sha256(path.read_bytes()).hexdigest()
(LEAD / "audit-rerun-provenance.json").write_text(json.dumps({
    "sourceHashes": sources,
    "modification": "Output-only destination redirect; source, assertions and inputs unchanged.",
    "caution": "Preservation auditor hardcodes newGameLaunchesCampaign007=0. This field is rejected by the lead; the retained 007 live attempt proves a new launch."
}, indent=2) + "\n", encoding="utf-8")
