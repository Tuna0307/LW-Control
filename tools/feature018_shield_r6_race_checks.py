"""R6 native-schema Lua 5.3 shield pre-send/session inverse tests.

All Lua managers/network senders are in-memory fakes. No game, process, real
account, browser, native bridge, installation or RPC is accessed.
"""

import unittest

import feature018_hotkey_shield_checks as shield
import feature018_shield_shop_checks as shop


class ShieldShopPreSendRaces(unittest.TestCase):
    def test_status_tab_duration_changes_during_confirmation_no_purchase(self):
        lua, state, call = shop.fixture()
        lua.execute('''
          local previous=LocalController.instance
          LocalController.instance=function(...)
            local obj=previous(...)
            local prior_get=obj.getLine
            obj.getLine=function(self,...)
              state.statusReads=(state.statusReads or 0)+1
              -- Initial scan resolves all three template statuses; flip only
              -- at the pre-send second visit to the 8h StatusTab row.
              if state.statusReads==4 then state.seconds[501]=43200 end
              return prior_get(self,...)
            end
            return obj
          end
        ''')
        self.assertEqual(call('buy',gemOptIn='1')['state'], 'failed')
        self.assertEqual(state['sent'], 0)
        self.assertEqual(state['statusReads'], 4)

    def test_cached_shield_status_ids_do_not_override_reclassified_status_tab(self):
        lua, state, call = shop.fixture()
        lua.execute('''
          local previous=LocalController.instance
          LocalController.instance=function(...)
            local obj=previous(...)
            local prior_get=obj.getLine
            obj.getLine=function(self,...)
              state.statusReads=(state.statusReads or 0)+1
              if state.statusReads==4 then state.statusEffects[501]=123 end
              return prior_get(self,...)
            end
            return obj
          end
        ''')
        self.assertEqual(call('buy',gemOptIn='1')['state'], 'failed')
        self.assertEqual(state['statusReads'], 4)
        self.assertEqual(state['sent'], 0)

    def test_main_shield_classification_changes_during_confirmation_no_purchase(self):
        lua, state, call = shop.fixture()
        lua.execute('''
          local orig=DataCenter.StatusManager.GetShieldStatusIds
          local reads=0
          DataCenter.StatusManager.GetShieldStatusIds=function(...)
            reads=reads+1
            if reads>=2 then return {[501]=false,[502]=true,[503]=true} end
            return orig(...)
          end
        ''')
        self.assertEqual(call('buy',gemOptIn='1')['state'],'failed')
        self.assertEqual(state['sent'],0)

    def test_game_player_or_manager_changes_after_inspection_but_before_shop_send(self):
        for change in (
            "LuaEntry.Player={uid='2553945035002288',gold=1000, GetSourceServerId=function()return 2288 end,GetSelfServerId=function()return 2288 end,GetCurServerId=function()return 2288 end}",
            "DataCenter=setmetatable({}, {__index=DataCenter})",
        ):
            with self.subTest(change=change):
                lua, state, call = shop.fixture()
                lua.execute('''
                  local previous=DataCenter.ItemData.GetItemList
                  local reads=0
                  DataCenter.ItemData.GetItemList=function(self,...)
                    reads=reads+1
                    if reads==2 then SWITCH() end
                    return previous(self,...)
                  end
                '''.replace('SWITCH()',change))
                self.assertEqual(call('buy',gemOptIn='1')['state'], 'failed')
                self.assertEqual(state['sent'], 0)

    def test_changed_owner_during_purchase_result_does_not_retire_pending(self):
        lua, state, call = shop.fixture()
        self.assertEqual(call('buy',gemOptIn='1')['state'], 'accepted')
        lua.execute('''
          state.items={{itemId=9,uuid='owned-stack',count=1}}
          LuaEntry.Player.gold=500
          local prev=DataCenter.ItemData.GetItemList
          local reads=0
          DataCenter.ItemData.GetItemList=function(...)
            reads=reads+1
            if reads==1 then LuaEntry.Player.uid='3133546747002212' end
            return prev(...)
          end
        ''')
        self.assertEqual(call('status')['state'], 'failed')
        self.assertEqual(state['sent'],1)
        lua.execute("LuaEntry.Player.uid='2553945035002288'")
        self.assertEqual(call('status')['state'], 'confirmed')
        self.assertEqual(state['sent'], 1)

    def test_same_uid_new_player_during_purchase_status_keeps_no_resend_fence(self):
        lua, state, call = shop.fixture()
        self.assertEqual(call('buy',gemOptIn='1')['state'],'accepted')
        lua.execute('''
          state.items={{itemId=9,uuid='native-stack',count=1}}
          LuaEntry.Player.gold=500
          state.originalPlayer=LuaEntry.Player
          state.originalGetList=DataCenter.ItemData.GetItemList
          DataCenter.ItemData.GetItemList=function(...)
            local items=state.originalGetList(...)
            LuaEntry.Player={uid='2553945035002288',gold=500,
              GetSourceServerId=function()return 2288 end,
              GetSelfServerId=function()return 2288 end,
              GetCurServerId=function()return 2288 end}
            return items
          end
        ''')
        self.assertEqual(call('status')['state'],'failed')
        lua.execute('LuaEntry.Player=state.originalPlayer; DataCenter.ItemData.GetItemList=state.originalGetList')
        self.assertEqual(call('status')['state'],'confirmed')
        self.assertEqual(state['sent'],1)


