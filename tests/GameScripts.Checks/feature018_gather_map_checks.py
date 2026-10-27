"""Actual Map/Gather callbacks share a native class; no game or network access."""
from pathlib import Path
from feature018_gather_checks import gathering, ROOT


def check(source=None):
    text = source or (ROOT / 'src/LWBridge.GameScripts/current_live_resource_probe.lua').read_text('utf-8')
    body = 'function resource_scan_detail_runtime.reset()' + text.split(
        'function resource_scan_detail_runtime.reset()', 1)[1].split(
        'function resource_scan_detail_runtime.send(', 1)[0]
    for map_first in (False, True):
        vm = gathering()
        vm.execute('''
          resource_scan_detail_runtime={}
          local function safe_get(o,k)return o and o[k]end
        ''' + body + '''
          resource_scan_detail_runtime.observe=function()state.mapCallbacks=(state.mapCallbacks or 0)+1 end
          function mapInstall()assert(resource_scan_detail_runtime.install())end
        ''')
        if map_first:
            vm.execute('mapInstall()')
        vm.execute('hydrate();assert(dispatch(job));native_result();assert(verify(job))')
        if not map_first:
            vm.execute('mapInstall()')
        vm.execute('''
          assert(resource_scan_detail_runtime.install(),'Map accepts a known gathering successor')
          state.config.resourceGather.recallOnDisable=true
          local recall=gather.Stop('resourceGather').cleanupJobs[1]
          assert(recall and dispatch(recall),'Map must not block an exact owned gathering recall')
          state.marches[41].targetType=9;state.marches[41].clientCreateGuid=''
          assert(verify(recall));gather.FinishStop('resourceGather')
          -- A retired Map wrapper can remain underneath a newer observer.
          mapInstall();local class=classes.WorldGetDetailMessage
          local previous=class.HandleMessage
          local successor=function(...)return previous(...)end
          class.HandleMessage=successor
          assert(observer_api.RegisterObserverLink(class,'HandleMessage',successor,previous))
          resource_scan_detail_runtime.reset()
          assert(class.HandleMessage==successor,'Reset preserves the successor')
          local before=state.mapCallbacks or 0
          class.HandleMessage({},{});assert((state.mapCallbacks or 0)==before,'Retired Map observer is inert')
          mapInstall();class.HandleMessage({},{});assert(state.mapCallbacks==before+1,'One observation after rearm')
          -- An unknown wrapper remains refused on both sides, even if it calls through.
          previous=class.HandleMessage;class.HandleMessage=function(...)return previous(...)end
          assert(not resource_scan_detail_runtime.install(),'Unknown Map successor must fail closed')
          local replacement=class.HandleMessage;resource_scan_detail_runtime.reset()
          assert(class.HandleMessage==replacement)
        ''')


if __name__ == '__main__':
    check()
    print('GATHER_MAP_CALLBACK_PASS both-install-orders exact-recall successor-retirement unknown-refusal')
