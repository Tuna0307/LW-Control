"""R8 original ItemUseHandle receipt correlation, shield 12/24h, owner/session tests.

Source-shaped Lua 5.3 fixture ONLY: no real game/account/wallet/native sends.
"""

import unittest

import feature018_hotkey_shield_checks as shield


def setup(hours=12):
    lua, module, state = shield.run()
    lua.execute('''
      state.wallNow=100
      os.time=function() return state.wallNow end
      state.originalHandle=DataCenter.ItemManager.ItemUseHandle
    ''')
    return lua, module, state


def native_reply(lua, *, slot=12, uuid=None, item=None, count=None,
                 error=None, op_type=None):
    ids={8:9, 12:10, 24:11}
    uuids={8:'101001', 12:'101002',24:'101003'}
    counts={8:1, 12:0, 24:0}
    lua.globals().DataCenter.ItemManager.ItemUseHandle(
        lua.globals().DataCenter.ItemManager,
        lua.table_from({
            'uuid': uuid if uuid is not None else uuids[slot],
            'itemId': item if item is not None else ids[slot],
            'count': count if count is not None else counts[slot],
            'errorCode':error,'opType':op_type}))


def apply_poststate(lua, slot):
    lua.execute(f'''
      local which = ({ {8: 1, 12: 2, 24: 3}[slot] })
      state.items[which].count=state.items[which].count-1
      state.effectEnd[{ {8: 501, 12: 502, 24: 503}[slot] }]=state.serverTime+{slot}*3600000
    ''')


