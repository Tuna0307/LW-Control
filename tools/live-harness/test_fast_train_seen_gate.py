"""Inert executable contract for the production fast Train march hot path.

Runs the actual Lua collector against mock main-thread game collections.
No game, native bridge, files, network, or scan owner are started.
"""

from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime


SOURCE = ROOT / "src/LWBridge.GameScripts/current_live_resource_probe.lua"
START = "local function train_march_aoi_records("
END = "\nlocal function same_enum_value("

FAKE_GAME = r"""
local MAX_POINTS = 50000
local counters = { typeReads = 0, uuidReads = 0, trainLookups = 0, enumerated = 0 }
local function safe_get(object, key)
    return object ~= nil and object[key] or nil
end
local function call(object, name, ...)
    local fn = safe_get(object, name)
    if type(fn) ~= "function" then return false, nil end
    return pcall(fn, object, ...)
end
local function reflected_value(object, key) return safe_get(object, key) end
local function integer_field(object, names)
    if object and object.kind == "train" and names[1] == "type" then
        counters.typeReads = counters.typeReads + 1
    end
    for _, name in ipairs(names) do
        local numeric = tonumber(safe_get(object, name))
        if numeric ~= nil and numeric == math.floor(numeric) then return numeric end
    end
    return nil
end
local function scalar_field(object, names)
    if object and object.kind == "march" and names[1] == "uuid" then
        counters.uuidReads = counters.uuidReads + 1
    end
    for _, name in ipairs(names) do
        local value = safe_get(object, name)
        if value ~= nil then return value end
    end
    return nil
end
local function index_to_tile(world, index) return {x = index % 1000, y = math.floor(index / 1000)} end
local function current_server_id() return 2212 end
local function reflected_type_name() return "WorldMarch" end
local function normalize_train_current_goods() return {}, 1 end
local function resolve_reward_display_metadata() return "item", "icon" end
local function bounded_enumerator_completion_error() return nil end
DataCenter = { LWTrainDataManager = {
    GetOneTrain = function(_, uuid)
        counters.trainLookups = counters.trainLookups + 1
        return {type = 2, arriveTs = 9000000, maxLootPerTrain = 1,
            marchInfo = {robTimes = 1, protectTime = 9000000, vipOn = false},
            GetCurRewardData = function() return {} end,
            ToJson = function() return "{}" end }
    end,
} }
local function mock_world(marches)
    local collection = {
        GetEnumerator = function(self)
            local enumerator = {offset = 0, Current = nil}
            function enumerator:MoveNext()
                self.offset = self.offset + 1
                if marches.failMoveAt == self.offset then error("fixture_enumerator_failure") end
                self.Current = marches[self.offset] and {Value = marches[self.offset]} or nil
                if self.Current ~= nil then counters.enumerated = counters.enumerated + 1 end
                return self.Current ~= nil
            end
            return enumerator
        end,
    }
    return {MarchDataManager = {GetAllMarchesByCS = function() return collection end}}
end
"""


def source_collector():
    source = SOURCE.read_text(encoding="utf-8")
    return START + source.split(START, 1)[1].split(END, 1)[0]


def native_record(value):
    """Keep every field for before/after source comparison, including rewards."""
    if value is None or isinstance(value, (str, bool, int, float)):
        return value
    try:
        keys = list(value.keys())
    except AttributeError:
        return value
    if not keys:
        return []
    if all(type(key) is int and key >= 1 for key in keys):
        return [native_record(value[index]) for index in sorted(keys)]
    return {str(key): native_record(value[key]) for key in keys}


def run_case(scenario):
    lua = LuaRuntime(unpack_returned_tuples=True)
    source = FAKE_GAME + "\n" + source_collector() + "\n" + scenario
    result = lua.execute(source)
    rows = [native_record(result["rows"][index])
            for index in range(1, len(result["rows"]) + 1)]
    counts = {key: result["counters"][key] for key in
              ("typeReads", "uuidReads", "trainLookups", "enumerated")}
    return rows, counts, result["error"]


def march(uuid, train_type, position=1001):
    # Fixture content is intentionally simple Lua data, with no account ID.
    return (
        '{kind="march",uuid="%s",position=%d,'
        'GetMarchCurPosIndex=function(self) return self.position end,'
        'ownerName="fixture",endTime=9000000,'
        'train={kind="train",type=%s,uuid="t%s",cfgId=42,'
        'config={quality=3,carriageNum=2}}}'
        % (uuid, position, train_type, uuid)
    )


