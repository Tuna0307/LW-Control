"""Inert R29 release-boundary checks; no publish, ZIP, network or game access."""

from __future__ import annotations

import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import feature018_r29_package as package


class R29PackageBoundaryChecks(unittest.TestCase):
    @staticmethod
    def assessment():
        value = {
            "sourceCommit": "a" * 40,
            "checks": ["Inert boundary fixtures passed; no live claim implied"],
            "limitations": ["Four populated squads are unavailable for live timing."],
            "ownerTests": ["Review all tab and performance limitations before testing."],
        }
        for key in ("qRedirect", "relocation", "equipment", "spaceCountdown", "priorR24"):
            value[key] = {"status": "PARTIAL", "summary": "Inherited fixture; no new native proof."}
        value["spaceCountdown"] = {"status": "REMOVED", "summary": "Owner-directed removal retained."}
        section = {"status": "PARTIAL", "summary": "Fixture; outcome remains unproved.",
                   "limitations": ["Fixture provides no live result."],
                   "evidence": ["Offline fixture exercises validation only."]}
        for key in ("partA", "tabSweep", "performance"):
            value[key] = copy.deepcopy(section)
        value["tabSweep"]["tabs"] = {key: copy.deepcopy(section) for key in package.TAB_KEYS}
        value["performance"]["measuredSmoothnessFixes"] = [
            "No measured fix in this inert fixture; native sampling remains unknown."]
        return value

    def parse(self, value):
        with tempfile.TemporaryDirectory(prefix="r29-assessment-") as temporary:
            path = Path(temporary) / "assessment.json"
            path.write_text(json.dumps(value), encoding="utf-8")
            result = package.read_final_assessment(path)
            self.assertEqual([path], list(Path(temporary).iterdir()))
            return result

    def test_account_bytes_refused_ascii_utf16le_utf16be_even_inside_binary(self):
        for uid in package.TEST_ACCOUNT_UIDS:
            for encoding in ("ascii", "utf-16le", "utf-16be"):
                with self.subTest(uid=uid, encoding=encoding), self.assertRaisesRegex(RuntimeError, "test account UID"):
                    package.verify_no_test_account_bytes("fixture.dll", b"\0\xff" + uid.encode(encoding) + b"\x80\0")
        package.verify_no_test_account_bytes("safe.bin", b"authenticated session only\0")

    def test_readme_preserves_evidence_scope_retired_features_and_exclusions(self):
        value = self.parse(self.assessment())
        readme = package.owner_readme(value["sourceCommit"], value)
        for expected in ("PART A - PRESETS, ACCOUNT BAR AND FPS/PING [PARTIAL]",
                         "PART B - TAB SWEEP (AUTOMATION EXCLUDED) [PARTIAL]",
                         "PART C - PERFORMANCE AND SMOOTHNESS [PARTIAL]",
                         "FAST PRESETS", "Alt+1-4", "fresh complete native",
                         "restore button", "Show FPS", "Show ping", "never replayed",
                         "query-only Resume confirmation", "R29-FINAL-ASSESSMENT.json"):
            self.assertIn(expected, readme)
        for key in package.TAB_KEYS:
            self.assertIn(key + " [PARTIAL]", readme)
        for item in (*package.EXCLUDED_LIVE_ACTIONS, *package.RETIRED_FEATURES):
            self.assertIn(item, readme)
        self.assertIn(value["performance"]["measuredSmoothnessFixes"][0], readme)
        self.assertIn(value["limitations"][0], readme)
        self.assertIn(value["ownerTests"][0], readme)
        for step in range(1, 6):
            self.assertIn(f"{step}. ", readme)
        self.assertNotIn("R28-FINAL-ASSESSMENT.json", readme)

    def test_explicit_unknown_survives_without_being_promoted_to_proven(self):
        value = self.assessment()
        for key in ("partA", "tabSweep", "performance"):
            value[key]["status"] = "UNKNOWN"
        self.assertEqual("UNKNOWN", self.parse(value)["performance"]["status"])
        self.assertIn("PERFORMANCE AND SMOOTHNESS [UNKNOWN]", package.owner_readme(value["sourceCommit"], value))

    def test_incomplete_or_mislabelled_assessment_is_refused(self):
        mutations = [
            lambda v: v.pop("partA"), lambda v: v.pop("tabSweep"), lambda v: v.pop("performance"),
            lambda v: v["partA"].update(status="PASS"),
            lambda v: v["performance"].update(status="RECOVERED"),
            lambda v: v["performance"].update(limitations=[]),
            lambda v: v["performance"].update(evidence=[]),
            lambda v: v["performance"].update(measuredSmoothnessFixes=[]),
            lambda v: v["tabSweep"]["tabs"].pop("settings"),
            lambda v: v["tabSweep"]["tabs"].update(automation={}),
            lambda v: v["tabSweep"].update(status="PROVEN"),
            lambda v: v["tabSweep"]["tabs"]["cityLayout"].update(summary=" "),
            lambda v: v.update(checks=[False]),
            lambda v: v.update(sourceCommit="short"),
            lambda v: v["performance"].update(summary=package.TEST_ACCOUNT_UIDS[-1]),
        ]
        for index, mutate in enumerate(mutations):
            value = self.assessment()
            mutate(value)
            with self.subTest(case=index), self.assertRaises(RuntimeError):
                self.parse(value)

    def test_release_rejects_wrong_branch_dirty_source_and_unpushed_commit(self):
        good = {
            ("branch", "--show-current"): package.BRANCH,
            ("status", "--porcelain", "--untracked-files=no"): "",
            ("rev-parse", "HEAD"): "a" * 40,
            ("ls-remote", "origin", "refs/heads/" + package.BRANCH): "a" * 40 + "\trefs/heads/" + package.BRANCH,
            ("rev-parse", "HEAD^{tree}"): "b" * 40,
        }
        for key, bad in ((("branch", "--show-current"), "main"),
                         (("status", "--porcelain", "--untracked-files=no"), " M src/dirty.cs"),
                         (("rev-parse", "HEAD"), "short"),
                         (("ls-remote", "origin", "refs/heads/" + package.BRANCH), "b" * 40 + "\tbranch")):
            replies = dict(good)
            replies[key] = bad
            with self.subTest(failed_gate=key), patch.object(package.previous, "git", side_effect=lambda *args: replies[args]):
                with self.assertRaises(RuntimeError):
                    package.clean_pushed_commit()
        with patch.object(package.previous, "git", side_effect=lambda *args: good[args]):
            self.assertEqual(("a" * 40, "b" * 40), package.clean_pushed_commit())

    def test_published_app_still_requires_frontend_fingerprint_and_native_source_identity(self):
        # Exercise the inherited gate call order without executing Node,
        # PowerShell or a real application. Neither a matching PE/version nor
        # a passing frontend check can waive mismatched linked native sources.
        with tempfile.TemporaryDirectory(prefix="r29-publish-fixture-") as temporary:
            publish = Path(temporary)
            for relative in package.previous.REQUIRED_PUBLISH:
                path = publish / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"MZfixture" if relative.endswith(".exe") else b"fixture")
            with patch.object(package.previous.subprocess, "check_output", return_value="1.0+" + "a" * 40), \
                    patch.object(package.previous.subprocess, "run", side_effect=RuntimeError("frontend fingerprint mismatch")), \
                    patch.object(package.previous, "verify_bridge_sources") as bridge:
                with self.assertRaisesRegex(RuntimeError, "frontend fingerprint"):
                    package.previous.verify_published_app(publish, "a" * 40)
                bridge.assert_not_called()
            with patch.object(package.previous.subprocess, "check_output", return_value="1.0+" + "a" * 40), \
                    patch.object(package.previous.subprocess, "run") as ui, \
                    patch.object(package.previous, "verify_bridge_sources", side_effect=RuntimeError("stale native bridge")):
                with self.assertRaisesRegex(RuntimeError, "stale native bridge"):
                    package.previous.verify_published_app(publish, "a" * 40)
                self.assertEqual("node", ui.call_args.args[0][0])

    def test_private_profile_data_and_proof_uid_rejected_before_zip_creation(self):
        with tempfile.TemporaryDirectory(prefix="r29-payload-fixture-") as temporary:
            publish = Path(temporary)
            for index in range(25):
                (publish / f"runtime-{index}.dll").write_bytes(b"runtime")
            (publish / "debug.pdb").write_bytes(b"debug")
            self.assertEqual(25, len(package.payload_files(publish)))
            for relative, data in (("profiles.json", b"{}"), ("credentials.json", b"{}"),
                                   ("profiles/session.dat", b"session"),
                                   ("proof.bin", package.TEST_ACCOUNT_UIDS[0].encode("utf-16be"))):
                path = publish / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
                with self.subTest(relative=relative), self.assertRaises(RuntimeError):
                    package.payload_files(publish)
                path.unlink()
            self.assertFalse(list(publish.glob("*.zip")))


if __name__ == "__main__":
    unittest.main(verbosity=2)
