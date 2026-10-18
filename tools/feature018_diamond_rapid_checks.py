"""Inert R14 paid march-rapid checks against the current Lua adapter.

The fixture models the original UILWRapid buy callback, native diamond modal,
optimistic ConfirmBuy cache mutation, WorldMarchRapid sender and reply. It does
not connect to a game, account, network, window, or installed package.
"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

HERE = Path(__file__).resolve().parent
SOURCE = (HERE / "feature018_attack_actions.lua").read_text(encoding="utf-8")

GAME = r"""
state={
 uid="1183884595002296",server=2296,focused=true,endTime=3000,now=2000,
 target=11,reminder="1",todayConfirm=true,tokenId=0,tokenCount=0,
 itemPresent=true,windowOpen=true,sends=0,payments=0,modals=0,refreshes=0,
}
MarchTargetType={ATTACK_CITY=11,ATTACK_ARMY=12,ATTACK_MONSTER=13}
GOODS_TYPE={GOODS_TYPE_108=108}
ItemSpdMenu={ItemSpdMenu_Troop=1}
ConsumeType={ConsumeType_Resource_Item=7}
UserSettingKey={DIAMOND_USAGE_REMINDER=101}
TodayNoSecondConfirmType={BuyUseDialog=202}
UIWindowNames={UILWRapid="UILWRapid"}
MsgDefines={WorldMarchRapid=173,WorldMarchFormationNew=174,WorldMarchChange=175}

LuaEntry={Player={uid=state.uid,allianceId="0",gold=100,
 GetSourceServerId=function(self)return state.server end,
 GetSelfServerId=function(self)return state.server end,
 GetCurServerId=function(self)return state.server end,
 GetUserSetting=function(self,key)return state.reminder end,
}}

local function march()
 return {uuid=555110,ownerUid=state.uid,ownerFormationUuid=1001,endTime=state.endTime,
  GetMarchTargetType=function(self)return state.target end}
end
DataCenter={
 ArmyFormationDataManager={GetOneArmyInfoByIndex=function(self,slot)return {uuid=1001} end},
 WorldMarchDataManager={
  GetOwnerFormationMarch=function(self,uid,formation,alliance)return march() end,
  GetMarch=function(self,uuid)return march() end,
 },
 SecondConfirmManager={
  GetTodayCanShowSecondConfirm=function(self,showType)return state.todayConfirm end,
 },
}
UITimeManager={GetInstance=function(self)return {
 GetServerTime=function(self)return state.now end,
} end}
CurrencyUtil={GetCurrencyCount=function(tokenId,tokenType)return state.tokenCount end}
GoToUtil={GotoPayTips=function(amount)state.payments=state.payments+1 end}

local template={id=9999,price=10,para3=20}
rapidView={uuid=555110,speedType=1,endTime=3000,worldType=0,itemNum=0,items={},buyItems={}}
function rapidView:GetAllItems()
 state.refreshes=state.refreshes+1
 self.items={}
 self.itemNum=0
 self.buyItems=state.itemPresent and {template} or {}
end
function rapidView:GetTokenIdAndType() return state.tokenId,7 end
function rapidView:ConfirmBuy(chosen,reduceTime)
 SFSNetwork.SendMessage(MsgDefines.WorldMarchRapid,self.uuid,chosen.id,true)
 self.endTime=self.endTime-200
 local credit=0
 if state.tokenId>0 then credit=math.min(state.tokenCount,chosen.price) end
 LuaEntry.Player.gold=LuaEntry.Player.gold-math.max(chosen.price-credit,0)
end
function rapidView:CellsCallBack(index,chosen,isBuy)
 assert(isBuy==true and chosen==template)
 local credit=0
 if state.tokenId>0 then credit=math.min(state.tokenCount,chosen.price) end
 local need=math.max(chosen.price-credit,0)
 if need>LuaEntry.Player.gold then GoToUtil.GotoPayTips(need); return end
 if need<=0 then self:ConfirmBuy(chosen,200); return end
 UIUtil.ShowUseDiamondConfirm(TodayNoSecondConfirmType.BuyUseDialog,"spend",2,"ok","cancel",
  function() self:ConfirmBuy(chosen,200) end)
end
rapidWindow={View=rapidView}
local ui={}
function ui:IsWindowOpen(name)return state.windowOpen end
function ui:GetWindow(name)return state.windowOpen and rapidWindow or nil end
function ui:OpenWindow(name,options,speedType,uuid)
 state.windowOpen=true;rapidView.uuid=uuid;rapidView.speedType=speedType;return true
end
UIManager={GetInstance=function(self)return ui end}

UIUtil={ShowUseDiamondConfirm=function(showType,message,style,confirm,cancel,callback)
 state.modals=state.modals+1
 state.confirm=callback
 state.cancel=function() state.confirm=nil end
end}

SFSNetwork={SendMessage=function(route,...)
 local args=table.pack(...)
 state.sends=state.sends+1
 state.lastRoute=route;state.lastUuid=args[1];state.lastItem=args[2];state.lastUseGold=args[3]
 return true
end}

package.preload["Net.Msgs.WorldMarchRapidMessage"]=function()
 local cls={}
 function cls.HandleMessage(self,response)
  if response.errorCode==nil and response.remainGold~=nil then
   LuaEntry.Player.gold=response.remainGold
  end
 end
 return cls
end

