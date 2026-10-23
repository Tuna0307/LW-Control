"""Inert checks for the programme package boundary; no build, game or network."""

from __future__ import annotations

from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import lwtool_full_improvement_package as package


class ProgrammePackageChecks(unittest.TestCase):
    def test_clean_gate_requires_branch_clean_source_pushed_head_and_only_allows_claude(self):
        commit, tree = "a" * 40, "b" * 40
        good = {
            ("branch", "--show-current"): package.BRANCH,
            ("status", "--porcelain", "--untracked-files=no"): "",
            ("status", "--porcelain", "--untracked-files=all"): "?? .claude/local.json",
            ("rev-parse", "HEAD"): commit,
            ("ls-remote", "origin", "refs/heads/" + package.BRANCH): commit + "\trefs/heads/" + package.BRANCH,
            ("rev-parse", "HEAD^{tree}"): tree,
        }
        with patch.object(package, "git", side_effect=lambda *args: good[args]):
            self.assertEqual((commit, tree), package.clean_pushed_commit())
        mutations = [
            (("branch", "--show-current"), "main"),
            (("status", "--porcelain", "--untracked-files=no"), " M src/dirty.cs"),
            (("status", "--porcelain", "--untracked-files=all"), "?? unexpected.txt"),
            (("ls-remote", "origin", "refs/heads/" + package.BRANCH), "c" * 40 + "\tbranch"),
        ]
        for key, value in mutations:
            replies = dict(good); replies[key] = value
            with self.subTest(key=key), patch.object(package, "git", side_effect=lambda *args, replies=replies: replies[args]):
                with self.assertRaises(RuntimeError):
                    package.clean_pushed_commit()

    def test_payload_rejects_private_competitor_state_and_test_uid(self):
        with tempfile.TemporaryDirectory(prefix="lwtool-programme-package-") as temporary:
            publish = Path(temporary)
            for index in range(25):
                (publish / f"runtime-{index}.dll").write_bytes(b"runtime")
            self.assertEqual(25, len(package.payload_files(publish)))
            fixtures = [
                ("profiles/account.json", b"{}"),
                ("recovered-core.py", b"private"),
                ("lwtool.dll", b"private"),
                ("state.sqlite", b"state"),
                ("capture.pcap", b"capture"),
                ("proof.bin", package.proven.TEST_ACCOUNT_UIDS[0].encode("utf-16le")),
            ]
            for relative, data in fixtures:
                path = publish / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
                with self.subTest(relative=relative), self.assertRaises(RuntimeError):
                    package.payload_files(publish)
                path.unlink()


if __name__ == "__main__":
    unittest.main(verbosity=2)