class ShieldUseAfterCooldownRaces(unittest.TestCase):
    def test_replacement_arming_same_uid_player_change_is_not_consent(self):
        lua, module, state, arm, _ = shop.replacement_fixture()
        lua.execute('''
          local original=DataCenter.StatusManager.GetBuffTimeInfo
          DataCenter.StatusManager.GetBuffTimeInfo=function(self,...)
            local result=original(self,...)
            local player=LuaEntry.Player
            LuaEntry.Player={uid=player.uid,gold=player.gold,
              GetSourceServerId=player.GetSourceServerId,
              GetSelfServerId=player.GetSelfServerId,
              GetCurServerId=player.GetCurServerId}
            return result
          end
        ''')
        self.assertEqual(arm(shieldReplaceOptIn='1')['state'],'failed')
        self.assertEqual(state['sent'],0)

    def test_original_item_uuid_count_template_duration_or_status_advance_blocks_use(self):
        mutations = (
            "state.items[1].count=1",
            "state.items[1].uuid='new-stack'",
            "state.templates[1].para1=502",
            "state.seconds[501]=43200",
            "state.statusEffects[501]=9",
            "state.effectEnd[501]=state.serverTime+8*3600000",
            "state.serverTime=1",
        )
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                lua, module, state = shield.run()
                lua.execute('''
                  local original=DataCenter.StatusManager.SetUseShieldCD
                  DataCenter.StatusManager.SetUseShieldCD=function(self)
                    original(self)
                    MUTATE()
                  end
                '''.replace('MUTATE()',mutation))
                self.assertEqual(shield.command(lua,module,8)['state'], 'failed')
                self.assertEqual(state['cooldownSets'], 1)
                self.assertEqual(state['sent'], 0)

    def test_false_native_cooldown_result_blocks_item_use(self):
        lua, module, state = shield.run()
        lua.execute('''
          DataCenter.StatusManager.SetUseShieldCD=function(self)
            state.cooldownSets=state.cooldownSets+1
            return false
          end
        ''')
        self.assertEqual(shield.command(lua,module,8)['state'],'failed')
        self.assertEqual(state['sent'],0)

    def test_same_uid_new_native_player_after_cooldown_blocks_item_use(self):
        lua, module, state = shield.run()
        lua.execute('''
          local old=LuaEntry.Player
          DataCenter.StatusManager.SetUseShieldCD=function(self)
            state.cooldownSets=state.cooldownSets+1
            LuaEntry.Player={uid=old.uid,
              GetSourceServerId=old.GetSourceServerId,
              GetSelfServerId=old.GetSelfServerId,
              GetCurServerId=old.GetCurServerId}
          end
        ''')
        self.assertEqual(shield.command(lua,module,8)['state'], 'failed')
        self.assertEqual(state['sent'],0)

    def test_item_use_status_owner_swap_does_not_consume_pending(self):
        lua, module, state = shield.run()
        self.assertEqual(shield.command(lua,module,8)['state'], 'accepted')
        lua.execute('''
          state.items[1].count=1
          state.effectEnd[501]=state.serverTime+8*3600000
          state.previousGetItemList=DataCenter.ItemData.GetItemList
          DataCenter.ItemData.GetItemList=function(...)
            LuaEntry.Player.uid='3133546747002212'
            return state.previousGetItemList(...)
          end
        ''')
        self.assertEqual(shield.command(lua,module,8,query=True)['state'], 'failed')
        self.assertEqual(state['sent'],1)
        lua.execute("LuaEntry.Player.uid='2553945035002288'; DataCenter.ItemData.GetItemList=state.previousGetItemList")
        self.assertEqual(shield.command(lua,module,8,query=True)['state'],'confirmed')
        self.assertEqual(state['sent'],1)

    def test_item_use_query_same_uid_new_player_does_not_retire_pending(self):
        lua, module, state = shield.run()
        self.assertEqual(shield.command(lua,module,8)['state'],'accepted')
        lua.execute('''
          state.items[1].count=1
          state.effectEnd[501]=state.serverTime+8*3600000
          state.originalPlayer=LuaEntry.Player
          state.originalGetList=DataCenter.ItemData.GetItemList
          DataCenter.ItemData.GetItemList=function(...)
            local items=state.originalGetList(...)
            LuaEntry.Player={uid='2553945035002288',
              GetSourceServerId=function()return 2288 end,
              GetSelfServerId=function()return 2288 end,
              GetCurServerId=function()return 2288 end}
            return items
          end
        ''')
        self.assertEqual(shield.command(lua,module,8,query=True)['state'],'failed')
        lua.execute('LuaEntry.Player=state.originalPlayer; DataCenter.ItemData.GetItemList=state.originalGetList')
        self.assertEqual(shield.command(lua,module,8,query=True)['state'],'confirmed')
        self.assertEqual(state['sent'],1)

    def test_explicit_native_paid_rapid_callback_not_available_from_shield_module(self):
        lua, module, state = shield.run()
        attempted=module.Handle(lua.table_from({
            'action':'speedupDiamond','slot':'1','gemOptIn':'1',
            'profileId':'profile','launchSessionId':'launch','challenge':'nonce','gamePid':'1',
        }))
        self.assertEqual(attempted['state'], 'failed')
        self.assertEqual(state['sent'],0)


if __name__ == '__main__':
    unittest.main()
