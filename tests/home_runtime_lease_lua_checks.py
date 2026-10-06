"""Execute production Lua consumers with inert CS/file seams and isolated roots."""
from pathlib import Path
import importlib
import os
import tempfile
import unittest
import lupa
LuaRuntime = importlib.import_module("lupa." + os.environ.get("LWB317_TEST_LUA_ENGINE", "lua54")).LuaRuntime


class LeaseConsumerChecks(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="lwb317-lua-lease-")
        self.root = self.directory.name
        self.lua = LuaRuntime(unpack_returned_tuples=True)
        self.lua.globals().TEST_ROOT = self.root
        self.lua.execute(r'''
            files, busy, reads, writes = {}, {}, {}, {}
            NOW = 1800000000
            os.getenv = function(key) if key == "LOCALAPPDATA" then return TEST_ROOT end return nil end
            os.time = function() return NOW end
            os.clock = function() return NOW end
            os.remove = function(path) files[path] = nil; return true end
            io.open = function(path, mode)
                if mode == "rb" then
                    reads[path] = (reads[path] or 0) + 1
                    if busy[path] or files[path] == nil then return nil end
                    return {read=function() return files[path] end, close=function() end}
                end
                return {write=function(_, text) files[path]=text; writes[path]=(writes[path] or 0)+1 end,
                        close=function() end}
            end
            local snapshot = function(path)
                if busy[path] == "sharing" then return "busy\n" end
                if busy[path] or files[path] == nil then return "unavailable\n" end
                return "ok\n" .. files[path]
            end
            CS = {System={Reflection={Assembly={LoadFrom=function()
                return {GetType=function()
                    return {GetField=function(_, name)
                        return {GetValue=function() if name == "ReadRuntimeSnapshot" then return snapshot end
                            return function() error("Connect must never run in this check") end end}
                    end}
                end}
            end}}}}
            OVERVIEW_ROOT = TEST_ROOT .. [[\LWBridgeRebuild\overview-bridge]]
            CONTROL = OVERVIEW_ROOT .. [[\control.txt]]
            LEASE = OVERVIEW_ROOT .. [[\lease.txt]]
            HEARTBEAT = OVERVIEW_ROOT .. [[\heartbeat.json]]
            PROBE_ROOT = TEST_ROOT .. [[\LWBridgeRebuild\live-resource]]
            function seed(owner)
                files[CONTROL] = "schema=1\nbridgeVersion=lwbridge-overview-bridge-1\nprofileId=profile-" .. owner ..
                    "\nsessionId=" .. owner .. "\nchallenge=nonce-" .. owner .. "\ngamePid=123\n" ..
                    "controlPipePath=\\\\.\\pipe\\lwbridge-control-v1-0123456789abcdef\n" ..
                    "pipeAdapterPath=C:\\isolated\\LWBridge.GamePipeAdapter.dll\n"
                files[LEASE] = "schema=1\nbridgeVersion=lwbridge-overview-bridge-1\nsessionId=" .. owner ..
                    "\nchallenge=nonce-" .. owner .. "\nupdatedAt=" .. tostring(NOW) .. "\n"
            end
            seed("A")
        ''')
        repository = Path(__file__).resolve().parents[1]
        self.lua.globals().overview = self.lua.execute((repository / "tools/current_overview_bridge.lua").read_text(encoding="utf-8"))
        self.lua.execute("LWBridgeOverviewBridge = overview; assert(overview.Pump() == true)")
        self.lua.globals().probe = self.lua.execute((repository / "tools/current_live_resource_probe.lua").read_text(encoding="utf-8"))

    def tearDown(self):
        self.directory.cleanup()

    def test_busy_preserves_message_and_all_queued_requests_then_resumes(self):
        self.lua.execute(r'''
            local names = {"asset-image.txt", "treasure-state.txt", "aoi-diagnostic.txt", "runtime-diagnostic.txt",
                "bulk-aoi-diagnostic.txt", "monster-protection-detail.txt", "resource-detail-diagnostic.txt",
                "resource-scan-detail.txt", "train-list-diagnostic.txt", "command.txt"}
            local queued = {}
            for _, name in ipairs(names) do
                local path = PROBE_ROOT .. "\\" .. name
                files[path] = "schema=1\nrequestId=queued\n"
                queued[path] = files[path]
            end
            local heartbeat = files[HEARTBEAT]
            busy[LEASE] = "sharing"; NOW = NOW + 1
            assert(overview.Pump() == false)
            assert(files[HEARTBEAT] == heartbeat, "busy Overview must not destroy/message/stale heartbeat")
            reads = {}
            assert(probe.Pump() == false)
            for path, text in pairs(queued) do
                assert(files[path] == text and reads[path] == nil, "busy probe consumed queued input")
            end
            busy[LEASE] = nil; seed("A")
            assert(overview.Pump() == true)
            assert(probe.Pump() == true)
            assert(files[PROBE_ROOT .. [[\asset-image.txt]]] == nil, "available probe should resume queued lane")
        ''')

    def test_busy_expiry_terminalizes_without_lease_extension(self):
        self.lua.execute(r'''
            local runtime = probe._treasureStateRuntime
            runtime.request = {requestId="active", launchSessionId="A", profileId="profile-A", challenge="nonce-A",
                gamePid=123, records={}, serverId=1}
            runtime.startedAt = 0
            busy[LEASE] = "sharing"; NOW = NOW + 5
            assert(overview.Pump() == false and probe.Pump() == false)
            assert(runtime.request ~= nil, "existing five-second boundary is inclusive")
            NOW = NOW + 1
            assert(overview.Pump() == true)
            assert(string.find(files[HEARTBEAT], "host_lease_stale", 1, true))
            assert(probe.Pump() == false and runtime.request == nil, "expired lease must terminalize active probe lane")
        ''')

    def test_busy_control_foreign_and_malformed_never_gain_freshness(self):
        self.lua.execute(r'''
            busy[CONTROL] = "sharing"; NOW = NOW + 1
            assert(overview.Pump() == false and probe.Pump() == false)
            busy[CONTROL] = nil; seed("B"); busy[LEASE] = "sharing"
            assert(overview.Pump() == true, "foreign visible control cannot use prior-owner deferral")
            assert(probe.Pump() == false)
            busy[LEASE] = nil; seed("A"); files[LEASE] = files[LEASE] .. "malformed\n"
            assert(overview.Pump() == true)
            assert(string.find(files[HEARTBEAT], "host_lease_stale", 1, true))
            assert(probe.Pump() == false)
        ''')

    def test_unknown_failure_and_missing_do_not_use_busy_grace(self):
        self.lua.execute(r'''
            busy[LEASE] = "unknown"; NOW = NOW + 1
            assert(overview.Pump() == true)
            assert(string.find(files[HEARTBEAT], "host_lease_stale", 1, true))
            assert(probe.Pump() == false)
            busy[LEASE] = nil; files[LEASE] = nil
            assert(overview.Pump() == true and probe.Pump() == false)
        ''')

    def test_all_active_lanes_terminalize_and_stale_callback_is_fenced(self):
        self.lua.execute(r'''
            local function upvalue(name, replacement, set)
                for index = 1, 80 do
                    local key, value = debug.getupvalue(probe.Pump, index)
                    if key == nil then break end
                    if key == name then
                        if set then debug.setupvalue(probe.Pump, index, replacement) end
                        return value
                    end
                end
                error("missing production Pump upvalue: " .. name)
            end
            local function request()
                return {requestId="active", launchSessionId="A", profileId="profile-A", challenge="nonce-A",
                    gamePid=123, serverId=1, records={}, targets={}, sourceMode="assetPath", assetPath="inert"}
            end
            local asset = upvalue("asset_image_runtime")
            local train = upvalue("train_list_runtime")
            local scan = upvalue("resource_scan_detail_runtime")
            local destroyed = 0
            CS.UnityEngine = {SpriteRenderer={}, Object={Destroy=function() destroyed=destroyed+1 end}}
            CS.UnityEngine.GameObject = setmetatable({}, {__call=function()
                return {AddComponent=function()
                    return {LoadSpriteAuto=function(_, _, callback) LATE_CALLBACK=callback end}
                end}
            end})
            typeof = function(value) return value end
            asset.request = request(); asset.startedAt = NOW; asset.begin(asset.request)
            assert(type(LATE_CALLBACK) == "function", "actual asset callback must be captured")
            train.request=request(); train.startedAt=NOW
            scan.request=request(); scan.startedAt=NOW
            probe._treasureStateRuntime.request=request(); probe._treasureStateRuntime.startedAt=NOW
            upvalue("resource_detail_request", request(), true)
            upvalue("monster_protection_request", request(), true)
            upvalue("monster_protection_scan", {inflightTarget={}, requestQueue={}}, true)
            upvalue("bulk_aoi_request", request(), true)
            upvalue("active_request_id", "active", true)
            local queued = PROBE_ROOT .. [[\command.txt]]
            files[queued] = "queued"
            busy[LEASE]="sharing"; NOW=NOW+6
            assert(probe.Pump() == false)
            assert(asset.request == nil and train.request == nil and scan.request == nil)
            assert(probe._treasureStateRuntime.request == nil)
            for _, name in ipairs({"resource_detail_request", "monster_protection_request", "monster_protection_scan",
                "bulk_aoi_request", "active_request_id"}) do assert(upvalue(name) == nil, name) end
            assert(files[queued] == "queued", "expired lease must not consume new queued input")
            local failures=0
            for path, value in pairs(files) do
                if string.find(path, "result.json", 1, true) and string.find(value, '"requestId":"active"', 1, true) then
                    assert(string.find(value, '"state":"failed"', 1, true), path .. " reported fake success")
                    failures=failures+1
                end
            end
            assert(failures == 8, "every admitted lane must write a terminal failure: " .. failures)
            local before = destroyed
            LATE_CALLBACK({})
            assert(destroyed == before and asset.request == nil, "retired callback must not revive/render a request")
        ''')

    def test_pump_error_clears_per_pump_identity(self):
        self.lua.execute(r'''
            local body = probe._sharedRuntimeOwnership.PumpVerified
            probe._sharedRuntimeOwnership.PumpVerified = function() error("isolated expected pump error") end
            local ok, value = pcall(probe.Pump)
            assert(not ok and string.find(tostring(value), "isolated expected pump error", 1, true))
            assert(probe._sharedRuntimeOwnership.pumpIdentity == nil, "error leaked an authorization snapshot")
            probe._sharedRuntimeOwnership.PumpVerified = body
            assert(probe.Pump() == true)
        ''')


if __name__ == "__main__":
    print("lupa=" + lupa.__version__ + " lua=" + str(LuaRuntime().eval("_VERSION")))
    unittest.main()
