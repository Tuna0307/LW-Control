"""MAP-015: native AOI batches preserve true observation ordering for moving Cities.

Source-contract checks are inert. A single terminal timestamp previously made
two different real City positions appear simultaneous and triggered camera
fallback, even though all 63 direct native AOI replies were verified.
"""

from pathlib import Path
import unittest

from luaparser import ast


ROOT = Path(__file__).resolve().parents[2]
LUA = ROOT / "src/LWBridge.GameScripts/current_live_resource_probe.lua"
CAMERA_FREE = ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.CameraFree.cs"
FAST_CITY = ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs"


def require_true_batch_times(lua: str, managed: str, cities: str) -> None:
    ast.parse(lua)
    start = lua.split("function M._map015CameraFree.capture(s)", 1)[1].split(
        "function M._map015CameraFree.pump(now)", 1)[0]
    pump = lua.split("function M._map015CameraFree.pump(now)", 1)[1].split(
        "function M._sharedRuntimeOwnership.PumpVerified(now)", 1)[0]
    if ("capturedAtUnixMs = math.floor(s.wallClockStartMs +" not in start or
            "(runtime_clock() - s.startedAt) * 1000" not in start or
            "wallClockStartMs = os.time() * 1000" not in pump):
        raise AssertionError("Native batch lacks an actual timestamp anchored to its owned runtime")

    import_block = managed.split("long lastBatchCapturedMs = 0;", 1)[1].split(
        "if (state.Covered.Count != 10000", 1)[0]
    expected = (
        'batch.TryGetProperty("capturedAtUnixMs", out JsonElement captureValue)',
        "batchCapturedMs <= lastBatchCapturedMs",
        "batchCapturedMs < terminalCapturedMs - 40_000",
        "batchCapturedMs > terminalCapturedMs + 2_000",
        "lastBatchCapturedMs = batchCapturedMs",
        "DateTimeOffset.FromUnixTimeMilliseconds(batchCapturedMs)",
    )
    for guard in expected:
        if guard not in import_block:
            raise AssertionError("Managed importer lost native per-batch validation: " + guard)
    if "batch, pointRequest, resultPath, capturedAt" in import_block:
        raise AssertionError("Managed importer reused one terminal timestamp for every native batch")

    merge = cities.split("internal static void MergeLatestCityByOwner(", 1)[1].split(
        "private static bool IncludesMonsterSource", 1)[0]
    if ("candidate.Record.UpdatedAt > prior.Record.UpdatedAt" not in merge or
            "candidate.Record.UpdatedAt == prior.Record.UpdatedAt" not in merge or
            "candidate.Record.PointIndex != prior.Record.PointIndex" not in merge or
            "City owner has conflicting locations at the same observation time." not in merge):
        raise AssertionError("City owner conflict detection must remain strict for truly simultaneous positions")


class CityBatchCaptureTimeChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.lua = LUA.read_text(encoding="utf-8")
        cls.managed = CAMERA_FREE.read_text(encoding="utf-8")
        cls.cities = FAST_CITY.read_text(encoding="utf-8")

    def test_actual_native_batch_capture_order_is_required(self):
        require_true_batch_times(self.lua, self.managed, self.cities)

    def test_terminal_timestamp_substitution_is_rejected(self):
        damaged = self.managed.replace(
            "DateTimeOffset.FromUnixTimeMilliseconds(batchCapturedMs)",
            "capturedAt", 1)
        self.assertNotEqual(damaged, self.managed)
        with self.assertRaisesRegex(AssertionError, "per-batch validation"):
            require_true_batch_times(self.lua, damaged, self.cities)

    def test_native_timestamp_omission_is_rejected(self):
        damaged = self.lua.replace(
            "capturedAtUnixMs = math.floor(s.wallClockStartMs +",
            "lostCapturedAt = math.floor(s.wallClockStartMs +", 1)
        self.assertNotEqual(damaged, self.lua)
        with self.assertRaisesRegex(AssertionError, "Native batch lacks"):
            require_true_batch_times(damaged, self.managed, self.cities)

    def test_monotonicity_guard_removal_is_rejected(self):
        damaged = self.managed.replace(
            "batchCapturedMs <= lastBatchCapturedMs ||", "false ||", 1)
        self.assertNotEqual(damaged, self.managed)
        with self.assertRaisesRegex(AssertionError, "per-batch validation"):
            require_true_batch_times(self.lua, damaged, self.cities)

    def test_same_time_owner_conflicts_still_rejected(self):
        damaged = self.cities.replace(
            "candidate.Record.UpdatedAt == prior.Record.UpdatedAt &&",
            "false &&", 1)
        self.assertNotEqual(damaged, self.cities)
        with self.assertRaisesRegex(AssertionError, "conflict detection"):
            require_true_batch_times(self.lua, self.managed, damaged)


if __name__ == "__main__":
    unittest.main()
