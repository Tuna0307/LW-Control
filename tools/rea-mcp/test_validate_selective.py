"""Inert MAP-013 retained-pseudocode inverse tests (no live target)."""
from __future__ import annotations

import importlib.util
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("map013_validate", Path(__file__).with_name("validate-selective.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
REPORT = ROOT / "artifacts/rea-toolchain/lead013-source-bounded/report.txt"


@unittest.skipUnless(REPORT.is_file(), "requires locally ignored native evidence")
class NativeRetainedCodeTests(unittest.TestCase):
    def test_complete_retained_source_bounds_and_hash(self):
        result = MODULE.audit(MODULE.DEFAULT_ORIGINAL, REPORT)
        self.assertTrue(any(row["address"] == "0x140191253" and row["chars"] > 10000
                            for row in result["complete"]))

    def test_missing_truncation_marker_is_rejected(self):
        original = REPORT.read_text(encoding="utf-8")
        changed = original.replace("PSEUDOCODE_TRUNCATED=false", "", 1)
        with tempfile.TemporaryDirectory(prefix="map013-audit-") as temporary:
            test_report = Path(temporary) / "bad-report.txt"
            test_report.write_text(changed, encoding="utf-8")
            with self.assertRaises((AssertionError, KeyError)):
                MODULE.audit(MODULE.DEFAULT_ORIGINAL, test_report)

    def test_mismatched_source_unwind_is_rejected(self):
        original = REPORT.read_text(encoding="utf-8")
        changed = original.replace("SOURCE_UNWIND_END_EXCLUSIVE=140191aca", "SOURCE_UNWIND_END_EXCLUSIVE=140191acb", 1)
        with tempfile.TemporaryDirectory(prefix="map013-audit-") as temporary:
            test_report = Path(temporary) / "bad-bounds.txt"
            test_report.write_text(changed, encoding="utf-8")
            with self.assertRaises(AssertionError):
                MODULE.audit(MODULE.DEFAULT_ORIGINAL, test_report)


if __name__ == "__main__":
    unittest.main()
