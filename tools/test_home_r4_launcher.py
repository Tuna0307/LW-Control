"""Controlled producer contract for the current-client official launcher stage.

Runs the genuine Python helper entry point with a stubbed process creation
boundary. Does not open the official launcher or game.
"""
from __future__ import annotations

import importlib.util
import pathlib
import unittest
from unittest import mock


ROOT = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location(
    "lwbridge_home_r4_launcher", ROOT / "run_overview_bridge.py"
)
assert spec is not None and spec.loader is not None
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)


class HomeR4LauncherProducerChecks(unittest.TestCase):
    def setUp(self):
        self.selected = ROOT / "definitely-not-a-real-last-war-installation" / "LastWarLauncher.exe"
        self.install = {"launcher": self.selected}

    def test_failed_process_spawn_produces_only_typed_launcher_failure(self):
        with mock.patch.object(
            helper.lr.subprocess, "Popen",
            side_effect=FileNotFoundError("controlled missing official launcher"),
        ) as fake:
            with self.assertRaises(helper.LauncherSpawnError) as raised:
                helper.spawn_selected_launcher(self.install, {"ISOLATED_TEST": "1"})
        self.assertEqual(type(raised.exception).__name__, "LauncherSpawnError")
        self.assertIn("could not start selected official launcher", str(raised.exception))
        self.assertIsInstance(raised.exception.__cause__, FileNotFoundError)
        fake.assert_called_once_with(
            [str(self.selected)], cwd=str(self.selected.parent),
            env={"ISOLATED_TEST": "1"},
        )

    def test_successful_spawn_remains_unclassified(self):
        expected = object()
        with mock.patch.object(helper.lr.subprocess, "Popen", return_value=expected):
            self.assertIs(
                helper.spawn_selected_launcher(self.install, {}),
                expected,
            )

    def test_other_errors_do_not_become_launcher_spawn_or_restart(self):
        with mock.patch.object(
            helper.lr.subprocess, "Popen",
            side_effect=RuntimeError("controlled unrelated error"),
        ):
            with self.assertRaisesRegex(RuntimeError, "controlled unrelated error"):
                helper.spawn_selected_launcher(self.install, {})


if __name__ == "__main__":
    unittest.main()
