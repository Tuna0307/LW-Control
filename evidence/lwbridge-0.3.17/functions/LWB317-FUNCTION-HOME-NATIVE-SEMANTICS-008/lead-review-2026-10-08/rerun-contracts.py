"""Run unchanged worker assertions, redirecting new output away from historical evidence."""
import hashlib
import json
from pathlib import Path
LEAD = Path(__file__).resolve().parent
REPO = next(p for p in LEAD.parents if (p / "AGENTS.md").is_file())
sources = {}
for name, old, new in [
    ("home008_close_contract.py", '(dest/"close-contract.json").write_text', '(LEAD/"close-contract.json").write_text'),
    ("home008_start_contract.py", 'd=ROOT/"evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008/startup-contract.json"', 'd=LEAD/"startup-contract.json"'),
    ("home008_validate.py", '(E/"closeout.json").write_text', '(LEAD/"closeout.json").write_text'),
]:
    path = REPO / "tools/lwbridge317" / name
    source = path.read_text(encoding="utf-8-sig")
    assert source.count(old) == 1, name
    exec(compile(source.replace(old, new), str(path), "exec"), {
        "__file__": str(path), "__name__": "__main__", "LEAD": LEAD})
    sources[name] = hashlib.sha256(path.read_bytes()).hexdigest()
(LEAD / "assertion-provenance.json").write_text(json.dumps({
    "sourceHashes": sources, "change": "Only output destinations redirected; source checks and historical inputs unchanged.",
    "limits": "Original-byte assertions and bounded inert comparisons; not original-runtime execution or complete Home parity."
}, indent=2)+"\n", encoding="utf-8")