class ItemUseNativeReplyChecks(unittest.TestCase):
    def test_both_12_and_24_native_item_receipt_plus_poststate_not_nonce(self):
        for slot in (12,24):
            with self.subTest(slot=slot):
                lua,module,state=setup(slot)
                self.assertEqual(shield.command(lua,module,slot)['state'],'accepted')
                self.assertEqual(state['sent'],1)
                self.assertFalse(lua.eval('DataCenter.ItemManager.ItemUseHandle == state.originalHandle'))
                native_reply(lua,slot=slot)
                self.assertEqual(state['originalItemUseReplyHandled'],1)
                wait=shield.command(lua,module,slot,query=True)
                self.assertEqual(wait['state'],'accepted')
                self.assertEqual(wait['data']['nativeItemUseReplyMatched'],True)
                self.assertEqual(wait['data']['nativeReplyCausalityConfirmed'],False)
                apply_poststate(lua,slot)
                result=shield.command(lua,module,slot,query=True)
                self.assertEqual(result['state'],'confirmed')
                self.assertEqual(result['data']['observation'],
                    'native_owned_shield_consumption_and_MAIN_SHIELD')
                self.assertEqual(result['data']['itemUseProofKind'],
                    'matching_original_itemuse_reply_and_shield_poststate_no_nonce')
                self.assertEqual(result['data']['nativeItemUseReplyObserved'],True)
                self.assertEqual(result['data']['nativeItemUseReplyMatched'],True)
                self.assertEqual(result['data']['nativeItemUseReplyCount'],1)
                self.assertEqual(result['data']['nativeReplyCausalityConfirmed'],False)
                self.assertEqual(result['data']['consumedCount'],1)
                self.assertEqual(state['sent'],1)
                self.assertTrue(lua.eval('DataCenter.ItemManager.ItemUseHandle == state.originalHandle'))

    def test_without_reply_cached_poststate_remains_distinct_not_forged_ack(self):
        lua,module,state=setup()
        self.assertEqual(shield.command(lua,module,12)['state'],'accepted')
        apply_poststate(lua,12)
        result=shield.command(lua,module,12,query=True)
        self.assertEqual(result['state'],'confirmed')
        self.assertEqual(result['data']['itemUseProofKind'],
                         'native_cached_shield_inventory_and_MAIN_SHIELD_only')
        self.assertEqual(result['data']['nativeItemUseReplyMatched'],False)
        self.assertEqual(result['data']['nativeItemUseReplyObserved'],False)
        self.assertEqual(result['data']['nativeReplyCausalityConfirmed'],False)
        self.assertEqual(state['sent'],1)

    def test_reply_alone_or_wrong_status_never_confirms(self):
        lua,module,state=setup()
        self.assertEqual(shield.command(lua,module,12)['state'],'accepted')
        native_reply(lua)
        self.assertEqual(shield.command(lua,module,12,query=True)['state'],'accepted')
        lua.execute('state.items[2].count=0')
        self.assertEqual(shield.command(lua,module,12,query=True)['state'],'accepted')
        lua.execute('state.effectEnd[502]=state.serverTime+12*3600000')
        self.assertEqual(shield.command(lua,module,12,query=True)['state'],'confirmed')

    def test_mismatched_uuid_id_count_error_or_op_type_is_ambiguous(self):
        for overrides in ({'uuid':'different-uuid'}, {'item':11},
                          {'count':1}, {'count':-1}, {'error':401},
                          {'op_type':1}):
            with self.subTest(overrides=overrides):
                lua,module,state=setup()
                self.assertEqual(shield.command(lua,module,12)['state'],'accepted')
                native_reply(lua, **overrides)
                apply_poststate(lua,12)
                pending=shield.command(lua,module,12,query=True)
                self.assertEqual(pending['state'],'accepted')
                self.assertEqual(pending['data']['nativeReplyAmbiguous'],True)
                self.assertEqual(pending['data']['nativeItemUseReplyMatched'],False)
                self.assertEqual(pending['data']['nativeReplyCausalityConfirmed'],False)
                self.assertEqual(shield.command(lua,module,12)['state'],'failed')
                self.assertEqual(state['sent'],1)

    def test_duplicate_reply_remains_unresolved_even_with_matching_poststate(self):
        lua,module,state=setup()
        self.assertEqual(shield.command(lua,module,12)['state'],'accepted')
        native_reply(lua)
        native_reply(lua)
        apply_poststate(lua,12)
        pending=shield.command(lua,module,12,query=True)
        self.assertEqual(pending['state'],'accepted')
        self.assertEqual(pending['data']['nativeReplyAmbiguous'],True)
        self.assertEqual(pending['data']['nativeItemUseReplyCount'],2)
        self.assertEqual(shield.command(lua,module,12)['state'],'failed')
        self.assertEqual(state['sent'],1)

    def test_sender_then_throw_reply_evidence_remains_available_without_replay(self):
        lua,module,state=setup()
        lua.execute('''
          local original=SFSNetwork.SendMessage
          SFSNetwork.SendMessage=function(...)
            original(...)
            error('after native ItemUse send')
          end
        ''')
        self.assertEqual(shield.command(lua,module,12)['state'],'failed')
        self.assertEqual(state['sent'],1)
        native_reply(lua)
        apply_poststate(lua,12)
        result=shield.command(lua,module,12,query=True)
        self.assertEqual(result['state'],'confirmed')
        self.assertEqual(result['data']['nativeItemUseReplyMatched'],True)
        self.assertEqual(state['sent'],1)

    def test_original_handler_throw_propagates_and_never_credits_reply(self):
        lua,module,state=setup()
        lua.execute('''
          DataCenter.ItemManager.ItemUseHandle=function(self,reply)
            state.originalItemUseReplyHandled=(state.originalItemUseReplyHandled or 0)+1
            error('original item use handler error')
          end
        ''')
        self.assertEqual(shield.command(lua,module,12)['state'],'accepted')
        with self.assertRaises(Exception): native_reply(lua)
        apply_poststate(lua,12)
        result=shield.command(lua,module,12,query=True)
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['nativeReplyAmbiguous'],True)
        self.assertEqual(state['sent'],1)

    def test_same_uid_different_player_inside_native_handler_is_ambiguous(self):
        lua,module,state=setup()
        lua.execute('''
          local prior=DataCenter.ItemManager.ItemUseHandle
          DataCenter.ItemManager.ItemUseHandle=function(self,reply)
            prior(self,reply)
            local p=LuaEntry.Player
            LuaEntry.Player={uid=p.uid,
              GetSourceServerId=p.GetSourceServerId,
              GetSelfServerId=p.GetSelfServerId,
              GetCurServerId=p.GetCurServerId}
          end
        ''')
        self.assertEqual(shield.command(lua,module,12)['state'],'accepted')
        native_reply(lua)
        apply_poststate(lua,12)
        self.assertEqual(shield.command(lua,module,12,query=True)['state'],'failed')
        self.assertEqual(state['sent'],1)

    def test_other_handler_replacement_preserved_never_false_confirms(self):
        lua,module,state=setup()
        self.assertEqual(shield.command(lua,module,12)['state'],'accepted')
        lua.execute('''
          state.otherHandler=function(...)end
          DataCenter.ItemManager.ItemUseHandle=state.otherHandler
        ''')
        apply_poststate(lua,12)
        result=shield.command(lua,module,12,query=True)
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['nativeReplyAmbiguous'],True)
        self.assertTrue(lua.eval('DataCenter.ItemManager.ItemUseHandle == state.otherHandler'))
        self.assertEqual(state['sent'],1)

    def test_status_timeout_and_backward_clock_restore_observer_not_tombstone(self):
        for next_time in (146,99):
            with self.subTest(next_time=next_time):
                lua,module,state=setup()
                self.assertEqual(shield.command(lua,module,12)['state'],'accepted')
                lua.execute(f'state.wallNow={next_time}')
                self.assertEqual(shield.command(lua,module,12,query=True)['state'],'failed')
                self.assertTrue(lua.eval('DataCenter.ItemManager.ItemUseHandle == state.originalHandle'))
                self.assertEqual(shield.command(lua,module,12)['state'],'failed')
                self.assertEqual(state['sent'],1)

    def test_missing_original_item_manager_handler_denies_before_mutation(self):
        for expr in ('DataCenter.ItemManager=nil',
                     'DataCenter.ItemManager.ItemUseHandle=nil'):
            with self.subTest(expr=expr):
                lua,module,state=setup()
                lua.execute(expr)
                result=shield.command(lua,module,12)
                self.assertEqual(result['state'],'unsupported')
                self.assertEqual(state['sent'],0)
                self.assertEqual(state['cooldownSets'],0)

    def test_synchronous_original_itemuse_reply_seen_before_sender_returns(self):
        lua,module,state=setup()
        lua.execute('''
          local prior=SFSNetwork.SendMessage
          SFSNetwork.SendMessage=function(...)
            prior(...)
            DataCenter.ItemManager:ItemUseHandle({
              uuid='101002',itemId=10,count=0})
          end
        ''')
        self.assertEqual(shield.command(lua,module,12)['state'],'accepted')
        apply_poststate(lua,12)
        result=shield.command(lua,module,12,query=True)
        self.assertEqual(result['state'],'confirmed')
        self.assertEqual(result['data']['nativeItemUseReplyMatched'],True)
        self.assertEqual(state['originalItemUseReplyHandled'],1)
        self.assertEqual(state['sent'],1)


if __name__ == '__main__':
    unittest.main()
