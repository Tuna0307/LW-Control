"""Controlled Lua frame deduplication/error preservation checks; no game."""
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

FIXTURE = (Path(__file__).with_name("frame_telemetry.lua")).read_text(encoding="utf-8")


class FrameTelemetryChecks(unittest.TestCase):
    def runtime(self):
        lua = LuaRuntime(unpack_returned_tuples=True)
        lua.execute('''
            CS={UnityEngine={Time={frameCount=1,realtimeSinceStartup=10,unscaledDeltaTime=0.02}}}
            local _trace_root="private"
            _control={profileId="p",sessionId="s",challenge="c",gamePid="123"}
            local function _trace_kv(path) return _control end
            local function _trace_json(t) local copy={} for k,v in pairs(t) do copy[k]=v end return copy end
            _writes=0
            io={open=function(path,mode) return {
                write=function(self,value) _captured=value;_writes=_writes+1 end,
                close=function(self) end} end}
            probe={Pump=function(...) return 42,"preserved" end}
        ''' + FIXTURE)
        return lua

    def test_multiple_pumps_never_count_the_same_frame_twice(self):
        lua = self.runtime()
        self.assertEqual(lua.eval("probe.Pump")(), (42, "preserved"))
        lua.execute("probe.Pump();probe.Pump()")
        self.assertEqual(lua.eval("_captured.samples"), 1)
        self.assertEqual(lua.eval("_writes"), 1)
        lua.execute("CS.UnityEngine.Time.frameCount=2;CS.UnityEngine.Time.realtimeSinceStartup=12;probe.Pump()")
        self.assertEqual(lua.eval("_captured.samples"), 2)

    def test_missing_owner_withholds_output_and_original_errors_propagate(self):
        lua = self.runtime()
        lua.execute("_control=nil;probe.Pump()")
        self.assertEqual(lua.eval("_writes"), 0)
        throwing = LuaRuntime(unpack_returned_tuples=True)
        throwing.execute('''
            local _trace_root="private"
            probe={Pump=function() error("original-sentinel") end}
        ''' + FIXTURE)
        with self.assertRaisesRegex(Exception, "original-sentinel"):
            throwing.eval("probe.Pump")()

    def test_actual_wrapper_plus_fixture_compiles_in_lua53(self):
        combined = (ROOT / "tools/research/map_009_offline/map013_wrapper_trace.lua").read_text(encoding="utf-8") + "\n" + FIXTURE
        self.assertLessEqual(len(combined.encode("utf-8")), 24000)
        result = LuaRuntime().eval("load")(combined, "map019-wrapper", "t")
        self.assertFalse(isinstance(result, tuple), result)
        self.assertIsNotNone(result)


if __name__ == "__main__":
    unittest.main()
