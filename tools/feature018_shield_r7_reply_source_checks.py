"""Read-only v26 bytecode proof for R7 no-nonce shield shop reply observation.

This reads the preserved original package. It does not launch, patch, modify,
or send anything to the game/account. It intentionally cannot prove a 2289
reward is currently claimable or how much Gold it would yield.
"""

import hashlib
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
sys.path.insert(0, str(TOOLS / 'map_009_offline'))
import feature018_mini_lua as source  # noqa: E402
from inspect_current import root_methods  # noqa: E402


EXPECTED = {
    'Net/Msgs/ItemUseMessage.luac':
        '9313f37b8183fd7338fcfc82cfd3a0d211db1d9cc5ac67fc5d0692de78262a1c',
    'Net/Msgs/UIShopBuyMessage.luac':
        'f7bcd8091f79c695ee6da6e9a4eeb7d86cfea6dc281c8c2dffaf445cb002ba0d',
    'DataCenter/ItemManager/ItemManager.luac':
        'febee83973a019ce47dc154190a7bae182b6ea5d5546b63c7f412ec60222847c',
    'DataCenter/DailyTaskData/DailyTaskManager.luac':
        'f61d968cc0895910a30189c63b747947e3df401b32ecce3ad2cf039ec0a9f871',
    'DataCenter/RewardManager/RewardManager.luac':
        '4a0fb530b6bf99d7f987bbef543eebe5a649cb705999ad48a2c721856419341e',
}


def check():
    version, content_version, entries = source.probe.read_lwlf(source.probe.paths()['data'])
    assert (version, content_version) == (3, 26)
    originals = dict(entries)
    original = originals.get(source.probe.ORIGINAL_LUA_ENTRY,
                             originals.get(source.probe.LUA_ENTRY))
    assert original and hashlib.sha256(original).hexdigest() == source.ORIGINAL_ENTRY_SHA256
    files = dict(entries)

    def method(module, name, required):
        raw = files[module]
        assert hashlib.sha256(raw).hexdigest() == EXPECTED[module], module
        root = source.lua.parse_chunk(source.probe.decode_lenc(raw))
        method_path = root_methods(root)[name]
        proto = next(obj for path,obj in source.walk(root) if path == method_path)
        constants = set(proto['constants'])
        assert all(field in constants for field in required), (module, name, required)
        return constants

    shop = 'Net/Msgs/UIShopBuyMessage.luac'
    inventory = 'DataCenter/ItemManager/ItemManager.luac'
    daily = 'DataCenter/DailyTaskData/DailyTaskManager.luac'
    rewards = 'DataCenter/RewardManager/RewardManager.luac'
    send_fields = method(shop, 'OnCreate', ['itemId', 'num', 'PutUtfString', 'PutInt'])
    handler_fields = method(shop, 'HandleMessage', ['ItemManager', 'ItemBuyHandle'])
    reply_fields = method(inventory, 'ItemBuyHandle', ['errorCode', 'remainGold',
        'costGold', 'item', 'id', 'itemId', 'gold', 'payTotal', 'UpdateOneItem'])
    assert 'requestUuid' not in send_fields and 'requestUuid' not in reply_fields
    assert 'requestId' not in send_fields and 'requestId' not in reply_fields
    assert 'clientCreateUuid' not in handler_fields and 'clientCreateUuid' not in reply_fields

    # R8 source-matched ItemUse receiver and count update. The original
    # SendMessage sends only uuid+num; reply contains uuid,itemId,count and
    # errorCode, which ItemManager.ItemUseHandle applies to owned ItemData.
    use = 'Net/Msgs/ItemUseMessage.luac'
    use_send = method(use, 'OnCreate', ['PutUtfString', 'uuid', 'PutInt', 'num'])
    use_receiver = method(use, 'HandleMessage', ['ItemManager', 'ItemUseHandle', 'errorCode'])
    use_handler = method(inventory, 'ItemUseHandle', ['errorCode', 'uuid',
        'itemId', 'count', 'GetItemByUuid', 'UpdateOneItem', 'OnUseRet', 'opType'])
    assert 'requestUuid' not in use_send and 'requestId' not in use_handler
    assert 'clientCreateUuid' not in use_receiver and 'clientCreateUuid' not in use_handler

    # The original DailyTask claim response owns reward updates, but its
    # Gold view preview reads Player.sm_addGoldCount instead of a task grant.
    method(daily, 'DailyTaskRewardMessageHandle',
           ['errorCode', 'AddRewardsAndRes', 'taskInfo', 'UpdateOneDailyTaskInfo'])
    method(rewards, 'ReturnRewardParamForView',
           ['Gold', 'sm_addGoldCount', 'rewardType', 'count'])
    print('FEATURE018_R7_SHIELD_REPLY_NATIVE_V26_SOURCE_PASS')
    print('ORIGINAL_SHOP_REPLY_FIELDS: errorCode,item.id,costGold,remainGold; NO request nonce')
    print('ORIGINAL_ITEMUSE_REPLY_FIELDS: errorCode,uuid,itemId,count; NO request nonce')
    print('GOLD_REWARD_AMOUNT: no verified current-2289 claimable Gold payout')


if __name__ == '__main__':
    check()
