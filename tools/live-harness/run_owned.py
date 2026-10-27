"""Bounded isolated native proof with the actual managed Map production pipeline.

No desktop input, fake provider, owner configuration, account action or denied
historical session retrieval. Own helper session, lease and exact restoration.
Raw game rows/challenge remain in ignored artifacts.
"""
from pathlib import Path
import argparse, hashlib, json, os, secrets, shutil, sqlite3, subprocess, sys, threading, time

REPO=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(REPO/'src/LWBridge.GameScripts'))
import run_overview_bridge_current as current
api=current.base

REVIEW_WORKLOADS=('city','resource','monster','zombie_boss','dispatch','ghost','treasure','railway','truck','other','city-resource','all-ten')
BENCHMARK_WORKLOADS=REVIEW_WORKLOADS+('all-nine','truck-dispatch','mixed','all-nontruck','all-nonboss')

def review_plan(scenario, environ):
    """Validate opt-in matrix before admitting/starting any game session."""
    if scenario!='benchmark-review': return None
    workloads=tuple(value.strip() for value in environ.get('LWBRIDGE_MAP019_WORKLOADS',','.join(REVIEW_WORKLOADS)).split(',') if value.strip())
    methods=tuple(value.strip() for value in environ.get('LWBRIDGE_MAP019_METHODS','direct,camera').split(',') if value.strip())
    rounds=int(environ.get('LWBRIDGE_MAP019_ROUNDS','4'))
    minutes=int(environ.get('LWBRIDGE_MAP019_MAX_MINUTES','180'))
    if not workloads or len(set(workloads))!=len(workloads) or any(value not in BENCHMARK_WORKLOADS for value in workloads):
        raise ValueError('MAP019 workloads must be unique supported benchmark workloads')
    if not methods or any(value not in ('direct','camera','coarse-boss') for value in methods):
        raise ValueError('MAP019 methods must contain direct, camera and/or coarse-boss')
    if 'coarse-boss' in methods and workloads!=('zombie_boss',):
        raise ValueError('coarse-boss requires only the zombie_boss workload')
    if not 1<=rounds<=6 or not 1<=minutes<=180:
        raise ValueError('MAP019 rounds must be 1..6 and max minutes 1..180')
    server=environ.get('LWBRIDGE_MAP019_SERVER')
    if server is not None and server not in ('2175','2180','2198','2203'):
        raise ValueError('MAP019 server must be an owner-supplied read-only target')
    return dict(workloads=workloads,methods=methods,roundsPerWorkload=rounds,
        measurements=len(workloads)*rounds,deadlineSeconds=minutes*60,
        methodOrder='Methods alternate by round within each workload; direct,camera,direct,camera is two pairs',
        gameActions=0)

