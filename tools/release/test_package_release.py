"""Inert checks for the release packager; no build, game or network."""

from __future__ import annotations

from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import package_release as package


class PackageReleaseChecks(unittest.TestCase):
    def test_clean_gate_requires_clean_pushed_branch_head_and_only_allows_claude(self):
        commit, tree, branch = "a" * 40, "b" * 40, "main"
        good = {
            ("branch", "--show-current"): branch,
            ("status", "--porcelain", "--untracked-files=no"): "",
            ("status", "--porcelain", "--untracked-files=all"): "?? .claude/local.json",
            ("rev-parse", "HEAD"): commit,
            ("ls-remote", "origin", "refs/heads/" + branch): commit + "\trefs/heads/" + branch,
            ("rev-parse", "HEAD^{tree}"): tree,
        }
        with patch.object(package, "git", side_effect=lambda *args: good[args]):
            self.assertEqual((branch, commit, tree), package.clean_pushed_commit())
        mutations = [
            (("branch", "--show-current"), ""),
            (("status", "--porcelain", "--untracked-files=no"), " M src/dirty.cs"),
            (("status", "--porcelain", "--untracked-files=all"), "?? unexpected.txt"),
            (("ls-remote", "origin", "refs/heads/" + branch), "c" * 40 + "\tbranch"),
        ]
        for key, value in mutations:
            replies = dict(good)
            replies[key] = value
            with self.subTest(key=key), patch.object(package, "git", side_effect=lambda *a, r=replies: r[a]):
                with self.assertRaises(RuntimeError):
                    package.clean_pushed_commit()

    def test_payload_rejects_private_competitor_state_and_test_uid(self):
        with tempfile.TemporaryDirectory(prefix="lw-control-package-") as temporary:
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
                ("proof.bin", package.TEST_ACCOUNT_UIDS[0].encode("utf-16le")),
            ]
            for relative, data in fixtures:
                path = publish / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
                with self.subTest(relative=relative), self.assertRaises(RuntimeError):
                    package.payload_files(publish)
                path.unlink()

    def test_desktop_project_declares_existing_game_scripts(self):
        declared = package.declared_game_scripts()
        for required in package.REQUIRED_PUBLISH:
            if required.startswith("OverviewBridge/") and required.endswith((".lua", ".py")):
                self.assertIn(required, declared)
        for source in declared.values():
            self.assertEqual(package.GAME_SCRIPTS, source.parent)
        shipped = {path.name for path in declared.values()}
        on_disk = {path.name for path in package.GAME_SCRIPTS.iterdir() if path.suffix in (".py", ".lua")}
        self.assertEqual(on_disk, shipped, "every file in src/LWBridge.GameScripts must be shipped")

    def test_shipped_sources_have_no_test_account_uids(self):
        for path in sorted((package.ROOT / "src").rglob("*")):
            if path.is_file() and not {"node_modules", "bin", "obj"} & set(path.parts) and path.suffix in (
                    ".cs", ".lua", ".py", ".js", ".jsx", ".json", ".html", ".css", ".mjs"):
                with self.subTest(path=path.relative_to(package.ROOT).as_posix()):
                    package.verify_no_test_account_bytes(path.name, path.read_bytes())


if __name__ == "__main__":
    unittest.main()
