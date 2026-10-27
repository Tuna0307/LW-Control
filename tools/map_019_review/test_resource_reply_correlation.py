"""Actual Lua Resource request/reply observer; inert adverse native envelopes."""
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime

SOURCE = (ROOT / 'tools/current_live_resource_probe.lua').read_text(encoding='utf-8')
BODY = 'function resource_scan_detail_runtime.same_state' + SOURCE.split(
    'function resource_scan_detail_runtime.same_state', 1)[1].split(
    'local function write_resource_detail_diagnostic_result', 1)[0]
MODEL = r'''
resource_scan_detail_runtime={}
M={VERSION='inert-actual-source'}
local root='inert-only'
local function safe_get(o,k)return o and o[k]end
local function reflected_value(o,k)return safe_get(o,k)end
local function call(o,k,...)if not o or type(o[k])~='function'then return false end;return pcall(o[k],o,...)end
local function current_server_id()return liveServer end
local identity={profileId='p',sessionId='s',challenge='c',gamePid=123}
local function active_overview_identity()return identity end
local cache={}
local function resource_detail_manager()return cache end
local function resource_detail_snapshot(manager,p)
 local d=manager[p];if not d then return nil end
 return {resourceId=1,remainRes=d.remainRes,initRes=d.initRes}
end
local function runtime_clock()return testClock or 1 end
local function read_kv_file(path)if path==resource_scan_detail_runtime.cancelPath then return cancelInput end;return nil end
local function valid_token()return true end
local function write_json(path,value)lastReceipt=value end
liveServer=2212
local future={_futureId=100}
local network={_config={},m_proxy={},IsConnected=true,getFutureManager=function()return future end}
CS={GameEntry={Network=network}}
local class={HandleMessage=function(self,response)
 cache[response.pointId]={remainRes=response.remainRes,initRes=response.initRes}
 return 'native-return',17
end}
local original=class.HandleMessage
package.preload['Net.Msgs.WorldGetDetailMessage']=function()return class end
MsgDefines={WorldGetDetail=7}
SFSNetwork={SendMessage=function(route,point,server,world,kind,uid,...)
 assert(route==7 and point==42 and server==liveServer and world==0 and kind==7 and uid=='0')
 assert(select('#',...)==0,'OnCreate must receive exactly five arguments')
 future._futureId=future._futureId+1
end}
request={requestId='request1',profileId='p',launchSessionId='s',challenge='c',gamePid=123,serverId=2212,scanRunId='run',includeResourceDetails=true}
local function queue()
 return resource_scan_detail_runtime.queue(request,{pointId=42,serverId=liveServer,worldId=0,pointType=7,uid='0'})
end
local function reply(id,point,amount,server)
 local a,b=class.HandleMessage({}, {_id=id,pointId=point,remainRes=amount,initRes=200,scrServer=server})
 assert(a=='native-return'and b==17,'Original handler result changed')
end
'''


