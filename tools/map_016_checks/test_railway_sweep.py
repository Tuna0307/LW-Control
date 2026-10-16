"""MAP-016 Railway regression checks: real Lua, inert native owners only."""
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/map_014_lead_native"))
from test_map015_fast_truck_r4 import (LUA_PATH, method, section,
                                     simulate_native_one_tick)
from lupa.lua53 import LuaRuntime


def railway_tick(source, scenario=""):
    setup = r'''
s.railwayOnly = true
s.includeRailway = true
s.captureType = 2
s.railwayRecords = {}
s.records = {}
s.marchManager._lastGetBlockMsgTime = s.lastPushStamp
s.group = {indices = {}, sent = 25, acks = 25, cursor = 26}
for n = 600, 624 do s.group.indices[#s.group.indices+1] = n end
for n = 600, 624 do s.completed[n] = nil end
s.verified = 600
s.pushes = 625
'''
    return simulate_native_one_tick(source, setup + scenario)


class RailwayNative(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = LUA_PATH.read_text(encoding="utf-8")

    def test_genuinely_empty_railway_completes_all_625_without_truck_rows(self):
        receipt = railway_tick(self.source)["receipt"]
        self.assertEqual(receipt["state"], "proven")
        self.assertEqual(receipt["rectanglesCompleted"], 625)
        self.assertTrue(receipt["railwayOnly"])
        self.assertTrue(receipt["railwayIncluded"])
        self.assertEqual(receipt["railwayRecordCount"], 0)
        self.assertEqual(receipt["trainRecordCount"], 0)
        self.assertTrue(receipt["curViewRestored"])

    def test_station_dwelling_railway_does_not_use_truck_segment_expiry(self):
        receipt = railway_tick(self.source, r'''
function train_march_aoi_records(_, _, _, _, _, capture_type)
    assert(capture_type == 2)
    return {{uuid = "station-train", trainType = 2, endTime = 1}}, nil
end
''')["receipt"]
        self.assertEqual(receipt["state"], "proven")
        self.assertEqual(receipt["railwayRecordCount"], 1)
        self.assertEqual(receipt["railwayRecords"][1]["uuid"], "station-train")
        self.assertGreater(receipt["railwayRecords"][1]["capturedAtMs"], 0)
        self.assertEqual(receipt["trainRecordCount"], 0)

    def test_shared_sweep_keeps_truck_expiry_and_types_separate(self):
        receipt = railway_tick(self.source, r'''
s.railwayOnly = false
s.captureType = 3
function train_march_aoi_records(_, _, _, _, _, capture_type)
    assert(capture_type == 3)
    return {{uuid = "expired-truck", trainType = 1, endTime = 1},
            {uuid = "valid-truck", trainType = 1, endTime = os.time() * 1000 + 60000},
            {uuid = "dwelling-railway", trainType = 2, endTime = 1},
            {uuid = "unknown", trainType = 7}}, nil
end
''')["receipt"]
        self.assertEqual(receipt["state"], "proven")
        self.assertEqual(receipt["trainRecordCount"], 1)
        self.assertEqual(receipt["trainRecords"][1]["uuid"], "valid-truck")
        self.assertEqual(receipt["railwayRecordCount"], 1)
        self.assertEqual(receipt["railwayRecords"][1]["uuid"], "dwelling-railway")

    def test_missing_rectangle_cannot_certify_empty(self):
        receipt = railway_tick(self.source, r'''
s.group = nil
s.verified = 624
s.completed[624] = nil
s.pass = 2
s.repairCursor = 2
''')["receipt"]
        self.assertEqual(receipt["state"], "failed")
        self.assertEqual(receipt["error"], "lost_march_push")
        self.assertEqual(len(receipt["railwayRecords"]), 0)

    def test_actual_builder_type_filter_does_not_discard_railway_before_capture(self):
        # Execute the production builder with game-shaped inert dictionaries.
        body = section(self.source, "local function train_march_aoi_records(",
                       "local function same_enum_value(")
        harness = r'''
MAX_POINTS = 10
function safe_get(o,k) return o and o[k] end
function reflected_value(o,k) return o and o[k] end
function scalar_field(o,names) for _,k in ipairs(names) do if o and o[k] ~= nil then return o[k] end end end
integer_field = scalar_field
function reflected_type_name() return "WorldMarch" end
function current_server_id() return 2211 end
function index_to_tile() return {x=623, y=300} end
function normalize_train_current_goods() return {}, 2 end
function bounded_enumerator_completion_error() return nil end
function call(o,k,...) if o and type(o[k]) == "function" then return pcall(o[k], o, ...) end return false,nil end
local function world_for(kind)
 local march = {uuid="live-train", train={type=kind, config={quality=5,carriageNum=6},cfgId=152},
  GetMarchCurPosIndex=function() return 300624 end, serverId=2211, worldId=0}
 local collection = {GetEnumerator=function()
  local done=false
  return {MoveNext=function() if done then return false end done=true return true end,
   Current={Value=march}}
 end}
 return {MarchDataManager={GetAllMarchesByCS=function() return collection end}}
end
'''
        lua = LuaRuntime(unpack_returned_tuples=True)
        run = lua.execute(harness + "local function train_march_aoi_records(" + body + r'''
return function(kind, capture_type)
 local rows,err=train_march_aoi_records(world_for(kind),10,100,{[3062]=true},{},capture_type)
 assert(err==nil)
 return #rows
end
''')
        self.assertEqual(run(2, 2), 1)
        self.assertEqual(run(2, 3), 1)
        self.assertEqual(run(2, 1), 0)
        self.assertEqual(run(1, 2), 0)
        self.assertEqual(run(7, 3), 0)


class RailwayRouting(unittest.TestCase):
    def test_shared_receipt_is_used_once_and_recommendation_cannot_replace_it(self):
        world = (ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs").read_text("utf-8")
        camera = (ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.CameraFree.cs").read_text("utf-8")
        railway = (ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastRailway.cs").read_text("utf-8")
        truck = (ROOT / "src/LWBridge.Desktop/CurrentClientMapBlockSource.FastTruck.cs").read_text("utf-8")
        direct = section(world, "IReadOnlyList<FastTrainPrepared>? fastTrucks", "catch (Exception error)")
        self.assertIn('else if (request.SelectedTypes.Contains("railway"', direct)
        self.assertEqual(direct.count("CaptureFastRailwayRowsAsync("), 1)
        self.assertIn('request.SelectedTypes.All(kind => kind is "truck" or "railway")', direct)
        self.assertIn("ProjectFastMobileTrains(", direct)
        self.assertIn('&& !state.RailwaySweepVerified', world)
        self.assertIn('kind is not ("truck" or "railway")', camera)
        self.assertIn('state.RailwaySweepVerified = request.SelectedTypes.Contains("railway"', camera)
        self.assertIn("PrepareFastRailwayRows(response, request, capturedAt, railwayOnly: false)", truck)
        projection = method(railway, "ProjectFastMobileTrains(")
        self.assertNotIn("byBlock.Count == 0", projection)
        self.assertIn('raw.Record.Kind == "truck" &&', projection)
        for guard in ('"railwayIncluded", true', '"rectanglesCompleted", 625',
                      '"unprovenRectangles", 0', '"trainType", 2',
                      '"railwayRecordCount", nativeRows.GetArrayLength()'):
            self.assertIn(guard, railway)


if __name__ == "__main__":
    unittest.main()
