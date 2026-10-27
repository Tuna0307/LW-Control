"""Bounded R30 simultaneous-engine measurement inside an already owned proof.

Uses real profile and game services. No OS input or synthetic game state. Stop
and restore all saved configs in finally; uncertain actions are never replayed.
"""
import copy
import json
import time
from feature018_live_queue import submit

KINDS=('allianceHelp','allianceGift','allianceDonate','officialPosition','treatment',
       'dispatch','dispatchAssist','resourceGather','strongholdResource',
       'allianceCenterResource','allianceGather','tradeStation')


def measure(root, output, uid, server):
    operations=[]
    def invoke(command,payload=None,profile=False):
        result=submit(root,{'kind':'profileCommand'if profile else'command',
            'command':command,'payload':payload or{}},timeout=60,expected_uid=uid,expected_server=server)
        operations.append(result)
        (output/'performance-operations.json').write_text(json.dumps(operations,indent=2))
        if result.get('state')!='returned'or result.get('error'):
            raise RuntimeError('One-shot performance operation failed; never replay: '+str(result))
        return result.get('result')
    def status():return invoke('feature018_automation_status')
    def save(kind,config):
        invoke('feature018_automation_config_save',{'kind':kind,'config':config},True)
        invoke('feature018_automation_configure',{'kind':kind})
    metrics=root/'overview-bridge/r29-perf.json'
    def sample(label):
        before=json.loads(metrics.read_text(encoding='utf-8-sig'))
        time.sleep(45)
        after=json.loads(metrics.read_text(encoding='utf-8-sig'))
        seconds=after['runtimeSeconds']-before['runtimeSeconds']
        if seconds<35:raise RuntimeError('Performance counters did not advance for a full sample')
        def rate(key):return 1000*(after.get(key,0)-before.get(key,0))/seconds
        result={'before':before,'after':after,'seconds':seconds,
            'totalLuaMsPerSecond':rate('overviewSeconds')+rate('featureSeconds'),
            'overviewLuaMsPerSecond':rate('overviewSeconds'),'featureLuaMsPerSecond':rate('featureSeconds'),
            'automationLuaMsPerSecond':rate('automationSeconds'),
            'services':{k:rate('automation_'+k+'Seconds')for k in KINDS}}
        (output/(label+'.json')).write_text(json.dumps(result,indent=2))
        return result
    originals={k:invoke('feature018_automation_config_get',{'kind':k},True)for k in KINDS}
    chat_originals={k:invoke('feature018_chat_config_get',{'kind':k},True)for k in ('redPacket','fireworks','treasure')}
    if any(c.get('enabled')for c in originals.values()):raise RuntimeError('Performance requires a fresh all-off proof profile')
    if any(c.get('enabled')for c in chat_originals.values()):raise RuntimeError('Performance requires all Chat off too')
    enabled=[];chat_enabled=[];excluded={'treasure':'Owner hold; no claims or dispatch'};final=None;failure=None
    try:
        off_status=status();off=sample('all-off-perf')
        for kind in ('redPacket','fireworks'):
            c=copy.deepcopy(chat_originals[kind]);c.update(enabled=True,replyEnabled=False)
            invoke('feature018_chat_config_save',{'kind':kind,'config':c},True)
            chat_enabled.append(kind)
            invoke('feature018_chat_configure',{'kind':kind})
        # All configured native engines run concurrently. No quality refresh
        # spending, premium speed-ups, chat messages or Treasure claims.
        for kind in KINDS:
            c=copy.deepcopy(originals[kind]);c['enabled']=True
            if kind=='officialPosition':c['positionId']=10006
            if kind=='dispatch':c.update(autoExecute=True,collectRewards=True,weeklyQualities=['none']*7)
            if kind=='dispatchAssist':c.update(autoHelp=True,qualities=['n','r','sr','ssr','ur','special'])
            if kind=='resourceGather':
                c.update(recallOnDisable=True)
                for s in c['squads']:s.update(enabled=s['squadIndex']==1,resource='food',level=1)
            if kind=='allianceGather':
                # Use the same squad as ordinary gathering. It may not be sent
                # on a second march; if no ordinary march exists, do not risk
                # leaving an alliance gathering squad without a recall owner.
                current=status()['services'].get('resourceGather',{})
                if current.get('confirmed',0)<1:
                    observed=invoke('feature018_automation_observe',{'kind':'resourceGather'})
                    busy=next((s for s in observed['gatherSquads']if s['squadIndex']==1 and s.get('collecting')and s.get('ownerUid')==uid),None)
                    if not busy:
                        excluded[kind]='No busy ordinary gathering squad; alliance dispatch deferred to avoid an untracked test march'
                        continue
                c['allianceGatherSquads']=[1]
            if kind=='tradeStation'and not c['selectedItemIds']:
                observed=invoke('feature018_automation_observe',{'kind':kind})
                excluded[kind]={'reason':'No saved/native selected goods; no arbitrary test purchase','observation':observed}
                continue
            save(kind,c);enabled.append(kind)
            if kind=='resourceGather':
                for _ in range(45):
                    r=status()['services'].get(kind,{})
                    if r.get('state')in('success','nothing_available','error','unconfirmed'):break
                    time.sleep(1)
        on_status=status();on=sample('all-on-perf');on_final=status()
        chat_final=invoke('feature018_chat_status')
    except Exception as error:
        failure=repr(error)
        raise
    finally:
        # Stop the ordinary gatherer LAST, after alliance gathering is off.
        for kind in chat_enabled:
            invoke('feature018_chat_config_save',{'kind':kind,'config':chat_originals[kind]},True)
            invoke('feature018_chat_configure',{'kind':kind})
        for kind in reversed(enabled):
            invoke('feature018_automation_stop',{'kind':kind})
            save(kind,originals[kind])
        time.sleep(20)
        final=status()
        (output/'performance-cleanup.json').write_text(json.dumps({'enabledDuringSample':enabled,
            'excluded':excluded,'final':final,'failure':failure},indent=2))
        if any(r.get('enabled')for r in final['services'].values()):raise RuntimeError('An Automation service remained enabled')
        gather=final['services'].get('resourceGather',{})
        if gather.get('confirmed',0)!=gather.get('cleanupConfirmed',0):raise RuntimeError('Performance gathering recall was absent or unconfirmed')
    result={'state':'returned','result':{'allOff':off,'allOn':on,'enabled':enabled,'chatEnabled':chat_enabled,'excluded':excluded,
        'offStatus':off_status,'onStatus':on_status,'onFinal':on_final,'chatFinal':chat_final,'cleanup':final,
        'scope':'Bounded simultaneous native Automation and Red Packet/Fireworks load; replies off; held Treasure and unavailable selections recorded'}}
    (output/'performance-result.json').write_text(json.dumps(result,indent=2))
    return result
