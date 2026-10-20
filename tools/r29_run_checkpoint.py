"""Run one isolated background R29 checkpoint; never uses desktop input.

The account is supplied by the operator, never embedded. Only this invocation's
app/root is controlled. All gameplay commands are reauthorized by the staged
native provider. A failed operation is never replayed.
"""
from pathlib import Path
import argparse
import copy
import json
import os
import subprocess
import sys
import time
import uuid

import r29_part_c_sampler as sampler
from r29_measure_phases import process_tree

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def wait_read(path, predicate=lambda d: True, seconds=120):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        try:
            data = read(path)
            if predicate(data):
                return data
        except (OSError, ValueError):
            pass
        time.sleep(.25)
    raise TimeoutError('Observation did not settle: ' + str(path))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--exe', type=Path, required=True)
    parser.add_argument('--uid', required=True)
    parser.add_argument('--server', type=int, required=True)
    parser.add_argument('--saved-db', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--skip-memory', action='store_true')
    parser.add_argument('--skip-afk', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    exe = args.exe.resolve(strict=True)
    env = dict(os.environ, LWBRIDGE_FEATURE018_BACKGROUND_ONLY='1',
        LWBRIDGE_FEATURE018_STAGED_PROOF='1', LWBRIDGE_FEATURE018_PROOF_UID=args.uid,
        LWBRIDGE_FEATURE018_PROOF_SERVER=str(args.server), LWBRIDGE_R29_SWEEP_PROOF='1',
        LWBRIDGE_R29_PERF_PROOF='1', LWBRIDGE_MAP014_UI_EXE=str(exe))
    runner = subprocess.Popen([sys.executable, str(ROOT/'tools/map_014_lead_native/run_ui.py'),
        'feature018-tabs', '--saved-db', str(args.saved_db.resolve(strict=True))],
        cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
        encoding='utf-8', errors='replace')
    log = (args.output/'runner.log').open('w', encoding='utf-8')
    root = None
    for line in runner.stdout:
        log.write(line); log.flush()
        if line.startswith('MAP014_OWNED_UI_PID='):
            root = Path(line.split(' ROOT=', 1)[1].strip())
            break
    if root is None:
        raise RuntimeError('Owned runner did not create a proof root; inspect runner.log')
    (args.output/'root.json').write_text(json.dumps({'root': str(root), 'exe': str(exe)}))
    owned = read(root/'owned-process.json')
    if Path(owned['publish']).resolve() != exe:
        raise RuntimeError('Captured app path differs from requested executable')
    print('R29_CHECKPOINT_ROOT', root, flush=True)

    def operation(kind, **fields):
        admission = read(root/'feature018-account-admission.json')
        if admission['uid'] != args.uid or admission['serverId'] != args.server:
            raise RuntimeError('Exact proof account differs; no operation issued')
        identity = 'r29-' + uuid.uuid4().hex
        request = dict(id=identity, kind=kind, **fields)
        temporary = root/'r29-next-request.tmp'
        temporary.write_text(json.dumps(request), encoding='utf-8')
        temporary.replace(root/'feature018-live-request.json')
        data = wait_read(root/'native-ui-map.json',
            lambda d: any(x.get('id') == identity for x in d.get('operations', [])),
            seconds=2000 if fields.get('surface') == 'r29-memory' else 240)
        result = next(x for x in data['operations'] if x.get('id') == identity)
        if result['state'] != 'returned':
            raise RuntimeError('One-shot operation failed: ' + json.dumps(result))
        print('R29_OPERATION_RETURNED', kind, fields.get('surface', fields.get('command', '')), flush=True)
        return result.get('result')

    def quiet_sample(name, seconds):
        admission = read(root/'feature018-account-admission.json')
        path = root/'overview-bridge/r29-perf.json'
        before = wait_read(path)
        report = sampler.run_sampler(process_tree(owned['pid']) +
            [sampler.ProcessTargetSpec('game', admission['gamePid'])],
            [sampler.FileRootSpec('runtime', root/'overview-bridge')],
            duration_seconds=seconds, interval_seconds=.25)
        report.update(native_metrics_before=before, native_metrics_after=read(path), phase=name)
        (args.output/(name+'.json')).write_text(json.dumps(report), encoding='utf-8')
        print('R29_QUIET_SAMPLE', name, flush=True)

    failed = None
    phase_sampler = None
    runtime = root/'profiles/map014-native-ui/runtime/config.json'
    original_afk = None
    afk_was_absent = False
    afk_changed = False
    try:
        wait_read(root/'feature018-account-admission.json', seconds=240)
        wait_read(root/'native-ui-map.json', lambda d: d.get('state') == 'running')
        operation('r23Ui', surface='r29-part-a')
        operation('r23Ui', surface='r29-scroll')
        quiet_sample('idle-final-cache', 60)
        if not args.skip_afk:
            initial = operation('command', command='feature018_attack_prepare', payload={'mode':'inventory'})
            squads = initial.get('squads', {})
            slot = next((int(k) for k,v in squads.items() if v.get('unlocked') and
                v.get('lookupSucceeded') and not v.get('marchUuid') and v.get('staffedForAttack')), None)
            if slot is None:
                raise RuntimeError('No independently observed idle/staffed owned squad for bounded Monster AFK')
            catalog = operation('command', command='monster_catalog_options')
            target = next(x for x in catalog['options'] if x.get('group')=='normal' and
                x.get('searchable') and not x.get('rally') and x.get('minLevel')==1)
            tasks = read(runtime).get('tasks', {})
            afk_was_absent = 'monsterSweep' not in tasks
            original_afk = copy.deepcopy(tasks.get('monsterSweep', {'enabled':False,'strategies':[],
                'allianceDrill':{'enabled':False,'activeRally':False,'squadIndexes':[]}}))
            if original_afk.get('enabled'):
                raise RuntimeError('Do not replace an existing active Monster AFK configuration')
            strategy = dict(id='r29-bounded-native-search', name='R29 bounded native search',
                enabled=True, kind='farm', targetKey=target['key'], lastListTargetKey=target['key'],
                customTarget=False,targetNameQuery='',monsterType=target['monsterType'],
                monsterNameKey=target['monsterNameKey'],monsterSpecial=target.get('monsterSpecial',0),
                monsterIds=target['monsterIds'],source=target['source'],action='attack',rally=False,
                searchable=True,minLevel=1,maxLevel=1,squadIndexes=[slot],executionLimit=1,
                attackEnabled=True,joinEnabled=False,continuousAttack=False,continuousJoin=False,
                levelFilterEnabled=True,progressiveLevels=False,distanceFilterEnabled=True,maxDistance=1)
            config = dict(enabled=True,strategies=[strategy],allianceDrill={'enabled':False,'activeRally':False,'squadIndexes':[]})
            afk_changed = True
            operation('profileCommand', command='monster_afk_config_save', payload=config)
            operation('command', command='feature018_afk_configure')
            quiet_sample('monster-afk-active', 60)
            status = operation('command', command='feature018_afk_status')
            (args.output/'monster-afk-native-status.json').write_text(json.dumps(status), encoding='utf-8')
            operation('command', command='automation_stop', payload={'taskName':'monsterSweep'})
            operation('profileCommand', command='monster_afk_config_save', payload=original_afk)
            # Only recall the exact squad observed idle before this proof.
            after = operation('command', command='feature018_attack_prepare', payload={'mode':'inventory'})
            if after['squads'][str(slot)].get('marchUuid'):
                operation('command',command='feature018_hotkey_action',payload={'action':'recall','slot':slot})
            deadline=time.monotonic()+180
            while after['squads'][str(slot)].get('marchUuid'):
                if time.monotonic()>deadline:
                    raise RuntimeError('Owned test march has not returned; retain exact-session cleanup')
                time.sleep(2)
                after=operation('command',command='feature018_attack_prepare',payload={'mode':'inventory'})
            (args.output/'afk-restoration.json').write_text(json.dumps({'before':initial,'after':after,
                'slot':slot,'originalConfigRestored':True,'ownedTestMarchReturned':True}),encoding='utf-8')
            afk_changed=False
        if not args.skip_memory:
            phase_log=(args.output/'phase-sampler.log').open('w',encoding='utf-8')
            phase_sampler=subprocess.Popen([sys.executable,str(ROOT/'tools/r29_measure_phases.py'),
                '--root',str(root),'--output',str(args.output/'phases')],cwd=ROOT,stdout=phase_log,stderr=subprocess.STDOUT)
            operation('r23Ui',surface='r29-memory')
            phase_sampler.wait(timeout=30)
        operation('r23Ui',surface='r29-sweep')
    except Exception as error:
        failed=str(error)
        print('R29_CHECKPOINT_FAILED',failed,flush=True)
        if afk_changed and runtime.exists():
            try:
                operation('command',command='automation_stop',payload={'taskName':'monsterSweep'})
                operation('profileCommand',command='monster_afk_config_save',payload=original_afk)
            except Exception as cleanup:
                print('R29_AFK_CLEANUP_REQUIRES_REVIEW',str(cleanup),flush=True)
        if (root/'feature018-account-admission.json').exists():
            (root/'feature018-live-request.json').write_text(json.dumps({'id':'finish-'+uuid.uuid4().hex,'kind':'finish'}))
    for line in runner.stdout:
        log.write(line);log.flush()
    runner.wait(timeout=180)
    if afk_was_absent and runtime.exists():
        value=read(runtime);value.get('tasks',{}).pop('monsterSweep',None)
        runtime.write_text(json.dumps(value,indent=2),encoding='utf-8')
    outcome=read(root/'owned-exit.json')
    if failed or runner.returncode or outcome.get('restorationAudit',{}).get('state')!='proven':
        raise SystemExit('Checkpoint remains incomplete; scripts/exit receipt: '+str(root/'owned-exit.json'))
    print('R29_CHECKPOINT_RESTORED',root,flush=True)


if __name__=='__main__':
    main()
