"""Inert release-boundary checks. No build, network, game, or ZIP creation."""
from __future__ import annotations

import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SOURCE = Path(__file__).with_name("feature018_r23_package.py")
SPEC = importlib.util.spec_from_file_location("feature018_r23_package", SOURCE)
PACKAGE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PACKAGE)


class ReleaseBoundaryChecks(unittest.TestCase):
    def test_account_byte_scan_all_encodings_and_locations(self):
        # Executables/manifests may place a literal next to unrelated binary
        # bytes, with no text boundary or BOM. The scanner must still refuse.
        for uid in PACKAGE.TEST_ACCOUNT_UIDS:
            for encoding in ("ascii", "utf-16le", "utf-16be"):
                for prefix, suffix in ((b"", b""), (b"\0\xff", b"\x80\0")):
                    with self.subTest(uid=uid, encoding=encoding, embedded=bool(prefix)):
                        with self.assertRaisesRegex(RuntimeError, "test account UID"):
                            PACKAGE.verify_no_test_account_bytes("binary-or-manifest", prefix + uid.encode(encoding) + suffix)
        PACKAGE.verify_no_test_account_bytes("safe-runtime", b"\0LWBridge.Desktop\xffauthenticated account only")

    @staticmethod
    def assessment():
        value = {
            "sourceCommit": "a" * 40,
            "checks": ["Inert release-boundary checks passed; no native claim implied"],
            "limitations": ["Historical disconnect trigger remains unknown."],
            "ownerTests": ["Observe an available event before crediting a live claim."],
        }
        for key in ("q", "f10", "disconnect", "equipment", "cityImages", "chat"):
            value[key] = {"status": "PARTIAL", "summary": "Inert fixture; native outcome is unproved."}
        value["disconnect"] = {"status": "UNKNOWN", "summary": "No persisted historical trigger."}
        return value

    def test_assessment_preserves_unknown_and_owner_limits_without_creating_package(self):
        with tempfile.TemporaryDirectory(prefix="r23-package-check-") as temporary:
            path = Path(temporary) / "assessment.json"
            value = self.assessment()
            path.write_text(json.dumps(value), encoding="utf-8")
            parsed = PACKAGE.read_final_assessment(path)
            self.assertEqual(value, parsed)
            readme = PACKAGE.owner_readme(value["sourceCommit"], parsed)
            self.assertIn("MID-SESSION RECONNECT DIAGNOSIS [UNKNOWN]", readme)
            self.assertIn(value["limitations"][0], readme)
            self.assertIn(value["ownerTests"][0], readme)
            self.assertNotIn("R22 has background packaged proof", readme)
            self.assertEqual([path], list(Path(temporary).iterdir()))

    def test_incomplete_or_mislabelled_assessment_refused_before_packaging(self):
        mutations = (
            lambda v: v.pop("chat"),
            lambda v: v.update(sourceCommit="short"),
            lambda v: v.update(checks=[]),
            lambda v: v["q"].update(status="PASS"),
            lambda v: v["f10"].update(summary=" "),
            lambda v: v.update(limitations="unknown"),
            lambda v: v.update(ownerTests=[False]),
            lambda v: v["chat"].update(summary="fixture " + PACKAGE.TEST_ACCOUNT_UIDS[-1]),
        )
        with tempfile.TemporaryDirectory(prefix="r23-package-negative-") as temporary:
            path = Path(temporary) / "assessment.json"
            for index, mutate in enumerate(mutations):
                value = copy.deepcopy(self.assessment())
                mutate(value)
                path.write_text(json.dumps(value), encoding="utf-8")
                with self.subTest(case=index), self.assertRaises(RuntimeError):
                    PACKAGE.read_final_assessment(path)
            self.assertFalse(list(Path(temporary).glob("*.zip")))


if __name__ == "__main__":
    unittest.main(verbosity=2)
