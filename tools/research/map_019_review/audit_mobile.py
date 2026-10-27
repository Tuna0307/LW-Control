"""Independent offline audit of exact-owned MAP-019 mobile-review receipts.

python tools/research/map_019_review/audit_mobile.py --run <owned-run-dir> --output artifacts/map019/mobile-audit.json

Native timing excludes normalization/publication; camera timing includes its
diagnostic scan pipeline. No synthetic result is genuine qualification evidence.
"""
from __future__ import annotations
import argparse
from collections import Counter
from contextlib import closing
from datetime import datetime
import hashlib
import json
from pathlib import Path
import re
import sqlite3
import statistics

REPO=Path(__file__).resolve().parents[3]


def require(condition, message):
    if not condition:raise ValueError(message)


def redacted(value):
    text=str(value)
    text=re.sub(r'(?i)(?:[a-z]:\\|\\\\)[^\r\n\"\']+','[local-path]',text)
    text=re.sub(r'(?i)/(?:home|users|github|mnt|tmp)/[^\r\n\"\']+','[local-path]',text)
    return re.sub(r'\b(?:\d{13,}|[0-9a-fA-F]{32,})\b','[private-id]',text)


def bind_server(current, observed):
    require(type(observed)is int and observed>0,'Mobile round has no valid source server')
    if current is None:return observed
    require(current==observed,'Mobile round changed source server')
    return current


def run_label(root):
    try:return root.relative_to(REPO).as_posix()
    except ValueError:return root.name


def sha(path):
    with path.open('rb')as handle:return hashlib.file_digest(handle,'sha256').hexdigest()


def millis(value):return int(datetime.fromisoformat(value.replace('Z','+00:00')).timestamp()*1000)


def owned(receipt, owner):
    return all(receipt.get(key)==owner.get(expected)for key,expected in(
        ('profileId','profileId'),('launchSessionId','sessionId'),('challenge','challenge'),('gamePid','gamePid')))


def deadline_ms(value):
    if isinstance(value,bool):return None
    try:
        parsed=int(value)
    except (TypeError,ValueError):return None
    if isinstance(value,float)and value!=parsed:return None
    return parsed*1000 if 0<parsed<100_000_000_000 else parsed if parsed>0 else None


def alive(data, at):
    return not any(end is not None and end<=at for end in(deadline_ms(data.get('endTime')),deadline_ms(data.get('arriveTs'))))


