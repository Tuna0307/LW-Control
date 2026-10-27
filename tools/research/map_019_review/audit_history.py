"""Re-audit genuine MAP-015 receipts, read-only, without emitting player identities.

python tools/research/map_019_review/audit_history.py --output artifacts/map019/history/audit.json

Historical observations are not current-build benchmarks. Local JSON byte counts
are artifact sizes, never claimed network bytes. Field presence is not parity.
"""
from __future__ import annotations

import argparse
from contextlib import closing
import hashlib
import json
from pathlib import Path
import sqlite3

REPO = Path(__file__).resolve().parents[3]
KINDS = ('city', 'resource', 'monster', 'zombie_boss', 'dispatch', 'ghost', 'treasure', 'railway', 'truck')
STORED_FIELDS = {'name': '$stored.name', 'alliance_name': '$stored.allianceName', 'level': '$stored.level',
                 'quality': '$stored.quality', 'power': '$stored.power', 'shield_end_time': '$stored.shieldEndTime'}
FIELDS = {
    'city': ('ownerUid', 'ownerName', 'level', 'protectEndTime', 'allianceId', 'health'),
    'resource': ('resourceNameKey', 'resourceTypeId', 'resourceConfigId', 'level', 'resourceRemainingAmount', 'resourceMaxAmount', 'resourceDetailKnown', 'gatherOccupancyKnown', 'blackTileKnown'),
    'monster': ('monsterNameKey', 'level', 'monsterProtectionKnown', 'monsterProtectionEndTime', 'hp'),
    'zombie_boss': ('monsterNameKey', 'level', 'ownerName', 'recommendPower', 'monsterProtectionKnown', 'monsterProtectionEndTime', 'hp'),
    'dispatch': ('dispatchNameKey', 'level', 'ownerUid', 'ownerName', 'quality', 'rewarded', 'completionTime'),
    'ghost': ('ownerName', 'ownerUid', 'rewards', 'rewardConfig', 'quality', 'protectTime', 'taskExpireTime'),
    'treasure': ('treasureNameKey', 'ownerName', 'ownerUid', 'rewardMax', 'remainingBoxes', 'expireTime'),
    'railway': ('uuid', 'ownerName', 'quality', 'arriveTs', 'currentGoods'),
    'truck': ('ownerName', 'ownerUid', 'quality', 'power', 'trainType', 'currentGoods', 'arriveTs', 'endTime'),
}
MANIFEST = {}


def enrich_stored(conn, population):
    """Attach observed typed columns under explicit virtual names, never infer JSON fields.

    Old/synthetic schemas may lack these columns; absence remains absence.
    """
    columns = {row[1] for row in conn.execute('PRAGMA table_info(map_records)')}
    available = [column for column in STORED_FIELDS if column in columns]
    if not available:
        return
    query = 'SELECT kind,server_id,record_key,'+','.join(available)+' FROM map_records'
    for kind, server, key, *values in conn.execute(query):
        collection = population.get(kind, {})
        record = collection.get((server, key)) if (server, key) in collection else collection.get(key)
        if record is None:
            continue
        fields = record['fields'] if isinstance(record, dict) else record[2]
        fields.update({STORED_FIELDS[column]: value for column, value in zip(available, values)})


def relative(path):
    return path.resolve().relative_to(REPO).as_posix()


def source(path):
    if path.exists():
        with path.open('rb') as handle:
            digest = hashlib.file_digest(handle, 'sha256').hexdigest()
        MANIFEST[relative(path)] = {'sha256': digest, 'bytes': path.stat().st_size}
    return path


def read_json(path):
    return json.loads(source(path).read_text(encoding='utf8'))


