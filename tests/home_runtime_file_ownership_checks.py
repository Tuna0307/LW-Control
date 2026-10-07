"""Windows function-only ownership proof; never invokes launch/update/game code."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


def load_functions():
    helper = Path(__file__).resolve().parents[1] / "tools" / "run_overview_bridge.py"
    real_spec = importlib.util.spec_from_file_location

    class ResourceStubLoader:
        def create_module(self, spec):
            return None

        def exec_module(self, module):
            module.LUA_ENTRY = "test-only-entry"
            module.ORIGINAL_LUA_ENTRY = "test-only-original"

    def isolated_spec(name, path, *args, **kwargs):
        if name == "lwbridge_live_resource_probe":
            return importlib.util.spec_from_loader(name, ResourceStubLoader())
        return real_spec(name, path, *args, **kwargs)

    with patch.object(importlib.util, "spec_from_file_location", isolated_spec):
        spec = real_spec("home_runtime_ownership_test_subject", helper)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
    return module


class RuntimeOwnershipChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.helper = load_functions()

    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="lwb317-helper-ownership-")
        self.root = Path(self.directory.name)
        self.path = self.root / "lease.txt"
        self.values = {"schema": 1, "sessionId": "A", "challenge": "nonce-A", "updatedAt": 1800000000}
        self.foreign = b"schema=1\nsessionId=B\nchallenge=nonce-B\nupdatedAt=1800000000\n"

    def tearDown(self):
        self.directory.cleanup()

    def test_foreign_and_malformed_publication_and_cleanup(self):
        for contents in (self.foreign, b"\xff\x00", b"sessionId=A\nchallenge=nonce-A\nmalformed\n",
                         b"sessionId=B\nsessionId=A\nchallenge=nonce-A\n"):
            self.path.write_bytes(contents)
            with self.assertRaises(self.helper.OverviewBridgeError):
                self.helper.write_kv_atomic(self.path, self.values)
            self.assertFalse(self.helper._delete_owned_runtime(self.path, "A", "nonce-A"))
            self.assertEqual(self.path.read_bytes(), contents)

    def test_replacement_barrier_and_same_handle_deletion(self):
        self.helper.write_kv_atomic(self.path, self.values)
        replacement = self.root / "foreign-replacement.txt"
        replacement.write_bytes(self.foreign)
        attempted = []

        def compete(path, operation):
            with self.assertRaises(OSError):
                replacement.replace(path)
            with self.assertRaises(OSError):
                path.read_bytes()
            attempted.append(operation)

        self.helper.write_kv_atomic(self.path, self.values, before_mutation=compete)
        self.assertTrue(self.helper._runtime_bytes_match(self.path.read_bytes(), "A", "nonce-A"))
        self.assertTrue(self.helper._delete_owned_runtime(self.path, "A", "nonce-A", before_mutation=compete))
        self.assertEqual(attempted, ["write", "delete"])
        self.assertFalse(self.path.exists())
        replacement.replace(self.path)
        self.assertFalse(self.helper._delete_owned_runtime(self.path, "A", "nonce-A"))
        self.assertEqual(self.path.read_bytes(), self.foreign)

    def test_canonical_producers_and_cleanup(self):
        p = {"runtime": self.root}
        self.helper.write_lease(p, "A", "nonce-A")
        self.helper.write_control(p, "profile-A", "A", "nonce-A", 123)
        self.assertEqual(self.helper.read_kv(self.root / "control.txt")["profileId"], "profile-A")
        cancel = self.root / "cancel-start.txt"
        self.helper.write_kv_atomic(cancel, self.values)
        foreign_ready = self.root / "ready.json"
        foreign_ready.write_bytes(b'{"sessionId":"B","challenge":"nonce-B"}')
        heartbeat = self.root / "heartbeat.json"
        heartbeat.write_bytes(b'{"sessionId":"A","challenge":"nonce-A"}')
        self.helper.clear_stale_runtime(p, "A", "nonce-A", preserve_start_cancel=("A", "nonce-A"))
        self.assertTrue(cancel.exists())
        self.assertTrue(foreign_ready.exists())
        self.assertFalse(heartbeat.exists())
        self.assertFalse(self.path.exists())
        self.helper.clear_stale_runtime(p, "A", "nonce-A")
        self.assertFalse(cancel.exists())
        self.path.write_bytes(self.foreign)
        with self.assertRaises(self.helper.OverviewBridgeError):
            self.helper.write_lease(p, "A", "nonce-A")
        self.helper.clear_stale_runtime(p, "A", "nonce-A")
        self.assertEqual(self.path.read_bytes(), self.foreign)

    def test_abandoned_runtime_cleanup_requires_no_game_and_no_fresh_lease(self):
        p = {"runtime": self.root, "game": self.root / "LastWar.exe"}
        control = self.root / "control.txt"
        ready = self.root / "ready.json"
        heartbeat = self.root / "heartbeat.json"
        cancel = self.root / "cancel-start.txt"

        control.write_bytes(self.foreign)
        self.path.write_bytes(
            b"schema=1\nsessionId=B\nchallenge=nonce-B\nupdatedAt=1800000000\n"
        )
        ready.write_bytes(b'{"sessionId":"B","challenge":"nonce-B"}')
        heartbeat.write_bytes(b"malformed")
        cancel.write_bytes(self.foreign)

        with patch.object(
            self.helper.lr, "require_no_selected_game_process", create=True
        ) as no_game, patch.object(self.helper.time, "time", return_value=1800000010):
            self.helper.clear_abandoned_runtime(p)
        no_game.assert_called_once_with(p)
        self.assertFalse(control.exists())
        self.assertFalse(self.path.exists())
        self.assertFalse(ready.exists())
        self.assertFalse(heartbeat.exists())
        self.assertTrue(cancel.exists(), "abandoned cleanup must not consume cancellation state")

        control.write_bytes(self.foreign)
        self.path.write_bytes(
            b"schema=1\nsessionId=B\nchallenge=nonce-B\nupdatedAt=1800000000\n"
        )
        with patch.object(
            self.helper.lr, "require_no_selected_game_process", create=True
        ), patch.object(self.helper.time, "time", return_value=1800000000):
            with self.assertRaisesRegex(
                self.helper.OverviewBridgeError,
                "fresh shared bridge lease",
            ):
                self.helper.clear_abandoned_runtime(p)
        self.assertTrue(control.exists())
        self.assertTrue(self.path.exists())

    def test_no_non_windows_mutation_fallback(self):
        with patch.object(self.helper.os, "name", "posix"):
            with self.assertRaises(self.helper.OverviewBridgeError):
                self.helper.write_kv_atomic(self.path, self.values)
            with self.assertRaises(self.helper.OverviewBridgeError):
                self.helper._delete_owned_runtime(self.path, "A", "nonce-A")
        self.assertFalse(self.path.exists())


if __name__ == "__main__":
    unittest.main()
