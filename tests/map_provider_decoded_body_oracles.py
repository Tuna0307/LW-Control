"""Execute selected actual decoded current-v22 Lua bodies under isolated Lua 5.3 stubs."""
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
    "provider_semantics", REPO / "tools" / "lwbridge317" / "inspect_map_provider_semantics.py"
)
if _spec is None or _spec.loader is None:
    raise RuntimeError("unable to load provider semantic inspector")
sem = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(sem)


class DecodedBodyOracles(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.entry_map, cls.package = sem.load_package(Path(probe.paths()["data"]))

    def make_runtime(self):
        lua = LuaRuntime(unpack_returned_tuples=True, encoding=None)
        lua.execute(
            br'''
            writes = {}
            calls = {}
            function BaseClass(name, base)
                return {}
            end
            SFSBaseMessage = {
                OnCreate=function(self)
                    self.sfsObj = {
                        PutLong=function(_, key, value) writes[string.char(76)..key] = value end,
                        PutInt=function(_, key, value) writes[string.char(73)..key] = value end,
                    }
                end,
                HandleMessage=function(self, message)
                    calls.baseHandle = (calls.baseHandle or 0) + 1
                end,
            }
            function require(name)
                return {}
            end
            '''
        )
        return lua

    def load_entry(self, lua, name: str):
        decoded = probe.decode_lenc(self.entry_map[name])
        normalized = sem.normalize_compact_lua53(decoded)
        loader = lua.eval(b"load")
        loaded = loader(normalized, name.encode("utf-8"), b"b")
        if isinstance(loaded, tuple):
            fn, error = loaded
            self.fail(f"Lua 5.3 rejected normalized current chunk {name}: {error!r}")
        else:
            fn = loaded
        return fn()

    def test_actual_ghost_request_and_terminal_response_body(self):
        lua = self.make_runtime()
        lua.execute(
            br'''
            DataCenter = {
                RewardManager={
                    AddRewardsAndRes=function(_, msg)
                        calls.ghostRewards = (calls.ghostRewards or 0) + 1
                        calls.ghostRewardMessage = msg
                    end
                },
                ActGhostreconManager={
                    GhostReconStealHandler=function(_, msg)
                        calls.ghostHandler = (calls.ghostHandler or 0) + 1
                        calls.ghostHandlerMessage = msg
                    end
                },
            }
            UIUtil = {ShowTipsId=function(code) calls.ghostTip = code end}
            '''
        )
        module = self.load_entry(lua, "Net/Msgs/Ghostrecon/GhostReconStealMessage.luac")
        lua.globals().ACTUAL = module
        lua.execute(
            br'''
            local instance = {}
            ACTUAL.OnCreate(instance, 123, 33)
            assert(writes["Luuid"] == 123)
            assert(writes["IownerServer"] == 33)

            local success = {uuid=123, reward={}}
            ACTUAL.HandleMessage(instance, success)
            assert(success.fromGhostreconStealMessage == true)
            assert(calls.ghostRewards == 1 and calls.ghostHandler == 1)
            assert(calls.ghostRewardMessage == success and calls.ghostHandlerMessage == success)

            local beforeRewards, beforeHandler = calls.ghostRewards, calls.ghostHandler
            ACTUAL.HandleMessage(instance, {uuid=123, errorCode="ghost-reject"})
            assert(calls.ghostTip == "ghost-reject")
            assert(calls.ghostRewards == beforeRewards and calls.ghostHandler == beforeHandler)
            '''
        )

    def test_actual_ghost_protection_body_uses_seconds(self):
        lua = self.make_runtime()
        lua.execute(
            br'''
            SERVER_TIME = 1800000000000
            UITimeManager = {GetInstance=function()
                return {GetServerTime=function() return SERVER_TIME end}
            end}
            '''
        )
        module = self.load_entry(
            lua, "DataCenter/ActivityListData/ActGhostrecon/ActGhostreconTaskTemplate.luac"
        )
        lua.globals().ACTUAL = module
        lua.execute(
            br'''
            local self = {protectTime=300}
            assert(ACTUAL.CheckCanSteal(self, SERVER_TIME - 300000) == true)
            assert(ACTUAL.CheckCanSteal(self, SERVER_TIME - 299999) == false)
            assert(ACTUAL.CheckCanSteal(self, SERVER_TIME - 600000) == true)
            '''
        )

    def test_actual_treasure_claim_request_and_response_body(self):
        lua = self.make_runtime()
        lua.execute(
            br'''
            LuaEntry = {Player={GetSourceServerId=function() return 77 end}}
            CS = {GameEntry={Localization={GetString=function(_, code) return tostring(code) end}}}
            UIUtil = {
                ShowTips=function(text) calls.claimTip = text end,
                ShowTipsId=function(code) calls.claimTip = code end,
            }
            UIManager = {GetInstance=function()
                return {OpenWindow=function() calls.claimPopup=(calls.claimPopup or 0)+1 end}
            end}
            UIWindowNames = {UIGetLuckyBuffPopup="popup"}
            DataCenter = {
                RewardManager={
                    AddRewardsAndRes=function(_, msg) calls.claimRewards=(calls.claimRewards or 0)+1 end,
                    ShowCommonReward=function(_, msg) calls.claimCommon=(calls.claimCommon or 0)+1 end,
                },
                ActDetectTreasureDataManager={
                    OnGetDigTimesMsg=function(_, msg)
                        calls.claimDigUpdates=(calls.claimDigUpdates or 0)+1
                        calls.claimDigMessage=msg
                    end
                },
                FlowerTrainDataManager={OnClaimLvBoxReward=function() calls.flower=(calls.flower or 0)+1 end},
            }
            SeasonUtil = {GetSeasonType=function() return 0 end}
            SeasonMapType = {NineNation=101, Darkness=102}
            SFSNetwork = {SendMessage=function() calls.claimExtraSend=(calls.claimExtraSend or 0)+1 end}
            MsgDefines = {FetchUserCardBoxList="fetch"}
            FlowerTrainUtils = {IsFlowerTrainLvBoxReward=function() return false end}
            '''
        )
        module = self.load_entry(lua, "Net/Msgs/RadarCenter/DetectEventClaimTreasureMessage.luac")
        lua.globals().ACTUAL = module
        lua.execute(
            br'''
            local instance = {}
            ACTUAL.OnCreate(instance, 456, nil, 9)
            assert(writes["Luuid"] == 456)
            assert(writes["ItargetServer"] == 77)
            assert(writes["Itype"] == 9)

            writes = {}
            ACTUAL.OnCreate(instance, 456, 88, nil)
            assert(writes["Luuid"] == 456)
            assert(writes["ItargetServer"] == 88)
            assert(writes["Itype"] == nil)

            ACTUAL.HandleMessage(instance, {reward={}, hasLuckSiphonbuff=false})
            assert(calls.claimRewards == 1)
            assert(calls.claimCommon == 1)
            assert(calls.claimDigUpdates == 1)
            assert(calls.claimPopup == nil)

            local beforeRewards, beforeDig = calls.claimRewards, calls.claimDigUpdates
            ACTUAL.HandleMessage(instance, {errorCode="claim-reject"})
            assert(calls.claimTip == "claim-reject")
            assert(calls.claimRewards == beforeRewards and calls.claimDigUpdates == beforeDig)
            '''
        )

    def test_actual_claim_info_request_and_response_body(self):
        lua = self.make_runtime()
        lua.execute(
            br'''
            SeeDetectEventGetTreasureClaimInfoType = {Chat=4, World=5}
            LuaEntry = {Player={GetSourceServerId=function() return 71 end}}
            CS = {GameEntry={Localization={GetString=function(_, code) return tostring(code) end}}}
            UIUtil = {ShowTips=function(text) calls.statusTip=text end}
            DataCenter = {RadarCenterDataManager={
                GetDetectEventTreasureClaimInfo=function(_, msg)
                    calls.statusUpdates=(calls.statusUpdates or 0)+1
                    calls.statusMessage=msg
                end
            }}
            '''
        )
        module = self.load_entry(
            lua, "Net/Msgs/RadarCenter/DetectEventGetTreasureClaimInfoMessage.luac"
        )
        lua.globals().ACTUAL = module
        lua.execute(
            br'''
            local instance = {}
            ACTUAL.OnCreate(instance, 789, nil, nil)
            assert(writes["Luuid"] == 789)
            assert(writes["Isource"] == 4)
            assert(writes["ItargetServer"] == 71)

            writes = {}
            ACTUAL.OnCreate(instance, 789, 5, 91)
            assert(writes["Isource"] == 5 and writes["ItargetServer"] == 91)

            local success = {uuid=789}
            ACTUAL.HandleMessage(instance, success)
            assert(calls.statusUpdates == 1 and calls.statusMessage == success)

            ACTUAL.HandleMessage(instance, {uuid=789,errorCode="status-reject"})
            assert(calls.statusTip == "status-reject")
            assert(calls.statusUpdates == 1)
            '''
        )


if __name__ == "__main__":
    print(
        "lupa=" + lupa.__version__
        + " lua=" + str(LuaRuntime(encoding=None).eval(b"_VERSION").decode())
        + " package=" + DecodedBodyOracles.package["sha256"]
        if hasattr(DecodedBodyOracles, "package") else
        "lupa=" + lupa.__version__
    )
    unittest.main()
