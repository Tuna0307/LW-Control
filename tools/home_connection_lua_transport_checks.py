"""Execute Home's genuine Lua direct-transport state machine in an inert VM.

Extraction includes refresh/ensure, JSON field readers, result/heartbeat writers,
the inbound consumer, and the actual pump's ensure -> refresh -> inbound ->
heartbeat call sequence. Only environment, game observation, JSON serialization,
and managed adapter delegates are mocked. This tests Lua orchestration; it does
not establish real pipe concurrency, xLua dispatch, or game-thread ownership.
No game/app launch, installed-script changes, file mailbox, or network access.
"""
from __future__ import annotations

import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime  # noqa: E402

SOURCE = (ROOT / "tools/current_overview_bridge.lua").read_text(encoding="utf-8")


def section(start: str, end: str) -> str:
    begin = SOURCE.index(start)
    return SOURCE[begin:SOURCE.index(end, begin)]


FUNCTIONS = (
    section("local function refresh_pipe_adapter_state()", "-- Explicit isolated pilot observability")
    + section("local function ensure_pipe_hello(control)", "-- Clone publication adaptation")
    + section("local function pipe_json_string(text, name)", "local RECOVERY_WINDOWS =")
)
# Retain the real production order rather than recreating it in the test.
PUMP = section("    ensure_pipe_hello(active)\n", "    -- Native UpdateManager")

FAKES = r'''
local control = {profileId="profile-A",sessionId="owner-A",gamePid=4242,
 controlPipePath="\\\\.\\pipe\\test",pipeAdapterPath="inert-adapter"}
local active = control
local pipe_runtime = {adapterActive=true,clientConnected=true,directActive=true,
 directCapable=true,adapterGeneration=41,sessionId=control.sessionId,
 state="connected",authenticatedOnceForSession=true,reconnectAttempts=0}
local last_pipe_heartbeat_clock = 90
local root, pipe_adapter_state_path = "inert", "inert-state"
local CONTROL_PIPE_PREFIX = "\\\\.\\pipe\\"
local M = {VERSION="test-build"}
T = {clock=100,generation=41,state="connected",running=true,
 inbound={},published={},attempted={},connects={},observations=0,runtime=pipe_runtime,
 control=control}
local environment = {LWBRIDGE_PROFILE_ID=control.profileId,
 LWBRIDGE_INSTANCE_ID=control.sessionId,LWBRIDGE_BUILD_ID=M.VERSION,
 LWBRIDGE_PIPE_TOKEN=string.rep("t",43)}
os.getenv = function(key) return environment[key] end
os.time = function() return 1000 end
-- A reconnect wait may use the legacy state-file branch, which sees no file.
local io = {open=function() return nil end}
local function runtime_clock() return T.clock end
local function json_encode(value) return encode(value) end
local function valid_control_pipe_path(_) return true end
local function valid_pipe_adapter_path(_) return true end
local function load_pipe_adapter(_) return true end
local function pipe_invocation_receipt(...) end
local function write_pipe_transport_diagnostic() end
local function close_pipe_runtime() error("unexpected owner rotation") end
local function observe_game_connection()
 T.observations=T.observations+1
 return {observed=true,ready=true,loggedIn=true,connected=true,connecting=false,
  gameUid="123",serverId=2212,worldPos=12345}
end
pipe_runtime.adapterState = function(owner)
 assert(owner==control.sessionId)
 if T.unavailable then return "unavailable" end
 return "ok\n"..T.generation.."\n"..T.state
end
pipe_runtime.adapterDequeue = function(owner)
 assert(owner==control.sessionId)
 if T.dequeueOverride then local v=T.dequeueOverride;T.dequeueOverride=nil;return v end
 if #T.inbound==0 then return "empty\n"..T.generation end
 return "frame\n"..T.generation.."\n"..table.remove(T.inbound,1)
end
pipe_runtime.adapterPublish = function(value)
 T.attempted[#T.attempted+1]=value
 local generation,owner = value:match("^([0-9]+)\n([^\n]+)\n")
 if T.publishFault then
  T.state,T.running=T.publishFault,false;T.publishFault=nil;return "stale"
 end
 if tonumber(generation)~=T.generation or owner~=control.sessionId or not T.running then
  return "stale"
 end
 T.published[#T.published+1]=value;return "ok"
end
pipe_runtime.adapterEnableDirect = function(owner)
 assert(owner==control.sessionId);return "ok"
end
pipe_runtime.adapterConnect = function(path,hello,runtime)
 T.connects[#T.connects+1]={path=path,hello=hello,runtime=runtime}
 T.generation=T.generation+1;T.running=true;T.state="connected";T.inbound={}
end
'''

