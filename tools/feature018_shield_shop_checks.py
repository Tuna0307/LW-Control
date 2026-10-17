"""Inert original-schema shield provisioning and no-resend checks."""
import feature018_hotkey_shield_checks as base
import unittest


def fixture():
    lua, module, state = base.run()
    lua.execute('''
    LuaEntry={Player={uid='3133546747002289',gold=1000,
      GetSourceServerId=function() return 2289 end,
      GetSelfServerId=function() return 2289 end,
      GetCurServerId=function() return 2289 end}}
    state.items={}
    state.templates[1].price=500
    state.templates[2].price=2500
    state.templates[3].price=5000
    MsgDefines.UIShopBuy='UIShopBuy'
    DataCenter.ItemManager={ItemBuyHandle=function(self,reply)
      state.originalBuyReplyHandled=(state.originalBuyReplyHandled or 0)+1
      state.lastBuyReply=reply
    end}
    SFSNetwork.SendMessage=function(name,data)
      assert(name=='UIShopBuy' and (data.itemId=='9' or data.itemId=='10' or data.itemId=='11') and data.num==1)
      state.sent=state.sent+1
      state.lastPurchasedItemId=data.itemId
    end
    ''')
    def call(mode='inspect', slot=8, **extra):
        return module.ShieldShop(lua.table_from(dict(
            slot=str(slot),mode=mode,profileId='profile',launchSessionId='launch',
            challenge='challenge',gamePid='7020',**extra)))
    return lua, state, call


def main():
    lua, state, call = fixture()
    assert call()['data']['nativePrice'] == 500 and state.sent == 0
    assert call('buy')['state'] == 'failed' and state.sent == 0
    lua.globals().LuaEntry.Player.uid='3133546747002212'
    assert call('buy',gemOptIn='1')['state'] == 'failed' and state.sent == 0
    lua.globals().LuaEntry.Player.uid='3133546747002289'
    assert call('buy',gemOptIn='1')['state'] == 'accepted' and state.sent == 1
    assert call('buy',gemOptIn='1')['state'] == 'failed' and state.sent == 1
    assert call('status')['state'] == 'accepted' and state.sent == 1
    lua.execute("state.items={{itemId=9,uuid=123,count=1}}")
    assert call('status')['state'] == 'accepted', 'Inventory alone does not prove price paid'
    lua.globals().LuaEntry.Player.gold=500
    result=call('status')
    assert result['state']=='confirmed' and result['data']['spentGems']==500 and state.sent==1
    assert call('buy',gemOptIn='1')['state']=='failed' and state.sent==1
    print('FEATURE018_SHIELD_SHOP_LUA53_OFFLINE_PASS')


def replacement_fixture():
    lua, module, state = base.run()
    lua.execute('''
      LuaEntry={Player={uid='3133546747002289',gold=10000,
        GetSourceServerId=function() return 2289 end,
        GetSelfServerId=function() return 2289 end,
        GetCurServerId=function() return 2289 end}}
      Clock=100; os.time=function() return Clock end
      state.protectEnd=state.serverTime+4*3600000
      state.effectEnd[501]=state.protectEnd
    ''')
    def shop(hours=8, challenge='challenge', **extra):
        return module.ShieldShop(lua.table_from(dict(
            slot=str(hours), mode='armUse', profileId='profile',
            launchSessionId='launch', challenge=challenge, gamePid='7020', **extra)))
    def key(hours=8, challenge='challenge', **extra):
        return module.Handle(lua.table_from(dict(
            action='shieldUse',slot=str(hours),profileId='profile',
            launchSessionId='launch',challenge=challenge,gamePid='7020', **extra)))
    return lua, module, state, shop, key