class ResourceReplyCorrelation(unittest.TestCase):
    def check(self, code):
        self.assertTrue(LuaRuntime().execute(MODEL + BODY + code + '\nreturn true'))

    def test_registered_successor_preserves_reply_and_unknown_wrapper_refused(self):
        vm = LuaRuntime()
        vm.globals().api = vm.execute((ROOT / 'tools/feature018_attack_actions.lua').read_text('utf-8'))
        vm.execute("package.loaded['DataCenter.Global.LWBridgeFeature018AttackActions']=api")
        self.assertTrue(vm.execute(MODEL + BODY + r'''
assert(queue());local state=resource_scan_detail_runtime.state
local previous=class.HandleMessage
class.HandleMessage=function(...)return previous(...)end
assert(api.RegisterObserverLink(class,'HandleMessage',class.HandleMessage,previous))
assert(resource_scan_detail_runtime.install());reply(101,42,75,2212)
assert(select(2,resource_scan_detail_runtime.rows(state))==1)
previous=class.HandleMessage;class.HandleMessage=function(...)return previous(...)end
assert(not resource_scan_detail_runtime.install())
assert(select(2,resource_scan_detail_runtime.rows(state))==0)
return true
'''))

    def test_cached_foreign_detail_is_ignored_and_fresh_reply_frozen(self):
        self.check(r'''
cache[42]={remainRes=99,initRes=200}
assert(queue());local state=resource_scan_detail_runtime.state
assert(state.requestCount==1 and state.cacheBeforeCount==0 and state.ignoredCacheCount==1)
assert(select(2,resource_scan_detail_runtime.rows(state))==0)
reply(100,42,88,2213) -- delayed foreign packet updates native cache only
assert(select(2,resource_scan_detail_runtime.rows(state))==0)
reply(101,42,75,2212)
local rows,ready=resource_scan_detail_runtime.rows(state)
assert(ready==1 and rows[1].detail.remainRes==75 and rows[1].replyId==101)
reply(100,42,11,2213)
rows=resource_scan_detail_runtime.rows(state);assert(rows[1].detail.remainRes==75)
resource_scan_detail_runtime.reset();assert(class.HandleMessage==original)
''')

    def test_wrong_point_wrong_server_and_missing_id_fail_closed(self):
        self.check(r'''
assert(queue());local state=resource_scan_detail_runtime.state
reply(101,43,70,2212);reply(101,42,70,2213);reply(nil,42,70,2212)
assert(select(2,resource_scan_detail_runtime.rows(state))==0)
reply(101,42,70,nil) -- _id supplies request provenance when server echo absent
assert(select(2,resource_scan_detail_runtime.rows(state))==1)
''')

    def test_connection_reset_with_reused_id_is_rejected(self):
        self.check(r'''
assert(queue());local state=resource_scan_detail_runtime.state
network._config={};future._futureId=101
reply(101,42,70,2212)
assert(select(2,resource_scan_detail_runtime.rows(state))==0)
''')

    def test_repair_retires_old_request_id(self):
        self.check(r'''
assert(queue());local state=resource_scan_detail_runtime.state
assert(resource_scan_detail_runtime.send(state.targets[1],state))
assert(state.targets[1].expectedReplyId==102 and state.byReplyId[101]==nil)
reply(101,42,70,2212);assert(select(2,resource_scan_detail_runtime.rows(state))==0)
reply(102,42,60,2212);assert(select(2,resource_scan_detail_runtime.rows(state))==1)
''')

    def test_missing_reply_repairs_are_bounded_and_restore_handler(self):
        self.check(r'''
assert(queue());resource_scan_detail_runtime.request=request
resource_scan_detail_runtime.startedAt=0
testClock=9;resource_scan_detail_runtime.pump(9)
assert(request.repairPass==1 and resource_scan_detail_runtime.state.retryRequestCount==1)
testClock=18;resource_scan_detail_runtime.pump(18)
assert(request.repairPass==2 and resource_scan_detail_runtime.state.retryRequestCount==2)
testClock=27;resource_scan_detail_runtime.pump(27)
assert(resource_scan_detail_runtime.state==nil and class.HandleMessage==original)
assert(lastReceipt.error=='resource_scan_detail_partial_response'and lastReceipt.readyCount==0)
''')

    def test_stop_before_due_repair_acknowledges_after_retirement(self):
        self.check(r'''
assert(queue());resource_scan_detail_runtime.request=request
resource_scan_detail_runtime.startedAt=0;testClock=9
cancelInput={};for k,v in pairs(request)do cancelInput[k]=v end
resource_scan_detail_runtime.pump(9)
assert(future._futureId==101,'Stop must precede the due retry')
assert(resource_scan_detail_runtime.state==nil and class.HandleMessage==original)
assert(lastReceipt.state=='cancelled'and lastReceipt.nativeLaneStopped==true)
''')

    def test_foreign_stop_cannot_cancel_current_lane(self):
        self.check(r'''
assert(queue());resource_scan_detail_runtime.request=request
resource_scan_detail_runtime.startedAt=0;testClock=9
cancelInput={};for k,v in pairs(request)do cancelInput[k]=v end
cancelInput.scanRunId='foreign-run'
resource_scan_detail_runtime.pump(9)
assert(resource_scan_detail_runtime.state~=nil and future._futureId==102)
''')

    def test_stop_arriving_before_first_hydration_pump(self):
        self.check(r'''
assert(queue());cancelInput={};for k,v in pairs(request)do cancelInput[k]=v end
resource_scan_detail_runtime.read=function()return request end
testClock=1;resource_scan_detail_runtime.pump(1)
testClock=2;resource_scan_detail_runtime.pump(2)
assert(future._futureId==101 and resource_scan_detail_runtime.state==nil)
assert(lastReceipt.state=='cancelled'and lastReceipt.nativeLaneStopped==true)
assert(class.HandleMessage==original)
''')

    def test_completion_cancel_race_acknowledges_retired_lane(self):
        self.check(r'''
assert(queue());resource_scan_detail_runtime.request=request
reply(101,42,70,2212);resource_scan_detail_runtime.pump(1)
assert(resource_scan_detail_runtime.state==nil)
cancelInput={};for k,v in pairs(request)do cancelInput[k]=v end
resource_scan_detail_runtime.pump(2)
assert(lastReceipt.state=='cancelled'and lastReceipt.nativeLaneStopped==true)
assert(future._futureId==101)
''')

    def test_retired_run_response_cannot_complete_successor(self):
        self.check(r'''
assert(queue());request.scanRunId='successor';assert(queue())
local state=resource_scan_detail_runtime.state
reply(101,42,70,2212);assert(select(2,resource_scan_detail_runtime.rows(state))==0)
reply(102,42,60,2212);assert(select(2,resource_scan_detail_runtime.rows(state))==1)
liveServer=2213;assert(select(2,resource_scan_detail_runtime.rows(state))==0)
''')

    def test_hook_replacement_and_owner_change_invalidate_readiness(self):
        self.check(r'''
assert(queue());local state=resource_scan_detail_runtime.state
identity.sessionId='foreign';reply(101,42,70,2212)
assert(select(2,resource_scan_detail_runtime.rows(state))==0)
class.HandleMessage=function()end
assert(select(2,resource_scan_detail_runtime.rows(state))==0)
local replacement=class.HandleMessage;resource_scan_detail_runtime.reset()
assert(class.HandleMessage==replacement,'Must not overwrite another observer')
''')


if __name__ == '__main__':
    unittest.main()
