"""R5 inert original-v26 currency, reward viewers and rapid-confirm source audit.

This is a bytecode provenance check, not an eligible 2289 reward, a completed
native confirmation dialog or a receipt for WorldMarchRapid. No gameplay RPC,
game window, installed script edit or mutable account action occurs here.
"""

from __future__ import annotations

import hashlib
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
sys.path.insert(0, str(TOOLS / "map_009_offline"))
import feature018_mini_lua as source  # noqa: E402
from inspect_current import root_methods, disassemble  # noqa: E402


MODULES = {
    "UI/UILWCityShield/Component/CityShieldPage.luac":
        "f405905566075affbed4bd2dc26d5ab3323346bd0715539a8699ce18051ed50a",
    "Net/Msgs/UIShopBuyMessage.luac":
        "f7bcd8091f79c695ee6da6e9a4eeb7d86cfea6dc281c8c2dffaf445cb002ba0d",
    "UI/UILWRapid/View/UILWRapidView.luac":
        "19bd32d6cedc24586089e19e12c070b3b73fac7e170dd4917ca8041a11720da3",
    "Util/CurrencyUtil.luac":
        "af84359d3b485c27d903a6d5846c765887bc5f9a30501d7c2fdf11d6a29b1afa",
    "Net/Msgs/WorldMarchRapidMessage.luac":
        "1ba6497b62d041729d2e7b5a7dd907e5067fec8b92893e5e63ecd9cc2e00a8f5",
    "UI/UILWQuest/UILWQuestList/Component/UILWDailyTaskItem.luac":
        "94ef0fb6f815c891519d85acb6fc91d21e3171bd76780efc18c9ecff63a5f3c8",
    "DataCenter/LWActSignInManager/LWActSignInManager.luac":
        "574e9e5703b9c609fd9e771cbb999a7cd1b578ec231ebf469f3edaa1bd4c12e5",
    "UI/UILWAlliance/UILWAllianceGift/Controller/UILWAllianceGiftCtrl.luac":
        "0f371655d126e1142c4603ed7c9527368eb7ab3f73b97d6906d55783713f71ae",
    "DataCenter/DailyTaskData/DailyTaskManager.luac":
        "f61d968cc0895910a30189c63b747947e3df401b32ecce3ad2cf039ec0a9f871",
    "DataCenter/DailyTaskData/DailyTaskInfo.luac":
        "3f94e5b34f3eb229524be883d0e8d9eeee7501d1731a2cb927df7a596e020f9a",
    "DataCenter/RewardManager/RewardManager.luac":
        "4a0fb530b6bf99d7f987bbef543eebe5a649cb705999ad48a2c721856419341e",
    "DataCenter/LWActSignInManager/LWActSignInInfo.luac":
        "016c286eaa75109c47253fdcbe3ff900de1c019a85408d57a81174fd67dac72e",
}


