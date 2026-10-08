"""Read-only preservation audit; output only to the new lead packet."""
import hashlib
import json
import sqlite3
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p / "AGENTS.md").is_file())
sys.path.insert(0, str(ROOT / "tools/lwbridge317"))
import validate_campaign007_r1 as historical

checks = []
for p in [historical.REFERENCE, ROOT / "reference/lwbridge-0.3.17.exe"]:
    digest = historical.sha(p)
    assert digest == historical.EXPECTED_EXE
    checks.append({"path": str(p), "sha256": digest})
for name, expected in historical.TRIPLET.items():
    p = historical.LOCAL / name
    digest = historical.sha(p)
    assert digest == expected
    checks.append({"path": str(p), "sha256": digest})
safe = historical.read(historical.BASE / "safe-real-db-snapshot.json")
original, copy = Path(safe["originalSourcePath"]), Path(safe["snapshotPath"])
assert historical.sha(original) == safe["sourceBytesSha256BeforeAfter"]
assert historical.sha(copy) == safe["snapshotSha256"]
for suffix, val in safe["originalSidecarHashes"].items():
    if suffix.startswith("-"):
        assert historical.sha(Path(str(original) + suffix)) == val["sha256"]
with sqlite3.connect(copy.resolve().as_uri() + "?mode=ro", uri=True) as db:
    db.execute("PRAGMA query_only=ON")
    rows = db.execute("SELECT count(*) FROM map_records WHERE kind='resource' AND server_id=2212").fetchone()[0]
    assert rows == 8008
report = {"check": "PASS", "scope": "read-only hashes and archived snapshot; no game launch or desktop use", "referenceAndScripts": checks, "archivedSourceAndSidecarsUnchanged": True, "resourceRows": rows}
(HERE / "preservation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print("HOME009_LEAD_PRESERVATION_OK reference/scripts/archive rows=8008")
