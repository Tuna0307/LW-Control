"""Bounded isolated native proof with the actual managed Map production pipeline.

No desktop input, fake provider, owner configuration, account action or denied
historical session retrieval. Own helper session, lease and exact restoration.
Raw game rows/challenge remain in ignored artifacts.
"""
from pathlib import Path
import argparse, hashlib, json, os, secrets, shutil, sqlite3, subprocess, sys, threading, time

REPO=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(REPO/'tools'))
import run_overview_bridge_current as current
api=current.base
def persisted_snapshot(path):
    """Hash published row identities without exposing private records in a receipt."""
    digest=hashlib.sha256()
    with sqlite3.connect(f'file:{path.as_posix()}?mode=ro',uri=True) as db:
        count=0
        for row in db.execute('SELECT kind,server_id,record_key,updated_at,data_json FROM map_records ORDER BY kind,server_id,record_key'):
            digest.update(json.dumps(row,ensure_ascii=False,separators=(',',':')).encode('utf8')+b'\n')
            count+=1
        jobs={table: db.execute(f'SELECT COUNT(*) FROM {table}').fetchone()[0] for table in (
            'dispatch_plunder_jobs','truck_plunder_jobs','dispatch_assist_jobs')}
    return dict(rows=count,sha256=digest.hexdigest(),jobs=jobs)