def insist(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError("FEATURE018_RAPID_CURRENCY_SOURCE: " + message)


def get_method(records: dict, name: str, method: str):
    content = records[name]
    insist(hashlib.sha256(content).hexdigest() == MODULES[name],
           f"unrecognized original v26 module {name}")
    root = source.lua.parse_chunk(source.probe.decode_lenc(content))
    path = method if method.startswith("root/") else root_methods(root)[method]
    proto = next(p for k, p in source.walk(root) if k == path)
    return set(proto["constants"]), disassemble(proto)


def check() -> None:
    version, content_version, entries = source.probe.read_lwlf(source.probe.paths()["data"])
    records = dict(entries)
    original = records.get(source.probe.ORIGINAL_LUA_ENTRY,
                           records.get(source.probe.LUA_ENTRY))
    insist((version, content_version) == (3, 26) and original is not None and
           hashlib.sha256(original).hexdigest() == source.ORIGINAL_ENTRY_SHA256,
           "original v26 content not admitted")

    def has(name: str, method: str, *expected: str):
        constants, instructions = get_method(records, name, method)
        insist(all(s in constants for s in expected),
               f"{name}.{method} native delegates changed: {expected}")
        return instructions

    shield = "UI/UILWCityShield/Component/CityShieldPage.luac"
    shop = "Net/Msgs/UIShopBuyMessage.luac"
    rapid = "UI/UILWRapid/View/UILWRapidView.luac"
    currency = "Util/CurrencyUtil.luac"
    reply = "Net/Msgs/WorldMarchRapidMessage.luac"

    # Explicitly distinguish the original buy-and-use shortcut from the
    # separately proven inventory-only UIShopBuy followed by ItemUse.
    has(shield, "ConfirmBuy", "ItemBuyAndUse", "itemId", "num", "SetUseShieldCD")
    has(shield, "ConfirmUse", "ItemUse", "uuid", "num")
    has(shield, "BuyOrUse", "ShowUseDiamondConfirm", "BuyUseDialog", "gold",
        "price", "CONFIRM", "CANCEL", "GotoPayTips")
    has(shield, "root/11/1", "NeedNoticeShield", "ConfirmBuy")
    has(shop, "OnCreate", "PutUtfString", "itemId", "PutInt", "num")

    has(rapid, "GetTokenIdAndType", "worldType", "GetBattleFieldSpeedUpToken",
        "ConsumeType_Resource_Item")
    instructions = has(rapid, "CellsCallBack", "GetTokenIdAndType", "gold",
                       "GetCurrencyCount", "ShowUseDiamondConfirm", "BuyUseDialog",
                       "CONFIRM", "CANCEL", "ConfirmBuy", "DIAMOND")
    confirm_ui = next(i for i, line in enumerate(instructions)
                      if "'ShowUseDiamondConfirm'" in line)
    paid_send = next(i for i, line in enumerate(instructions)
                     if "'ConfirmBuy'" in line)
    insist(confirm_ui < paid_send,
           "original paid rapid UI confirmation no longer precedes ConfirmBuy")
    # The actual dialog callback, and not its invocation alone, owns the
    # original user-acknowledged purchase path.
    has(rapid, "root/20/0", "GetServerTime", "endTime", "para3", "ConfirmBuy")
    has(rapid, "ConfirmBuy", "WorldMarchRapid", "ReduceTime", "UpdateGold",
        "GetTokenIdAndType", "GetCurrencyCount")
    has(rapid, "ConfirmUse", "WorldMarchRapid", "UseTool", "ReduceTime")
    has(currency, "GetCurrencyCount", "ConsumeType_Resource_Item",
        "ConsumeType_Item", "ConsumeType_Resource", "ResourceType", "Gold",
        "Player", "gold", "GetCountByItemId", "GetItemCount")
    has(reply, "HandleMessage", "errorCode", "remainGold")

    # Native read-only daily manager and parsed quest-view fields support a
    # cache-only claimability inventory. The RewardManager's Gold preview
    # sources Player.sm_addGoldCount, so it is NOT a per-task award amount.
    has("DataCenter/DailyTaskData/DailyTaskManager.luac", "GetAllDailyTask",
        "dailyQuestTasks", "state", "TaskState", "Received", "GetQuestTemplate",
        "show", "QuestShowType")
    has("DataCenter/DailyTaskData/DailyTaskInfo.luac", "UpdateInfo",
        "state", "reward", "ReturnRewardParamForView")
    has("DataCenter/RewardManager/RewardManager.luac", "ReturnRewardParamForView",
        "rewardType", "ResourceType", "Gold", "sm_addGoldCount", "count")
    # These are verified game claim routes, not verified live-gold payouts.
    has("UI/UILWQuest/UILWQuestList/Component/UILWDailyTaskItem.luac",
        "OnReceiveClick", "rewardType", "itemId", "DailyTaskReward", "SendMessage")
    has("DataCenter/LWActSignInManager/LWActSignInManager.luac",
        "OnGetEventInfo", "infos", "UpdateData")
    has("DataCenter/LWActSignInManager/LWActSignInManager.luac",
        "ClaimActReward", "IsEnd", "CanClaimAtDay", "GetCanClaimRewardDay",
        "ClaimActSignInReward", "SendMessage")
    has("DataCenter/LWActSignInManager/LWActSignInInfo.luac",
        "GetCanClaimRewardDay", "dayArr", "state", "day", "startTime", 86400000)
    has("DataCenter/LWActSignInManager/LWActSignInInfo.luac",
        "IsEnd", "endTime", "endViewTime", "GetServerTime")
    has("DataCenter/LWActSignInManager/LWActSignInInfo.luac",
        "CanClaimAtDay", "dayArr", "GetServerTime", 86400000)
    has("UI/UILWAlliance/UILWAllianceGift/Controller/UILWAllianceGiftCtrl.luac",
        "OnGetClick", "SetGiftReceive", "AllianceGiftGetReward", "SendMessage")

    print("FEATURE018_R5_RAPID_CURRENCY_REWARD_VIEW_V26_SOURCE_PASS")
    print("GOLD_REWARD_CANDIDATES_ONLY: native visible daily task state & sign-in count; no 2289 claim or payout asserted")
    print("PAID_RAPID_CONFIRMATION: native ShowUseDiamondConfirm; app opt-in does not prove dialog completion")


if __name__ == "__main__":
    check()
