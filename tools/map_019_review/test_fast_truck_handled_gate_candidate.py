"""Inert, executable Lua 5.3 model of the unshipped fast Truck handled gate.

No application, game, installed script, socket, or scan owner is launched.
Tests exercise Lua gate byte-for-byte, including DateTime.CompareTo semantics,
delayed source-cache population, complete 625-response sweeps and ambiguity.
They do not qualify a production performance win or game-runtime compatibility.
"""

from pathlib import Path
import random
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

GATE = Path(__file__).with_name("fast_truck_handled_gate_candidate.lua")
PRODUCTION = ROOT / "tools/current_live_resource_probe.lua"


class GateModel:
    def __init__(self):
        self.lua = LuaRuntime(unpack_returned_tuples=True)
        self.gate = self.lua.execute(GATE.read_text(encoding="utf-8"))
        self.clock = self.lua.eval("""
            function(t)
                return setmetatable({
                    ticks=t,
                    CompareTo=function(self,other)
                        if type(other)~="table" or other.ticks==nil then
                            error("bad DateTime")
                        end
                        if self.ticks<other.ticks then return -1 end
                        if self.ticks>other.ticks then return 1 end
                        return 0
                    end
                }, {__tostring=function(self) return "DateTime:"..tostring(self.ticks) end})
            end""")
        self.compare = self.lua.eval("""
            function(a,b)
                if a==nil or b==nil then return nil end
                local ok,value=pcall(function() return a:CompareTo(b) end)
                if ok then return value end
                return nil
            end""")
        self.base = self.clock(100)
        self.state = self.gate.new(self.base)
        self.enumerations = 0
        self.captured = []
        self.cache = []

    def step(self, recv=100, handled=100, outstanding=True):
        rec = self.clock(recv) if isinstance(recv, int) else recv
        hdl = self.clock(handled) if isinstance(handled, int) else handled
        result = self.gate.step(self.state, rec, hdl, outstanding, self.compare)
        if isinstance(result, tuple):
            status, error = result
        else:
            status, error = result, None
        if status == "capture":
            self.enumerations += 1
            self.captured.extend(self.cache)
        return status, error