class ArmedShieldChecks(unittest.TestCase):
    def test_arm_requires_exact_explicit_consent_without_spending(self):
        lua, module, state, shop, key = replacement_fixture()
        self.assertEqual(shop()['state'], 'failed')
        self.assertEqual(shop(shieldReplaceOptIn='0')['state'], 'failed')
        self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'confirmed')
        self.assertEqual(state.sent, 0)
        self.assertEqual(state.cooldownSets, 0)

    def test_successful_arm_is_duration_bound_and_consumed_once(self):
        lua, module, state, shop, key = replacement_fixture()
        self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'confirmed')
        self.assertEqual(key(12)['state'], 'failed')
        self.assertEqual(state.sent, 0)
        first = key()
        self.assertEqual(first['state'], 'accepted')
        self.assertEqual(state.sent, 1)
        self.assertEqual(state.lastUuid, '101001')
        self.assertEqual(key()['state'], 'failed')
        self.assertEqual(state.sent, 1)
        lua.execute('''state.items[1].count=state.items[1].count-1
          state.effectEnd[501]=state.serverTime+8*3600000
          state.protectEnd=state.effectEnd[501]''')
        self.assertEqual(key(queryOnly='1')['state'], 'confirmed')
        # An independently shortened shield does not restore the used permit.
        lua.execute('state.effectEnd[501]=state.serverTime+4*3600000; state.protectEnd=state.effectEnd[501]')
        self.assertEqual(key()['state'], 'failed')
        self.assertEqual(state.sent, 1)

    def test_owner_token_mismatch_cannot_use_an_armed_permit(self):
        lua, module, state, shop, key = replacement_fixture()
        self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'confirmed')
        self.assertEqual(key(challenge='foreign')['state'], 'failed')
        self.assertEqual(state.sent, 0)

    def test_native_uid_and_all_server_getters_fence_arming(self):
        for expression in (
            "LuaEntry.Player.uid='3133546747002212'",
            'LuaEntry.Player.GetSourceServerId=function() return 2212 end',
            'LuaEntry.Player.GetSelfServerId=function() return 2212 end',
            'LuaEntry.Player.GetCurServerId=function() return 2212 end',
        ):
            with self.subTest(expression=expression):
                lua, module, state, shop, key = replacement_fixture()
                lua.execute(expression)
                self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'failed')
                self.assertEqual(state.sent, 0)

    def test_expired_arm_cannot_replace(self):
        lua, module, state, shop, key = replacement_fixture()
        self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'confirmed')
        lua.execute('Clock=161')
        self.assertEqual(key()['state'], 'failed')
        self.assertEqual(state.sent, 0)

    def test_equal_or_longer_current_duration_cannot_arm(self):
        for remaining in (8*3600000, 12*3600000):
            with self.subTest(remaining=remaining):
                lua, module, state, shop, key = replacement_fixture()
                state.effectEnd[501] = state.serverTime + remaining
                self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'failed')
                self.assertEqual(state.sent, 0)

    def test_current_duration_is_rechecked_after_arming(self):
        lua, module, state, shop, key = replacement_fixture()
        self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'confirmed')
        lua.execute('state.effectEnd[501]=state.serverTime+24*3600000; state.protectEnd=state.effectEnd[501]')
        self.assertEqual(key()['state'], 'failed')
        self.assertEqual(state.sent, 0)

    def test_ambiguous_simultaneous_native_shields_fail_closed(self):
        lua, module, state, shop, key = replacement_fixture()
        lua.execute('state.effectEnd[502]=state.serverTime+12*3600000')
        self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'failed')
        self.assertEqual(key(shieldReplaceOptIn='1')['state'], 'failed')
        self.assertEqual(state.sent, 0)

    def test_inventory_debit_with_unchanged_near_full_existing_shield_is_not_ack(self):
        lua, module, state, shop, key = replacement_fixture()
        lua.execute('''
          state.protectEnd=state.serverTime+8*3600000-60000
          state.effectEnd[501]=state.protectEnd
        ''')
        self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'confirmed')
        self.assertEqual(key()['state'], 'accepted')
        state['items'][1]['count'] = 1
        # The previous 7h59m shield is still active, so an inventory debit
        # alone does not prove the new 8h effect was actually applied.
        self.assertEqual(key(queryOnly='1')['state'], 'accepted')
        self.assertEqual(state.sent, 1)
        lua.execute('state.effectEnd[501]=state.serverTime+8*3600000')
        self.assertEqual(key(queryOnly='1')['state'], 'confirmed')

    def test_arm_does_not_bypass_native_warnings(self):
        for field, value in (('cd', True), ('war', 1), ('season', True), ('meteorite', True)):
            with self.subTest(field=field):
                lua, module, state, shop, key = replacement_fixture()
                self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'confirmed')
                state[field] = value
                self.assertNotEqual(key()['state'], 'accepted')
                self.assertEqual(state.sent, 0)

    def test_send_then_throw_retains_uncertain_item_fence(self):
        lua, module, state, shop, key = replacement_fixture()
        self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'confirmed')
        lua.execute('local original=SFSNetwork.SendMessage; SFSNetwork.SendMessage=function(...) original(...); error("after send") end')
        self.assertEqual(key()['state'], 'failed')
        self.assertEqual(state.sent, 1)
        # Explicit consent is not permission to replay an unresolved request.
        self.assertEqual(key(shieldReplaceOptIn='1')['state'], 'failed')
        self.assertEqual(state.sent, 1)

    def test_foreign_query_cannot_retire_original_owner_pending_fence(self):
        lua, module, state, shop, key = replacement_fixture()
        self.assertEqual(shop(shieldReplaceOptIn='1')['state'], 'confirmed')
        self.assertEqual(key()['state'], 'accepted')
        self.assertEqual(key(challenge='foreign', queryOnly='1')['state'], 'failed')
        self.assertEqual(key(shieldReplaceOptIn='1')['state'], 'failed')
        self.assertEqual(state.sent, 1)


