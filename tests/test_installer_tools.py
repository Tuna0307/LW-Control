import tempfile
import unittest
from hashlib import sha256
from pathlib import Path
from unittest import mock

from tools.install_loader_probe import (
    APPLY_DISABLED_REASON,
    LUA_ENTRY,
    ORIGINAL_LUA_ENTRY,
    PROBE_ENTRY,
    InstallRefused,
    _entry_source,
    _installed_probe_state,
    _prepare_lenc_entries,
    _prepare_entries,
    _probe_source,
    apply_install,
    crc32_file,
    read_lwlf,
    write_lwlf,
)
from tools.extract_lenc_v3 import EXPECTED_KEY, EXPECTED_NONCE, encode_lenc_bytes


class InstallerToolChecks(unittest.TestCase):
    def test_apply_remains_fail_closed_after_bounded_live_acceptance(self):
        with self.assertRaisesRegex(InstallRefused, "candidate"):
            apply_install({})
        self.assertIn("--prepare-dir", APPLY_DISABLED_REASON)
        self.assertIn("bounded current-game load", APPLY_DISABLED_REASON)

    def test_live_probe_allows_only_bounded_daily_list_refresh_contract(self):
        source = _probe_source().decode("utf-8")
        self.assertIn("TryReqUpdateData", source)
        self.assertIn("if refresh_requested then return false end", source)
        self.assertIn("daily-task-snapshot.json", source)
        self.assertIn("daily-task-refresh.json", source)
        for forbidden in (
            "DailyQuestReward",
            "DailyTaskReward",
            "daily.quest.reward",
            "daily.task.reward",
        ):
            self.assertNotIn(forbidden, source)

    def test_lwlf_round_trip(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "LWScripts.data"
            entries = [(LUA_ENTRY, b"original"), ("Other.luac", b"payload")]
            write_lwlf(path, 3, 12, entries)
            file_version, content_version, loaded = read_lwlf(path)
            self.assertEqual((file_version, content_version), (3, 12))
            self.assertEqual(loaded, entries)
            self.assertIsInstance(crc32_file(path), int)

    def test_probe_entries_preserve_original(self):
        original = b"compiled-original-entry"
        prepared = dict(_prepare_entries([(LUA_ENTRY, original)]))
        self.assertEqual(prepared[ORIGINAL_LUA_ENTRY], original)
        self.assertEqual(prepared[LUA_ENTRY], _entry_source())
        self.assertEqual(prepared[PROBE_ENTRY], _probe_source())
        self.assertIn(b"LWCONTROL_LOADER_PROBE", prepared[LUA_ENTRY])

    def test_encrypted_probe_state_recognizes_exact_current_payloads(self):
        original = b"synthetic-official-entry"
        mapped = {
            LUA_ENTRY: encode_lenc_bytes(_entry_source(), EXPECTED_KEY, EXPECTED_NONCE),
            ORIGINAL_LUA_ENTRY: original,
            PROBE_ENTRY: encode_lenc_bytes(_probe_source(), EXPECTED_KEY, EXPECTED_NONCE),
        }
        native = {
            "key": EXPECTED_KEY,
            "nonce": EXPECTED_NONCE,
            "sha256": "synthetic-xlua",
        }
        with mock.patch("tools.extract_lenc_v3.derive_xlua_key_nonce", return_value=native), mock.patch(
            "tools.install_loader_probe.EXPECTED_LUA_ENTRY_SHA256", sha256(original).hexdigest()
        ):
            state = _installed_probe_state(mapped, Path("unused-xlua.dll"))
        self.assertTrue(state["installed"])
        self.assertTrue(state["payloads_verified"])
        self.assertEqual(state["official_entry"], original)

    def test_encrypted_probe_state_rejects_different_probe_payload(self):
        original = b"synthetic-official-entry"
        mapped = {
            LUA_ENTRY: encode_lenc_bytes(_entry_source(), EXPECTED_KEY, EXPECTED_NONCE),
            ORIGINAL_LUA_ENTRY: original,
            PROBE_ENTRY: encode_lenc_bytes(b"return {}", EXPECTED_KEY, EXPECTED_NONCE),
        }
        native = {
            "key": EXPECTED_KEY,
            "nonce": EXPECTED_NONCE,
            "sha256": "synthetic-xlua",
        }
        with mock.patch("tools.extract_lenc_v3.derive_xlua_key_nonce", return_value=native), mock.patch(
            "tools.install_loader_probe.EXPECTED_LUA_ENTRY_SHA256", sha256(original).hexdigest()
        ):
            with self.assertRaisesRegex(InstallRefused, "different encrypted"):
                _installed_probe_state(mapped, Path("unused-xlua.dll"))


if __name__ == "__main__":
    unittest.main()