EXPORTS = r'''
function T.pump()
''' + PUMP + r'''
end
function T.ensure() return ensure_pipe_hello(control) end
function T.heartbeat() return write_pipe_heartbeat(control) end
function T.result(request_id) return write_pipe_result(control,request_id,{readOnly=true}) end
function T.set_last_heartbeat(value) last_pipe_heartbeat_clock=value end
'''


def encode(value):
    def convert(item):
        if hasattr(item, "items"):
            return {key: convert(child) for key, child in item.items()}
        return item
    return json.dumps(convert(value), separators=(",", ":"))


class HomeConnectionLuaTransportChecks(unittest.TestCase):
    def setUp(self):
        self.vm = LuaRuntime(unpack_returned_tuples=True)
        self.vm.globals().encode = encode
        self.vm.execute(FAKES + FUNCTIONS + EXPORTS)
        self.t = self.vm.globals().T
        self.runtime = self.t.runtime

    def frame(self, message_type="hello.ack", request_id="", **changes):
        frame = dict(version=1, type=message_type, profileId="profile-A",
                     instanceId="owner-A", requestId=request_id, timestamp=1000,
                     payload={})
        frame.update(changes)
        return json.dumps(frame, separators=(",", ":"))

    def enqueue(self, frame):
        self.t.inbound[len(self.t.inbound) + 1] = frame

    def reconnect(self, state):
        self.t.state, self.t.running = state, False
        self.t.pump()
        self.assertEqual(self.runtime.state, "error")
        self.assertEqual(len(self.t.attempted), 0)
        self.t.clock += 0.1
        self.t.pump()
        self.assertFalse(self.runtime.adapterActive)
        self.assertEqual(self.runtime.reconnectAttempts, 1)
        self.assertEqual(len(self.t.connects), 0)
        self.t.clock += 0.5
        self.t.pump()
        self.assertEqual(len(self.t.connects), 0, "retry delay was skipped")
        self.t.clock += 0.6
        self.t.pump()
        self.assertEqual(len(self.t.connects), 1)
        hello = json.loads(self.t.connects[1].hello)
        self.assertEqual(hello["instanceId"], "owner-A")
        self.assertEqual(hello["profileId"], "profile-A")
        self.assertEqual(hello["payload"]["pid"], 4242)
        self.assertEqual(self.runtime.adapterGeneration, 42)
        self.enqueue(self.frame())
        self.t.clock += 0.1
        self.t.pump()
        self.assertTrue(self.runtime.authenticatedOnceForSession)
        self.assertEqual(self.runtime.reconnectAttempts, 0)
        self.assertEqual(self.runtime.state, "connected")

    def test_transport_failure_classes_reconnect_same_session(self):
        for state in ("error:Win32Exception:PeekNamedPipe failed (Win32 109)",
                      "error:direct_inbound_overflow", "error:direct_outbound_overflow",
                      "error:frame_length_invalid"):
            with self.subTest(state=state):
                self.setUp()
                self.reconnect(state)

    def test_heartbeat_publication_race_keeps_underlying_error_and_recovers(self):
        self.t.publishFault = "error:Win32Exception:PeekNamedPipe failed (Win32 109)"
        self.t.pump()
        self.assertEqual(self.runtime.error, "pipe_direct_heartbeat_publish_failed:stale")
        self.assertEqual(len(self.t.attempted), 1)
        self.t.clock += 0.1
        self.t.pump()
        self.assertIn("PeekNamedPipe", self.runtime.error)
        self.assertEqual(len(self.t.attempted), 1,
                         "failed state must prevent heartbeat from overwriting transport error")
        self.t.clock += 0.1
        self.t.pump()
        self.assertFalse(self.runtime.adapterActive)
        self.assertEqual(self.runtime.reconnectAttempts, 1)

    def test_authentication_errors_do_not_trigger_transport_retry(self):
        self.t.state, self.t.running = "error:authentication_failed", False
        for _ in range(4):
            self.t.pump()
            self.t.clock += 2
        self.assertEqual(self.runtime.error, "authentication_failed")
        self.assertEqual(self.runtime.reconnectAttempts, 0)
        self.assertEqual(len(self.t.connects), 0)
        self.assertEqual(len(self.t.published), 0)

    def test_response_preserves_exact_owner_generation_and_request_id(self):
        for request in ("cmd_1", "cmd_2"):
            self.enqueue(self.frame("command", request,
                payload=dict(id=request, kind="call", fn="getStatus", args={})))
        self.t.set_last_heartbeat(100)
        self.t.pump()
        self.assertEqual(self.t.observations, 2)
        self.assertEqual(len(self.t.published), 2)
        for index, request in enumerate(("cmd_1", "cmd_2"), 1):
            generation, owner, raw = self.t.published[index].split("\n", 2)
            response = json.loads(raw)
            self.assertEqual((generation, owner), ("41", "owner-A"))
            self.assertEqual(response["profileId"], "profile-A")
            self.assertEqual(response["instanceId"], "owner-A")
            self.assertEqual(response["requestId"], request)
            self.assertEqual(response["payload"]["id"], request)
        self.t.pump()
        self.assertEqual(len(self.t.published), 2, "dequeued commands were replayed")

    def test_invalid_inbound_identity_and_correlation_do_not_dispatch_or_retry(self):
        variants = (
            {"profileId": "foreign"}, {"instanceId": "retired-owner"}, {"version": 2},
            {"requestId": ""}, {"payload": dict(id="wrong",kind="call",fn="getStatus",args={})},
        )
        for change in variants:
            with self.subTest(change=change):
                self.setUp()
                frame = json.loads(self.frame("command", "cmd_1",
                    payload=dict(id="cmd_1",kind="call",fn="getStatus",args={})))
                frame.update(change)
                self.enqueue(json.dumps(frame, separators=(",", ":")))
                self.t.pump()
                self.assertEqual(self.runtime.state, "error")
                self.assertEqual(self.t.observations, 0)
                self.assertEqual(len(self.t.published), 0)
                self.t.ensure()
                self.assertEqual(self.runtime.reconnectAttempts, 0)

    def test_stale_generation_publication_is_not_reported_success_or_replayed(self):
        # Managed rejection is mocked here; C# checks independently exercise its fence.
        self.runtime.adapterGeneration = 40
        self.assertFalse(self.t.result("retired_cmd"))
        self.assertEqual(self.runtime.error, "pipe_direct_publish_failed:stale")
        self.assertEqual(len(self.t.published), 0)
        self.assertEqual(len(self.t.attempted), 1)
        self.t.set_last_heartbeat(100)
        self.t.pump()
        self.assertEqual(self.runtime.adapterGeneration, 41)
        self.assertEqual(len(self.t.attempted), 1, "failed response was replayed")

    def test_unavailable_adapter_and_stale_dequeue_are_not_accepted_as_frames(self):
        self.t.unavailable = True
        self.t.pump()
        self.assertEqual(self.runtime.error, "pipe_direct_state_invalid")
        self.assertEqual(len(self.t.published), 0)
        self.t.unavailable = False
        self.t.dequeueOverride = "stale"
        self.t.pump()
        self.assertEqual(self.runtime.error, "pipe_direct_dequeue_invalid")
        self.assertEqual(self.t.observations, 0)
        self.assertEqual(len(self.t.published), 0)


if __name__ == "__main__":
    unittest.main()
