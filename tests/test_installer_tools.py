import tempfile
import unittest
from pathlib import Path

from tools.install_loader_probe import (
    APPLY_DISABLED_REASON,
    LUA_ENTRY,
    ORIGINAL_LUA_ENTRY,
    PROBE_ENTRY,
    InstallRefused,
    _entry_source,
    _prepare_entries,
    _probe_source,
    apply_install,
    crc32_file,
    read_lwlf,
    write_lwlf,
)


class InstallerToolChecks(unittest.TestCase):
    def test_apply_is_fail_closed_until_write_compatible_lenc_path_is_proven(self):
        with self.assertRaisesRegex(InstallRefused, "LENC encrypted format"):
            apply_install({})
        self.assertIn("plaintext Lua", APPLY_DISABLED_REASON)
        self.assertIn("write-compatible", APPLY_DISABLED_REASON)

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


if __name__ == "__main__":
    unittest.main()
