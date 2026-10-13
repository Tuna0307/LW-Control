"""Actual current Lua City producer, with inert native-view vs retained inverses.

No fake row here establishes a genuine City positive: genuine one-region native
qualification is kept in a separate owned receipt.
"""
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

TEXT = (ROOT / "tools/current_live_resource_probe.lua").read_text(encoding="utf-8")
FN = "local function city_aoi_records" + TEXT.split("local function city_aoi_records", 1)[1].split(
    "local function resource_source_metadata", 1)[0]
MODEL = r'''
local function safe_get(info,k) return info and info[k] end
local function reflected_value(manager,k) return manager and manager[k] end
local function call(manager,method)
    if manager and type(manager[method])=="function" then
        return true, manager[method]()
    end
    return false, nil
end
local function reflected_call(manager,method) return call(manager,method) end
local function collection_count(c) return c and #c or nil end
local function each(c,max,callback)
    local count=0
    for _,raw in ipairs(c) do
        if callback(raw)==false then break end
        count=count+1
    end
    return count
end
local function integer_field(info,names)
    for _,key in ipairs(names) do
        local v=tonumber(info[key])
        if v then return math.floor(v) end
    end
end
local function scalar_field(info,names)
    for _,key in ipairs(names) do
        local v=info[key]
        if v~=nil then return v end
    end
end
local function index_to_tile(world,id) return {x=id,y=10} end
local function reflected_type_name(info) return "BuildPointInfo" end
local function current_server_id() return 2212 end
local function player_city_health_snapshot(info)
    return {calculationMode="inert_only",effectiveHp=1}
end
local MAX_POINTS=50000
'''
CHECKS = r'''
local function row(id,owner)
  return {pointIndex=id,pointType=6,ownerUid=owner or tostring(id),
          playerName="controlled",serverId=2212,health=1}
end
local selected={[110]=true}
local function run(retained,current)
  return city_aoi_records({},{
     GetAllMainBaseList=function()return retained end,_pointInfos=current
  },10,100,selected)
end
local both={row(101),row(102)}
local records,err,diag=run(both,{row(101),row(102)})
assert(err==nil and #records==2)
assert(diag.nativeCurrentCityCount==2 and diag.missingNativeCurrentPointIndices==0)
-- Critical count-only false positive: counts are equal but point 102 has
-- been substituted with unrelated retained 103. Fail by identity.
records,err,diag=run({row(101),row(103)},{row(101),row(102)})
assert(records==nil and err=="city_native_candidate_identity_incomplete")
assert(diag.accepted==2 and diag.nativeCurrentCityCount==2)
assert(diag.missingNativeCurrentPointIndices==1)
records,err,diag=run({row(101),row(101)},{row(101)})
assert(records==nil and diag.duplicateRetainedPointIndices==1)
records,err,diag=run({row(101)},{row(101),row(102)})
assert(records==nil and diag.missingNativeCurrentPointIndices==1)
records,err,diag=run({row(101),row(102)},{row(101)})
assert(err==nil and #records==2 and diag.nativeCurrentCityCount==1)
records,err,diag=run({row(101)},{row(101,"")})
assert(err==nil and #records==1) -- native current alone lacks full owner fields
records,err,diag=run({row(101,"0")},{row(101)})
assert(records==nil and diag.missingOwnerUid==1)
return true
'''


class CitySourceSubset(unittest.TestCase):
    def test_real_native_producer_source_has_inverse_identity_join(self):
        self.assertTrue(LuaRuntime().execute(MODEL + FN + CHECKS))

    def test_new_source_remains_lua53_syntax_valid(self):
        code = LuaRuntime().eval("load")(TEXT, "city source", "t")
        self.assertFalse(isinstance(code, tuple), code)


if __name__ == "__main__":
    unittest.main()