class InertHandledGate(unittest.TestCase):
    def test_source_guard_uses_independent_receipt_and_handle_markers(self):
        production = PRODUCTION.read_text(encoding="utf-8")
        self.assertIn('"_lastGetBlockMsgTime"', production)
        self.assertIn('"_lastHandleBlockMsgTime"', production)
        self.assertIn('function M._map017CompareDateTimes(left, right)', production)
        self.assertIn('local rows, err = train_march_aoi_records(', production)
        self.assertIn('s.fenceSize or 25', production)
        self.assertIn('rate = requested_rate', production)
        self.assertIn('if s.verified == 625 then', production)
        self.assertIn('repairCursor', production)

    def test_idle_frames_never_enumerate(self):
        s = GateModel()
        for _ in range(3000):
            self.assertEqual(s.step()[0], "idle")
        self.assertEqual(s.enumerations, 0)

    def test_receive_before_handle_defers_until_cache_rows_are_visible(self):
        s = GateModel()
        self.assertEqual(s.step(101, 100), ("deferred", None))
        self.assertEqual(s.enumerations, 0)
        # Source queues push at receive, then processes/updates cache later.
        for _ in range(20):
            self.assertEqual(s.step(101, 100)[0], "deferred")
        s.cache = ["late-truck", "late-railway"]
        self.assertEqual(s.step(101, 101), ("capture", None))
        self.assertEqual(s.captured, ["late-truck", "late-railway"])
        self.assertEqual(s.enumerations, 1)
        self.assertEqual(s.step(101, 101)[0], "idle")

    def test_handle_already_completed_captures_once(self):
        s = GateModel()
        s.cache = ["already-handled"]
        self.assertEqual(s.step(101, 101)[0], "capture")
        self.assertEqual(s.step(101, 105)[0], "idle")
        self.assertEqual(s.captured, ["already-handled"])

    def test_no_handling_before_stamp_does_not_certify_fence(self):
        s = GateModel()
        self.assertEqual(s.step(101, 100)[0], "deferred")
        self.assertEqual(s.state.consumed, 0)
        self.assertEqual(s.state.deferred, 1)
        self.assertEqual(s.step(101, 101)[0], "capture")
        self.assertEqual(s.state.consumed, 1)

    def test_multiple_responses_before_handling_fail_closed(self):
        s = GateModel()
        self.assertEqual(s.step(101, 100)[0], "deferred")
        self.assertEqual(s.step(102, 100),
                         (None, "overlapping_unhandled_march_push"))
        self.assertEqual(s.enumerations, 0)
        self.assertEqual(s.state.consumed, 0)

    def test_second_receive_after_handled_capture_is_valid(self):
        s = GateModel()
        self.assertEqual(s.step(101, 100)[0], "deferred")
        s.cache = ["first"]
        self.assertEqual(s.step(101, 101)[0], "capture")
        s.cache = ["second"]
        self.assertEqual(s.step(102, 102)[0], "capture")
        self.assertEqual(s.state.consumed, 2)
        self.assertEqual(s.enumerations, 2)

    def test_unattributed_receive_fails_closed(self):
        s = GateModel()
        self.assertEqual(s.step(101, 101, outstanding=False),
                         (None, "unattributed_march_push"))

    def test_reordered_marker_fails_closed(self):
        s = GateModel()
        self.assertEqual(s.step(102, 102)[0], "capture")
        self.assertEqual(s.step(101, 101),
                         (None, "nonmonotonic_march_receive"))

    def test_no_fake_success_when_native_datetime_unavailable(self):
        s = GateModel()
        self.assertEqual(s.step(101, None),
                         (None, "march_datetime_unavailable"))
        s = GateModel()
        self.assertEqual(s.step(None, 101),
                         (None, "march_datetime_unavailable"))

    def test_uncomparable_handle_does_not_capture(self):
        s = GateModel()
        self.assertEqual(s.step(101, "unsupported datetime"),
                         (None, "march_datetime_comparison_unavailable"))
        self.assertEqual(s.enumerations, 0)

    def test_equal_receive_timestamp_does_not_double_ack(self):
        s = GateModel()
        self.assertEqual(s.step(101, 101)[0], "capture")
        for _ in range(30):
            self.assertEqual(s.step(101, 101)[0], "idle")
        self.assertEqual(s.state.consumed, 1)

    def test_empty_typed_snapshot_still_counts_handled_response(self):
        s = GateModel()
        self.assertEqual(s.step(101, 101)[0], "capture")
        self.assertEqual(s.captured, [])
        self.assertEqual(s.state.consumed, 1)

    def test_625_handled_responses_complete_without_idle_enumeration(self):
        s = GateModel()
        acked = 0
        for i in range(625):
            # Simulates one received push, one frame of deferred native work,
            # then cache mutation before the next Update and acknowledgement.
            stamp = 101 + i
            self.assertEqual(s.step(stamp, stamp - 1)[0], "deferred")
            s.cache = ["march:%d" % i]
            self.assertEqual(s.step(stamp, stamp)[0], "capture")
            acked += 1
            for _ in range(2):
                self.assertEqual(s.step(stamp, stamp)[0], "idle")
        self.assertEqual(acked, 625)
        self.assertEqual(s.enumerations, 625)
        self.assertEqual(s.state.consumed, 625)
        self.assertEqual(len(s.captured), 625)
        self.assertEqual(len(set(s.captured)), 625)
        self.assertEqual(s.captured[-1], "march:624")

    def test_25_request_fences_keep_625_coverage(self):
        s = GateModel()
        verified = set()
        for fence in range(25):
            acknowledgements = 0
            for local in range(25):
                index = fence * 25 + local
                stamp = 101 + index
                self.assertEqual(s.step(stamp, stamp - 1)[0], "deferred")
                s.cache = [index]
                self.assertEqual(s.step(stamp, stamp)[0], "capture")
                acknowledgements += 1
                verified.add(index)
            self.assertEqual(acknowledgements, 25)
        self.assertEqual(len(verified), 625)
        self.assertEqual(s.state.consumed, 625)

    def test_seeded_delays_preserve_all_rows_without_early_sampling(self):
        for seed in (0, 11, 41, 89, 121):
            with self.subTest(seed=seed):
                rng = random.Random(seed)
                s = GateModel()
                for index in range(625):
                    stamp = 101 + index
                    frames = rng.randint(0, 8)
                    for _ in range(frames):
                        self.assertEqual(s.step(stamp, stamp - 1)[0], "deferred")
                    s.cache = [index]
                    self.assertEqual(s.step(stamp, stamp)[0], "capture")
                self.assertEqual(s.captured, list(range(625)))
                self.assertEqual(s.enumerations, 625)

    def test_delayed_response_crossing_next_receive_rejected(self):
        for delay in (1, 2, 5, 20):
            with self.subTest(delay=delay):
                s = GateModel()
                for _ in range(delay):
                    self.assertEqual(s.step(101, 100)[0], "deferred")
                self.assertEqual(s.step(102, 101),
                                 (None, "overlapping_unhandled_march_push"))
                self.assertEqual(s.enumerations, 0)

    def test_unsupported_clock_does_not_enable_legacy_early_cache_sampling(self):
        s = GateModel()
        self.assertEqual(s.step(101, "not-a-DateTime")[0], None)
        self.assertEqual(s.captured, [])
        self.assertEqual(s.state.consumed, 0)


if __name__ == "__main__":
    unittest.main()