def native_receipt(receipt, measurement, owner, server=None):
    require(owned(receipt,owner),'Foreign native receipt owner/session/challenge/PID')
    require(receipt.get('schemaVersion')==1 and receipt.get('probeVersion')=='lwbridge-live-resource-probe-2','Native schema/probe version mismatch')
    require(receipt.get('scanRunId')==measurement.get('scanRunId')and receipt.get('requestId')==measurement.get('nativeRequestId'),'Native request/run correlation mismatch')
    require(receipt.get('serverId')and receipt.get('worldId')==0 and(server is None or receipt['serverId']==server),'Native server/world mismatch')
    rate=measurement['rate'];sent=receipt.get('requestsSent');passes=receipt.get('repairPasses');pushes=receipt.get('pushesObserved')
    require(rate in(15,20,25,30)and receipt.get('requestsPerSecond')==rate,'Unassigned mobile request rate')
    require(receipt.get('state')=='proven'and receipt.get('initialRequestsSent')==625 and receipt.get('rectanglesCompleted')==625 and receipt.get('unprovenRectangles')==0,'Incomplete625-rectangle native sweep')
    require(type(passes)is int and 0<=passes<=2 and type(sent)is int and 625<=sent<=625*(passes+1)and receipt.get('repairRequestsSent')==sent-625,'Invalid bounded repair accounting')
    require(type(pushes)is int and 625<=pushes<=sent,'Incomplete/excess march-push accounting')
    fence=measurement.get('fenceSize',25)
    require(fence in(5,10,25)and receipt.get('responseFenceSize')==fence and receipt.get('responseAttribution')=='bounded_sequential_fences','Unexpected response attribution')
    require(receipt.get('cameraStable')is True and receipt.get('curViewRestored')is True,'Native camera/cache restoration unverified')
    for key in('requestsSent','repairPasses','repairRequestsSent'):
        require(receipt.get(key)==measurement.get(key),'Measurement/native accounting disagreement')
    raw=receipt.get('trainRecords')
    require(isinstance(raw,list)and len(raw)>0 and receipt.get('trainRecordCount')==len(raw)==measurement.get('rawRecords'),'Missing/truncated positive native train records')
    require(isinstance(measurement.get('admittedAtUnixMs'),int),'Exact admission timestamp missing')
    admission=measurement['admittedAtUnixMs'];eligible={};seen=set();expired=0
    for row in raw:
        uid=str(row.get('uuid',''))
        require(uid and uid not in seen,'Missing/duplicate native march UUID');seen.add(uid)
        require(row.get('serverId')==receipt['serverId']and row.get('worldId')==0,'Foreign native train source')
        if not alive(row,admission):expired+=1;continue
        require(row.get('trainType')==1 and row.get('truckMetadataKnown')is True and row.get('trainQuality',0)>=1 and
            row.get('carriageNum',-1)>=0 and isinstance(row.get('ownerName'),str)and row['ownerName'].strip()and
            (deadline_ms(row.get('arriveTs'))or 0)>0 and row.get('power',-1)>=0 and isinstance(row.get('truckCurrentGoodsRaw'),list),
            'Eligible native Truck lacks production metadata')
        eligible[uid]=row
    return eligible,{'rawRows':len(raw),'arrivalFilteredAtAdmission':expired,'eligibleRows':len(eligible),'requestsSent':sent,
        'repairPasses':passes,'repairRequestsSent':sent-625,'pushesObserved':pushes,'rectanglesCompleted':625,'rate':rate,'fenceSize':fence,
        'perRequestProofScope':'Native625-completed rectangle/fence accounting; raw packet-to-request mappings are not exported.',
        'transportBytes':None}


def normalized_rows(raw, server, admission, eligible=None):
    pop={}
    for row in raw:
        data=row.get('data')or{};uuid=str(row.get('uuid',''));key=(row.get('serverId'),row.get('recordKey'))
        require(row.get('kind')=='truck'and row.get('serverId')==server and key[1]and key not in pop and uuid,'Missing/duplicate/foreign normalized Truck identity')
        require(data.get('uuid')==uuid and data.get('trainType')==1,'Normalized UUID/type mismatch')
        require(row.get('name')and data.get('ownerName')and row.get('quality',0)>=1 and row.get('power',-1)>=0 and isinstance(data.get('currentGoods'),list),'Normalized Truck production metadata missing')
        if eligible is not None:
            require(uuid in eligible,'Normalized row was not source-eligible at exact admission')
            require(alive(data,admission),'Normalized row arrived before admission')
            original=eligible[uuid]
            for native,field in(('ownerUid','ownerUid'),('ownerName','ownerName'),('trainQuality','quality'),('power','power'),('arriveTs','arriveTs'),('endTime','endTime'),('trainCfgId','trainCfgId'),('carriageNum','carriageNum')):
                if native in original:require(original[native]==data.get(field),'Normalized/source metadata disagreement: '+field)
        fields=dict(data)
        for column,field in(('name','$stored.name'),('allianceName','$stored.allianceName'),('quality','$stored.quality'),('power','$stored.power')):
            if column in row:fields[field]=row[column]
        pop[key]={'fields':fields,'uuid':uuid,'updatedAt':row.get('updatedAt')}
    require(len({item['uuid']for item in pop.values()})==len(pop),'Duplicate normalized march UUID')
    if eligible is not None:require({item['uuid']for item in pop.values()}==set(eligible),'Eligible native Truck omitted from normalization')
    return pop


