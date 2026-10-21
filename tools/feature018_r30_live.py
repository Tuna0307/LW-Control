"""One task-owned R30 background packaged WebView proof; no desktop input.

The existing native harness owns account admission, game stop and exact script
restoration. Never targets an existing owner app/game or unrelated node process.
"""
from pathlib import Path
import argparse,json,os,subprocess,sys,time
from feature018_live_queue import submit

ROOT=Path(__file__).resolve().parents[1]

def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--exe',type=Path,required=True)
    p.add_argument('--saved-db',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--kind',action='append',required=True)
    p.add_argument('--idle-seconds',type=int,default=20)
    args=p.parse_args()
    args.output.mkdir(parents=True,exist_ok=False)
    exe=args.exe.resolve(strict=True);db=args.saved_db.resolve(strict=True)
    env=dict(os.environ,LWBRIDGE_FEATURE018_BACKGROUND_ONLY='1',LWBRIDGE_FEATURE018_STAGED_PROOF='1',
        LWBRIDGE_FEATURE018_PROOF_UID='4799432499002212',LWBRIDGE_FEATURE018_PROOF_SERVER='2212',
        LWBRIDGE_R29_PERF_PROOF='1',LWBRIDGE_MAP014_UI_EXE=str(exe))
    runner=subprocess.Popen([sys.executable,str(ROOT/'tools/map_014_lead_native/run_ui.py'),
        'feature018-tabs','--saved-db',str(db)],cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,
        text=True,encoding='utf-8',errors='replace',creationflags=subprocess.CREATE_NO_WINDOW)
    root=None
    log=(args.output/'runner.log').open('w',encoding='utf-8')
    for line in runner.stdout:
        log.write(line);log.flush()
        if line.startswith('MAP014_OWNED_UI_PID='):
            root=Path(line.split(' ROOT=',1)[1].strip());break
    if root is None:raise RuntimeError('No owned session was created; inspect runner.log')
    (args.output/'root.json').write_text(json.dumps({'root':str(root),'exe':str(exe)}))
    print('R30_OWNED_ROOT='+str(root),flush=True)
    finished=False
    try:
        deadline=time.monotonic()+180
        while time.monotonic()<deadline:
            admission=root/'feature018-account-admission.json'
            if admission.exists()and(root/'native-ui-map.json').exists():
                a=read(admission)
                if a.get('uid')!='4799432499002212'or a.get('serverId')!=2212:
                    raise RuntimeError('Wrong account; no gameplay command submitted')
                if read(root/'native-ui-map.json').get('state')=='running':break
            time.sleep(.5)
        else:raise TimeoutError('Owned game admission did not become ready')
        metrics=root/'overview-bridge/r29-perf.json'
        before=read(metrics)if metrics.exists()else None
        time.sleep(max(0,min(args.idle_seconds,45)))
        after=read(metrics)if metrics.exists()else None
        (args.output/'idle-perf.json').write_text(json.dumps({'before':before,'after':after},indent=2))
        outcomes=[]
        for kind in args.kind:
            result=submit(root,{'kind':'r23Ui','surface':'r30-'+kind},timeout=150)
            outcomes.append(result)
            (args.output/'operations.json').write_text(json.dumps(outcomes,indent=2))
            print('R30_OPERATION '+json.dumps({'kind':kind,'state':result.get('state'),
                'terminal':result.get('result',{}).get('terminal',{}).get('state'),'error':result.get('error')}),flush=True)
            if result.get('error')or result.get('state')in('failed','error'):
                raise RuntimeError('R30 one-shot proof failed; preserve receipt and do not replay')
    finally:
        if (root/'feature018-account-admission.json').exists()and not(root/'owned-exit.json').exists():
            try:
                submit(root,{'kind':'finish'},timeout=45);finished=True
            except Exception as error:
                print('R30_FINISH_ERROR '+str(error),flush=True)
        for line in runner.stdout:
            log.write(line);log.flush()
        runner.wait(timeout=60)
        log.close()
    if not finished or runner.returncode!=0:raise RuntimeError('Owned exit/restoration did not pass')
    exit_record=read(root/'owned-exit.json')
    if exit_record.get('restorationAuditError')or not all(exit_record.get('originalScriptsRestored',{}).values()):
        raise RuntimeError('Exact original restoration audit failed')
    print('R30_OWNED_EXIT_RESTORATION_PASS',flush=True)

if __name__=='__main__':main()
