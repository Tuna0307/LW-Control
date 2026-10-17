"""R7 v26-shaped purchase reply observer, ambiguity, and no-resend checks.

All network data and game manager calls below are synthetic Lua 5.3 tables.
No actual game, account, Windows package, or network operation is performed.
"""
import unittest

from feature018_shield_shop_checks import fixture
import feature018_hotkey_shield_checks as shield


def reply(lua, *, item='9', cost=500, remain=500, error=None):
    """Call precisely the v26 native ItemManager.ItemBuyHandle mock seam."""
    lua.globals().DataCenter.ItemManager.ItemBuyHandle(
        lua.globals().DataCenter.ItemManager,
        lua.table_from({'item':lua.table_from({'id':item}), 'costGold':cost,
                        'remainGold':remain, 'errorCode':error}))


def after(lua):
    lua.execute("state.items={{itemId=9,uuid='owned-stack',count=1}}; LuaEntry.Player.gold=500")


class ShopOriginalReplyChecks(unittest.TestCase):
    def test_source_matching_original_reply_plus_poststate_is_distinct_not_nonce_ack(self):
        lua, state, call = fixture()
        lua.execute('state.originalShopHandler=DataCenter.ItemManager.ItemBuyHandle')
        self.assertEqual(call('buy',gemOptIn='1')['state'], 'accepted')
        self.assertEqual(state['sent'], 1)
        self.assertNotEqual(lua.globals().DataCenter.ItemManager.ItemBuyHandle,
                            state['originalShopHandler'])
        reply(lua)
        self.assertEqual(state['originalBuyReplyHandled'],1)
        waiting = call('status')
        self.assertEqual(waiting['state'],'accepted')
        self.assertEqual(waiting['data']['nativeShopReplyCount'],1)
        self.assertEqual(waiting['data']['nativeShopReplyMatched'],True)
        self.assertEqual(waiting['data']['nativeReplyCausalityConfirmed'],False)
        after(lua)
        result = call('status')
        self.assertEqual(result['state'],'confirmed')
        data = result['data']
        self.assertEqual(data['purchaseProofKind'],
                         'matching_original_reply_and_cached_poststate_no_nonce')
        self.assertEqual(data['observation'],'native_shield_purchase_inventory_and_gold')
        self.assertEqual(data['nativeShopReplyObserved'],True)
        self.assertEqual(data['nativeShopReplyMatched'],True)
        self.assertEqual(data['nativeReplyCausalityConfirmed'],False)
        self.assertEqual(data['nativeShopReplyCount'],1)
        self.assertEqual(data['spentGems'],500)
        self.assertEqual(data['purchasedCount'],1)
        self.assertEqual(state['sent'],1)
        self.assertEqual(lua.eval('DataCenter.ItemManager.ItemBuyHandle == state.originalShopHandler'),True)

    def test_prior_stock_and_balance_proof_stays_distinct_without_reply(self):
        lua, state, call = fixture()
        lua.execute('state.originalShopHandler=DataCenter.ItemManager.ItemBuyHandle')
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        after(lua)
        result=call('status')
        self.assertEqual(result['state'],'confirmed')
        data=result['data']
        self.assertEqual(data['purchaseProofKind'],'native_cached_inventory_and_gold_only')
        self.assertEqual(data['observation'],'native_shield_purchase_inventory_and_gold')
        self.assertEqual(data['nativeShopReplyObserved'],False)
        self.assertEqual(data['nativeShopReplyMatched'],False)
        self.assertEqual(data['nativeReplyCausalityConfirmed'],False)
        self.assertEqual(state['sent'],1)
        self.assertEqual(lua.eval('DataCenter.ItemManager.ItemBuyHandle == state.originalShopHandler'),True)

    def test_wrong_item_cost_gold_or_error_reply_never_confirms_even_if_cached_state_matches(self):
        trials=({'item':'10'}, {'item':'oops'}, {'cost':499},
                {'remain':499}, {'error':401})
        for case in trials:
            with self.subTest(case=case):
                lua,state,call=fixture()
                self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
                reply(lua,**case)
                after(lua)
                result=call('status')
                self.assertEqual(result['state'],'accepted')
                self.assertEqual(result['data']['nativeShopReplyObserved'],True)
                self.assertEqual(result['data']['nativeReplyAmbiguous'],True)
                self.assertEqual(result['data']['nativeShopReplyMatched'],False)
                self.assertEqual(result['data']['nativeReplyCausalityConfirmed'],False)
                self.assertEqual(call('buy',gemOptIn='1')['state'],'failed')
                self.assertEqual(state['sent'],1)

    def test_original_handler_id_precedence_blocks_conflicting_reply_item_ids(self):
        lua,state,call=fixture()
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        lua.execute('''
          DataCenter.ItemManager:ItemBuyHandle({
            item={id=10,itemId=9,count=1},costGold=500,remainGold=500})
        ''')
        after(lua)
        result=call('status')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['nativeReplyAmbiguous'],True)
        self.assertEqual(result['data']['nativeShopReplyMatched'],False)
        self.assertEqual(state['sent'],1)

    def test_duplicate_matching_replies_ambiguous_and_never_resend(self):
        lua,state,call=fixture()
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        reply(lua)
        reply(lua)
        after(lua)
        result=call('status')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['nativeShopReplyCount'],2)
        self.assertEqual(result['data']['nativeReplyAmbiguous'],True)
        self.assertEqual(result['data']['nativeShopReplyMatched'],False)
        self.assertEqual(call('buy',gemOptIn='1')['state'],'failed')
        self.assertEqual(state['sent'],1)

    def test_reply_arrives_after_uncertain_network_send_return(self):
        lua,state,call=fixture()
        lua.execute('''
          local original=SFSNetwork.SendMessage
          SFSNetwork.SendMessage=function(...)
            original(...)
            return false
          end
        ''')
        self.assertEqual(call('buy',gemOptIn='1')['state'],'failed')
        self.assertEqual(state['sent'],1)
        reply(lua)
        after(lua)
        result=call('status')
        self.assertEqual(result['state'],'confirmed')
        self.assertEqual(result['data']['nativeShopReplyMatched'],True)
        self.assertEqual(result['data']['nativeReplyCausalityConfirmed'],False)
        self.assertEqual(state['sent'],1)

    def test_callback_must_be_armed_before_synchronous_native_response(self):
        lua,state,call=fixture()
        lua.execute('''
          local previous=SFSNetwork.SendMessage
          SFSNetwork.SendMessage=function(...)
            previous(...)
            DataCenter.ItemManager:ItemBuyHandle({
              item={id=9,count=1}, costGold=500,remainGold=500})
          end
        ''')
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        self.assertEqual(state['originalBuyReplyHandled'],1)
        after(lua)
        result=call('status')
        self.assertEqual(result['state'],'confirmed')
        self.assertEqual(result['data']['nativeShopReplyMatched'],True)
        self.assertEqual(state['sent'],1)

    def test_account_swaps_inside_native_reply_are_ambiguous(self):
        lua,state,call=fixture()
        lua.execute('''
          local previous=DataCenter.ItemManager.ItemBuyHandle
          DataCenter.ItemManager.ItemBuyHandle=function(self,data)
            previous(self,data)
            LuaEntry.Player.uid='3133546747002212'
          end
          state.handlerBefore=DataCenter.ItemManager.ItemBuyHandle
        ''')
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        reply(lua)
        lua.execute("LuaEntry.Player.uid='3133546747002289'")
        after(lua)
        result=call('status')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['nativeReplyAmbiguous'],True)
        self.assertEqual(result['data']['nativeShopReplyMatched'],False)
        self.assertEqual(state['sent'],1)

    def test_current_item_manager_changes_before_status_without_clearing_pending(self):
        lua,state,call=fixture()
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        reply(lua)
        after(lua)
        lua.execute('''
          state.originalItemManager=DataCenter.ItemManager
          DataCenter.ItemManager={ItemBuyHandle=state.originalItemManager.ItemBuyHandle}
        ''')
        self.assertEqual(call('status')['state'],'failed')
        self.assertEqual(call('buy',gemOptIn='1')['state'],'failed')
        lua.execute('DataCenter.ItemManager=state.originalItemManager')
        self.assertEqual(call('status')['state'],'confirmed')
        self.assertEqual(state['sent'],1)

    def test_original_handler_error_is_not_swallowed_or_counted_as_valid_reply(self):
        lua,state,call=fixture()
        lua.execute('''
          DataCenter.ItemManager.ItemBuyHandle=function(self,data)
            state.originalBuyReplyHandled=(state.originalBuyReplyHandled or 0)+1
            error('original handler failed')
          end
        ''')
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        with self.assertRaises(Exception):
            reply(lua)
        after(lua)
        result=call('status')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['nativeReplyAmbiguous'],True)
        self.assertEqual(state['originalBuyReplyHandled'],1)
        self.assertEqual(state['sent'],1)

    def test_missing_source_handler_fails_before_buy_and_remains_retryable(self):
        for change in ('DataCenter.ItemManager=nil',
                       'DataCenter.ItemManager.ItemBuyHandle=nil'):
            with self.subTest(change=change):
                lua,state,call=fixture()
                lua.execute(change)
                result=call('buy',gemOptIn='1')
                self.assertEqual(result['state'],'unsupported')
                self.assertEqual(state['sent'],0)
                self.assertEqual(state['cooldownSets'],0)

    def test_expired_observer_restores_original_but_keeps_no_resend_tombstone(self):
        lua,state,call=fixture()
        lua.execute('''
          state.originalShopHandler=DataCenter.ItemManager.ItemBuyHandle
          state.wallNow=100
          os.time=function()return state.wallNow end
        ''')
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        lua.execute('state.wallNow=200')
        result=call('status')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['nativeReplyObserverExpired'],True)
        self.assertEqual(lua.eval('DataCenter.ItemManager.ItemBuyHandle == state.originalShopHandler'),True)
        self.assertEqual(call('buy',gemOptIn='1')['state'],'failed')
        self.assertEqual(state['sent'],1)
        reply(lua)
        self.assertEqual(state['originalBuyReplyHandled'],1)
        after(lua)
        final=call('status')
        self.assertEqual(final['state'],'confirmed')
        self.assertEqual(final['data']['purchaseProofKind'],'native_cached_inventory_and_gold_only')
        self.assertEqual(final['data']['nativeShopReplyObserved'],False)
        self.assertEqual(state['sent'],1)

    def test_external_handler_replacement_is_not_overwritten_or_confirmed(self):
        lua,state,call=fixture()
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        lua.execute('''
          state.foreignHandler=function(self,reply)end
          DataCenter.ItemManager.ItemBuyHandle=state.foreignHandler
        ''')
        after(lua)
        result=call('status')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['nativeReplyAmbiguous'],True)
        self.assertEqual(lua.eval('DataCenter.ItemManager.ItemBuyHandle == state.foreignHandler'),True)
        self.assertEqual(state['sent'],1)


