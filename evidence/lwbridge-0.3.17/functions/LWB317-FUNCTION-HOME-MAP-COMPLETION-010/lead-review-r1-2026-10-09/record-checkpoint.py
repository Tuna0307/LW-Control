"""Place one short current-authority pointer above older saved status entries."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
banner = ("**Current lead checkpoint, 2026-10-09: worker STOPPED by owner; Home/Map "
          "PARTIAL / CHANGES_REQUIRED.** Independent audit credits the captured-run "
          "ownership fix and bounded live receipts, but returns four query/status/"
          "publication-proof corrections. Current authority: `docs/PROJECT_STATUS.md` "
          "and the dated `COMPLETION-010-R1-LEAD` review. Earlier entries below are "
          "historical and do not resume the worker.\n\n")
for name in ("LOOP_QUEUE.md", "implementation-handoff.md", "live-test-handoff.md",
             "lwbridge-feature-ledger.md", "lwbridge-parity-matrix.md",
             "tabs/home.md", "tabs/map-data.md"):
    path = ROOT / "docs" / name
    text = path.read_text(encoding="utf-8")
    if not text.startswith(banner):
        path.write_text(banner + text, encoding="utf-8")
print("CURRENT_STATUS_POINTERS=7")
