"""Inert original-path Land Cell request/reply correlation and event checks."""
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parent / 'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime

FIXTURE = r'''
LandLockState={Locked=1, Unlocked=2, Finished=3}
LandLockCondition={Unknown=0}
local row={state=1, GetCurPve=function() return 0 end}
Row=row
DataCenter={LandLockManager={Enabled=function() return true end,
  GetCurrentToBeUnlockedId=function() return 6 end,
  GetLandLockDataById=function(self,id) if id==6 then return row end end,
  CheckLandLockConditionById=function() return 0 end},
  LWSheepDataManager={GetActivityData=function() return nil end,
    IsVail=function() return false end}}
MsgDefines={UnlockUserLand='unlock'}
local cls={OnCreate=function(self,id) self.sentId=id end,
  HandleMessage=function(self,p)
    if p.errorCode==nil and p.landId==6 then row.state=p.state end
  end}
NativeClass=cls
OriginalCreate=cls.OnCreate
OriginalReply=cls.HandleMessage
package.loaded['Net.Msgs.UnlockUserLandMessage']=cls
SendCount=0
SFSNetwork={SendMessage=function(command,id)
  SendCount=SendCount+1
  cls.OnCreate({},id)
end}
function Reply(id,state,errorCode)
  -- Exactly like v26 SFSNetwork.HandleMessage: NewEmpty, then HandleMessage.
  cls.HandleMessage({}, {landId=id,state=state,errorCode=errorCode})
end
'''


def fixture():
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute(FIXTURE)
    mod = lua.execute((ROOT / 'feature018_mini_game.lua').read_text(encoding='utf-8'))
    return lua, mod


class LandPacketChecks(unittest.TestCase):
    def start(self):
        lua, mod = fixture()
        before = mod.LandStatus(lua.table_from({'expectedCellId': '6'}))
        self.assertEqual(before['data']['state'], 'locked')
        sent = mod.LandUnlockOne(lua.table_from({'expectedCellId': '6', 'requestId': 'fresh1'}))
        self.assertEqual(sent['state'], 'accepted')
        self.assertEqual(sent['data']['requestProof']['targetRequestCount'], 1)
        self.assertTrue(sent['data']['requestProof']['ownRequestObserved'])
        return lua, mod

    def proof(self, lua, mod):
        return mod.LandStatus(lua.table_from({'expectedCellId': '6'}))['data']['requestProof']

    def test_one_request_matching_reply_and_authoritative_poststate(self):
        lua, mod = self.start()
        self.assertFalse(self.proof(lua, mod)['requestCausalityConfirmed'])
        lua.globals().Reply(6, 2, None)
        proof = self.proof(lua, mod)
        self.assertEqual(proof['requestId'], 'fresh1')
        self.assertTrue(proof['requestCausalityConfirmed'])
        self.assertEqual(proof['replyLandId'], 6)
        self.assertTrue(proof['replyAfterUnlocked'])
        self.assertEqual(lua.globals().SendCount, 1)

    def test_unrelated_reply_and_external_poststate_never_prove_causality(self):
        lua, mod = self.start()
        lua.globals().Reply(7, 2, None)
        lua.execute('Row.state=2')  # push/manual state change, without our reply
        proof = self.proof(lua, mod)
        self.assertFalse(proof['replyObserved'])
        self.assertFalse(proof['requestCausalityConfirmed'])

    def test_duplicate_native_target_request_invalidates_correlation(self):
        lua, mod = self.start()
        lua.execute("SFSNetwork.SendMessage('unlock',6)")
        lua.globals().Reply(6, 2, None)
        proof = self.proof(lua, mod)
        self.assertEqual(proof['targetRequestCount'], 2)
        self.assertFalse(proof['requestCausalityConfirmed'])

    def test_error_reply_or_locked_reply_cannot_confirm(self):
        for state, error in [(2, 12), (1, None)]:
            lua, mod = self.start()
            lua.globals().Reply(6, state, error)
            self.assertFalse(self.proof(lua, mod)['requestCausalityConfirmed'])
            self.assertEqual(lua.globals().Row['state'], 1)

    def test_no_serializer_and_handler_means_no_send(self):
        lua, mod = fixture()
        lua.execute("package.loaded['Net.Msgs.UnlockUserLandMessage']=nil")
        result = mod.LandUnlockOne(lua.table())
        self.assertEqual(result['state'], 'unsupported')
        self.assertEqual(lua.globals().SendCount, 0)

    def test_throw_after_send_retains_duplicate_fence(self):
        lua, mod = fixture()
        lua.execute('local old=SFSNetwork.SendMessage; SFSNetwork.SendMessage=function(...) old(...); error("late") end')
        self.assertEqual(mod.LandUnlockOne(lua.table())['state'], 'failed')
        self.assertEqual(mod.LandUnlockOne(lua.table())['state'], 'failed')
        self.assertEqual(lua.globals().SendCount, 1)

    def test_cleanup_restores_only_own_methods(self):
        lua, mod = self.start()
        mod.CleanupAfterLeaseLoss()
        self.assertTrue(lua.eval('NativeClass.OnCreate == OriginalCreate'))
        self.assertTrue(lua.eval('NativeClass.HandleMessage == OriginalReply'))
        lua, mod = self.start()
        lua.execute('Foreign=function() end; NativeClass.HandleMessage=Foreign')
        mod.CleanupAfterLeaseLoss()
        self.assertTrue(lua.eval('NativeClass.HandleMessage==Foreign'))

    def test_changed_observer_cannot_claim_exclusive_request_correlation(self):
        lua, mod = self.start()
        lua.globals().Reply(6, 2, None)
        lua.execute('NativeClass.OnCreate=function() end')
        self.assertFalse(self.proof(lua, mod)['requestCausalityConfirmed'])
        self.assertFalse(self.proof(lua, mod)['observerIntact'])

    def test_cleanup_of_uncertain_send_does_not_allow_retry(self):
        lua, mod = self.start()
        mod.CleanupAfterLeaseLoss()
        self.assertEqual(mod.LandUnlockOne(lua.table())['state'], 'failed')
        self.assertEqual(lua.globals().SendCount, 1)

    def test_food_event_unavailable_is_native_activity_observation(self):
        lua, mod = fixture()
        status = mod.SheepStatus()
        self.assertEqual(status['data']['availability'], 'LIVE_EVENT_UNAVAILABLE')
        self.assertTrue(status['data']['activityObserved'])
        self.assertFalse(status['data']['activityPresent'])
        self.assertEqual(lua.globals().SendCount, 0)


if __name__ == '__main__':
    unittest.main()