def managed_deadline_seconds(scenario,environ):
    plan=review_plan(scenario,environ)
    if plan: return plan['deadlineSeconds']
    if scenario=='mobile-review': return 1860
    return 2100 if scenario=='direct-full' and environ.get('LWBRIDGE_MAP015_PAIRED_TRUCK')=='1' else 1860 if scenario.startswith('benchmark-') else 780
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
    p=argparse.ArgumentParser(); p.add_argument('scenario',choices=['city','mixed','other','treasure-other','stop','march-source','lens-matrix','terrain-probe','lod-snapshot','direct-aoi','hold-lens','direct-full','shape-review','mobile-review','benchmark-review']+['benchmark-'+kind for kind in BENCHMARK_WORKLOADS]); p.add_argument('--saved-db',help='Optional immutable genuine prior publication, only for native Stop retention proof'); opt=p.parse_args()
    if opt.saved_db and opt.scenario!='stop': p.error('--saved-db is only supported for stop')
    if opt.scenario in ('benchmark-review','shape-review','mobile-review'):
        os.environ['LWBRIDGE_FEATURE018_BACKGROUND_ONLY']='1'
    diagnostic_dll=(REPO/'tools/research/map_019_review/mobile_native/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll'
        if opt.scenario=='mobile-review' else Path(__file__).parent/'bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll')
    if opt.scenario=='mobile-review' and not diagnostic_dll.is_file():
        p.error('Build the opt-in mobile_native diagnostic before launching its owned game')
    directory=REPO/'artifacts'/'map014-lead-live'/('native-'+opt.scenario+'-'+time.strftime('%Y%m%d-%H%M%S'))
    directory.mkdir(parents=True,exist_ok=False)
    plan=review_plan(opt.scenario,os.environ)
    if plan: (directory/'benchmark-plan.json').write_text(json.dumps(plan,indent=2))
    if opt.scenario=='shape-review':
        # Explicit private experiment, never a shipped/default probe mutation.
        sys.path.insert(0,str(REPO/'tools/research/map_019_review'))
        from shape_probe import instrument
        original_probe=api.resource_probe_source()
        shape_probe=instrument(original_probe)
        api.resource_probe_source=lambda: shape_probe
        (directory/'shape-probe-source.lua').write_bytes(shape_probe)
        (directory/'shape-probe-hashes.json').write_text(json.dumps({
            'productionSourceSha256':hashlib.sha256(original_probe).hexdigest(),
            'privateProbeSha256':hashlib.sha256(shape_probe).hexdigest(),
            'scope':'raw City/Resource discovery only; 80/160 AOIs; serial; no engine publication',
        },indent=2))
    os.environ['LWBRIDGE_REBUILD_DATA_ROOT']=str(directory)
    # Passive scene observation prevents this diagnostic host from invoking
    # world entry while the official client is still loading its City scene.
    trace_source=REPO/'tools/research/map_009_offline/map013_wrapper_trace.lua'
    own_trace=directory/'map013-wrapper-trace.lua'; shutil.copyfile(trace_source,own_trace)
    if os.environ.get('LWBRIDGE_MAP019_TELEMETRY')=='1':
        combined=own_trace.read_bytes()+b'\n'+(REPO/'tools/research/map_019_review/frame_telemetry.lua').read_bytes()
        if len(combined)>24000: raise RuntimeError('MAP019 telemetry exceeds wrapper trace size cap')
        own_trace.write_bytes(combined)
    if os.environ.get('LWBRIDGE_MAP014_FIXTURE_ZOOM'):
        # Harness-only test fixtures (zoom/terrain/direct): never part of the shipped probe.
        fixture=(Path(__file__).parent/'map014_zoom_fixture.lua').read_bytes()
        combined=own_trace.read_bytes()+b'\n'+fixture
        if len(combined)>24000: raise RuntimeError('Zoom fixture exceeds the wrapper trace size cap')
        own_trace.write_bytes(combined)
    if os.environ.get('LWBRIDGE_MAP014_FULLSCAN_FIXTURE'):
        combined=own_trace.read_bytes()+bytes([10])+(Path(__file__).parent/'map014_fullscan_fixture.lua').read_bytes()
        if len(combined)>24000: raise RuntimeError('Fullscan fixture exceeds the wrapper trace size cap')
        own_trace.write_bytes(combined)
    if os.environ.get('LWBRIDGE_MAP014_HOLD_FIXTURE'):
        combined=own_trace.read_bytes()+bytes([10])+(Path(__file__).parent/'map014_hold_fixture.lua').read_bytes()
        if len(combined)>24000: raise RuntimeError('Hold fixture exceeds the wrapper trace size cap')
        own_trace.write_bytes(combined)
    if os.environ.get('LWBRIDGE_MAP014_DIRECT_FIXTURE'):
        combined=own_trace.read_bytes()+bytes([10])+(Path(__file__).parent/'map014_direct_fixture.lua').read_bytes()
        if len(combined)>24000: raise RuntimeError('Direct fixture exceeds the wrapper trace size cap')
        own_trace.write_bytes(combined)
    if os.environ.get('LWBRIDGE_MAP014_TERRAIN_FIXTURE'):
        # Harness-only read-only terrain observation; never part of the shipped probe.
        combined=own_trace.read_bytes()+bytes([10])+(Path(__file__).parent/'map014_terrain_fixture.lua').read_bytes()
        if len(combined)>24000: raise RuntimeError('Terrain fixture exceeds the wrapper trace size cap')
        own_trace.write_bytes(combined)
    if os.environ.get('LWBRIDGE_MAP014_PACKET_SAMPLER'):
        # Read-only incoming packet observer (tools/research/map_009_offline/managed_sampler); harness only.
        sampler=REPO/'tools/research/map_009_offline/managed_sampler/bin/Release/netstandard2.0/LWBridge.MapTraceSampler.dll'
        own_sampler=directory/sampler.name; shutil.copyfile(sampler,own_sampler)
        os.environ['LWBRIDGE_MAP013_SAMPLER_PATH']=str(own_sampler.resolve())
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
        sys.path.insert(0,str(REPO/'tools/research/map_009_offline'))
        from run_map013_owned_observer import observe_scene_gate
        scene=observe_scene_gate(directory/'live-resource',directory,profile=profile,
            session=session,challenge=challenge,game_pid=started['gamePid'])
        (directory/'scene-admission.json').write_text(json.dumps(scene,indent=2))
        dll=diagnostic_dll
        source_files=(REPO/'src/LWBridge.GameScripts/current_live_resource_probe.lua',REPO/'src/LWBridge.GameScripts/current_overview_bridge.lua',
            Path(__file__),Path(__file__).with_name('Program.cs'),REPO/'tools/research/map_019_review/frame_telemetry.lua')
        if opt.scenario=='mobile-review':
            source_files+=(REPO/'tools/research/map_019_review/mobile_native/Program.cs',
                REPO/'tools/research/map_019_review/mobile_native/MobileReview.csproj')
        launch_sources={str(file.relative_to(REPO)):hashlib.sha256(file.read_bytes()).hexdigest()
            for file in source_files if file.is_file()}
        launch_binaries={str(file.relative_to(REPO)):hashlib.sha256(file.read_bytes()).hexdigest()
            for file in dll.parent.glob('LWBridge*.dll')}
        (directory/'diagnostic-launch-hashes.json').write_text(json.dumps(dict(
            sourceHashesAtLaunch=launch_sources,binaryHashesAtLaunch=launch_binaries,
            note='Source files at launch are not proof that the compiled DLL was built from them; binary hashes identify the actual runtime artifacts.'),indent=2))
        with (directory/'managed-native.log').open('w',encoding='utf8') as log:
            process=subprocess.Popen(['dotnet',str(dll),str(directory/'owner.json'),opt.scenario],stdout=log,stderr=subprocess.STDOUT)
            deadline=time.monotonic()+managed_deadline_seconds(opt.scenario,os.environ)
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
        receipt['workingSourceHashesAtCompletion']={str(file.relative_to(REPO)):hashlib.sha256(file.read_bytes()).hexdigest()
            for file in (REPO/'src/LWBridge.GameScripts/current_live_resource_probe.lua',REPO/'src/LWBridge.GameScripts/current_overview_bridge.lua',
                         Path(__file__),Path(__file__).with_name('Program.cs'))}
        (directory/'final.json').write_text(json.dumps(receipt,indent=2)); print('MAP014_RAW_ATTEMPT',directory,flush=True)
    return code if failure is None else 2
if __name__=='__main__': raise SystemExit(main())
