"""One-shot background R21 native redirect qualification, never physical keys."""
from pathlib import Path
import argparse,json,time
from feature018_live_queue import submit


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('directory',type=Path)
    root=parser.parse_args().directory.resolve(strict=True)
    target=root/'r21-retarget-proof.json'
    if target.exists():raise FileExistsError('Preserve this one-shot receipt; never replay automatically')
    evidence={'state':'running','operations':[]}
    def save():target.write_text(json.dumps(evidence,indent=2),encoding='utf-8')
    def op(command,payload,kind='command'):
        if command=='feature018_hotkey_action':payload=dict(payload,authorizedUid='4799432499002212')
        result=submit(root,{'kind':kind,'command':command,'payload':payload},timeout=35)
        evidence['operations'].append(result);save()
        if result.get('code')=='GAME_ACTION_UNCONFIRMED':return result.get('details',{})
        if result.get('state')!='returned':raise RuntimeError(json.dumps(result))
        return result['result']
    def action(action,query=False):return op('feature018_hotkey_action',dict(action=action,slot=2,**({'queryOnly':True}if query else {})))
    def poll(action):
        for _ in range(30):
            data=action_call(action)
            if data.get('serverAcknowledgementCorrelated') or data.get('nativePostStateObserved') or data.get('serverAppliedPostStateObserved'):return data
            time.sleep(.12)
        raise TimeoutError('No server poststate; no send replay')
    def action_call(name):return action(name,True)
    try:
        op('map_coordinate_jump',{'x':199,'y':572,'serverId':2212},'mapCommand')
        for _ in range(30):
            inventory=op('feature018_attack_prepare',{'mode':'inventory'})
            candidates=[c for c in inventory.get('candidates',[]) if 3<=c['level']<=9]
            if len(candidates)>=3:break
            time.sleep(.2)
        else:raise RuntimeError('Three eligible ordinary candidates unavailable')
        assert not inventory['squads']['2'].get('marchUuid'),'Squad 2 must start idle'
        candidates=sorted(candidates,key=lambda c:c['level'])[:3];evidence['targets']=candidates;save()
        op('feature018_attack_prepare',{'mode':'open','targetUuid':candidates[0]['uuid']});time.sleep(.4)
        action('attack');first=poll('attack');evidence['firstAttack']=first;save()
        evidence['beforeMovingRedirect']=op('feature018_attack_prepare',{'mode':'inventory'})
        op('feature018_attack_prepare',{'mode':'open','targetUuid':candidates[1]['uuid']});time.sleep(.2)
        action('attack');moving=poll('attack');evidence['movingRedirect']=moving;save()
        assert moving.get('serverAppliedChangePostState') and moving['marchUuid']==first['marchUuid']
        time.sleep(6) # Give the returning march a real travel window.
        action('recall');evidence['recall']=poll('recall');save()
        evidence['beforeReturningRedirect']=op('feature018_attack_prepare',{'mode':'inventory'})
        op('feature018_attack_prepare',{'mode':'open','targetUuid':candidates[2]['uuid']});time.sleep(.2)
        action('attack');returning=poll('attack');evidence['returningRedirect']=returning;save()
        assert returning.get('serverAppliedChangePostState') and returning['marchUuid']==first['marchUuid']
        action('recall');evidence['cleanupRecall']=poll('recall');save()
        evidence['state']='proven';save();print('R21_NATIVE_TWO_REDIRECTS_PROVEN',first['marchUuid'])
    except BaseException as error:
        evidence['state']='failed';evidence['error']=str(error);save();raise


if __name__=='__main__':main()
