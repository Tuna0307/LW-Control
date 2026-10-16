"""CONTROLLED/INERT MAP-017 point/March ordering checks under Lua 5.3.

Executes actual extracted production helpers against a synthetic native timeline.
No game, app, network, installed script or native DateTime is touched. These
checks establish queue fence behavior, not a genuine owner-case reproduction.
"""
from pathlib import Path
import sys

repo = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(repo / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

source = (repo / "tools/current_live_resource_probe.lua").read_text(encoding="utf-8")
classification = source[source.index("local function same_enum_value("):
                        source.index("local function monster_invasion_protection_deadline(")]
capture = source[source.index("function M._map015CameraFree.capture(s)"):
                 source.index("function M._map015CameraFree.pump(now)")]

prelude = r'''
local function safe_get(target, key) return target and target[key] end
local function reflected_value(target, key) return safe_get(target, key) end
local function scalar_field(target, names)
    for _, name in ipairs(names) do if target and target[name] ~= nil then return target[name] end end
end
local function call(target, method, ...)
    local fn = safe_get(target, method)
    if type(fn) ~= 'function' then return false, nil end
    return pcall(fn, target, ...)
end
local M = { _map015CameraFree = {} }
local clock, point_version, point_reads = 0, 7, 0
local function runtime_clock() return clock end
local native_rows = {}
local function city_aoi_records()
    point_reads = point_reads + 1
    return {{ kind = 'player_base', pointVersion = point_version }}
end
local function monster_march_aoi_records(world, block_size, block_count, selected, home)
    local rows = {}
    for _, row in ipairs(native_rows) do
        local aoi = math.floor(row.y / block_size) * block_count + math.floor(row.x / block_size)
        if selected[aoi] then rows[#rows + 1] = row end
    end
    return rows
end
local function datetime(value)
    return { value = value, CompareTo = function(self, other)
        if self.value < other.value then return -1 end
        if self.value > other.value then return 1 end
        return 0
    end }
end
local function state(received, handled, before)
    return { pending = { indices = { 0 }, marchBeforeSend = datetime(before or 10) },
        world = { MarchDataManager = {
            _lastGetBlockMsgTime = datetime(received or 10),
            _lastHandleBlockMsgTime = datetime(handled or 10),
        } }, batches = {}, completed = 0,
        wallClockStartMs = 1800000000000, startedAt = 0,
        includeCity = true, includeMonster = true, includeMonsterProtection = false }
end
'''
exercise = r'''
local function check(value, reason) assert(value, 'MAP017_ZOMBIE_SOURCE_LUA: ' .. reason) end
WorldMonsterSpecialType = { MonsterInvasionBoss = 10 }
check(M._map017MonsterClassifierAvailable(), 'game-owned discriminator admitted')
check(is_monster_invasion_template({ special = 10 }), 'special 10 recognized')
check(not is_monster_invasion_template({ special = 30 }), 'other boss never substituted')
WorldMonsterSpecialType = nil
check(not M._map017MonsterClassifierAvailable(), 'missing discriminator unavailable')

-- Point reply precedes both receipt and processing of its companion push.
local s = state()
clock = 1
local ok, err = M._map015CameraFree.capture(s)
check(not ok and err == 'march_pending' and #s.batches == 0 and s.completed == 0,
    'point reply alone cannot advance footprint or publish an empty Monster set')
check(s.pending.pointBatch.point_records[1].pointVersion == 7, 'fresh point batch staged immediately')
s.world.MarchDataManager._lastGetBlockMsgTime = datetime(11)
point_version, clock = 99, 1.5
ok, err = M._map015CameraFree.capture(s)
check(not ok and err == 'march_pending', 'received but unprocessed companion remains pending')
native_rows = {{ uuid = 'controlled-boss', x = 5, y = 5, monsterProtectionEligible = true }}
s.world.MarchDataManager._lastHandleBlockMsgTime = datetime(11)
clock = 1.7
ok, err = M._map015CameraFree.capture(s)
check(ok and err == nil and s.completed == 1 and s.pending == nil, 'handled companion completes original footprint')
check(#s.batches[1].monster_march_records == 1, 'late original-footprint boss retained')
check(point_reads == 1 and s.batches[1].point_records[1].pointVersion == 7,
    'point source read once and survives eviction/change during March wait')
check(s.batches[1].marchSource.receivedComparedToBeforeSend == 1 and
      s.batches[1].marchSource.handledComparedToReceived == 0,
    'native comparison results retained without opaque-string ordering')
check(s.batches[1].marchCapturedAtUnixMs > s.batches[1].capturedAtUnixMs,
    'point and later March observations keep independent timestamps')

-- A more recent unrelated handle timestamp cannot certify a stale receive.
s = state(10, 12, 10)
clock = 3
ok, err = M._map015CameraFree.capture(s)
check(not ok and err == 'march_pending', 'new handler with stale receipt rejected')
clock = 5
ok, err = M._map015CameraFree.capture(s)
check(not ok and err == 'march_processing_timeout' and #s.batches == 0,
    'absent companion stops after bounded wait without publishing staged points')

s, clock = state(12, 11, 10), 6
ok, err = M._map015CameraFree.capture(s)
check(not ok and err == 'march_pending', 'reordered receipt waits for its own processed stage')

s = state(12, 12, 10)
s.world.MarchDataManager._lastHandleBlockMsgTime = 'opaque native timestamp'
ok, err = M._map015CameraFree.capture(s)
check(not ok and err == 'march_datetime_comparison_unavailable', 'unsupported native comparison rejected')
check(M._map017CompareDateTimes(nil, datetime(1)) == nil, 'missing timestamp rejected')
check(M._map017CompareDateTimes({ CompareTo = function() return 7 end }, datetime(1)) == nil,
    'malformed comparison rejected')

s, clock = state(12, 13, 11), 7
ok, err = M._map015CameraFree.capture(s)
check(ok and s.batches[1].marchSource.handledComparedToReceived == 1,
    'already processed companion needs no artificial delay')
'''

lua = LuaRuntime(unpack_returned_tuples=True)
lua.execute(prelude + classification + capture + exercise)
print("MAP017_ZOMBIE_SOURCE_LUA_CONTROLLED_INERT_OK delayed/reordered/absent/unsupported/point-retention")
