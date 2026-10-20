"""Execute the real bounded reflection helper in an inert Lua53 VM only."""

from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime


SOURCE = (ROOT / "tools/current_overview_bridge.lua").read_text(encoding="utf-8")
HELPERS = SOURCE[SOURCE.index("local REFLECTION_CACHE_MAX_TYPES ="):SOURCE.index("local function reflected_type_name")]
FAKES = """
flags = 52
local function reflection_flags() return flags end
function new_type()
    local t = {fieldCalls=0, propertyCalls=0, fields={}, properties={}}
    function t:GetField(key, binding)
        self.fieldCalls = self.fieldCalls + 1
        if self.fieldFail then self.fieldFail=false; error('transient field resolver failure') end
        return self.fields[key]
    end
    function t:GetProperty(key, binding)
        self.propertyCalls = self.propertyCalls + 1
        if self.propertyFail then self.propertyFail=false; error('transient property resolver failure') end
        return self.properties[key]
    end
    return t
end
function instance(t, value)
    return {value=value, GetType=function() return t end}
end
function descriptor()
    return {GetValue=function(self, object)
        if object.getterFail then error('transient live getter failure') end
        return object.value
    end}
end
"""


class ReflectionCacheChecks(unittest.TestCase):
    def check_lua(self, body):
        runtime = LuaRuntime(unpack_returned_tuples=True)
        runtime.execute(FAKES + HELPERS + body)

    def test_live_values_instances_and_types_are_never_cached(self):
        self.check_lua("""
local t = new_type(); t.fields.current = descriptor()
local a,b = instance(t,'first'), instance(t,'second')
assert(reflected_value(a,'current')=='first')
a.value='changed'; assert(reflected_value(a,'current')=='changed')
assert(reflected_value(b,'current')=='second')
assert(t.fieldCalls==1 and t.propertyCalls==0)
local other = new_type(); other.fields.current = descriptor()
local c=instance(other,'other type'); assert(reflected_value(c,'current')=='other type')
assert(other.fieldCalls==1)
flags=56; assert(reflected_value(a,'current')=='changed'); assert(t.fieldCalls==2)
""")

    def test_nil_field_value_returns_nil_without_property_fallback(self):
        self.check_lua("""
local t=new_type(); t.fields.current=descriptor()
t.properties.current={GetValue=function() return 'wrong fallback' end}
local a=instance(t,nil)
assert(reflected_value(a,'current')==nil and t.propertyCalls==0)
a.value='fresh'; assert(reflected_value(a,'current')=='fresh')
assert(t.fieldCalls==1 and t.propertyCalls==0)
""")

    def test_cached_field_getter_failure_still_falls_back_to_live_property(self):
        self.check_lua("""
local t=new_type(); t.fields.current=descriptor()
t.properties.current={GetValue=function(self,o) return o.fallback end}
local a=instance(t,'field'); a.fallback='property'
assert(reflected_value(a,'current')=='field')
a.getterFail=true; assert(reflected_value(a,'current')=='property')
a.fallback='property changed'; assert(reflected_value(a,'current')=='property changed')
a.getterFail=false; a.value='field recovered'; assert(reflected_value(a,'current')=='field recovered')
assert(t.fieldCalls==1 and t.propertyCalls==1)
""")

    def test_resolver_exceptions_are_retried_not_cached_as_absence(self):
        self.check_lua("""
local t=new_type(); t.fields.current=descriptor(); t.fieldFail=true
local a=instance(t,'fresh')
assert(reflected_value(a,'current')==nil)
assert(reflected_value(a,'current')=='fresh' and t.fieldCalls==2)
local p=new_type(); p.properties.current=descriptor(); p.propertyFail=true
local b=instance(p,'property recovered')
assert(reflected_value(b,'current')==nil)
assert(reflected_value(b,'current')=='property recovered')
assert(p.fieldCalls==1 and p.propertyCalls==2)
""")

    def test_nil_descriptors_and_throwing_property_preserve_original_result(self):
        self.check_lua("""
assert(reflected_value(nil,'missing')==nil)
assert(reflected_value({GetType=function() error('no type') end},'missing')==nil)
local t=new_type(); local a=instance(t,'unused')
assert(reflected_value(a,'missing')==nil and reflected_value(a,'missing')==nil)
assert(t.fieldCalls==1 and t.propertyCalls==1)
t.properties.current=descriptor(); a.getterFail=true
assert(reflected_value(a,'current')==nil)
a.getterFail=false; a.value='recovered'; assert(reflected_value(a,'current')=='recovered')
assert(t.fieldCalls==2 and t.propertyCalls==2)
""")

    def test_fifo_type_and_member_eviction_are_bounded(self):
        self.check_lua("""
local first=new_type(); first.fields.current=descriptor()
local a=instance(first,'first'); assert(reflected_value(a,'current')=='first')
for i=1,REFLECTION_CACHE_MAX_TYPES do
    local t=new_type(); t.fields.current=descriptor(); reflected_value(instance(t,i),'current')
end
assert(#reflection_type_order==REFLECTION_CACHE_MAX_TYPES)
assert(reflection_member_cache[first]==nil)
assert(reflected_value(a,'current')=='first' and first.fieldCalls==2)
for i=1,REFLECTION_CACHE_MAX_MEMBERS do reflected_value(a,'missing'..i) end
local entry=reflection_member_cache[first]
assert(#entry.order==REFLECTION_CACHE_MAX_MEMBERS)
assert(entry.members['52:current']==nil)
assert(reflected_value(a,'current')=='first' and first.fieldCalls==REFLECTION_CACHE_MAX_MEMBERS+3)
local types,members=0,0
for _,v in pairs(reflection_member_cache) do
    types=types+1; local n=0; for _ in pairs(v.members) do n=n+1 end
    assert(n<=REFLECTION_CACHE_MAX_MEMBERS); members=members+n
end
assert(types==REFLECTION_CACHE_MAX_TYPES)
assert(members<=REFLECTION_CACHE_MAX_TYPES*REFLECTION_CACHE_MAX_MEMBERS)
""")


if __name__ == "__main__":
    unittest.main(verbosity=2)
