"""Structural inverse guard for already recovered 5s authenticated heartbeat.

Actual native game/session proof must be recorded separately. This uses
source and Lua syntax only; it does not pretend to be a live transport.
"""
from pathlib import Path
import unittest
from lupa import LuaRuntime

ROOT = Path(__file__).resolve().parents[1]
BRIDGE = ROOT / "tools" / "current_overview_bridge.lua"
LIMITS = ROOT / "src" / "LWBridge.Desktop" / "LWBridgeControlPipeTransportLimits.cs"


class World004HeartbeatChecks(unittest.TestCase):
    def setUp(self):
        self.lua = BRIDGE.read_text(encoding="utf-8")
        self.limits = LIMITS.read_text(encoding="utf-8")

    def test_actual_lua_syntax(self):
        vm = LuaRuntime()
        vm.execute("assert(loadfile(" + repr(BRIDGE.as_posix()) + "))")

    def test_pump_sends_heartbeat_after_adapter_state_refresh(self):
        pump = self.lua[self.lua.index("function M.Pump()"):self.lua.index("function M.Register()")]
        self.assertEqual(pump.count("write_pipe_heartbeat(active)"), 1)
        self.assertLess(pump.index("ensure_pipe_hello(active)"), pump.index("refresh_pipe_adapter_state()"))
        self.assertLess(pump.index("refresh_pipe_adapter_state()"), pump.index("write_pipe_heartbeat(active)"))
        self.assertLess(pump.index("write_pipe_heartbeat(active)"), pump.index("write_pipe_transport_diagnostic()"))

    def test_heartbeat_uses_existing_authenticated_session_envelope(self):
        method = self.lua[self.lua.index("local function write_pipe_heartbeat(control)"):self.lua.index("local function process_pipe_inbound(control)")]
        for value in ('type = "heartbeat"', "profileId = control.profileId",
                      "instanceId = control.sessionId", "time = now_ms", "timestamp = now_ms",
                      'pipe_mailbox_path("outbound", sequence)', "if not write_json(temp_path, envelope)"):
            self.assertIn(value, method)
        self.assertIn("not pipe_runtime.clientConnected", method)
        self.assertIn('pipe_runtime.state ~= "connected"', method)
        self.assertIn("clock - last_pipe_heartbeat_clock < 5.0", method)
        self.assertNotIn("pipe_token", method)

    def test_timeout_not_inflated_and_single_writer(self):
        self.assertIn("IdleActivityTimeout = TimeSpan.FromSeconds(30)", self.limits)
        self.assertIn("local function write_pipe_heartbeat(control)", self.lua)
        self.assertEqual(self.lua.count("write_pipe_heartbeat(active)"), 1)


if __name__ == "__main__":
    unittest.main()
