"""MAP-015 R4 inert source-contract inverses for the genuine fast Truck path.

All exercises use local source text and a Lua 5.3 sandbox. No game, UI, network,
package, or account actions occur. These guards catch regressions where a Truck
expires between native capture and publication, or a missing/foreign march push
is counted as proof of all 625 requested rectangles.
"""

from pathlib import Path
import re
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime


LUA_PATH = ROOT / "tools/current_live_resource_probe.lua"
CS_TRUCK_PATH = ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastTruck.cs"
CS_WORLD_PATH = ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs"


def section(source, start, end):
    return source.split(start, 1)[1].split(end, 1)[0]


def method(source, signature):
    """Extract a C# brace scope, so checks cannot match unrelated methods."""
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 0
    for index in range(opening, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[opening + 1:index]
    raise ValueError("Unclosed production method " + signature)


def production_lua_unarrived(source):
    """Execute the actual short production helper, with no other game Lua."""
    fragment = section(source,
        "function M._map015FastTruck.unarrived", "function M._map015FastTruck.finish")
    lua = LuaRuntime(unpack_returned_tuples=True)
    check = lua.execute(
        "local M = { _map015FastTruck = {} }\n"
        "function M._map015FastTruck.unarrived" + fragment +
        "\nreturn M._map015FastTruck.unarrived")
    return lambda record, timestamp: bool(check(lua.table_from(record), timestamp))


def simulate_native_one_tick(source, scenario=""):
    """Run real native fast Truck finish/pump with fully inert fake game state.

    Start with 624 of 625 rectangle indices verified and one previously
    captured typed Truck. A scenario may install an extraneous push, put the
    final rectangle in flight, or exhaust the two repair passes.
    """
    native = section(source, "function M._map015FastTruck.unarrived",
                     "-- MAP-015 OWN_DESIGN")
    model = r'''
local M = { _map015FastTruck = {} }
local receipt = nil
local sentRectangles = {}
local clock = 1000
function runtime_clock() return clock end
function safe_get(object, key) return object and object[key] end
function reflected_value(object, key) return object and object[key] end
function reflected_set_value(object, key, value) object[key] = value; return true end
function current_server_id() return 2212 end
function collection_int_values(current) return current.values or {}, nil end
function collection_count() return 0 end
function read_kv_file() return nil end
function write_json(_, value) receipt = value end
function train_march_aoi_records() return {}, nil end
function call(object, name, ...)
    if object and type(object[name]) == "function" then
        return pcall(object[name], object, ...)
    end
    return false, nil
end
CS = {
    System = {
        Int32 = {Parse = function(value) return tonumber(value) end},
        Array = {CreateInstance = function() return {SetValue = function() end} end},
    },
    WorldGetBlockMessage = {
        Request = function() return {} end,
        Instance = {Send = function(_, request)
            sentRectangles[#sentRectangles + 1] = (request.y - 20) / 40 * 25 + (request.x - 20) / 40
        end},
    },
}
'''
    setup = r'''
local s = {
    requestId = "owned", scanRunId = "scan", profileId = "test",
    launchSessionId = "session", challenge = "test", gamePid = 123,
    serverId = 2212, originalX = 5, originalY = 6,
    world = { CurTilePosClamped = {x = 5, y = 6} },
    current = {values = {}, Add = function(self, n) self.values[#self.values + 1] = n end,
               Remove = function(self, n) for i, v in ipairs(self.values) do
                   if v == n then table.remove(self.values, i); break end end end},
    original = {}, originalCount = 0, added = {}, addedSet = {}, int32 = CS.System.Int32,
    manager = {}, marchManager = { _lastGetBlockMsgTime = "foreign-stamp" },
    seen = {}, wholeWorld = {}, records = {{ uuid = "valid-seed" }},
    sent = 625, initialSent = 625, pushes = 624, verified = 624,
    completed = {}, unproven = {624}, repair = {}, repairCursor = 1,
    pass = 0, group = nil,
    next = 625, lastPushStamp = "prior-stamp",
    startedAt = 990, lastSend = 999.99, rate = 20, interval = .05,
    logSeenCount = 0, originalReplyFlag = false,
    originalWorldReplyFlag = false,
}
for n = 0, 623 do s.completed[n] = true end
M._map015FastTruck.active = s
'''
    trailer = r'''
M._map015FastTruck.pump(1000)
return {receipt = receipt, sent = sentRectangles, pass = s.pass,
        verified = s.verified, completed624 = s.completed[624] == true,
        repair = s.repair, group = s.group}
'''
    result = LuaRuntime(unpack_returned_tuples=True).execute(model +
        "M.VERSION = 'inert'\nfunction typeof(x) return x end\n" +
        "function M._map015FastTruck.unarrived" + native + setup + scenario + trailer)
    return {
        "receipt": None if result["receipt"] is None else
            {key: result["receipt"][key] for key in result["receipt"].keys()},
        "sent": [result["sent"][n] for n in range(1, len(result["sent"]) + 1)],
        "pass": result["pass"], "verified": result["verified"],
        "completed624": result["completed624"],
        "repair": [result["repair"][n] for n in range(1, len(result["repair"]) + 1)],
        "group": result["group"] is not None,
    }


def check_native_arrival_admission(source):
    helper = section(source,
        "function M._map015FastTruck.unarrived", "function M._map015FastTruck.finish")
    if not all(value in helper for value in ('"endTime"', '"arriveTs"')):
        raise AssertionError("Lua must reject expiry from either native arrival field")
    # The Lua helper is meaningful only when applied before the seen UUID and
    # results list mutate; otherwise arrived rows can still escape to C#.
    pump = section(source,
        "function M._map015FastTruck.pump", "-- MAP-015 OWN_DESIGN")
    conditional = re.search(
        r"if\s+row\.uuid[^\n]*\n?(?:[^\n]*\n){0,4}?\s*"
        r"M\._map015FastTruck\.unarrived\(row,\s*captured_at_ms\)\s*then",
        pump)
    if not conditional:
        raise AssertionError("Native fast Truck admission does not invoke expiry check")
    seen = pump.find("s.seen[row.uuid] = true", conditional.end())
    append = pump.find("s.records[#s.records + 1] = row", conditional.end())
    if seen < conditional.end() or append < seen:
        raise AssertionError("Native expiry check must precede marking seen and appending")
    if not re.search(r"captured_at_ms\s*=\s*os\.time\(\)\s*\*\s*1000", pump):
        raise AssertionError("Native admission must use Unix milliseconds at capture")


def check_managed_arrival_admission(truck, world):
    gate = method(truck, "internal static bool FastTruckArrivalIsAfter(JsonElement row, long nowUnixMs)")
    if not all(token in gate for token in ('"endTime"', '"arriveTs"', '1000', '<= nowUnixMs')):
        raise AssertionError("C# expiry gate must normalize and reject both arrival deadlines")
    record_gate = method(truck,
        "private static bool FastTruckArrivalIsAfter(MapStoredRecord record, long nowUnixMs)")
    if "JsonDocument.Parse(record.DataJson)" not in record_gate or \
            "FastTruckArrivalIsAfter(document.RootElement, nowUnixMs)" not in record_gate:
        raise AssertionError("Stored Truck publication must recheck serialized arrival")

    acquire = method(truck, "private async Task<IReadOnlyList<FastTrainPrepared>> CaptureFastTruckRowsAsync(")
    check = acquire.find("FastTruckArrivalIsAfter(row, receiptTimeMs)")
    enqueue = acquire.find("currentRows.Add(row)")
    normalize = acquire.find("PrepareTrainRecords(")
    if min(check, enqueue, normalize) < 0 or not check < enqueue < normalize:
        raise AssertionError("Receipt-time expiry must filter BEFORE normalization")

    only = method(truck, "private static IReadOnlyList<MapScanBlockCapture> ProjectFastTruckOnly(")
    guard = re.search(r"FastTruckArrivalIsAfter\(raw\.Record,\s*"
                      r"(?:Now\(\)|DateTimeOffset\.UtcNow)\.ToUnixTimeMilliseconds\(\)\)", only)
    add = only.find("bucket.Add(item.Record)")
    if guard is None or add < 0 or guard.start() >= add:
        raise AssertionError("Truck-only publication must reject rows expired during processing")

    mixed = method(world, "private async Task<IReadOnlyList<MapScanBlockCapture>> CompleteFullWorldMapAsync(")
    truck_projection = section(mixed,
        'if (request.SelectedTypes.Contains("truck", StringComparer.Ordinal))',
        'if (request.SelectedTypes.Contains("railway", StringComparer.Ordinal))')
    if not re.search(r"if\s*\(FastTruckArrivalIsAfter\(item\.Record,\s*(?:Now\(\)|DateTimeOffset\.UtcNow)\.ToUnixTimeMilliseconds\(\)\)\)\s*"
                     r"AddRecordToBlock\(buckets,\s*item\.X,\s*item\.Y,\s*item\.Record\)",
                     truck_projection):
        raise AssertionError("Mixed all-nine publication must guard each Truck row")


def check_managed_rectangle_admission(truck):
    """Enforce source-bound native receipt independently of aggregate push count."""
    acquire = method(truck,
        "private async Task<IReadOnlyList<FastTrainPrepared>> CaptureFastTruckRowsAsync(")
    required = (
        'MatchesInt(response, "initialRequestsSent", 625)',
        'MatchesInt(response, "rectanglesCompleted", 625)',
        'MatchesInt(response, "unprovenRectangles", 0)',
        'MatchesInt(response, "responseFenceSize", 25)',
        'MatchesString(response, "responseAttribution", "bounded_sequential_fences")',
        'passCount is < 0 or > 2',
        'totalSent < 625',
        'observed < 625 || observed > totalSent',
        'MatchesInt(response, "repairRequestsSent", totalSent - 625)',
    )
    for guard in required:
        if guard not in acquire:
            raise AssertionError(f"C# native correlated rectangle admission missing: {guard}")
    if 'FastTruckSweepTimeout = TimeSpan.FromSeconds(240)' not in truck:
        raise AssertionError("Managed timeout must permit bounded 210s native repairs and cleanup")


class FastTruckArrivalR4(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.lua_source = LUA_PATH.read_text(encoding="utf-8")
        cls.truck_source = CS_TRUCK_PATH.read_text(encoding="utf-8")
        cls.world_source = CS_WORLD_PATH.read_text(encoding="utf-8")

    def test_actual_native_lua_boundary_and_unit_cases(self):
        check = production_lua_unarrived(self.lua_source)
        now = 1_792_103_400_000  # inert fixed 2026 epoch milliseconds
        for case, row, accepted in (
            ("past-milliseconds", {"endTime": now - 1}, False),
            ("at-arrival-milliseconds", {"endTime": now}, False),
            ("future-milliseconds", {"endTime": now + 1}, True),
            ("past-seconds", {"arriveTs": now // 1000 - 1}, False),
            ("at-arrival-seconds", {"arriveTs": now // 1000}, False),
            ("future-seconds", {"arriveTs": now // 1000 + 1}, True),
            ("earlier-field-wins", {"endTime": now + 5000, "arriveTs": now - 1}, False),
            ("other-earlier-field-wins", {"endTime": now - 1, "arriveTs": now + 5000}, False),
            ("unknown-zero-not-interpreted-as-expired", {"endTime": 0, "arriveTs": now + 1}, True),
        ):
            with self.subTest(case=case):
                self.assertEqual(check(row, now), accepted)

    def test_native_capture_uses_the_gate_before_record_append(self):
        check_native_arrival_admission(self.lua_source)

    def test_arrival_guard_removal_or_off_by_one_is_detected(self):
        altered = self.lua_source.replace(
            "M._map015FastTruck.unarrived(row, captured_at_ms) then", "true then", 1)
        with self.assertRaisesRegex(AssertionError, "does not invoke expiry"):
            check_native_arrival_admission(altered)
        changed = self.lua_source.replace("if arrival <= captured_at_ms then return false end",
                                          "if arrival < captured_at_ms then return false end", 1)
        self.assertTrue(production_lua_unarrived(changed)({"endTime": 1_792_103_400_000}, 1_792_103_400_000))

    def test_csharp_receipt_and_both_publication_paths_have_guard(self):
        check_managed_arrival_admission(self.truck_source, self.world_source)

    def test_csharp_guard_bypass_is_detected_at_each_boundary(self):
        corruptions = (
            (self.truck_source.replace("if (!FastTruckArrivalIsAfter(row, receiptTimeMs)) continue;",
                                       "if (false) continue;", 1), self.world_source, "Receipt-time"),
            (self.truck_source.replace("if (!FastTruckArrivalIsAfter(raw.Record, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())) continue;",
                                       "if (false) continue;", 1), self.world_source, "Truck-only"),
            (self.truck_source, self.world_source.replace(
                "if (FastTruckArrivalIsAfter(item.Record, Now().ToUnixTimeMilliseconds()))",
                "if (true)", 1), "Mixed all-nine"),
            (self.truck_source.replace("if (arrivalMs <= nowUnixMs) return false;",
                                       "if (arrivalMs < nowUnixMs) return false;", 1), self.world_source,
             "expiry gate"),
        )
        for truck, world, failure in corruptions:
            with self.subTest(failure=failure):
                with self.assertRaisesRegex(AssertionError, failure):
                    check_managed_arrival_admission(truck, world)


class FastTruckRectangleR4(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.lua_source = LUA_PATH.read_text(encoding="utf-8")
        cls.truck_source = CS_TRUCK_PATH.read_text(encoding="utf-8")

    def test_foreign_march_push_cannot_close_missing_625th_rectangle(self):
        receipt = simulate_native_one_tick(self.lua_source)["receipt"]
        self.assertIsNotNone(receipt, "Native lane did not publish a terminal receipt")
        self.assertNotEqual(receipt["state"], "proven",
                            "A stamp without the matching requested rectangle was falsely accepted")
        self.assertEqual(receipt["error"], "unattributed_march_push")
        self.assertEqual(receipt["rectanglesCompleted"], 624)
        self.assertEqual(receipt["unprovenRectangles"], 1)
        self.assertEqual(len(receipt["trainRecords"]), 0,
                         "Unmatched terminal must discard native staged Truck rows")

    def test_complete_25_rectangle_fence_receives_exactly_25_pushes(self):
        scenario = r'''
s.marchManager._lastGetBlockMsgTime = s.lastPushStamp
s.group = {indices = {}, sent = 25, acks = 25, cursor = 26}
for n = 600, 624 do s.group.indices[#s.group.indices+1] = n end
for n = 600, 624 do s.completed[n] = nil end
s.verified = 600
s.pushes = 625
'''
        trial = simulate_native_one_tick(self.lua_source, scenario)
        self.assertIsNotNone(trial["receipt"])
        self.assertEqual(trial["receipt"]["state"], "proven")
        self.assertEqual(trial["receipt"]["rectanglesCompleted"], 625)
        self.assertEqual(trial["receipt"]["unprovenRectangles"], 0)
        self.assertEqual(trial["receipt"]["responseFenceSize"], 25)
        self.assertEqual(trial["receipt"]["responseAttribution"], "bounded_sequential_fences")
        self.assertEqual(trial["receipt"]["initialRequestsSent"], 625)
        self.assertEqual(trial["receipt"]["repairRequestsSent"], 0)
        self.assertEqual(trial["receipt"]["repairPasses"], 0)
        self.assertTrue(trial["receipt"]["cameraStable"])
        self.assertTrue(trial["receipt"]["curViewRestored"])
        self.assertEqual(trial["sent"], [])

    def test_incomplete_25_push_fence_repairs_all_25_ambiguous_rectangles(self):
        scenario = r'''
s.marchManager._lastGetBlockMsgTime = s.lastPushStamp
s.lastSend = 996
s.group = {indices = {}, sent = 25, acks = 24, cursor = 26}
for n = 600, 624 do s.group.indices[#s.group.indices+1] = n end
for n = 600, 624 do s.completed[n] = nil end
s.verified = 600
'''
        trial = simulate_native_one_tick(self.lua_source, scenario)
        self.assertIsNone(trial["receipt"])
        self.assertEqual(trial["verified"], 600,
                         "24/25 acknowledges cannot certify any exact rectangle in that fence")
        self.assertEqual(trial["pass"], 1)
        self.assertEqual(trial["repair"], list(range(600, 625)))
        self.assertEqual(trial["sent"], [600],
                         "Repair must start at the first unproven rectangle and serialize sends")
        self.assertTrue(trial["group"])

        # Negative mutation: pretending 24/25 is complete would silently
        # certify all 25 and skip the selective repair pass.
        corrupted = self.lua_source.replace("if s.group.acks == s.group.sent then",
                                            "if s.group.acks >= s.group.sent - 1 then", 1)
        altered = simulate_native_one_tick(corrupted, scenario)
        self.assertNotEqual(altered["verified"], trial["verified"])

    def test_one_missing_rectangle_retries_only_that_unproven_index(self):
        trial = simulate_native_one_tick(self.lua_source,
            's.marchManager._lastGetBlockMsgTime = s.lastPushStamp\n'
            's.lastSend = 998\n')
        self.assertEqual(trial["pass"], 1)
        self.assertEqual(trial["sent"], [624],
                         "Repair sent a verified rectangle or retried the whole 625")
        self.assertEqual(trial["verified"], 624)
        self.assertEqual(trial["repair"], [624])

        corrupted = self.lua_source.replace(
            "if not s.completed[n] then s.repair[#s.repair + 1] = n end",
            "s.repair[#s.repair + 1] = n", 1)
        altered = simulate_native_one_tick(corrupted,
            's.marchManager._lastGetBlockMsgTime = s.lastPushStamp\n'
            's.lastSend = 998\n')
        self.assertEqual(altered["sent"], [0],
                         "Mutation sanity: whole-world retry must be distinguished")

    def test_single_outstanding_repair_ack_can_finish(self):
        scenario = r'''
s.marchManager._lastGetBlockMsgTime = s.lastPushStamp
s.group = {indices = {624}, sent = 1, acks = 1, cursor = 2}
s.pass = 1
s.repair = {624}
s.repairCursor = 2
s.sent = 626
s.pushes = 625
'''
        trial = simulate_native_one_tick(self.lua_source, scenario)
        self.assertEqual(trial["receipt"]["state"], "proven")
        self.assertEqual(trial["receipt"]["rectanglesCompleted"], 625)
        self.assertEqual(trial["receipt"]["repairRequestsSent"], 1)
        self.assertEqual(trial["receipt"]["repairPasses"], 1)
        self.assertEqual(trial["receipt"]["unprovenRectangles"], 0)
        self.assertTrue(trial["completed624"])

    def test_second_repair_pass_retries_only_remaining_unverified_rectangle(self):
        trial = simulate_native_one_tick(self.lua_source,
            's.marchManager._lastGetBlockMsgTime = s.lastPushStamp\n'
            's.pass = 1\n'
            's.repair = {624}\n'
            's.repairCursor = 2\n'
            's.lastSend = 999.9\n')
        self.assertIsNone(trial["receipt"])
        self.assertEqual(trial["pass"], 2)
        self.assertEqual(trial["repair"], [624])
        self.assertEqual(trial["sent"], [624])

    def test_two_exhausted_repair_passes_fail_closed(self):
        trial = simulate_native_one_tick(self.lua_source,
            's.marchManager._lastGetBlockMsgTime = s.lastPushStamp\n'
            's.pass = 2\n'
            's.repairCursor = 2\n')
        self.assertEqual(trial["receipt"]["state"], "failed")
        self.assertEqual(trial["receipt"]["error"], "lost_march_push")
        self.assertEqual(trial["sent"], [])
        self.assertEqual(trial["receipt"]["repairPasses"], 2)
        self.assertEqual(trial["receipt"]["unprovenRectangles"], 1)

    def test_full_625_push_count_with_unverified_rectangles_must_fail(self):
        trial = simulate_native_one_tick(self.lua_source,
            's.marchManager._lastGetBlockMsgTime = s.lastPushStamp\n'
            's.pass = 2\n'
            's.repairCursor = 2\n'
            's.pushes = 625\n')
        self.assertEqual(trial["receipt"]["state"], "failed")
        self.assertEqual(trial["receipt"]["rectanglesCompleted"], 624)
        self.assertEqual(trial["receipt"]["unprovenRectangles"], 1)

    def test_native_bounded_deadline_disallows_further_repairs(self):
        trial = simulate_native_one_tick(self.lua_source,
            's.marchManager._lastGetBlockMsgTime = s.lastPushStamp\n'
            's.startedAt = 790\n')
        self.assertEqual(trial["receipt"]["state"], "failed")
        self.assertEqual(trial["receipt"]["error"], "bounded_repair_deadline")
        self.assertTrue(trial["receipt"]["curViewRestored"])
        self.assertEqual(trial["sent"], [])

    def test_csharp_native_receipt_acceptance_rechecks_each_rectangle_metric(self):
        check_managed_rectangle_admission(self.truck_source)

    def test_csharp_receipt_field_mutations_are_detected(self):
        for old, new in (
            ('MatchesInt(response, "rectanglesCompleted", 625)', 'MatchesInt(response, "rectanglesCompleted", 624)'),
            ('MatchesInt(response, "unprovenRectangles", 0)', 'MatchesInt(response, "unprovenRectangles", 1)'),
            ('passCount is < 0 or > 2', 'passCount is < 0 or > 3'),
            ('MatchesInt(response, "initialRequestsSent", 625)', 'MatchesInt(response, "initialRequestsSent", 624)'),
            ('MatchesInt(response, "responseFenceSize", 25)', 'MatchesInt(response, "responseFenceSize", 50)'),
            ('"bounded_sequential_fences"', '"uncorrelated_stamps"'),
        ):
            with self.subTest(field=old):
                mutated = self.truck_source.replace(old, new, 1)
                self.assertNotEqual(mutated, self.truck_source)
                with self.assertRaisesRegex(AssertionError, "C# native correlated rectangle admission"):
                    check_managed_rectangle_admission(mutated)


class FastTruckReceiptClockR4(unittest.TestCase):
    """An arrival between receipt admission and parsing must not corrupt row reconciliation."""

    @classmethod
    def setUpClass(cls):
        cls.truck = CS_TRUCK_PATH.read_text(encoding="utf-8")
        cls.world = CS_WORLD_PATH.read_text(encoding="utf-8")

    @staticmethod
    def require_one_receipt_clock(truck, world):
        capture = method(truck, "CaptureFastTruckRowsAsync(")
        prepare = method(world, "private static IReadOnlyList<FastTrainPrepared> PrepareTrainRecords(")
        if ("long receiptTimeMs = Now().ToUnixTimeMilliseconds();" not in capture or
                "FastTruckArrivalIsAfter(row, receiptTimeMs)" not in capture or
                "Enumerable.Range(0, 10000).ToHashSet(), capturedAt,\n                receiptTimeMs" not in capture):
            raise AssertionError("Truck parsing must reuse its own receipt admission clock")
        if ("long? truckReceiptAdmissionUnixMs = null" not in world or
                "truckReceiptAdmissionUnixMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()" not in prepare):
            raise AssertionError("Train importer lost the fixed native receipt admission time")
        if ("FastTruckArrivalIsAfter(raw.Record, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())" not in truck or
                "FastTruckArrivalIsAfter(item.Record, Now().ToUnixTimeMilliseconds())" not in world):
            raise AssertionError("Truck arrival must still be rechecked during publication")

    def test_receipt_admission_clock_is_reused_and_publication_rechecks(self):
        self.require_one_receipt_clock(self.truck, self.world)

    def test_later_parse_clock_mutation_is_detected(self):
        mutated = self.truck.replace(
            "Enumerable.Range(0, 10000).ToHashSet(), capturedAt,\n                receiptTimeMs",
            "Enumerable.Range(0, 10000).ToHashSet(), capturedAt,\n                Now().ToUnixTimeMilliseconds()", 1)
        self.assertNotEqual(mutated, self.truck)
        with self.assertRaisesRegex(AssertionError, "receipt admission clock"):
            self.require_one_receipt_clock(mutated, self.world)

    def test_importer_independent_clock_mutation_is_detected(self):
        mutated = self.world.replace(
            "truckReceiptAdmissionUnixMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()",
            "DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()", 1)
        self.assertNotEqual(mutated, self.world)
        with self.assertRaisesRegex(AssertionError, "fixed native receipt admission time"):
            self.require_one_receipt_clock(self.truck, mutated)

    def test_publication_expiry_bypass_is_detected(self):
        mutated = self.truck.replace(
            "FastTruckArrivalIsAfter(raw.Record, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())",
            "FastTruckArrivalIsAfter(raw.Record, receiptTimeMs)", 1)
        self.assertNotEqual(mutated, self.truck)
        with self.assertRaisesRegex(AssertionError, "rechecked during publication"):
            self.require_one_receipt_clock(mutated, self.world)


if __name__ == "__main__":
    unittest.main()