class GemPurchaseChecks(unittest.TestCase):
    def test_exact_native_price_stock_and_balance_rechecked_before_spend(self):
        for mutation in (
            'state.templates[1].price=600',
            'LuaEntry.Player.gold=499',
            'state.items={{itemId=9,uuid=991,count=1}}',
        ):
            with self.subTest(mutation=mutation):
                lua, state, call = fixture()
                # Change native item/price/balance on the second lookup, after
                # read-only preflight but before the actual shop transaction.
                lua.execute('''
                  local first=DataCenter.ItemData.GetItemList
                  local reads=0
                  DataCenter.ItemData.GetItemList=function(self,...)
                    reads=reads+1
                    if reads==2 then MUTATE() end
                    return first(self,...)
                  end
                '''.replace('MUTATE()', mutation))
                self.assertEqual(call('buy', gemOptIn='1')['state'], 'failed')
                self.assertEqual(state.sent, 0)

    def test_send_error_or_false_retains_pending_purchase_without_resending(self):
        for sender in (
            'local previous=SFSNetwork.SendMessage; SFSNetwork.SendMessage=function(...) previous(...); error("network uncertainty") end',
            'local previous=SFSNetwork.SendMessage; SFSNetwork.SendMessage=function(...) previous(...); return false end',
        ):
            with self.subTest(sender=sender):
                lua, state, call = fixture()
                lua.execute(sender)
                self.assertEqual(call('buy', gemOptIn='1')['state'], 'failed')
                self.assertEqual(state.sent, 1)
                self.assertEqual(call('buy', gemOptIn='1')['state'], 'failed')
                self.assertEqual(state.sent, 1)
                self.assertEqual(call('status')['state'], 'accepted')
                lua.execute('state.items={{itemId=9,uuid=123,count=1}}; LuaEntry.Player.gold=500')
                result = call('status')
                self.assertEqual(result['state'], 'confirmed')
                self.assertEqual(result['data']['spentGems'], 500)
                self.assertEqual(state.sent, 1)

    def test_status_requires_both_exact_native_item_and_gem_changes(self):
        lua, state, call = fixture()
        self.assertEqual(call('buy', gemOptIn='1')['state'], 'accepted')
        lua.execute('LuaEntry.Player.gold=500')
        self.assertEqual(call('status')['state'], 'accepted')
        lua.execute('LuaEntry.Player.gold=1000; state.items={{itemId=9,uuid=123,count=1}}')
        self.assertEqual(call('status')['state'], 'accepted')
        lua.execute('LuaEntry.Player.gold=499')
        self.assertEqual(call('status')['state'], 'accepted')
        lua.execute('LuaEntry.Player.gold=500')
        self.assertEqual(call('status')['state'], 'confirmed')

    def test_main_account_never_makes_or_confirms_purchase(self):
        lua, state, call = fixture()
        self.assertEqual(call('buy', gemOptIn='1')['state'], 'accepted')
        lua.execute("LuaEntry.Player.uid='3133546747002212'")
        lua.execute('state.items={{itemId=9,uuid=123,count=1}}; LuaEntry.Player.gold=500')
        self.assertEqual(call('status')['state'], 'failed')
        self.assertEqual(call('buy', gemOptIn='1')['state'], 'failed')
        self.assertEqual(state.sent, 1)
        lua.execute("LuaEntry.Player.uid='3133546747002289'")
        self.assertEqual(call('status')['state'], 'confirmed')


