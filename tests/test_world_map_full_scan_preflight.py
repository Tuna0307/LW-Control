from pathlib import Path
import unittest
from unittest.mock import patch

from tools import install_loader_probe as loader
from tools import prepare_world_map_full_scan_probe as preparer


def runtime_state(*, installed: bool, manifest_hash_match: bool = True) -> dict[str, object]:
    hashes = {
        "data": "data-hash",
        "metadata": "metadata-hash",
        "version": "version-hash",
        "baseutils": "baseutils-hash",
    }
    return {
        "installed": installed,
        "runtime_entry_present": True,
        "preserved_original_present": True,
        "payloads_match_current_source": True,
        "manifest_present": True,
        "manifest_hash_match": manifest_hash_match,
        "current_hashes": hashes,
        "manifest": {"installedHashes": hashes},
    }


class WorldMapFullScanPreflightTests(unittest.TestCase):
    def test_packaged_localcache_resolves_to_real_user_localappdata(self):
        profile = r"C:\Users\tester"
        packaged = profile + r"\AppData\Local\Packages\OpenAI.Codex_test\LocalCache\Local"
        self.assertEqual(
            loader._canonical_local_appdata(packaged, profile),
            Path(profile) / "AppData" / "Local",
        )

    def test_normal_localappdata_is_preserved(self):
        profile = r"C:\Users\tester"
        normal = profile + r"\AppData\Local"
        self.assertEqual(loader._canonical_local_appdata(normal, profile), Path(normal))

    def test_exact_runtime_retry_recovers_from_transient_invalid_read(self):
        expected = runtime_state(installed=True)
        with patch.object(
            preparer,
            "inspect_daily_task_runtime",
            side_effect=[runtime_state(installed=False, manifest_hash_match=False), expected],
        ) as inspect, patch.object(preparer.time, "sleep") as sleep:
            actual = preparer._verified_daily_runtime_state({"data": Path("unused")})

        self.assertIs(actual, expected)
        self.assertEqual(inspect.call_count, 2)
        sleep.assert_called_once_with(preparer.DAILY_RUNTIME_INSPECTION_RETRY_SECONDS)

    def test_exact_runtime_retry_still_fails_closed_for_persistent_mismatch(self):
        invalid = runtime_state(installed=False, manifest_hash_match=False)
        invalid["current_hashes"] = {**invalid["current_hashes"], "data": "different"}
        with patch.object(preparer, "inspect_daily_task_runtime", return_value=invalid), \
             patch.object(preparer.time, "sleep"):
            with self.assertRaises(preparer.InstallRefused) as caught:
                preparer._verified_daily_runtime_state({"data": Path("unused")})

        message = str(caught.exception)
        self.assertIn("after 4 exact inspections", message)
        self.assertIn("manifest_hash_match=False", message)
        self.assertIn("mismatched_hashes=data", message)


if __name__ == "__main__":
    unittest.main()
