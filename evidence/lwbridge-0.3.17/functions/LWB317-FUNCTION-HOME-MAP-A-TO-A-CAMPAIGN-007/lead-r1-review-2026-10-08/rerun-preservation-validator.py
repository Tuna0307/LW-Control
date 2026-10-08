"""Execute unchanged worker validation with only the output JSON redirected."""
import hashlib
import json
from pathlib import Path

LEAD = Path(__file__).resolve().parent
REPO = next(p for p in LEAD.parents if (p / "AGENTS.md").is_file())
source_path = REPO / "tools/lwbridge317/validate_campaign007_r1.py"
source = source_path.read_text(encoding="utf-8-sig")
old = '(R1/"closeout.json").write_text'
assert source.count(old) == 1
modified = source.replace(old, '(OUTPUT/"closeout.json").write_text')
exec(compile(modified, str(source_path), "exec"), {
    "__file__": str(source_path), "__name__": "__main__", "OUTPUT": LEAD})
(LEAD / "validator-provenance.json").write_text(json.dumps({
    "sourceSha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
    "change": "Only output destination redirected; assertions and inputs unchanged.",
    "limit": "Metadata consistency and preservation do not prove original Home semantics."
}, indent=2) + "\n", encoding="utf-8")
