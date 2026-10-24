"""Read-only MAP-019 same-session benchmark/SQLite audit; emits aggregates only.

python tools/map_019_review/audit_live.py --run <owned-run-dir> --output artifacts/map019/live-audit.json

Missing controls produce UNKNOWN, never a pass. Run after exact native Stop for
final evidence; a running session can be inspected but remains provisional.
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
from audit_history import FIELDS, KINDS, REPO, STORED_FIELDS, enrich_stored


def digest(path):
    with path.open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def redacted(value):
    text = str(value)
    text = re.sub(r'(?i)(?:[a-z]:\\|\\\\)[^\r\n\"\']+', '[local-path]', text)
    text = re.sub(r'(?i)/(?:home|users|github|mnt|tmp)/[^\r\n\"\']+', '[local-path]', text)
    return re.sub(r'\b(?:\d{13,}|[0-9a-fA-F]{32,})\b', '[private-id]', text)


def run_label(root):
    try:
        return root.relative_to(REPO).as_posix()
    except ValueError:
        return root.name


def audit(root):
    sources = {}
    def load(path, optional=False):
        if optional and not path.exists():
            return None
        raw = path.read_bytes()
        sources[str(path.relative_to(root))] = {'sha256': hashlib.sha256(raw).hexdigest(), 'bytes': len(raw)}
        return json.loads(raw)
    entries = load(root/'benchmark.json')
    final = load(root/'final.json', True) or {}
    rounds, populations = [], {}
    for i, entry in enumerate(entries):
        ordinal = entry.get('ordinal', i)
        terminal = entry.get('terminal') or {}
        selected = entry.get('selectedTypes') or terminal.get('selectedTypes') or []
        workload = entry.get('workload') or '+'.join(sorted(selected))
        effective = entry.get('effectiveMethod')
        fallback = entry.get('fallbackEvidence') or load(root/f'benchmark-{ordinal}-fallback.json', True)
        if fallback and (fallback.get('runId') or fallback.get('scanRunId')) == terminal.get('scanRunId'):
            effective = 'camera-fallback'
        row = {'ordinal': ordinal, 'round': entry.get('round'), 'workload': workload, 'selectedTypes': selected,
               'serverId': terminal.get('serverId'),
               'requestedMethod': entry.get('method'), 'effectiveMethod': effective,
               'seconds': entry.get('seconds'), 'terminalPhase': terminal.get('phase'),
               'errorType': entry.get('errorType'), 'error': redacted(entry.get('error')) if entry.get('error') else None,
               'completed': False, 'counts': {}, 'fieldCoverage': {}, 'validationErrors': []}
        if fallback:
            row['fallbackReason'] = redacted(fallback.get('reason') or fallback.get('error') or 'recorded native fallback')
        before = (entry.get('cameraBefore') or {}).get('cameraSnapshot')
        after = (entry.get('cameraAfter') or {}).get('cameraSnapshot')
        cleanup = load(root/f'benchmark-{ordinal}-failure-cleanup.json', True)
        row['cameraRestored'] = before == after if before is not None and after is not None else (cleanup or {}).get('cameraRestored')
        if entry.get('error') or effective == 'unqualified' or terminal.get('phase') != 'completed':
            row['validationErrors'].append('negative/incomplete measurement; no published-population accuracy result')
        else:
            dbpath = root/f'benchmark-{ordinal}.db'
            try:
                with closing(sqlite3.connect(dbpath.resolve().as_uri()+'?mode=ro', uri=True)) as conn:
                    db_run = conn.execute('SELECT status,selected_types,total_blocks,completed_blocks,failed_blocks,created_at,updated_at FROM scan_runs WHERE id=?', (terminal.get('scanRunId'),)).fetchone()
                    if not db_run or db_run[:5] != ('completed', json.dumps(selected, separators=(',', ':')), 2500, 2500, 0):
                        raise ValueError('SQLite does not contain the exact completed terminal run/selection')
                    pop = {kind: {} for kind in selected}
                    for kind, server, key, updated, raw in conn.execute('SELECT kind,server_id,record_key,updated_at,data_json FROM map_records'):
                        if kind not in pop or server != terminal.get('serverId'):
                            raise ValueError('Unselected kind or foreign source server')
                        identity = (server, key)
                        if not key or identity in pop[kind]:
                            raise ValueError('Missing/duplicate record identity')
                        data = json.loads(raw)
                        if data.get('serverId', server) != server or data.get('recordKey', key) != key:
                            raise ValueError('JSON identity disagrees with SQLite')
                        pop[kind][identity] = {'updatedAt': updated, 'fields': data}
                    enrich_stored(conn, pop)
                sources[dbpath.name] = {'sha256': digest(dbpath), 'bytes': dbpath.stat().st_size,
                                       'walPresent': Path(str(dbpath)+'-wal').exists(),
                                       'sqliteReadIncludesWal': True}
                # A live WAL may hold rows absent from the main .db file; hash it too.
                wal = Path(str(dbpath)+'-wal')
                if wal.exists():
                    sources[wal.name] = {'sha256': digest(wal), 'bytes': wal.stat().st_size}
                for kind, records in pop.items():
                    row['counts'][kind] = len(records)
                    expected = (entry.get('summary') or {}).get('counts', {}).get(kind)
                    if expected is not None and expected != len(records):
                        raise ValueError('Engine summary count disagrees with SQLite')
                    values = [record['fields'] for record in records.values()]
                    fields = ('x', 'y') + FIELDS.get(kind, ())+tuple(STORED_FIELDS.values())
                    row['fieldCoverage'][kind] = {field: {'absent': sum(field not in v for v in values),
                        'null': sum(field in v and v[field] is None for v in values),
                        'nonNull': sum(field in v and v[field] is not None for v in values)} for field in fields}
                    if kind == 'truck':
                        row['truckArrivalCheck'] = {
                            'arrivedAtCapture': sum(r['fields'].get('endTime', 0) <= r['updatedAt'] for r in records.values()),
                            'arrivedAtRunLastUpdate': sum(r['fields'].get('endTime', 0) <= db_run[6] for r in records.values()),
                            'nonTruckTypedRows': sum(r['fields'].get('trainType') != 1 for r in records.values()),
                            'caveat': 'run last update is not an independently observed publication timestamp'}
                row['startedAtMs'], row['lastRunUpdateMs'] = db_run[5:7]
                row['completed'] = True
                populations[ordinal] = pop
            except (ValueError, sqlite3.Error, OSError) as error:
                row['validationErrors'].append(redacted(error))
        metrics = load(root/f'benchmark-{ordinal}-probe-metrics.json', True)
        if metrics:
            row['metrics'] = {key: val for key, val in metrics.items() if not isinstance(val, (dict, list))}
            row['metricsCorrelated'] = (metrics.get('identity') or {}).get('RunId') == terminal.get('scanRunId')
        stage = load(root/f'benchmark-{ordinal}-stage-metrics.json', True) or entry.get('stageMetrics')
        if stage:
            correlated_stage = (stage.get('runId') == terminal.get('scanRunId') and
                                stage.get('serverId') == terminal.get('serverId'))
            numeric = ('sourceCaptureMilliseconds', 'checkpointMilliseconds',
                       'publicationMilliseconds', 'wholeEngineMilliseconds')
            if not correlated_stage or any(type(stage.get(key)) not in (int, float) or stage.get(key) < 0
                                           for key in numeric):
                row['validationErrors'].append('engine stage metrics are missing, invalid or belong to another run')
            else:
                row['stageMetrics'] = {key: stage.get(key) for key in ('scanMode', *numeric)}
                row['stageMetricsCorrelated'] = True
        request_counts = []
        for lane in ('direct-evidence', 'mobile-evidence', 'camera-free-failed-evidence', 'fast-truck-failed-evidence'):
            evidence = load(root/f'benchmark-{ordinal}-{lane}.json', True)
            if evidence:
                correlated = evidence.get('scanRunId') == terminal.get('scanRunId')
                allowed = ('state', 'error', 'requestsSent', 'initialRequestsSent', 'repairRequestsSent', 'pushesObserved',
                           'rectanglesCompleted', 'completedAois', 'repliesCaptured', 'cameraStable', 'curViewRestored', 'elapsedSeconds')
                row[lane] = {key: redacted(evidence[key]) if key == 'error' else evidence[key] for key in allowed if key in evidence}
                row[lane]['sameRunCorrelated'] = correlated
                if correlated and lane in ('direct-evidence', 'mobile-evidence'):
                    request_counts.append((lane, evidence.get('requestsSent', 0)))
        row['countedNativeRequestLowerBound'] = sum(value for _, value in request_counts) + sum((metrics or {}).get(key, 0) or 0 for key in ('resourceDetailNativeRequests', 'monsterDetailNativeRequests'))
        row['networkBytes'] = None
        row['process'] = {key: entry.get(key) for key in ('processCpuSeconds', 'averageMachineCpuPercent', 'sampledPeakWorkingSetBytes',
            'sampledPeakPrivateBytes', 'memorySamples', 'memorySamplingIntervalSeconds', 'fpsMeasured')}
        for key in ('processIdleBefore', 'processBefore', 'processAfter'):
            sample = entry.get(key)
            if sample:
                row['process'][key] = {k: v for k, v in sample.items() if k not in ('gamePid', 'pid')}
        fbefore = load(root/f'benchmark-{ordinal}-frames-before.json', True)
        fafter = load(root/f'benchmark-{ordinal}-frames-after.json', True)
        fidle = load(root/f'benchmark-{ordinal}-frames-idle-before.json', True)
        if fbefore and fafter:
            counters = ('frameCount', 'samples', 'over100ms', 'over33ms', 'deltaSecondsSum', 'elapsedSeconds')
            delta = {key: fafter[key]-fbefore[key] for key in counters if key in fafter and key in fbefore}
            if delta.get('elapsedSeconds', 0) > 0 and delta.get('frameCount', -1) >= 0:
                delta['unityFrameCounterRatePerSecond'] = delta['frameCount']/delta['elapsedSeconds']
            row['frameTelemetry'] = {'delta': delta, 'validMonotonicCounters': all(v >= 0 for v in delta.values()),
                'meaning': 'Unique Unity frame-counter delta over telemetry wall time; observed probe frame samples/delta buckets are cumulative differences. Not independent rendering FPS or causal performance impact.',
                'maxDeltaSecondsAfterCumulativeNotIntervalMaximum': fafter.get('maxDeltaSeconds')}
            if fidle:
                idle_seconds = fbefore.get('elapsedSeconds', 0)-fidle.get('elapsedSeconds', 0)
                idle_frames = fbefore.get('frameCount', 0)-fidle.get('frameCount', 0)
                row['frameTelemetry']['precedingIdleSeconds'] = idle_seconds
                row['frameTelemetry']['precedingIdleFrameCounterRatePerSecond'] = idle_frames/idle_seconds if idle_seconds > 0 and idle_frames >= 0 else None
        rounds.append(row)
    pairs = []
    for pos, left in enumerate(rounds):
        for right in rounds[pos+1:]:
            if not left['completed'] or not right['completed'] or left['workload'] != right['workload'] or set(left['selectedTypes']) != set(right['selectedTypes']):
                continue
            same = bool(left['effectiveMethod'] and left['effectiveMethod'] == right['effectiveMethod'])
            kind_results = {}
            for kind in left['selectedTypes']:
                a, b = populations[left['ordinal']][kind], populations[right['ordinal']][kind]
                common = a.keys() & b.keys()
                field_result = {}
                for field in ('x', 'y')+FIELDS.get(kind, ())+tuple(STORED_FIELDS.values()):
                    states = Counter()
                    for key in common:
                        av, bv = a[key]['fields'], b[key]['fields']
                        if field not in av or field not in bv:
                            states['bothAbsent' if field not in av and field not in bv else 'absentMismatch'] += 1
                        elif av[field] is None or bv[field] is None:
                            states['bothNull' if av[field] is None and bv[field] is None else 'nullMismatch'] += 1
                        else:
                            states['equalNonNull' if av[field] == bv[field] else 'changedNonNull'] += 1
                    field_result[field] = dict(states)
                only_a, only_b = a.keys()-b.keys(), b.keys()-a.keys()
                # Temporal eligibility uses only explicit start/end fields; unknown remains unknown.
                def temporal(records, keys, other):
                    result = Counter()
                    for key in keys:
                        data = records[key]['fields']
                        start, end = data.get('startTime'), data.get('endTime')
                        if isinstance(end, (int, float)) and end > 0 and end <= other['startedAtMs']:
                            result['endedBeforeOtherScan'] += 1
                        elif isinstance(start, (int, float)) and start > other['lastRunUpdateMs']:
                            result['startedAfterOtherScan'] += 1
                        else:
                            result['notExplainedByExplicitLifetime'] += 1
                    return dict(result)
                kind_results[kind] = {'leftRows': len(a), 'rightRows': len(b), 'shared': len(common),
                    'leftOnly': len(only_a), 'rightOnly': len(only_b), 'symmetricDifference': len(only_a)+len(only_b),
                    'leftOnlyTemporal': temporal(a, only_a, right), 'rightOnlyTemporal': temporal(b, only_b, left), 'fields': field_result}
            pairs.append({'leftOrdinal': left['ordinal'], 'rightOrdinal': right['ordinal'], 'workload': left['workload'],
                          'leftMethod': left['effectiveMethod'], 'rightMethod': right['effectiveMethod'], 'sameMethodDriftControl': same, 'kinds': kind_results})
    for pair in pairs:
        if pair['sameMethodDriftControl']:
            continue
        controls = [p for p in pairs if p['sameMethodDriftControl'] and p['workload'] == pair['workload']]
        pair['driftAssessment'] = {}
        for kind, values in pair['kinds'].items():
            per_method = {}
            for method in (pair['leftMethod'], pair['rightMethod']):
                drift = [p['kinds'][kind]['symmetricDifference'] for p in controls if p['leftMethod'] == method]
                per_method[method or 'unknown'] = max(drift) if drift else None
            known = [d for d in per_method.values() if d is not None]
            positive_populations = values['leftRows'] > 0 and values['rightRows'] > 0
            pair['driftAssessment'][kind] = {'sameMethodDriftMaxByMethod': per_method,
                'identityDifferenceNoWorseThanBothDriftControls': (values['symmetricDifference'] <= min(known)) if len(known) == 2 and positive_populations else None,
                'identityDifferenceNoWorseThanLargestDriftControl': (values['symmetricDifference'] <= max(known)) if len(known) == 2 and positive_populations else None,
                'fieldParityQualified': None, 'accuracyQualified': None,
                'caveat': 'Missing control/empty populations cannot prove accuracy. Field absence/null/changes above require source and temporal interpretation.'}
    launches = load(root/'diagnostic-launch-hashes.json', True)
    cleanup = {key: final.get(key) for key in ('managedExit', 'sourceCommit', 'exactStopSucceeded', 'originalScriptsRestored', 'externalAccountActions', 'workingSourceHashes', 'workingSourceHashesAtCompletion')}
    cleanup['error'] = redacted(final.get('error')) if final.get('error') else None
    return {'schemaVersion': 1, 'run': run_label(root), 'provisionalUntilFinalStop': not bool(final.get('exactStopSucceeded') and final.get('originalScriptsRestored')),
            'cleanup': cleanup,
            'launchHashes': launches,
            'rounds': rounds, 'pairs': pairs, 'sources': sources,
            'auditScriptSha256': digest(Path(__file__)),
            'limits': ['No raw rows, UIDs or native UUIDs emitted.', 'No globally optimal-method claim.', 'An acquisition receipt state=proven does not prove downstream hydration/publication succeeded.',
                       'Native request lower bounds omit uninstrumented fallback, Railway, remote transitions and unrelated game traffic. JSON file bytes are not network bytes.',
                       'Sequential field changes are observations; names may resolve by config key. Both-null and both-absent never count as value parity.',
                       '$stored.* virtual fields are observed SQLite typed columns. Absent JSON ownerName does not establish missing UI name.',
                       'Temporal eligibility explains only explicit lifetime bounds and is not a recall oracle.']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(args.run.resolve())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False)+'\n', encoding='utf8')
    print(json.dumps({'output': str(args.output), 'rounds': len(result['rounds']), 'pairs': len(result['pairs']), 'provisional': result['provisionalUntilFinalStop']}))


if __name__ == '__main__':
    main()