def main():
    p=argparse.ArgumentParser(); p.add_argument('scenario',choices=['city','mixed','other','treasure-other','stop','march-source']+['benchmark-'+kind for kind in ['city','resource','monster','truck','railway','dispatch','mixed']]); p.add_argument('--saved-db',help='Optional immutable genuine prior publication, only for native Stop retention proof'); opt=p.parse_args()
    if opt.saved_db and opt.scenario!='stop': p.error('--saved-db is only supported for stop')
    directory=REPO/'artifacts'/'map014-lead-live'/('native-'+opt.scenario+'-'+time.strftime('%Y%m%d-%H%M%S'))
    directory.mkdir(parents=True,exist_ok=False)
    os.environ['LWBRIDGE_REBUILD_DATA_ROOT']=str(directory)
    # Passive scene observation prevents this diagnostic host from invoking
    # world entry while the official client is still loading its City scene.
    trace_source=REPO/'tools/map_009_offline/map013_wrapper_trace.lua'
    own_trace=directory/'map013-wrapper-trace.lua'; shutil.copyfile(trace_source,own_trace)
    os.environ['LWBRIDGE_MAP013_WRAPPER_TRACE']=str(own_trace)
    adapter=REPO/'src/LWBridge.GamePipeAdapter/bin/Release/netstandard2.0/LWBridge.GamePipeAdapter.dll'
    own_adapter=directory/adapter.name; shutil.copyfile(adapter,own_adapter)
    os.environ['LWBRIDGE_PIPE_ADAPTER_PATH']=str(own_adapter)
    profile='map014lead'+secrets.token_hex(5); session='map014lead'+secrets.token_hex(12); challenge=secrets.token_hex(32)
    game_root=Path.home()/'AppData/Local/FunFly/Last War-Survival Game'
    stop_event=threading.Event(); renewal=None; started=None; stop=None; failure=None; code=2
    prior=None; prior_path=None; prior_sha=None; retained=None
    try:
        check=api.run_check_only(game_root)
        (directory/'admission.json').write_text(json.dumps(check,indent=2))
        if not check.get('ok'): raise RuntimeError('Current game admission failed')
        print('MAP014_NATIVE_ADMISSION_OK',flush=True)
        if opt.saved_db:
            candidate=Path(opt.saved_db)
            prior_path=(candidate if candidate.is_absolute() else REPO/candidate).resolve(strict=True)
            if not prior_path.is_relative_to((REPO/'artifacts').resolve()) or prior_path.suffix.lower()!='.db':
                raise ValueError('Stop retention source must be an existing task-owned artifacts .db')
            prior_sha=hashlib.sha256(prior_path.read_bytes()).hexdigest()
            prior=persisted_snapshot(prior_path)
            if prior['rows']==0 or any(prior['jobs'].values()):
                raise ValueError('Native Stop retention requires positive published rows and no pre-armed jobs')
            shutil.copyfile(prior_path,directory/'map-014.db')
            if persisted_snapshot(directory/'map-014.db')!=prior:
                raise ValueError('Genuine prior publication could not be safely copied into isolated owner')
            (directory/'prior-publication.json').write_text(json.dumps(dict(source=str(prior_path),sourceSha256=prior_sha,
                snapshot=prior),indent=2))
        started=api.run_start(profile,session,challenge,165,game_root)
        if not started.get('ok') or not started.get('gameRunning'): raise RuntimeError('Exact native startup failed')
        (directory/'owner.json').write_text(json.dumps(dict(profileId=profile,sessionId=session,challenge=challenge,
            gamePid=started['gamePid'],gamePath=started['gamePath'],gameStartedAtUtc=started['gameStartedAtUtc'])))
        (directory/'start.json').write_text(json.dumps(started,indent=2))
        print('MAP014_NATIVE_READY',started['gamePid'],flush=True)
        def lease():
            while not stop_event.is_set():
                try: api.write_lease(api.overview_paths(game_root),session,challenge)
                except Exception as e: print('MAP014_LEASE_FAILED',repr(e),flush=True); return
                stop_event.wait(1)
        renewal=threading.Thread(target=lease,name='map014-exact-native-owner',daemon=True); renewal.start()
        sys.path.insert(0,str(REPO/'tools/map_009_offline'))
        from run_map013_owned_observer import observe_scene_gate
        scene=observe_scene_gate(directory/'live-resource',directory,profile=profile,
            session=session,challenge=challenge,game_pid=started['gamePid'])
        (directory/'scene-admission.json').write_text(json.dumps(scene,indent=2))
        dll=Path(__file__).parent/'bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll'
        with (directory/'managed-native.log').open('w',encoding='utf8') as log:
            process=subprocess.Popen(['dotnet',str(dll),str(directory/'owner.json'),opt.scenario],stdout=log,stderr=subprocess.STDOUT)
            deadline=time.monotonic()+(1860 if opt.scenario.startswith('benchmark-') else 780)
            while process.poll() is None:
                if time.monotonic()>deadline:
                    process.terminate(); process.wait(timeout=30); raise TimeoutError('Own managed diagnostic exceeded its bound')
                time.sleep(1)
            code=process.returncode
        print('MAP014_MANAGED_PIPELINE_EXIT',code,flush=True)
        if code==0 and prior:
            after=persisted_snapshot(directory/'map-014.db')
            with sqlite3.connect(directory/'map-014.db') as db:
                report=json.loads((directory/'stop-native-report.json').read_text())
                run_id=report['terminal']['scanRunId']
                run=db.execute('SELECT status FROM scan_runs WHERE id=?',(run_id,)).fetchone()
                staged=db.execute('SELECT COUNT(*) FROM scan_records WHERE run_id=?',(run_id,)).fetchone()[0]
                live=db.execute("SELECT COUNT(*) FROM scan_runs WHERE status IN ('running','paused')").fetchone()[0]
            retained=dict(before=prior,after=after,runId=run_id,runStatus=run[0] if run else None,
                stagedRows=staged,liveScanRuns=live,originalSourceStillUnchanged=
                hashlib.sha256(prior_path.read_bytes()).hexdigest()==prior_sha,
                actualNativeSource=True,packagedUi=False)
            retained['pass']=prior==after and run==('cancelled',) and staged==0 and live==0 and retained['originalSourceStillUnchanged']
            (directory/'stop-published-retention.json').write_text(json.dumps(retained,indent=2))
            if not retained['pass']: raise RuntimeError('Native Stop did not retain prior publication, durable cancellation or clean staging')
    except Exception as e:
        failure=f'{type(e).__name__}: {e}'; print('MAP014_NATIVE_FAILURE',failure,flush=True)
    finally:
        # Keep lease ownership until exact game stop/restore has settled.
        if started and started.get('gamePid'):
            for name in ('heartbeat.json','ready.json'):
                file=directory/'overview-bridge'/name
                if file.is_file(): shutil.copyfile(file,directory/('last-'+name))
            try:
                stop=api.run_stop(profile,session,int(started['gamePid']),str(started['gamePath']),
                    game_root,str(started['gameStartedAtUtc']),challenge)
                (directory/'stop.json').write_text(json.dumps(stop,indent=2))
                print('MAP014_EXACT_STOP',stop.get('ok'),stop.get('installedFilesChanged'),flush=True)
            except Exception as e:
                failure=(failure or '')+' STOP: '+repr(e); print('MAP014_STOP_PENDING',repr(e),flush=True)
        stop_event.set()
        if renewal: renewal.join(timeout=3)
        receipt=dict(scenario=opt.scenario,managedExit=code,error=failure,sourceCommit=subprocess.check_output(
            ['git','rev-parse','HEAD'],cwd=REPO,text=True).strip(),diagnosticHost=True,packagedUi=False,
            exactStopSucceeded=bool(stop and stop.get('ok')),originalScriptsRestored=bool(stop and stop.get('installedFilesChanged') is False),
            externalAccountActions=0,adapterSha256=hashlib.sha256(own_adapter.read_bytes()).hexdigest())
        if retained is not None:
            receipt['retainedPublicationProof']=retained['pass']
        receipt['workingSourceHashes']={str(file.relative_to(REPO)):hashlib.sha256(file.read_bytes()).hexdigest()
            for file in (REPO/'tools/current_live_resource_probe.lua',REPO/'tools/current_overview_bridge.lua',
                         Path(__file__),Path(__file__).with_name('Program.cs'))}
        (directory/'final.json').write_text(json.dumps(receipt,indent=2)); print('MAP014_RAW_ATTEMPT',directory,flush=True)
    return code if failure is None else 2
if __name__=='__main__': raise SystemExit(main())
