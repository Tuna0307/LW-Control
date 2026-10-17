"""R5 isolated Lua 5.3 read-only source-shaped DailyTask reward observation.

All player/manager data are synthetic. This checks no reward claim or gold
credit is invented and no network or payment path is reached.
"""

import unittest
import feature018_shield_shop_checks as shop


def fixture():
    lua, state, inspect = shop.fixture()
    lua.execute('''
      TaskState={CanReceive=2,Received=3,NoComplete=1}
      ResourceType={Gold=77}
      state.daily={
        {id=4101,state=2,reward={{rewardType=77,itemId='',count=600}}},
        {id=4102,state=2,reward={{rewardType=78,itemId=55,count=300}}},
        {id=4103,state=1,reward={{rewardType=77,itemId='',count=5000}}},
        {id=4104,state=3,reward={{rewardType=77,itemId='',count=5000}}},
      }
      DataCenter.DailyTaskManager={
        dailyQuestTasks=state.daily,
        GetAllDailyTask=function(self)
          local result={}
          for _,task in ipairs(state.daily) do
            if task.state~=TaskState.Received then
              result[#result+1]=task
            end
          end
          return result
        end,
      }
      state.signInEvents={
        [401]={
          IsEnd=function()return false end,
          GetCanClaimRewardDay=function()return 3 end,
          CanClaimAtDay=function(self,day)assert(day==3);return true end},
        [402]={
          IsEnd=function()return true end,
          GetCanClaimRewardDay=function()return 5 end,
          CanClaimAtDay=function()return true end},
        [403]={
          IsEnd=function()return false end,
          GetCanClaimRewardDay=function()return 0 end,
          CanClaimAtDay=function()return false end},
      }
      DataCenter.LWActSignInManager={infos=state.signInEvents}
    ''')
    return lua, state, inspect