def database(path, run_id=None):
    source(path)
    populations = {kind: {} for kind in KINDS}
    with closing(sqlite3.connect(path.resolve().as_uri() + '?mode=ro', uri=True)) as conn:
        runs = conn.execute('SELECT id,status,selected_types,total_blocks,completed_blocks,failed_blocks,created_at,updated_at FROM scan_runs ORDER BY created_at DESC').fetchall()
        run = next((r for r in runs if run_id is None or r[0] == run_id), None)
        if run is None or run[1] != 'completed' or run[3:6] != (2500, 2500, 0):
            raise ValueError(f'No complete correlated scan: {relative(path)}')
        for kind, key, server, updated, raw in conn.execute('SELECT kind,record_key,server_id,updated_at,data_json FROM map_records'):
            if kind not in populations:
                raise ValueError(f'Unexpected kind {kind}')
            if key in populations[kind]:
                raise ValueError('Duplicate SQLite record key')
            row = json.loads(raw)
            populations[kind][key] = (server, updated, row)
        enrich_stored(conn, populations)
    summaries = {}
    for kind in json.loads(run[2]):
        values = list(populations[kind].values())
        summaries[kind] = {
            'rows': len(values), 'uniqueKeys': len(populations[kind]),
            'fields': {field: {'present': sum(field in row for _, _, row in values),
                               'nonNull': sum(row.get(field) is not None for _, _, row in values)} for field in FIELDS[kind]+tuple(STORED_FIELDS.values())},
        }
        if kind == 'truck':
            summaries[kind].update(nonTruckRows=sum(row.get('trainType') != 1 for _, _, row in values),
                arrivedAtCapture=sum(row.get('endTime', 0) <= updated for _, updated, row in values),
                arrivedAtRunLastUpdated=sum(row.get('endTime', 0) <= run[7] for _, _, row in values),
                publicationComparisonCaveat='scan_runs.updated_at is the final durable run update, not independently observed publication time')
    return populations, {'selectedTypes': json.loads(run[2]), 'databaseRunSeconds': (run[7]-run[6])/1000,
                         'completedBlocks': run[4], 'kinds': summaries}


def metrics(path):
    if not path.exists():
        return None
    value = read_json(path)
    keys = ('source', 'phase', 'elapsedMilliseconds', 'acquisitionMilliseconds', 'railwayRefreshMilliseconds',
            'truckHydrationMilliseconds', 'resourceHydrationMilliseconds', 'monsterHydrationMilliseconds',
            'resourceDetailNativeRequests', 'resourceDetailNativeReady', 'monsterDetailNativeRequests',
            'monsterDetailNativeReady', 'attributableLuaProbeAttempts', 'attributableRetries',
            'uniqueCoveredAois', 'rawNetworkRequestCount', 'oldestCapturedAgeMilliseconds')
    return {key: value.get(key) for key in keys}


def compare(left, right):
    result = {}
    for kind in KINDS:
        a, b = left[kind], right[kind]
        if not a and not b:
            continue
        common = a.keys() & b.keys()
        fields = ('x', 'y') + FIELDS[kind]+tuple(STORED_FIELDS.values())
        result[kind] = {'leftRows': len(a), 'rightRows': len(b), 'shared': len(common),
            'leftOnly': len(a.keys()-b.keys()), 'rightOnly': len(b.keys()-a.keys()),
            'changedAmongShared': {field: sum(a[key][2].get(field) != b[key][2].get(field) for key in common) for field in fields},
            'comparableNonNullShared': {field: sum(a[key][2].get(field) is not None and b[key][2].get(field) is not None for key in common) for field in fields}}
    return result


def benchmark(root):
    entries = read_json(root/'benchmark.json')
    final = read_json(root/'final.json') if (root/'final.json').exists() else {}
    populations, rounds = [], []
    for entry in entries:
        i = entry['round']
        if entry['terminal'].get('phase') != 'completed':
            raise ValueError(f'Historical terminal is not completed: {relative(root)} round {i}')
        pop, summary = database(root/f'benchmark-{i}.db', entry['terminal']['scanRunId'])
        populations.append(pop)
        method = entry.get('effectiveMethod') or entry.get('method')
        configuration = method if method in ('direct', 'camera', 'camera-fallback') else (
            f"lens:{entry['lensZoom']}:{entry['lensFov']}" if entry.get('lensZoom') is not None and entry.get('lensFov') is not None else None)
        rounds.append({'round': i, 'method': entry.get('method'), 'effectiveMethod': entry.get('effectiveMethod'),
            'configuration': configuration, 'configurationIsEffectiveEvidence': bool(entry.get('effectiveMethod')),
            'seconds': entry['seconds'], 'summary': summary,
            'cameraRestored': entry['cameraBefore']['cameraSnapshot'] == entry['cameraAfter']['cameraSnapshot'],
            'metrics': metrics(root/f'benchmark-{i}-probe-metrics.json')})
    pairs = [{'left': i, 'right': j,
              'sameMethodDriftControl': bool(rounds[i]['configuration'] and rounds[i]['configuration'] == rounds[j]['configuration']),
              'kinds': compare(populations[i], populations[j])}
             for i in range(len(rounds)) for j in range(i+1, len(rounds))]
    return {'path': relative(root), 'sourceCommit': final.get('sourceCommit'),
            'sourceHashes': final.get('workingSourceHashes'),
            'managedExit': final.get('managedExit'), 'error': final.get('error'),
            'exactStopSucceeded': final.get('exactStopSucceeded'),
            'originalScriptsRestored': final.get('originalScriptsRestored'), 'rounds': rounds, 'pairs': pairs,
            'accuracyQualifiedByThisAudit': False,
            'limitation': 'Temporal row/field differences and same-method drift only; no full-population truth or current-build qualification.'}