def scenario(marches, *, seen=None, capture=3):
    seen_lua = "nil" if seen is None else (
        "{" + ",".join('["%s"]=true' % uuid for uuid in seen) + "}"
    )
    return (
        "local rows, error_text = train_march_aoi_records("
        "mock_world({" + ",".join(marches) + "}),10,100,{[0]=true},"
        + seen_lua + "," + str(capture) + ")\n"
        "return {rows=rows or {},error=error_text,counters=counters}\n"
    )


class FastTrainSeenGate(unittest.TestCase):
    def test_seen_uuid_skips_reflection_but_fresh_uuid_is_still_hydrated(self):
        rows, counts, error = run_case(scenario(
            [march("seen", 1), march("new", 1)], seen=("seen",), capture=1))
        self.assertIsNone(error)
        self.assertEqual([row["uuid"] for row in rows], ["new"])
        self.assertEqual(rows[0]["trainType"], 1)
        self.assertEqual(rows[0]["ownerName"], "fixture")
        self.assertEqual(rows[0]["trainCfgId"], 42)
        self.assertEqual(rows[0]["trainQuality"], 3)
        self.assertEqual(rows[0]["carriageNum"], 2)
        self.assertTrue(rows[0]["truckMetadataKnown"])
        self.assertEqual(counts["enumerated"], 2)
        self.assertEqual(counts["typeReads"], 1)
        self.assertEqual(counts["trainLookups"], 1)
        self.assertEqual(counts["uuidReads"], 2)

    def test_both_mobile_kinds_remain_distinct_and_seen_railway_is_skipped(self):
        rows, counts, error = run_case(scenario(
            [march("seenRail", 2), march("truck", 1), march("rail", 2)],
            seen=("seenRail",), capture=3))
        self.assertIsNone(error)
        self.assertEqual([(row["uuid"], row["trainType"]) for row in rows],
                         [("truck", 1), ("rail", 2)])
        self.assertEqual(counts["typeReads"], 2)
        self.assertEqual(counts["trainLookups"], 2)

    def test_type_mismatch_is_filtered_in_fast_truck_lane(self):
        rows, counts, error = run_case(scenario(
            [march("rail", 2)], seen=(), capture=1))
        self.assertIsNone(error)
        self.assertEqual(rows, [])
        self.assertEqual(counts["typeReads"], 1)
        self.assertEqual(counts["trainLookups"], 0)

    def test_enum_string_type_hint_is_still_authoritative(self):
        rows, counts, error = run_case(scenario(
            [march("rail", '"TrainType: 2"')], seen=(), capture=3))
        self.assertIsNone(error)
        self.assertEqual([(row["uuid"], row["trainType"]) for row in rows],
                         [("rail", 2)])
        self.assertEqual(counts["trainLookups"], 1)

    def test_legacy_no_seen_path_preserves_traindata_type_fallback(self):
        rows, counts, error = run_case(scenario(
            [march("unknown", "nil")], seen=None))
        self.assertIsNone(error)
        self.assertEqual([(row["uuid"], row["trainType"]) for row in rows],
                         [("unknown", 2)])
        self.assertEqual(counts["trainLookups"], 1)
        self.assertGreaterEqual(counts["typeReads"], 1)

    def test_enumerator_error_discards_partial_rows(self):
        setup = (
            "local marches={" + march("first", 1) + "," + march("second", 1) + "}\n"
            "marches.failMoveAt = 2\n"
            "local rows, error_text = train_march_aoi_records("
            "mock_world(marches),10,100,{[0]=true},{},1)\n"
            "return {rows=rows or {},error=error_text,counters=counters}\n"
        )
        rows, counts, error = run_case(setup)
        self.assertEqual(rows, [])
        self.assertEqual(error, "march_enumerator_failed")
        self.assertEqual(counts["enumerated"], 1)

    def test_moving_seen_uuid_stays_deduplicated_but_new_march_uuid_is_seen(self):
        rows, counts, error = run_case(scenario(
            [march("seen", 1, 2001), march("newMarch", 1, 1001)],
            seen=("seen",), capture=1))
        self.assertIsNone(error)
        self.assertEqual([row["uuid"] for row in rows], ["newMarch"])
        self.assertEqual(counts["trainLookups"], 1)
        self.assertEqual(counts["typeReads"], 1)


if __name__ == "__main__":
    unittest.main()
