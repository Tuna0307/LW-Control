"""Actual scheduler/adapter/Gather integration across authority loss; inert Lua."""
from feature018_gather_checks import gathering, ROOT


def scenario(engine_source=None, gather_source=None):
    vm = gathering(gather_source)
    vm.execute("package.loaded['DataCenter.Global.LWBridgeFeature018GatherNative']=gather;bridge.PerformanceEnd=function()end")
    if engine_source is not None:
        vm.globals().engine = vm.execute(engine_source)
        vm.execute('engine.Register(feature,bridge,native)')
    vm.execute('''
      state.config.resourceGather.recallOnDisable=true
      assert(engine.Configure(request).state=='confirmed')
      hydrate();assert(dispatch(job));native_result();assert(verify(job))
      state.marches[41].clientCreateGuid=''
      function loseLease()
        state.token=nil;state.now=state.now+2000;engine.Pump()
      end
      function restoreLease(token)
        state.token=token or 'owner';assert(engine.Configure(request).state=='confirmed')
      end
    ''')
    return vm


def check(engine_source=None, gather_source=None):
    vm = scenario(engine_source, gather_source)
    vm.execute('''
      local before=#state.sends;loseLease()
      engine.Pump();assert(#state.sends==before,'No sends during a lease gap')
      restoreLease();assert(engine.Stop(request).state=='confirmed')
      assert(#state.sends==before+1 and state.sends[#state.sends].route==83,
        'Same-session lease recovery must retain and recall its exact gathering trip')
      state.marches[41].targetType=9;state.now=state.now+2000;engine.Pump()
      local status=engine.Status().data.services.resourceGather
      assert(status.cleanupSubmitted==1 and status.cleanupConfirmed==1)
      engine.Stop(request);assert(#state.sends==before+1,'Repeated Off must not resend')
    ''')


def main():
    check()
    for changed in ('token', 'account', 'formation', 'march'):
        vm = scenario()
        vm.execute('loseLease()')
        vm.execute({
            'token': "restoreLease('successor')",
            'account': "LuaEntry.Player.uid='2002';request.authorizedUid='2002';restoreLease()",
            'formation': "state.forms[1].uuid=42;restoreLease()",
            'march': "state.marches[41].uuid=62;restoreLease()",
        }[changed])
        vm.execute('''
          local before=#state.sends;engine.Stop(request)
          assert(#state.sends==before,'Changed owner/march must not be adopted')
        ''')
    vm = scenario()
    vm.execute('''
      state.throw=true;engine.Stop(request);local before=#state.sends
      loseLease();restoreLease();engine.Stop(request)
      assert(#state.sends==before,'Uncertain recall must not replay after lease recovery')
    ''')
    vm = scenario()
    vm.execute('''
      -- A checkbox save can arrive after the last Plan and immediately before Off.
      state.config.resourceGather.recallOnDisable=false;plan()
      state.config.resourceGather.recallOnDisable=true
      local before=#state.sends;engine.Stop(request)
      assert(#state.sends==before+1,'Stop reads current trusted settings, not the last Plan')
    ''')
    vm = scenario()
    vm.execute('''
      -- Inversely, immediately unchecking recall must suppress cleanup too.
      local copy={};for k,v in pairs(state.config.resourceGather)do copy[k]=v end
      copy.recallOnDisable=false;state.config.resourceGather=copy
      local before=#state.sends;engine.Stop(request)
      assert(#state.sends==before,'Latest unchecked recall setting must be honored')
    ''')
    vm = scenario()
    vm.execute('''
      loseLease();restoreLease('successor');plan()
      local before=#state.sends;engine.Stop(request)
      local row=engine.Status().data.services.resourceGather
      assert(#state.sends==before and row.error:find('ownership record',1,true),
        'Untracked gathering must be explained instead of silently disabled')
    ''')
    vm = scenario()
    vm.execute('''
      state.throw=true;engine.Stop(request);loseLease();restoreLease()
      local row=engine.Status().data.services.resourceGather
      assert(row.error:find('will not be repeated',1,true),'Interrupted confirmation must remain visible')
    ''')
    print('GATHER_LEASE_PASS same-session-recall no-lease-sends changed-owner-refusal uncertain-no-replay latest-checkbox')


if __name__ == '__main__':
    main()
