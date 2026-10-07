"""Execute the production overview-bridge Ghost fail-closed seam with inert Lua 5.3 stubs."""
from pathlib import Path
import importlib
import os
import tempfile
import unittest

import lupa

LuaRuntime = importlib.import_module(
    "lupa." + os.environ.get("LWB317_TEST_LUA_ENGINE", "lua53")
).LuaRuntime


class MapProviderSemanticsLuaChecks(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="lwb317-provider-semantics-")
        self.lua = LuaRuntime(unpack_returned_tuples=True)
        self.lua.globals().TEST_ROOT = self.directory.name
        self.lua.execute(
            r'''
            files, sends = {}, {}
            SERVER_TIME = 1800000000000
            os.getenv = function(key) if key == "LOCALAPPDATA" then return TEST_ROOT end return nil end
            os.time = function() return math.floor(SERVER_TIME / 1000) end
            os.clock = function() return SERVER_TIME / 1000 end
            os.remove = function(path) files[path] = nil; return true end
            io.open = function(path, mode)
                if mode == "rb" then
                    if files[path] == nil then return nil end
                    return {read=function() return files[path] end, close=function() end}
                end
                return {
                    write=function(_, text) files[path] = text end,
                    close=function() end,
                }
            end

            CS = {System={Int64={Parse=function(value)
                local number = tonumber(value)
                if number == nil then error("invalid int64") end
                return number
            end}}}
            UITimeManager = {GetInstance=function()
                return {GetServerTime=function() return SERVER_TIME end}
            end}
            LuaEntry = {Player={GetCurServerId=function() return 22 end}}

            local dispatchManager = {
                GetTodayStealNum=function() return 0 end,
                GetDispatchSetting=function(_, key) assert(key == "steal_count"); return 5 end,
                IsOpenCrossSteal=function() return true end,
            }
            DataCenter = {
                ActDispatchTaskDataManager=dispatchManager,
            }

            MsgDefines = {DispatchSteal="dispatch-command"}
            SFSNetwork = {SendMessage=function(command, uuid, server)
                sends[#sends + 1] = {command=command, uuid=tonumber(uuid), server=server}
            end}

            package.loaded["Net.Msgs.DispatchTask.DispatchStealMessage"] = {
                HandleMessage=function(self, message) self.originalDispatch = message end,
            }

            function get_upvalue(fn, wanted)
                for index=1,120 do
                    local name, value = debug.getupvalue(fn, index)
                    if name == nil then break end
                    if name == wanted then return value end
                end
                error("missing upvalue " .. wanted)
            end
            '''
        )
        repository = Path(__file__).resolve().parents[1]
        self.lua.globals().overview = self.lua.execute(
            (repository / "tools/current_overview_bridge.lua").read_text(encoding="utf-8")
        )
        self.lua.execute(
            r'''
            runtime = get_upvalue(overview.Pump, "dispatch_plunder_runtime")
            assert(runtime ~= nil)
            '''
        )

    def tearDown(self):
        self.directory.cleanup()

    def test_ghost_fails_closed_before_hook_manager_or_send(self):
        self.lua.execute(
            r'''
            local request = {
                requestId="ghost-1", taskKind="ghost", serverId=91, ownerServer=33,
                taskUuid="123", wireUuid=123, executeAt=SERVER_TIME,
            }
            runtime.begin(request)
            assert(request.armed ~= true and request.requestSent ~= true)
            assert(next(runtime.pending) == nil and next(runtime.pendingByTask) == nil)
            assert(#sends == 0, "blocked Ghost path sent transport")
            local found = false
            for _, value in pairs(files) do
                if string.find(value, '"requestId":"ghost-1"', 1, true) then
                    assert(string.find(value, '"state":"failed"', 1, true))
                    assert(string.find(value, '"errorCode":"DISPATCH_PLUNDER_MANAGER_UNAVAILABLE"', 1, true))
                    assert(string.find(value, '"requestSent":false', 1, true))
                    found = true
                end
            end
            assert(found, "blocked Ghost path did not emit explicit failure")
            '''
        )

    def test_ghost_guard_is_independent_of_dispatch_manager_availability(self):
        self.lua.execute(
            r'''
            DataCenter.ActDispatchTaskDataManager = nil
            local request = {
                requestId="ghost-no-manager", taskKind="ghost", serverId=91, ownerServer=33,
                taskUuid="124", wireUuid=124, executeAt=SERVER_TIME,
            }
            runtime.begin(request)
            assert(next(runtime.pending) == nil and #sends == 0)
            local found = false
            for _, value in pairs(files) do
                if string.find(value, '"requestId":"ghost-no-manager"', 1, true) then
                    assert(string.find(value, 'DISPATCH_PLUNDER_MANAGER_UNAVAILABLE', 1, true))
                    found = true
                end
            end
            assert(found)
            '''
        )

    def test_dispatch_path_same_runtime_still_correlates_by_uuid(self):
        self.lua.execute(
            r'''
            DataCenter.ActDispatchTaskDataManager = {
                GetTodayStealNum=function() return 0 end,
                GetDispatchSetting=function(_, key) assert(key == "steal_count"); return 5 end,
                IsOpenCrossSteal=function() return true end,
            }
            local dispatchClass = package.loaded["Net.Msgs.DispatchTask.DispatchStealMessage"]
            local originalDispatch = dispatchClass.HandleMessage
            local request = {
                requestId="dispatch-1", taskKind="dispatch", serverId=22, ownerServer=22,
                taskUuid="123", wireUuid=123, executeAt=SERVER_TIME,
            }
            runtime.begin(request)
            assert(request.armed == true)
            assert(dispatchClass.HandleMessage ~= originalDispatch)
            runtime.send(request, SERVER_TIME)
            assert(#sends == 1)
            assert(sends[1].command == "dispatch-command")
            assert(sends[1].uuid == 123 and sends[1].server == 22)

            dispatchClass.HandleMessage({}, {uuid=123})
            assert(request.responseReceived == true and request.responseSuccess == true)
            runtime.pump({})
            assert(runtime.pending["dispatch-1"] == nil)
            assert(dispatchClass.HandleMessage == originalDispatch)
            '''
        )


if __name__ == "__main__":
    print("lupa=" + lupa.__version__ + " lua=" + str(LuaRuntime().eval("_VERSION")))
    unittest.main()