class LongerShieldPurchaseChecks(unittest.TestCase):
    def test_12_and_24_hour_native_prices_are_read_only_until_exact_opt_in(self):
        for duration, item, price in ((12, '10', 2500), (24, '11', 5000)):
            with self.subTest(duration=duration):
                lua, state, call = fixture()
                state['items'] = lua.table_from({})
                state['sent'] = 0
                lua.globals().LuaEntry.Player.gold = 176
                info = call(slot=duration)
                self.assertEqual(info['state'], 'confirmed')
                data = info['data']
                self.assertEqual(data['itemId'], item)
                self.assertEqual(data['nativePrice'], price)
                self.assertEqual(data['gold'], 176)
                self.assertEqual(data['canAffordNativePrice'], False)
                self.assertEqual(data['additionalGoldNeeded'], price - 176)
                self.assertEqual(data['currencySource'], 'native_Player_gold')
                self.assertEqual(data['purchaseRoute'], 'native_UIShopBuy_one_item_with_consent')
                self.assertEqual(data['requestDispatched'], False)
                self.assertEqual(call('buy', slot=duration, gemOptIn='1')['state'], 'failed')
                self.assertEqual(state['sent'], 0)

                # The increased balance is a fake Lua fixture only. Live
                # native balance/earning eligibility must be reobserved.
                lua.globals().LuaEntry.Player.gold = price
                info = call(slot=duration)
                self.assertEqual(info['data']['canAffordNativePrice'], True)
                self.assertEqual(info['data']['additionalGoldNeeded'], 0)
                self.assertEqual(call('buy', slot=duration)['state'], 'failed')
                self.assertEqual(state['sent'], 0)
                self.assertEqual(call('buy', slot=duration, gemOptIn='1')['state'], 'accepted')
                self.assertEqual(state['lastPurchasedItemId'], item)
                self.assertEqual(state['sent'], 1)
                self.assertEqual(call('buy', slot=duration, gemOptIn='1')['state'], 'failed')
                self.assertEqual(call('status', slot=duration)['state'], 'accepted')
                state['items'] = lua.table_from([lua.table_from(
                    {'itemId': int(item), 'uuid': 'native-owned-stack', 'count': 1})])
                self.assertEqual(call('status', slot=duration)['state'], 'accepted')
                lua.globals().LuaEntry.Player.gold = 0
                result = call('status', slot=duration)
                self.assertEqual(result['state'], 'confirmed')
                self.assertEqual(result['data']['spentGems'], price)
                self.assertEqual(result['data']['purchasedCount'], 1)
                self.assertEqual(state['sent'], 1)

    def test_invalid_native_prices_never_allow_f7_f8_spending(self):
        for value in (-1, 0, 2.5, 'bogus', None):
            for slot, index in ((12, 2), (24, 3)):
                with self.subTest(value=value, slot=slot):
                    lua, state, call = fixture()
                    state['templates'][index]['price'] = value
                    info = call(slot=slot)
                    self.assertEqual(info['state'], 'confirmed')
                    self.assertIsNone(info['data']['canAffordNativePrice'])
                    self.assertIsNone(info['data']['additionalGoldNeeded'])
                    self.assertEqual(call('buy', slot=slot, gemOptIn='1')['state'], 'failed')
                    self.assertEqual(state['sent'], 0)


if __name__=='__main__':
    main()
    unittest.main()