class ShieldUsePendingLifecycleChecks(unittest.TestCase):
    def test_same_uid_different_native_player_cannot_claim_itemuse_poststate(self):
        lua,module,state=shield.run()
        self.assertEqual(shield.command(lua,module,8)['state'],'accepted')
        lua.execute('''
          state.items[1].count=1
          state.effectEnd[501]=state.serverTime+8*3600000
          state.originalPlayer=LuaEntry.Player
          local p=state.originalPlayer
          LuaEntry.Player={uid=p.uid,
            GetSourceServerId=p.GetSourceServerId,
            GetSelfServerId=p.GetSelfServerId,
            GetCurServerId=p.GetCurServerId}
        ''')
        self.assertEqual(shield.command(lua,module,8,query=True)['state'],'failed')
        self.assertEqual(shield.command(lua,module,8)['state'],'failed')
        self.assertEqual(state['sent'],1)
        lua.execute('LuaEntry.Player=state.originalPlayer')
        self.assertEqual(shield.command(lua,module,8,query=True)['state'],'confirmed')
        self.assertEqual(state['sent'],1)

    def test_negative_elapsed_clock_blocks_old_itemuse_without_replay(self):
        lua,module,state=shield.run()
        lua.execute('state.wallNow=200; os.time=function()return state.wallNow end')
        self.assertEqual(shield.command(lua,module,8)['state'],'accepted')
        lua.execute('state.wallNow=199')
        self.assertEqual(shield.command(lua,module,8,query=True)['state'],'failed')
        self.assertEqual(shield.command(lua,module,8)['state'],'failed')
        self.assertEqual(state['sent'],1)

    def test_wall_clock_rollback_retires_shop_observer_but_never_allows_replay(self):
        lua,state,call=fixture()
        lua.execute('''
          state.wallNow=300
          os.time=function()return state.wallNow end
          state.originalHandler=DataCenter.ItemManager.ItemBuyHandle
        ''')
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        lua.execute('state.wallNow=299')
        result=call('status')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['nativeReplyObserverExpired'],True)
        self.assertEqual(lua.eval('DataCenter.ItemManager.ItemBuyHandle == state.originalHandler'),True)
        self.assertEqual(call('buy',gemOptIn='1')['state'],'failed')
        self.assertEqual(state['sent'],1)


if __name__=='__main__':
    unittest.main()
