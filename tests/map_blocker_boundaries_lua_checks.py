"""Distinguishing inert oracles for Map blocker-boundary audit 004.

These execute selected actual decoded current-v22 Lua bodies under isolated Lua
5.3 stubs. Synthetic response fields are probes only; they are not server-schema
claims.
"""
from __future__ import annotations

import importlib
import importlib.util
import os
from pathlib import Path
import sys
import unittest

import lupa

LuaRuntime = importlib.import_module(
    "lupa." + os.environ.get("LWB317_TEST_LUA_ENGINE", "lua53")
).LuaRuntime

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))
import run_live_resource_probe as probe  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "provider_semantics_boundary",
    REPO / "tools" / "lwbridge317" / "inspect_map_provider_semantics.py",
)
if _spec is None or _spec.loader is None:
    raise RuntimeError("unable to load provider semantic inspector")
sem = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(sem)


class MapBlockerBoundaryLuaChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.entry_map, cls.package = sem.load_package(Path(probe.paths()["data"]))

    def make_runtime(self):
        lua = LuaRuntime(unpack_returned_tuples=True, encoding=None)
        lua.execute(
            br'''
            calls = {}
            function BaseClass(name, base) return {} end
            SFSBaseMessage = {
                HandleMessage=function(self, message)
                    calls.baseHandle=(calls.baseHandle or 0)+1
                end,
            }
            function require(name) return {} end
            '''
        )
        return lua

    def load_entry(self, lua, name: str):
        decoded = probe.decode_lenc(self.entry_map[name])
        normalized = sem.normalize_compact_lua53(decoded)
        loaded = lua.eval(b"load")(normalized, name.encode("utf-8"), b"b")
        if isinstance(loaded, tuple):
            _, error = loaded
            self.fail(f"Lua 5.3 rejected normalized current chunk {name}: {error!r}")
        return loaded()

    def test_direct_ghost_handler_forwards_extra_identity_when_present(self):
        lua = self.make_runtime()
        lua.execute(
            br'''
            DataCenter = {
                RewardManager={
                    AddRewardsAndRes=function(_, msg) calls.rewardMessage=msg end
                },
                ActGhostreconManager={
                    GhostReconStealHandler=function(_, msg) calls.managerMessage=msg end
                },
            }
            UIUtil={ShowTipsId=function(code) calls.tip=code end}
            '''
        )
        module = self.load_entry(lua, "Net/Msgs/Ghostrecon/GhostReconStealMessage.luac")
        lua.globals().ACTUAL = module
        lua.execute(
            br'''
            local raw = {
                uuid=987654321,
                ownerServer=77,
                opaqueIdentity="probe-only",
                stealTimes=2,
                reward={},
            }
            ACTUAL.HandleMessage({}, raw)
            assert(calls.managerMessage == raw)
            assert(calls.rewardMessage == raw)
            assert(calls.managerMessage.uuid == 987654321)
            assert(calls.managerMessage.ownerServer == 77)
            assert(calls.managerMessage.opaqueIdentity == "probe-only")
            assert(raw.fromGhostreconStealMessage == true)
            '''
        )

    def test_direct_ghost_handler_does_not_create_identity_when_absent(self):
        lua = self.make_runtime()
        lua.execute(
            br'''
            DataCenter = {
                RewardManager={
                    AddRewardsAndRes=function(_, msg) calls.rewardMessage=msg end
                },
                ActGhostreconManager={
                    GhostReconStealHandler=function(_, msg) calls.managerMessage=msg end
                },
            }
            UIUtil={ShowTipsId=function(code) calls.tip=code end}
            '''
        )
        module = self.load_entry(lua, "Net/Msgs/Ghostrecon/GhostReconStealMessage.luac")
        lua.globals().ACTUAL = module
        lua.execute(
            br'''
            local raw = {stealTimes=2,reward={}}
            ACTUAL.HandleMessage({}, raw)
            assert(calls.managerMessage == raw)
            assert(calls.managerMessage.uuid == nil)
            assert(calls.managerMessage.ownerServer == nil)
            assert(raw.fromGhostreconStealMessage == true)
            '''
        )

    def test_push_ghost_handler_forwards_push_location_identity(self):
        lua = self.make_runtime()
        lua.execute(
            br'''
            DataCenter = {
                ActDispatchTaskDataManager={
                    PushHeroDispatchMissionStealHandler=function(_, msg)
                        calls.pushMessage=msg
                    end
                }
            }
            UIUtil={ShowTipsId=function(code) calls.tip=code end}
            '''
        )
        module = self.load_entry(lua, "Net/Msgs/Ghostrecon/PushGhostReconStealMessage.luac")
        lua.globals().ACTUAL = module
        lua.execute(
            br'''
            local raw={serverId=91,pointId=444,playerInfo={uid=7}}
            ACTUAL.HandleMessage({}, raw)
            assert(calls.pushMessage == raw)
            assert(calls.pushMessage.serverId == 91)
            assert(calls.pushMessage.pointId == 444)
            assert(calls.pushMessage.uuid == nil)
            '''
        )

    def test_static_consumers_do_not_establish_direct_uuid_schema(self):
        direct_root = sem._dispatch.parse_chunk(
            probe.decode_lenc(self.entry_map["Net/Msgs/Ghostrecon/GhostReconStealMessage.luac"])
        )
        direct = sem.root_methods(direct_root)["HandleMessage"]
        direct_strings = {x for x in direct["constants"] if isinstance(x, str)}
        self.assertIn("errorCode", direct_strings)
        self.assertIn("GhostReconStealHandler", direct_strings)
        self.assertNotIn("uuid", direct_strings)
        self.assertNotIn("ownerServer", direct_strings)

        manager_root = sem._dispatch.parse_chunk(
            probe.decode_lenc(
                self.entry_map[
                    "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconManager.luac"
                ]
            )
        )
        manager = sem.root_methods(manager_root)["GhostReconStealHandler"]
        manager_strings = {x for x in manager["constants"] if isinstance(x, str)}
        self.assertIn("stealTimes", manager_strings)
        self.assertIn("reward", manager_strings)
        self.assertNotIn("uuid", manager_strings)
        self.assertNotIn("ownerServer", manager_strings)
        self.assertNotIn("pointId", manager_strings)


if __name__ == "__main__":
    print("lupa=" + lupa.__version__ + " lua=" + str(LuaRuntime(encoding=None).eval(b"_VERSION").decode()))
    unittest.main()