def field_states(a,b,common):
    fields=set().union(*(item['fields'].keys()for item in list(a.values())+list(b.values())))
    result={}
    for field in sorted(fields):
        count=Counter()
        for key in common:
            av,bv=a[key]['fields'],b[key]['fields']
            if field not in av or field not in bv:count['bothAbsent'if field not in av and field not in bv else'absentMismatch']+=1
            elif av[field]is None or bv[field]is None:count['bothNull'if av[field]is None and bv[field]is None else'nullMismatch']+=1
            else:count['equalNonNull'if av[field]==bv[field]else'changedNonNull']+=1
        result[field]=dict(count)
    return result


def audit(root):
    sources={}
    def read(name,optional=False):
        path=root/name
        if optional and not path.exists():return None
        sources[name]={'sha256':sha(path),'bytes':path.stat().st_size}
        return json.loads(path.read_text(encoding='utf8'))
    owner=read('owner.json');plan=read('mobile-review-plan.json');entries=read('mobile-review.json')
    final=read('final.json',True)or{};stop=read('stop.json',True)or{}
    methods=plan.get('methods')or[];fences=plan.get('fences')or[25]*len(methods)
    require(2<=len(methods)<=8 and len(fences)==len(methods)and
            all(method=='camera'or str(method)in('15','20','25','30')for method in methods)and
            all(fence in(5,10,25)for fence in fences)and
            plan.get('productionBehaviorChanged')is False and plan.get('gameActions')==0,
            'Unassigned mobile method plan')
    rounds=[];populations=[];server=None;native_ids=set();last_after=0
    for ordinal,entry in enumerate(entries):
        require(entry.get('ordinal')==ordinal and ordinal<len(methods),'Unexpected mobile round ordinal')
        expected=plan['methods'][ordinal];method='camera'if expected=='camera'else'native-'+expected
        require(entry.get('method')==method,'Mobile method order disagrees with plan')
        fence=fences[ordinal]
        if method!='camera':require(entry.get('fenceSize',25)==fence,'Mobile fence order disagrees with plan')
        before=read(f'mobile-{ordinal}-before-camera.json');after=read(f'mobile-{ordinal}-after-camera.json')
        require(owned(before,owner)and owned(after,owner)and before.get('state')=='proven'and after.get('state')=='proven','Foreign/failed camera checkpoint')
        require(before['cameraSnapshot']==after['cameraSnapshot'],'Mobile round changed independent camera geometry')
        start,end=millis(before['capturedAt']),millis(after['capturedAt'])
        admission=entry.get('admittedAtUnixMs');require(type(admission)is int and start-2000<=admission<=end+2000 and start>=last_after,'Admission timestamp outside monotonic round camera bracket');last_after=end
        raw=read(f'mobile-{ordinal}-normalized.json')
        if method!='camera':
            receipt=read(f'mobile-{ordinal}-native-receipt.json')
            eligible,evidence=native_receipt(receipt,entry,owner,server)
            require((receipt['scanRunId'],receipt['requestId'])not in native_ids,'Repeated native request/run identity');native_ids.add((receipt['scanRunId'],receipt['requestId']))
            require(start-2000<=millis(receipt['capturedAt'])<=admission+2000,'Native receipt outside exact round/admission window')
            server=bind_server(server,receipt.get('serverId'));pop=normalized_rows(raw,server,admission,eligible)
            timing='Native acquisition only; excludes normalization, camera checkpoints and engine/UI publication'
            method_label=f'{method}-fence{fence}'
        else:
            terminal=read(f'mobile-{ordinal}-camera-terminal.json');metrics=read(f'mobile-{ordinal}-camera-metrics.json')
            require(terminal.get('phase')=='completed'and terminal.get('scanRunId')==entry.get('scanRunId'),'Camera terminal not completed/correlated')
            server=bind_server(server,terminal.get('serverId'))
            identity=metrics.get('identity')or{}
            require(metrics.get('phase')=='prepared'and metrics.get('uniqueCoveredAois')==10000 and identity.get('RunId')==entry['scanRunId']and identity.get('ServerId')==server and identity.get('SelectedTypesKey')=='truck','Camera native coverage receipt incomplete/foreign')
            pop=normalized_rows(raw,server,admission)
            db=root/f'mobile-{ordinal}-camera.db'
            with closing(sqlite3.connect(db.resolve().as_uri()+'?mode=ro',uri=True))as conn:
                run=conn.execute('SELECT status,selected_types,total_blocks,completed_blocks,failed_blocks FROM scan_runs WHERE id=?',(entry['scanRunId'],)).fetchone()
                require(run==('completed','["truck"]',2500,2500,0),'Camera SQLite exact run incomplete')
                rows=conn.execute("SELECT server_id,record_key,uuid,name,alliance_name,quality,power,updated_at,data_json FROM map_records WHERE kind='truck'").fetchall()
                require({(row[0],row[1])for row in rows}==set(pop),'Camera SQLite/normalized identity sets differ')
                for src,key,uuid,name,alliance,quality,power,updated,data in rows:
                    exported=pop[(src,key)];fields=exported['fields']
                    require(exported['uuid']==uuid and exported['updatedAt']==updated and all(fields.get('$stored.'+column)==value for column,value in(('name',name),('allianceName',alliance),('quality',quality),('power',power))),'Camera typed storage columns disagree')
                    require({k:v for k,v in fields.items()if not k.startswith('$stored.')}==json.loads(data),'Camera raw JSON normalization differs from database')
            sources[db.name]={'sha256':sha(db),'bytes':db.stat().st_size}
            wal=Path(str(db)+'-wal')
            if wal.exists():sources[wal.name]={'sha256':sha(wal),'bytes':wal.stat().st_size}
            evidence={'uniqueAois':10000,'attributableLuaProbeAttempts':metrics.get('attributableLuaProbeAttempts'),'rawNetworkRequestCount':metrics.get('rawNetworkRequestCount')}
            timing='Completed isolated camera scan engine; includes its discovery/hydration/publication, excludes camera checkpoints'
            method_label=method
        require(len(pop)>0 and len(pop)==entry.get('records'),'Missing/truncated normalized positive rows')
        populations.append(pop)
        fields=set().union(*(item['fields'].keys()for item in pop.values()))
        coverage={field:{'absent':sum(field not in item['fields']for item in pop.values()),'null':sum(field in item['fields']and item['fields'][field]is None for item in pop.values()),'nonNull':sum(item['fields'].get(field)is not None for item in pop.values())}for field in sorted(fields)}
        rounds.append({'ordinal':ordinal,'method':method_label,'seconds':entry['seconds'],'nativeSeconds':entry.get('nativeSeconds'),'timingScope':timing,
            'normalizedRows':len(pop),'admittedAtUnixMs':admission,'fieldCoverage':coverage,'arrivalAtAdmission':sum(not alive(item['fields'],admission)for item in pop.values()),
            'arrivalAtCapture':sum(not alive(item['fields'],item['updatedAt'])for item in pop.values()),'independentCameraRestored':True,'evidence':evidence,
            'productionPublication':False,'packagedUi':False})
    pairs=[]
    for i in range(len(rounds)):
        for j in range(i+1,len(rounds)):
            a,b=populations[i],populations[j];common=a.keys()&b.keys();missing=a.keys()-b.keys();added=b.keys()-a.keys()
            expired=sum(not alive(a[key]['fields'],rounds[j]['admittedAtUnixMs'])for key in missing)
            new_after=sum((deadline_ms(b[key]['fields'].get('startTime'))or 0)>rounds[i]['admittedAtUnixMs']for key in added)
            pairs.append({'left':i,'right':j,'leftMethod':rounds[i]['method'],'rightMethod':rounds[j]['method'],
                'sameMethodDriftControl':rounds[i]['method']==rounds[j]['method'],'shared':len(common),'leftOnly':len(missing),'rightOnly':len(added),
                'leftOnlyExpiredByLaterAdmission':expired,'leftOnlyStillUnexpired':len(missing)-expired,
                'rightOnlyStartedAfterEarlierAdmission':new_after,'fieldStatesAmongShared':field_states(a,b,common),'accuracyQualified':None})
    complete=len(entries)==len(methods)
    clean=final.get('managedExit')==0 and final.get('error')is None and final.get('exactStopSucceeded')is True and final.get('originalScriptsRestored')is True and final.get('externalAccountActions')==0 and stop.get('ok')is True and stop.get('installedFilesChanged')is False
    speed={method:{'observations':len(values),'meanSeconds':statistics.mean(values),'minSeconds':min(values),'maxSeconds':max(values)}
           for method in sorted({row['method']for row in rounds})
           if(values:=[row['seconds']for row in rounds if row['method']==method])}
    cleanup={key:final.get(key)for key in('sourceCommit','managedExit','exactStopSucceeded','originalScriptsRestored','externalAccountActions','workingSourceHashesAtCompletion')}
    cleanup['error']=redacted(final.get('error'))if final.get('error')else None
    return {'schemaVersion':1,'run':run_label(root),'status':'MOBILE_OBSERVATION_AUDIT_READY'if complete and clean else'PROVISIONAL',
        'allConfiguredRoundsComplete':complete,'configuredRoundCount':len(methods),
        'allSixRoundsComplete':complete and len(methods)==6,
        'finalCleanupVerified':clean,'rounds':rounds,'pairs':pairs,'timingByMethod':speed,
        'cleanup':cleanup,
        'launchHashes':read('diagnostic-launch-hashes.json',True),'sources':sources,'auditScriptSha256':sha(Path(__file__)),
        'limits':['No raw names/UIDs/native UUIDs emitted. Identities are(server,record_key)and native march UUID internally.',
                  'Native timings exclude normalization/publication; camera scan times cover a larger pipeline. Compare native rate/fence candidates only within native timing scope.',
                  'Arrival checks use both positive endTime and arriveTs deadlines with Unix seconds/milliseconds normalization.',
                  'Camera rows may expire after their own capture and before later admission; this is reported separately, not silently dropped.',
                  'Source header/fence accounting proves retained native receipt admission, not a raw packet-to-request capture for each rectangle.',
                  'Missing/changed Trucks may launch/arrive/plunder naturally. Same-method controls are reported; no automatic accuracy or rate adoption verdict.',
                  'Both-null/absent fields are not equal-value parity. Stored name fields do not invent JSON fields.',
                  'Local JSON/SQLite sizes are not transport bytes. FPS/memory/CPU load not instrumented by this mobile host.']}


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--run',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();result=audit(args.run.resolve());args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n',encoding='utf8')
    lines=['# MAP-019 mobile observation audit','',result['status'],'','| Round | Method | Seconds | Normalized Trucks | Requests | Repairs |','|---|---|---:|---:|---:|---:|']
    for row in result['rounds']:lines.append(f"| {row['ordinal']} | {row['method']} | {row['seconds']:.3f} | {row['normalizedRows']} | {row['evidence'].get('requestsSent','not instrumented')} | {row['evidence'].get('repairRequestsSent','not instrumented')} |")
    lines+=['','Native and camera timing scopes differ; no full-pipeline improvement or global recall claim. See JSON for exact source/cleanup checks, arrival filtering and all-pair field/drift observations.','']
    args.output.with_suffix('.md').write_text('\n'.join(lines),encoding='utf8')
    print(json.dumps({'output':str(args.output),'status':result['status'],'rounds':len(result['rounds'])}))


if __name__=='__main__':main()