def packaged(root):
    ui = read_json(root/'native-ui-map.json')
    pop, summary = database(next(root.glob('profiles/*/map-data/map-data.db')), ui['completedStatus']['scanRunId'])
    evidence = {}
    for name in ('camera-free-scan-result.json', 'fast-truck-scan-result.json'):
        path = root/'live-resource'/name
        if not path.exists():
            continue
        value = read_json(path)
        allowed = ('state', 'elapsedSeconds', 'requestsSent', 'initialRequestsSent', 'repairRequestsSent',
                   'pushesObserved', 'rectanglesCompleted', 'repairPasses', 'unprovenRectangles',
                   'completedAois', 'repliesCaptured', 'requestsPerSecond', 'cameraStable', 'curViewRestored')
        evidence[name] = {key: value.get(key) for key in allowed if key in value}
    stages = metrics(root/'live-resource'/'map-fullworld-probe-metrics.json')
    native_lower = sum(e.get('requestsSent', 0) for e in evidence.values())
    if stages:
        native_lower += (stages.get('resourceDetailNativeRequests') or 0) + (stages.get('monsterDetailNativeRequests') or 0)
    return {'path': relative(root), 'genuineGame': ui.get('genuineGame'), 'controlledProvider': ui.get('controlledProvider'),
            'selection': ui.get('selection'), 'summary': summary, 'metrics': stages, 'evidence': evidence,
            'countedNativeRequestLowerBound': native_lower,
            'networkBytes': None, 'fpsImpact': None, 'memoryImpact': None,
            'limitation': 'Detail counts from managed metrics plus raw discovery receipt request counts; Railway, remote transition and other traffic not counted. Artifact bytes are not wire bytes.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    source(Path(__file__))
    for path in ('src/LWBridge.GameScripts/current_live_resource_probe.lua', 'tools/live-harness/Program.cs', 'tools/live-harness/run_owned.py'):
        source(REPO/path)
    base = REPO/'artifacts/map014-lead-live'
    names = ('city-20261016-033402', 'resource-20261016-033645', 'monster-20261016-035453',
             'zombie_boss-20261016-035708', 'dispatch-20261016-035248', 'ghost-20261016-035108',
             'treasure-20261016-035350', 'railway-20261016-035605', 'all-nontruck-20261016-034237',
             'zombie_boss-20261016-085925', 'all-nontruck-20261016-090114')
    benches = [benchmark(base/('native-benchmark-'+name)) for name in names]
    timing_summary = [{'receipt': item['path'], 'round': row['round'],
                      'selectedTypes': row['summary']['selectedTypes'], 'method': row['configuration'],
                      'seconds': row['seconds'], 'counts': {key: val['rows'] for key, val in row['summary']['kinds'].items()},
                      'sameSessionDriftControlsAvailable': any(pair['sameMethodDriftControl'] for pair in item['pairs'])}
                     for item in benches for row in item['rounds']]
    pair_gains = []
    for item in benches:
        for pair in item['pairs']:
            a, b = item['rounds'][pair['left']], item['rounds'][pair['right']]
            if a['configuration'] != b['configuration']:
                pair_gains.append({'receipt': item['path'], 'methods': [a['configuration'], b['configuration']],
                                   'seconds': [a['seconds'], b['seconds']],
                                   'directTimeReductionVsCameraPercent': (1-a['seconds']/b['seconds'])*100 if a['configuration']=='direct' and b['configuration']=='camera' else None,
                                   'accuracyQualification': 'Historical paired difference only; no same-method drift controls in this session'})
    prototypes = []
    for root in sorted(base.glob('native-direct-full-20261016*')):
        path = root/'direct-full.json'
        if not path.exists():
            continue
        rows = []
        for entry in read_json(path):
            result = entry.get('result') or {}
            rows.append({'config': {key: entry[key] for key in ('config', 'perTick', 'settle', 'gate', 'interval', 'linear', 'wallSeconds') if key in entry},
                         'result': {key: result[key] for key in ('doneAt', 'lastSendAt', 'chunks', 'seenTotal', 'trainSeen', 'monsterSeen', 'marchPushes', 'marchFrames') if key in result}})
        final_path = root/'final.json'
        final = read_json(final_path) if final_path.exists() else {}
        prototypes.append({'path': relative(root), 'sourceCommit': final.get('sourceCommit'),
                           'exactStopSucceeded': final.get('exactStopSucceeded'), 'originalScriptsRestored': final.get('originalScriptsRestored'), 'rounds': rows})
    result = {'schemaVersion': 1, 'evidencePeriod': '2026-10-16 historical observations; audited 2026-10-23',
              'command': 'python tools/research/map_019_review/audit_history.py --output artifacts/map019/history/audit.json',
              'currentBuildOrLiveProof': False,
              'timingSummary': timing_summary,
              'sameSessionTimingComparisons': pair_gains,
              'benchmarks': benches, 'packagedR4': [packaged(REPO/'artifacts/map014-takeover'/('native-ui-'+suffix)) for suffix in ('20261016-090222', '20261016-090519')],
              'historicalPrototypes': prototypes,
              'sources': MANIFEST,
              'limits': ['No UIDs or raw row data are emitted.', 'Field presence does not establish correct values; missing name fields may resolve from config in UI.',
                         '$stored.* fields come from SQLite typed columns; absent JSON ownerName does not establish missing UI name.',
                         'Only methods recorded inside the same benchmark directory form same-session comparisons.',
                         'Artifact byte sizes are disk sizes only. No historical FPS, process memory or transport byte measurement was found in these receipts.']}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False)+'\n', encoding='utf8')
    lines = ['# MAP-019 historical receipt re-audit', '',
             'These are 16-Oct measurements, not current-build/live MAP-019 proof. Reproduce with the command in audit.json.', '',
             '| Selection | Method | Seconds | Recorded counts | Receipt |', '|---|---|---:|---|---|']
    for row in timing_summary:
        lines.append('| '+','.join(row['selectedTypes'])+' | '+str(row['method'])+' | '+f"{row['seconds']:.3f}"+' | '+', '.join(f'{k}={v}' for k,v in row['counts'].items())+' | '+row['receipt']+f"/benchmark.json round {row['round']} |")
    lines += ['', 'All-eight non-Truck same-session direct/camera: 69.926/225.754 seconds (69.026% reduction). It has one round per method and no camera-camera drift control.',
              'R4 packaged Truck-only: 72.268 seconds, 687 unique typed Trucks, 700 native requests including 75 repair requests.',
              'R4 packaged all-nine: 159.439 seconds, 549 unique typed Trucks, at least 8711 counted native requests (750 mobile + 63 point + 7680 Resource detail + 218 Monster detail).',
              'R4 all-nine: zero Trucks expired at their capture; seven expire by the final durable scan-run update. That timestamp is not independently observed publication time.',
              'The JSON includes actual per-kind field presence and shared-row field differences. Null/absent values do not establish parity; final accuracy requires fresh same-session controls.',
              'Historical transport bytes, FPS impact and process memory impact remain unmeasured. Disk JSON byte sizes must not be described as network bytes.', '']
    args.output.with_suffix('.md').write_text('\n'.join(lines), encoding='utf8')
    print(json.dumps({'output': str(args.output), 'benchmarks': len(benches), 'sources': len(MANIFEST), 'historicalOnly': True}))


if __name__ == '__main__':
    main()
