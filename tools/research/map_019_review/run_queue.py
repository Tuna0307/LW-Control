"""Serial MAP-019 measurements, one exact-owned game at a time.

Waits for a named predecessor's verified final cleanup, then executes only the
fixed read-only review scenarios below. No retries of failed experiments.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[3]
RAW=ROOT/'artifacts/map014-lead-live'
OUT=ROOT/'artifacts/map019/queue'


def read(path):
    return json.loads(path.read_text(encoding='utf8'))


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--after',type=Path,required=True)
    args=p.parse_args()
    after=args.after.resolve()
    if not after.is_relative_to(RAW.resolve()) or after.name!='final.json':
        p.error('predecessor must be an exact owned-run final receipt under artifacts/map014-lead-live')
    OUT.mkdir(parents=True,exist_ok=True)
    index=OUT/'index.json'
    if index.exists(): p.error('preserve the previous queue; index already exists')
    state={'createdAtUtc':datetime.now(timezone.utc).isoformat(),'after':str(after),
           'scope':'serial read-only own-session Map experiments; no input/game actions',
           'status':'waiting-for-predecessor','experiments':[]}
    def save():
        temporary=index.with_suffix('.tmp')
        temporary.write_text(json.dumps(state,indent=2)+'\n',encoding='utf8')
        temporary.replace(index)
    save()
    deadline=time.monotonic()+3600
    while not after.exists():
        if time.monotonic()>deadline: raise TimeoutError('Predecessor did not finish within one hour')
        time.sleep(1)
    prior=read(after)
    if prior.get('exactStopSucceeded') is not True or prior.get('originalScriptsRestored') is not True:
        raise RuntimeError('Predecessor cleanup is not verified; no successor started')
    recipes=[
        ('shape','shape-review',{}),
        ('singles-ab','benchmark-review',{
            'WORKLOADS':'city,resource,monster,dispatch,ghost,treasure,railway,truck',
            'METHODS':'direct,camera,camera,direct','ROUNDS':'4','MAX_MINUTES':'120'}),
        ('mixed-ab','benchmark-review',{'WORKLOADS':'city-resource',
            'METHODS':'direct,camera,camera,direct','ROUNDS':'4','MAX_MINUTES':'30'}),
        ('all-nine-2175-ab','benchmark-review',{'WORKLOADS':'all-nine',
            'METHODS':'direct,camera,camera,direct','ROUNDS':'4','MAX_MINUTES':'60','SERVER':'2175'}),
        ('all-nine-home','benchmark-review',{'WORKLOADS':'all-nine',
            'METHODS':'direct,direct','ROUNDS':'2','MAX_MINUTES':'30'}),
    ]
    for label,scenario,values in recipes:
        state['status']='running-'+label
        env=os.environ.copy()
        for key in list(env):
            if key.startswith('LWBRIDGE_MAP019_') or key.startswith('LWBRIDGE_MAP014_') or key.startswith('LWBRIDGE_MAP015_'):
                env.pop(key)
        env['LWBRIDGE_FEATURE018_BACKGROUND_ONLY']='1'
        env['LWBRIDGE_MAP019_TELEMETRY']='1'
        env['LWBRIDGE_MAP014_BENCH_MODE']='fast'
        env.update({'LWBRIDGE_MAP019_'+key:value for key,value in values.items()})
        command=[sys.executable,str(ROOT/'tools/live-harness/run_owned.py'),scenario]
        before=set(RAW.glob('native-'+scenario+'-*'))
        entry={'label':label,'scenario':scenario,'configuration':values,'status':'running',
               'command':subprocess.list2cmdline(command),'startedAtUtc':datetime.now(timezone.utc).isoformat()}
        state['experiments'].append(entry);save()
        with (OUT/(label+'.log')).open('w',encoding='utf8') as log:
            child=subprocess.run(command,cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT,
                                 creationflags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0)
        created=set(RAW.glob('native-'+scenario+'-*'))-before
        if len(created)!=1: raise RuntimeError('A unique new exact-owned diagnostic directory was not established')
        run=created.pop();entry['run']=str(run);entry['returncode']=child.returncode
        final=read(run/'final.json')
        entry['exactStopSucceeded']=final.get('exactStopSucceeded')
        entry['originalScriptsRestored']=final.get('originalScriptsRestored')
        entry['status']='completed' if child.returncode==0 else 'negative'
        save()
        if not (entry['exactStopSucceeded'] and entry['originalScriptsRestored']):
            state['status']='blocked-unverified-cleanup';save()
            raise RuntimeError('Owned native cleanup unverified; no next experiment started')
        # A negative is retained. The next different recipe investigates a
        # different hypothesis/selection; there is no identical automatic retry.
    state['status']='completed';save()
    print('MAP019_SERIAL_QUEUE_COMPLETE',index,flush=True)


if __name__=='__main__': main()