class GoldClaimReadOnlyChecks(unittest.TestCase):
    def test_only_visible_unreceived_native_canreceive_is_claimable(self):
        lua, state, inspect = fixture()
        response = inspect()
        self.assertEqual(response['state'], 'confirmed')
        result = response['data']['nativeGoldClaimRoutes']
        self.assertEqual(result['availability'], 'native_observed')
        self.assertEqual(result['visibleTaskCount'], 3)
        self.assertEqual(result['claimableDailyTaskCount'], 2)
        self.assertEqual(result['goldTypeMatchedDailyTaskCount'], 1)
        self.assertEqual(result['claimableTasksTruncated'], False)
        tasks = list(result['claimableDailyTasks'].values())
        self.assertEqual([t['taskId'] for t in tasks], ['4101', '4102'])
        self.assertEqual([t['goldRewardTypeMatched'] for t in tasks], [True, False])
        self.assertEqual([t['sourceState'] for t in tasks], ['TaskState.CanReceive'] * 2)
        self.assertEqual(result['route'], 'DailyTaskReward')
        self.assertEqual(result['signInRoute'], 'ClaimActSignInReward')
        self.assertEqual(result['allianceGiftRoute'], 'AllianceGiftGetReward')
        self.assertEqual(result['signInClaimabilityVerified'], False)
        sign_in = result['signIn']
        self.assertEqual(sign_in['availability'], 'native_cached')
        self.assertEqual(sign_in['loadedEventCount'], 3)
        self.assertEqual(sign_in['claimableEventCount'], 1)
        self.assertEqual(sign_in['goldRewardVerified'], False)
        self.assertEqual(sign_in['requestDispatched'], False)
        events = list(sign_in['claimableEvents'].values())
        self.assertEqual(len(events), 1)
        self.assertEqual(events[0]['eventId'], '401')
        self.assertEqual(events[0]['originalClaimAnyDay'], 0)
        self.assertEqual(events[0]['nativeAvailableRewardCount'], 3)
        self.assertEqual(result['allianceGiftClaimabilityVerified'], False)
        self.assertEqual(result['goldAmountVerified'], False)
        self.assertEqual(result['goldEarned'], False)
        self.assertEqual(result['requestDispatched'], False)
        self.assertEqual(state['sent'], 0)

    def test_no_preview_gold_count_is_offered_as_per_task_gain(self):
        lua, state, inspect = fixture()
        lua.execute('''
          -- RewardManager.ReturnRewardParamForView uses Player.sm_addGoldCount
          -- for Gold display; it is unrelated to this task's payout.
          LuaEntry.Player.sm_addGoldCount=9999999
          state.daily[1].reward[1].count=9999999
        ''')
        result = inspect()['data']['nativeGoldClaimRoutes']
        self.assertEqual(result['goldTypeMatchedDailyTaskCount'], 1)
        self.assertEqual(result['goldAmountVerified'], False)
        self.assertIsNone(result['goldAmount'])
        self.assertIsNone(list(result['claimableDailyTasks'].values())[0]['count'])
        self.assertEqual(state['sent'], 0)

    def test_invalid_current_task_id_never_becomes_usable_claim_route(self):
        lua, state, inspect = fixture()
        lua.execute("state.daily[1].id='not-a-task-id'")
        result = inspect()['data']['nativeGoldClaimRoutes']
        self.assertEqual(result['claimableDailyTaskCount'], 1)
        self.assertEqual(result['unidentifiedClaimableTaskCount'], 1)
        self.assertEqual(result['goldTypeMatchedDailyTaskCount'], 0)
        self.assertEqual([task['taskId'] for task in result['claimableDailyTasks'].values()], ['4102'])
        self.assertEqual(state['sent'], 0)

    def test_missing_or_unknown_currency_never_classified_as_gold(self):
        for change in (
            'ResourceType.Gold=nil',
            'state.daily[1].reward={{rewardType=25}}',
            'state.daily[1].reward=nil',
            'state.daily[1].reward={{rewardType=77},{rewardType=nil}}',
            'TaskState.CanReceive=nil',
        ):
            with self.subTest(change=change):
                lua, state, inspect = fixture()
                lua.execute(change)
                result = inspect()['data']['nativeGoldClaimRoutes']
                self.assertEqual(result['goldTypeMatchedDailyTaskCount'] if
                                 result['availability'] == 'native_observed' else 0, 0)
                self.assertEqual(result['goldEarned'], False)
                self.assertEqual(state['sent'], 0)

    def test_no_cached_native_tasks_does_not_claim_zero_gold_available(self):
        for change in ('DataCenter.DailyTaskManager=nil',
                       'DataCenter.DailyTaskManager.dailyQuestTasks=nil',
                       'DataCenter.DailyTaskManager.GetAllDailyTask=nil'):
            with self.subTest(change=change):
                lua, state, inspect = fixture()
                lua.execute(change)
                result = inspect()['data']['nativeGoldClaimRoutes']
                self.assertEqual(result['availability'], 'source_unavailable')
                self.assertIsNone(result['claimableDailyTaskCount'])
                self.assertEqual(result['signIn']['claimableEventCount'], 1)
                self.assertEqual(state['sent'], 0)

    def test_sign_in_missing_unknown_and_bounded_native_events(self):
        lua, state, inspect = fixture()
        lua.execute('DataCenter.LWActSignInManager=nil')
        result = inspect()['data']['nativeGoldClaimRoutes']['signIn']
        self.assertEqual(result['availability'], 'source_unavailable')
        self.assertIsNone(result['claimableEventCount'])

        lua, state, inspect = fixture()
        lua.execute('''
          state.signInEvents[401].GetCanClaimRewardDay=function()return 'wrong' end
          state.signInEvents[402].IsEnd=function()return nil end
          state.signInEvents[403].CanClaimAtDay=function()error('broken')end
        ''')
        result = inspect()['data']['nativeGoldClaimRoutes']['signIn']
        self.assertEqual(result['claimableEventCount'], 0)
        self.assertEqual(state['sent'], 0)

        lua, state, inspect = fixture()
        lua.execute('''
          state.signInEvents={}
          for i=1,65 do
            state.signInEvents[i]={IsEnd=function()return false end,
              GetCanClaimRewardDay=function()return 1 end,
              CanClaimAtDay=function()return true end}
          end
          DataCenter.LWActSignInManager.infos=state.signInEvents
        ''')
        result = inspect()['data']['nativeGoldClaimRoutes']['signIn']
        self.assertEqual(result['availability'], 'too_many_unverified_sign_in_events')
        self.assertIsNone(result['claimableEventCount'])
        self.assertEqual(state['sent'], 0)

    def test_native_account_must_remain_uid2288_all_three_servers(self):
        for change in (
            "LuaEntry.Player.uid='3133546747002212'",
            'LuaEntry.Player.GetSourceServerId=function()return 2212 end',
            'LuaEntry.Player.GetSelfServerId=function()return 2212 end',
            'LuaEntry.Player.GetCurServerId=function()return 2212 end',
        ):
            with self.subTest(change=change):
                lua, state, inspect = fixture()
                lua.execute(change)
                self.assertEqual(inspect()['state'], 'failed')
                self.assertEqual(state['sent'], 0)

        lua, state, inspect = fixture()
        lua.execute('''
          local prior=DataCenter.DailyTaskManager.GetAllDailyTask
          DataCenter.DailyTaskManager.GetAllDailyTask=function(self)
            local result=prior(self)
            LuaEntry.Player.uid='3133546747002212'
            return result
          end
        ''')
        self.assertEqual(inspect()['state'], 'failed')
        self.assertEqual(state['sent'], 0)

    def test_same_uid_session_or_native_reward_cache_swap_fails_closed(self):
        # The user-facing session tokens are identical, and each mock still
        # presents UID2288: only the native Player/manager/cache identity fence
        # distinguishes an in-flight native session/claim transition.
        mutations = (
            "LuaEntry.Player={uid='2553945035002288',gold=1000,GetSourceServerId=function()return 2288 end,GetSelfServerId=function()return 2288 end,GetCurServerId=function()return 2288 end}",
            'DataCenter.DailyTaskManager.dailyQuestTasks={}',
            'DataCenter.DailyTaskManager={dailyQuestTasks={},GetAllDailyTask=function()return {} end}',
        )
        for change in mutations:
            with self.subTest(change=change):
                lua, state, inspect = fixture()
                lua.execute('''
                  local orig=DataCenter.DailyTaskManager.GetAllDailyTask
                  DataCenter.DailyTaskManager.GetAllDailyTask=function(self)
                    local tasks=orig(self)
                    MUTATE()
                    return tasks
                  end
                '''.replace('MUTATE()',change))
                self.assertEqual(inspect()['state'], 'failed')
                self.assertEqual(state['sent'], 0)

        lua, state, inspect = fixture()
        lua.execute('''
          local previous=state.signInEvents[401].GetCanClaimRewardDay
          state.signInEvents[401].GetCanClaimRewardDay=function(self)
            local result=previous(self)
            DataCenter.LWActSignInManager.infos={}
            return result
          end
        ''')
        self.assertEqual(inspect()['state'], 'failed')
        self.assertEqual(state['sent'], 0)

    def test_bounded_claims_no_autosend_or_fake_all_clear(self):
        lua, state, inspect = fixture()
        lua.execute('''
          state.daily={}
          for i=1,60 do
            state.daily[i]={id=8000+i,state=2,reward={{rewardType=77,count=1000}}}
          end
        ''')
        result = inspect()['data']['nativeGoldClaimRoutes']
        self.assertEqual(result['visibleTaskCount'], 60)
        self.assertEqual(result['claimableDailyTaskCount'], 60)
        self.assertEqual(result['goldTypeMatchedDailyTaskCount'], 60)
        self.assertEqual(result['claimableTasksTruncated'], True)
        self.assertEqual(len(list(result['claimableDailyTasks'].values())), 16)
        self.assertEqual(state['sent'], 0)

        lua.execute('''
          state.daily={}
          for i=1,257 do state.daily[i]={id=i,state=2,reward={}} end
        ''')
        result = inspect()['data']['nativeGoldClaimRoutes']
        self.assertEqual(result['availability'], 'too_many_unverified_tasks')
        self.assertIsNone(result['claimableDailyTaskCount'])
        self.assertEqual(state['sent'], 0)


if __name__ == '__main__':
    unittest.main()
