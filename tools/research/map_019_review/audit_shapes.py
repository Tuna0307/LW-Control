"""Strict read-only raw City/Resource 160/80/80/160 acquisition comparison.

python tools/research/map_019_review/audit_shapes.py --run <owned-shape-dir> --output artifacts/map019/shapes-audit.json

Acquisition only: no detail hydration, UI publication or full-pipeline speed claim.
"""
from __future__ import annotations
import argparse
from collections import Counter
from datetime import datetime
import hashlib
import json
from pathlib import Path
import statistics
from audit_history import REPO
from shape_probe import instrument


def sha(path):
    with path.open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def epoch(value):
    return datetime.fromisoformat(value.replace('Z','+00:00')).timestamp()*1000


def require(condition, message):
    if not condition:
        raise ValueError(message)


def audit(root):
    manifest={}
    def read(path, optional=False):
        if optional and not path.exists():
            return None
        manifest[path.name]={'sha256':sha(path),'bytes':path.stat().st_size}
        return json.loads(path.read_text(encoding='utf8'))
    owner=read(root/'owner.json')
    summary=read(root/'shape-review.json')
    hashes=read(root/'shape-probe-hashes.json')
    launches=read(root/'diagnostic-launch-hashes.json',True)
    final=read(root/'final.json',True) or {}
    source=root/'shape-probe-source.lua'
    production=REPO/'src/LWBridge.GameScripts/current_live_resource_probe.lua'
    require(sha(source)==hashes['privateProbeSha256'],'Private probe hash does not match the recorded source')
    require(sha(production)==hashes['productionSourceSha256'],'Current production source differs from shape baseline hash')
    require(source.read_bytes()==instrument(production.read_bytes()),'Private probe is not the exact limited 80/160 instrumented production source')
    manifest[source.name]={'sha256':sha(source),'bytes':source.stat().st_size}
    manifest['src/LWBridge.GameScripts/current_live_resource_probe.lua']={'sha256':sha(production),'bytes':production.stat().st_size}
    def owned(value):
        return all(value.get(key)==owner.get(expected) for key,expected in (
            ('profileId','profileId'),('launchSessionId','sessionId'),('challenge','challenge'),('gamePid','gamePid')))
    results=[]
    populations=[]
    expected_caps=[160,80,80,160]
    require(len(summary)<=4,'Unexpected additional shape rounds')
    context=None
    prior_round_time=0
    request_ids=set(); run_ids=set()
    for ordinal,row in enumerate(summary):
        require(row['ordinal']==ordinal and row['cap']==expected_caps[ordinal],'Unexpected order/cap')
        cap=row['cap']; expected=(10000+cap-1)//cap
        receipt=read(root/f'shape-{ordinal}-receipt.json')
        before=read(root/f'shape-{ordinal}-before-camera.json')
        after=read(root/f'shape-{ordinal}-after-camera.json')
        require(owned(receipt) and owned(before) and owned(after),'Receipt/camera does not belong to the exact owned session')
        require(receipt.get('state')=='proven' and receipt.get('schemaVersion')==1 and receipt.get('probeVersion')=='lwbridge-live-resource-probe-2','Native receipt is not proven')
        require(receipt.get('requestId') and receipt.get('scanRunId') and receipt['requestId'] not in request_ids and receipt['scanRunId'] not in run_ids,'Stale/duplicate per-round request or run identity')
        request_ids.add(receipt['requestId']);run_ids.add(receipt['scanRunId'])
        require(receipt.get('cameraStable') is True and receipt.get('curViewRestored') is True,'Native cleanup not verified')
        require(before.get('state')=='proven' and after.get('state')=='proven' and before['cameraSnapshot']==after['cameraSnapshot'],'Independent before/after camera geometry differs')
        captured=epoch(receipt['capturedAt'])
        require(epoch(before['capturedAt'])-2000<=captured<=epoch(after['capturedAt'])+2000,'Receipt timestamp outside this round camera bracket')
        require(epoch(before['capturedAt'])>=prior_round_time,'Shape rounds do not progress in time')
        prior_round_time=epoch(after['capturedAt'])
        scope=(receipt.get('serverId'),receipt.get('worldId'))
        require(scope[0] and scope[1]==0 and (context is None or scope==context),'Source server/world changed')
        context=scope
        batches=receipt.get('batches',[])
        require(receipt.get('map019AreaCap')==cap and receipt.get('completedAois')==10000 and receipt.get('repliesCaptured')==expected and receipt.get('requestsSent')==expected and len(batches)==expected,'Coverage/request headers do not match exact expected cap')
        seen=set(); previous=0; pop={'city':{},'resource':{}}; histogram=Counter();source_kinds=Counter()
        for batch in batches:
            indices=batch['indices']; time=batch['capturedAtUnixMs']
            require(type(time) is int and previous<time<=captured+2000,'Capture timestamps are nonmonotonic or newer than final receipt')
            require(time>=epoch(before['capturedAt'])-2000,'Batch predates round')
            previous=time
            require(0<len(indices)<=cap and all(type(x)is int and 0<=x<10000 and x not in seen for x in indices),'Invalid or duplicate AOI indices')
            require(len(set(indices))==len(indices),'Duplicate AOIs inside batch')
            seen.update(indices)
            for key,count in (batch.get('nativePointTypeHistogram') or {}).items():histogram[key]+=count
            points=batch.get('point_records')
            native_hist=batch.get('nativePointTypeHistogram') or {}
            require(isinstance(points,list) and batch.get('matchedCount')==sum(native_hist.values()) and
                    len(points)==native_hist.get('6',0)+native_hist.get('7',0),
                    'Selected City/Resource rows disagree with native source histogram; matchedCount covers all native kinds')
            require(not batch.get('monster_march_records') and not batch.get('train_march_records'),'Unexpected march collection in raw point experiment')
            for point in points:
                kind={'player_base':'city','resource_point':'resource'}.get(point.get('kind'))
                require(kind is not None,'Unexpected native point kind')
                require(point.get('pointType')==(6 if kind=='city' else 7),'Unexpected native City/Resource point type')
                require(point.get('serverId')==scope[0] and point.get('worldId')==scope[1],'Foreign row source context')
                x,y=point.get('x'),point.get('y')
                require(type(x)in(int,float) and type(y)in(int,float) and 0<=x<=999 and 0<=y<=999,'Invalid native coordinates')
                point_index=int(y*1000+x+1)
                require(point.get('pointId')==point_index and point.get('id')==point_index,'Native point index/coordinates disagree')
                require(int(y//10)*100+int(x//10) in indices,'Native row is outside captured batch AOIs')
                identity=(scope[0],str(point.get('ownerUid'))) if kind=='city' else (scope[0],point_index)
                require(identity[1]not in(None,'None','0','') and identity not in pop[kind],'Missing or duplicate stable identity')
                pop[kind][identity]=point
                source_kinds[point.get('source')]+=1
        require(seen==set(range(10000)),'Unique AOI union does not cover world')
        populations.append(pop)
        fields={kind:{field:{'absent':sum(field not in p for p in values.values()),
                    'null':sum(field in p and p[field] is None for p in values.values()),
                    'nonNull':sum(p.get(field) is not None for p in values.values())}
                    for field in sorted(set().union(*(p.keys() for p in values.values())))} for kind,values in pop.items()}
        results.append({'ordinal':ordinal,'cap':cap,'seconds':row['seconds'],'nativeSeconds':receipt['elapsedSeconds'],
            'requests':expected,'uniqueAois':len(seen),'timestampSequenceStrictlyIncreasing':True,'ownerReceiptMatched':True,
            'independentCameraRestored':True,'counts':{kind:len(value) for kind,value in pop.items()},
            'fieldCoverage':fields,'nativePointTypeHistogram':dict(histogram),'nativeSources':dict(source_kinds),
            'resourceDetailHydration':False,'productionPublication':False,'frameTelemetry':None})
    pairs=[]
    for i in range(len(results)):
        for j in range(i+1,len(results)):
            kinds={}
            for kind in ('city','resource'):
                a,b=populations[i][kind],populations[j][kind]; common=a.keys()&b.keys()
                fields={}
                union=set().union(*(p.keys() for p in list(a.values())+list(b.values())))
                for field in sorted(union):
                    counter=Counter()
                    for key in common:
                        av,bv=a[key],b[key]
                        if field not in av or field not in bv:counter['bothAbsent' if field not in av and field not in bv else 'absentMismatch']+=1
                        elif av[field]is None or bv[field]is None:counter['bothNull' if av[field]is None and bv[field]is None else 'nullMismatch']+=1
                        else:counter['equalNonNull' if av[field]==bv[field] else 'changedNonNull']+=1
                    fields[field]=dict(counter)
                kinds[kind]={'leftRows':len(a),'rightRows':len(b),'shared':len(common),'leftOnly':len(a.keys()-b.keys()),'rightOnly':len(b.keys()-a.keys()),'fields':fields}
            pairs.append({'left':i,'right':j,'sameCapDriftControl':results[i]['cap']==results[j]['cap'],'kinds':kinds})
    complete=len(summary)==4
    clean=final.get('managedExit')==0 and final.get('exactStopSucceeded')is True and final.get('originalScriptsRestored')is True and final.get('externalAccountActions')==0
    means={cap:statistics.mean(row['seconds']for row in results if row['cap']==cap)for cap in (160,80)if any(row['cap']==cap for row in results)}
    speed={'meanSecondsByAreaCap':means,'cap80IncreaseOver160Seconds':means[80]-means[160]if len(means)==2 else None,
           'cap80IncreaseOver160Percent':100*(means[80]/means[160]-1)if len(means)==2 else None,
           'meaning':'Raw City/Resource acquisition timing only; equal end-to-end field accuracy is not established by this experiment.'}
    return {'schemaVersion':1,'run':str(root),'acquisitionOnly':True,'allFourRoundsComplete':complete,'finalCleanupVerified':clean,
            'status':'RAW_ACQUISITION_AUDIT_READY' if complete and clean else 'PROVISIONAL',
            'blockers':([]if complete else ['Four planned raw acquisition rounds not complete'])+([]if clean else ['Successful final owned Stop/exact script restoration not yet confirmed']),
            'privateSourceHashes':hashes,'privateSourceExactInstrumentVerified':True,'launchHashes':launches,
            'cleanup':{key:final.get(key)for key in ('sourceCommit','managedExit','error','exactStopSucceeded','originalScriptsRestored','externalAccountActions','workingSourceHashesAtCompletion')},
            'speedComparison':speed,'rounds':results,'pairs':pairs,'sources':manifest,'auditScriptSha256':sha(Path(__file__)),
            'limits':['Raw discovery only: no remaining-amount details, managed enrichment, UI publication or end-to-end speed comparison.',
                      'Identity comparisons use City owner UID and Resource native point index internally; no identities/raw values emitted.',
                      'Native source type codes are City6/Resource7 in the observed decoder, not UI type labels.',
                      'Full AOI request coverage is not an independent worldwide population census.',
                      'No rendering FPS/process-load/transport-byte measurements in shape receipts; artifact byte sizes are local files.']}


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--run',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();result=audit(args.run.resolve());args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n',encoding='utf8')
    lines=['# MAP-019 raw shape acquisition audit','',result['status'],'','| Round | Areas/request | Requests | Seconds | City | Resource |','|---|---:|---:|---:|---:|---:|']
    for row in result['rounds']:lines.append(f"| {row['ordinal']} | {row['cap']} | {row['requests']} | {row['seconds']:.3f} | {row['counts']['city']} | {row['counts']['resource']} |")
    lines+=['','Acquisition only; no detail hydration or production publication. See JSON for exact source hashes, per-batch validation, pairwise fields and drift.','']
    args.output.with_suffix('.md').write_text('\n'.join(lines),encoding='utf8')
    print(json.dumps({'output':str(args.output),'status':result['status'],'rounds':len(result['rounds'])}))


if __name__=='__main__':main()