focus=function()return state.focused end
"""


def require(value, message):
    if not value:
        raise AssertionError("FEATURE018_DIAMOND_RAPID: " + message)


def fixture():
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute(GAME)
    module = lua.execute(SOURCE)
    state = lua.globals().state

    def call(**overrides):
        fields = {
            "action": "speedupDiamond", "slot": "2", "authorizedUid": state["uid"],
            "profileId": "profile", "launchSessionId": "launch",
            "challenge": "nonce", "gamePid": "9123",
        }
        fields.update(overrides)
        return module.Handle(lua.table_from(fields), lua.globals().focus)

    return lua, module, state, call


def test_preflight_and_guards():
    lua, module, state, call = fixture()
    pre = module.DiamondSpeedupPreflight(lua.table_from({
        "slot": "2", "authorizedUid": state["uid"], "profileId": "profile",
        "launchSessionId": "launch", "challenge": "nonce", "gamePid": "9123",
    }), lua.globals().focus)
    require(pre["state"] == "confirmed" and pre["data"]["options"][1]["itemId"] == "9999" and
            pre["data"]["options"][1]["gemCost"] == 10 and
            pre["data"]["options"][1]["safeToOffer"] is True and state["sends"] == 0,
            "read-only native buyItems preflight was not safe and non-mutating")
    require(call(itemId="9999")["state"] == "failed" and state["modals"] == 0,
            "paid rapid accepted without explicit diamond opt-in")

    for key, value in (("reminder", "0"), ("todayConfirm", False)):
        lua, module, state, call = fixture()
        state[key] = value
        result = call(itemId="9999", diamondOptIn="1")
        require(result["state"] == "failed" and state["modals"] == 0 and state["sends"] == 0,
                "silent-auto-confirm gate reached native paid callback: " + key)

    lua, module, state, call = fixture()
    lua.globals().LuaEntry.Player.gold = 5
    result = call(itemId="9999", diamondOptIn="1")
    require(result["state"] == "failed" and state["payments"] == 0 and
            state["modals"] == 0 and state["sends"] == 0,
            "insufficient gems entered native payment path")


def test_cancel_and_optimistic_state_are_unconfirmed():
    lua, module, state, call = fixture()
    opened = call(itemId="9999", diamondOptIn="1")
    require(opened["state"] == "accepted" and opened["data"]["awaitingUserConfirmation"] is True and
            state["modals"] == 1 and state["sends"] == 0,
            "original diamond modal did not open without dispatch")
    state["cancel"]()
    waiting = call(queryOnly="1")
    require(waiting["state"] == "accepted" and state["sends"] == 0,
            "native modal cancel caused a paid send")

    lua, module, state, call = fixture()
    call(itemId="9999", diamondOptIn="1")
    state["confirm"]()
    require(state["sends"] == 1 and state["lastUuid"] == 555110 and
            state["lastItem"] == 9999 and state["lastUseGold"] is True and
            lua.globals().LuaEntry.Player.gold == 90 and lua.globals().rapidView.endTime == 2800,
            "original ConfirmBuy did not emit exact paid rapid or optimistic local state")
    pending = call(queryOnly="1")
    require(pending["state"] == "accepted",
            "optimistic local gold/view mutation falsely confirmed paid rapid")
    lua.execute("require('Net.Msgs.WorldMarchRapidMessage').HandleMessage({}, {remainGold=90})")
    pending = call(queryOnly="1")
    require(pending["state"] == "accepted",
            "success reply without exact manager endTime decrease falsely confirmed")
    state["endTime"] = 2800
    confirmed = call(queryOnly="1")
    require(confirmed["state"] == "confirmed" and
            confirmed["data"]["observation"] == "native_paid_rapid_reply_gold_and_march_transition" and
            confirmed["data"]["priorGold"] == 100 and confirmed["data"]["currentGold"] == 90 and
            confirmed["data"]["priorEndTime"] == 3000 and confirmed["data"]["currentEndTime"] == 2800,
            "reply+remainGold+exact march endTime transition was not confirmed")


def test_error_competing_and_send_edge_drift():
    lua, module, state, call = fixture()
    call(itemId="9999", diamondOptIn="1")
    state["confirm"]()
    lua.execute("require('Net.Msgs.WorldMarchRapidMessage').HandleMessage({}, {errorCode='DENIED'})")
    require(call(queryOnly="1")["state"] == "failed",
            "native paid rapid error reply was not terminal failure")

    lua, module, state, call = fixture()
    call(itemId="9999", diamondOptIn="1")
    lua.execute("SFSNetwork.SendMessage(MsgDefines.WorldMarchRapid,555110,3108,false)")
    require(call(queryOnly="1")["state"] == "failed" and state["sends"] == 1,
            "competing WorldMarchRapid send did not make attribution ambiguous")

    lua, module, state, call = fixture()
    call(itemId="9999", diamondOptIn="1")
    exact_outside = lua.eval("pcall")(lua.globals().SFSNetwork.SendMessage,
                                      lua.globals().MsgDefines.WorldMarchRapid,
                                      555110, 9999, True)
    if isinstance(exact_outside, tuple):
        exact_outside = exact_outside[0]
    require(exact_outside is False and state["sends"] == 0 and
            call(queryOnly="1")["state"] == "failed",
            "exact paid packet outside original ConfirmBuy callback was attributed")

    for mutation in (
        "rapidView.uuid=555111",
        "state.itemPresent=false",
        "state.server=2297",
    ):
        lua, module, state, call = fixture()
        call(itemId="9999", diamondOptIn="1")
        lua.execute(mutation)
        ok = lua.eval("pcall")(state["confirm"])
        if isinstance(ok, tuple):
            ok = ok[0]
        require(ok is False and state["sends"] == 0 and
                lua.globals().LuaEntry.Player.gold == 100,
                "send-edge drift escaped exact paid revalidation: " + mutation)


def main():
    test_preflight_and_guards()
    test_cancel_and_optimistic_state_are_unconfirmed()
    test_error_competing_and_send_edge_drift()
    print("FEATURE018_DIAMOND_RAPID_LUA53_OFFLINE_PASS")


if __name__ == "__main__":
    main()
