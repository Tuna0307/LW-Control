"""Execute actual periodic-budget Lua with competing lifecycle callbacks."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime

source = (ROOT / 'tools/current_overview_bridge.lua').read_text(encoding='utf-8')
begin = source.index('local last_overview_tick, last_feature_tick')
end = source.index('local feature018_periodic_tick', begin)
lua = LuaRuntime(unpack_returned_tuples=True)
lua.execute('''M={};clock=0;calls=0;frames=0
function runtime_clock() return clock end
function profile_frame() frames=frames+1 end
function M.Pump() calls=calls+1;return true end''')
lua.execute(source[begin:end])
features = 0
for frame in range(1201):
    lua.globals().clock = frame / 120
    for entry in range(3):
        lua.globals().M.TryPump()
        features += bool(lua.globals().M.ShouldPumpFeature018())
assert 170 <= lua.globals().calls <= 201, lua.globals().calls
assert features == lua.globals().calls, (features, lua.globals().calls)
assert lua.globals().frames == 3603, 'diagnostics must still observe every entry/frame'
lua.globals().clock = -1
assert lua.globals().M.TryPump() is True, 'clock reset cannot stall the bridge'
assert lua.globals().M.ShouldPumpFeature018() is True
print('R29_PUMP_BUDGET_PASS: competing Update/LateUpdate/callback bounded; frame samples retained; clock reset recovered')
