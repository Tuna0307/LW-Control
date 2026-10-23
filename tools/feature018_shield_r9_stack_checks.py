"""R9 original v26 ItemData UUID-specific shop-reply/poststate checks.

All accounts, server responses and inventory are synthetic Lua 5.3 fixtures;
no real game, payment, gold claim, network or installer is accessed.
"""

import unittest

import feature018_shield_shop_checks as shop


def setup(hours):
    lua, state, call = shop.fixture()
    price = {12: 2500, 24: 5000}[hours]
    item = {12: 10, 24: 11}[hours]
    lua.globals().LuaEntry.Player.gold = price
    state['originalBuyReplyHandled'] = 0
    sent = call('buy', slot=hours, gemOptIn='1')
    assert sent['state'] == 'accepted', sent['error']
    assert state['sent'] == 1
    return lua, state, call, item, price


def reply(lua, item, price, *, uuid='r9-server-stack', count=1):
    payload = dict(id=item, count=count, uuid=uuid)
    if uuid is None:
        del payload['uuid']
    lua.globals().DataCenter.ItemManager.ItemBuyHandle(
        lua.globals().DataCenter.ItemManager,
        lua.table_from({'item': lua.table_from(payload),
                        'costGold': price, 'remainGold': 0}))


def inventory(lua, item, uuid, count=1):
    lua.execute('state.items={}')
    lua.globals().state['items'][1] = lua.table_from(
        {'itemId': item, 'uuid': uuid, 'count': count})
    lua.globals().LuaEntry.Player.gold = 0


class OriginalReplyOwnedStackChecks(unittest.TestCase):
    def test_both_12_and_24_matching_original_uuid_and_native_state(self):
        for hours in (12, 24):
            with self.subTest(hours=hours):
                lua, state, call, item, price = setup(hours)
                reply(lua, item, price, uuid='server-issued-uuid')
                self.assertEqual(state['originalBuyReplyHandled'], 1)
                inventory(lua, item, 'server-issued-uuid')
                result = call('status', slot=hours)
                self.assertEqual(result['state'], 'confirmed')
                data = result['data']
                self.assertEqual(data['nativeShopReplyStackMatched'], True)
                self.assertEqual(data['nativeShopReplyMatched'], True)
                self.assertEqual(data['purchaseProofKind'],
                    'matching_original_reply_owned_stack_and_cached_poststate_no_nonce')
                self.assertEqual(data['nativeReplyCausalityConfirmed'], False)
                self.assertEqual(data['observation'],
                                 'native_shield_purchase_inventory_and_gold')
                self.assertEqual(data['purchasedCount'], 1)
                self.assertEqual(data['spentGems'], price)
                self.assertEqual(state['sent'], 1)

    def test_different_owned_stack_same_item_count_and_spend_is_ambiguous(self):
        for hours in (12, 24):
            with self.subTest(hours=hours):
                lua, state, call, item, price = setup(hours)
                reply(lua, item, price, uuid='from-original-reply')
                inventory(lua, item, 'other-new-stack')
                result = call('status', slot=hours)
                self.assertEqual(result['state'], 'accepted')
                self.assertEqual(result['data']['nativeReplyStackMismatch'], True)
                self.assertEqual(result['data']['nativeShopReplyMatched'], False)
                self.assertEqual(result['data']['nativeReplyCausalityConfirmed'], False)
                self.assertEqual(call('buy', slot=hours, gemOptIn='1')['state'], 'failed')
                self.assertEqual(state['sent'], 1)

    def test_source_reply_unverifiable_uuid_or_stack_count_never_credits(self):
        for uuid, count in (('bad uuid', 1), ('', 1),
                            ('valid-stack', 2), ('valid-stack', -1),
                            ('valid-stack', 1.5), ('valid-stack', 'unknown')):
            with self.subTest(uuid=uuid, count=count):
                lua, state, call, item, price = setup(12)
                reply(lua, item, price, uuid=uuid, count=count)
                inventory(lua, item, 'valid-stack')
                result = call('status', slot=12)
                self.assertEqual(result['state'], 'accepted')
                self.assertEqual(result['data']['nativeReplyAmbiguous'], True)
                self.assertEqual(result['data']['nativeShopReplyMatched'], False)
                self.assertEqual(call('buy', slot=12, gemOptIn='1')['state'], 'failed')
                self.assertEqual(state['sent'], 1)

    def test_duplicate_same_uuid_inventory_rows_do_not_fake_single_stack(self):
        lua, state, call, item, price = setup(24)
        reply(lua, item, price, uuid='one-uuid')
        lua.execute('''
          state.items={{itemId=11,uuid='one-uuid',count=1},
                       {itemId=11,uuid='one-uuid',count=0}}
          LuaEntry.Player.gold=0
        ''')
        result = call('status', slot=24)
        self.assertEqual(result['state'], 'accepted')
        self.assertEqual(result['data']['nativeReplyStackMismatch'], True)
        self.assertEqual(result['data']['nativeReplyAmbiguous'], True)
        self.assertEqual(state['sent'], 1)

    def test_owned_stack_wrong_itemid_never_credits_receipt(self):
        lua, state, call, item, price = setup(12)
        reply(lua, item, price, uuid='wrong-template')
        inventory(lua, 9, 'wrong-template')
        result = call('status', slot=12)
        self.assertEqual(result['state'], 'accepted')
        self.assertEqual(result['data']['nativeReplyStackMismatch'], True)
        self.assertEqual(state['sent'], 1)

    def test_duplicate_exact_12_and_24_original_replies_never_clear_no_resend(self):
        for hours in (12, 24):
            with self.subTest(hours=hours):
                lua, state, call, item, price = setup(hours)
                reply(lua, item, price, uuid='same-original-reply')
                reply(lua, item, price, uuid='same-original-reply')
                inventory(lua, item, 'same-original-reply')
                result = call('status', slot=hours)
                self.assertEqual(result['state'], 'accepted')
                self.assertEqual(result['data']['nativeShopReplyCount'], 2)
                self.assertEqual(result['data']['nativeShopReplyMatched'], False)
                self.assertEqual(result['data']['nativeReplyAmbiguous'], True)
                self.assertEqual(result['data']['nativeReplyCausalityConfirmed'], False)
                self.assertEqual(call('buy', slot=hours, gemOptIn='1')['state'], 'failed')
                self.assertEqual(state['sent'], 1)

    def test_missing_server_uuid_preserves_prior_weak_no_nonce_proof(self):
        lua, state, call, item, price = setup(12)
        reply(lua, item, price, uuid=None)
        inventory(lua, item, 'native-unknown-uuid')
        result = call('status', slot=12)
        self.assertEqual(result['state'], 'confirmed')
        self.assertEqual(result['data']['purchaseProofKind'],
                         'matching_original_reply_and_cached_poststate_no_nonce')
        self.assertIsNone(result['data']['nativeShopReplyStackMatched'])
        self.assertEqual(result['data']['nativeReplyCausalityConfirmed'], False)
        self.assertEqual(state['sent'], 1)


if __name__ == '__main__':
    unittest.main()
