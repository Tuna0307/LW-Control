"""Read-only, source-identified audit of genuine paired Map engine snapshots.

Each SQLite row owns its server and record identity; data_json need not duplicate
those columns. Comparisons are temporal observations, never global recall proof.
"""
import argparse
import json
import sqlite3
import statistics
from pathlib import Path

STABLE_FIELDS = {
    'city': ('pointId', 'pointIndex', 'x', 'y', 'ownerUid', 'ownerName',
             'level', 'allianceId', 'allianceName', 'srcServerId'),
    'resource': ('pointId', 'x', 'y', 'pointType', 'resourceTypeId',
                 'resourceConfigId', 'level', 'resourceMaxAmount',
                 'isBlackTile', 'blackTileKnown', 'gatherOccupied',
                 'gatherOccupancyKnown'),
    'truck': ('uuid', 'x', 'y', 'power', 'arriveTs', 'endTime'),
    'railway': ('uuid', 'x', 'y', 'arriveTs', 'endTime'),
}


def _read_run(root: Path, i: int, run: dict):
    if run.get('round') != i or run.get('boundedLens') is not (i % 2 == 1):
        raise ValueError(f'round {i}: non-alternating measurement')
    terminal = run['terminal']
    if (terminal.get('phase') != 'completed' or
            terminal.get('failedBlocks') != 0 or
            terminal.get('completedBlocks') != 2500 or
            terminal.get('unreadBlocks') != 0 or
            terminal.get('nativeDroppedRecords', 0) != 0):
        raise ValueError(f'round {i}: failed/incomplete engine scan')
    if run['cameraBefore']['cameraSnapshot'] != run['cameraAfter']['cameraSnapshot']:
        raise ValueError(f'round {i}: camera not exactly restored')
    metrics = json.loads((root / f'benchmark-{i}-probe-metrics.json').read_text(encoding='utf8'))
    run_identity = metrics.get('identity', {})
    expected_selection = '\x1f'.join(sorted(terminal['selectedTypes']))
    if (metrics.get('phase') != 'prepared' or
            metrics.get('source') != 'CurrentClientMapBlockSource.ProbeFastCityBatchAsync' or
            metrics.get('uniqueCoveredAois') != 10000 or
            run_identity.get('RunId') != terminal.get('scanRunId') or
            run_identity.get('ServerId') != terminal.get('serverId') or
            run_identity.get('WorldId') != 0 or
            run_identity.get('SelectedTypesKey') != expected_selection or
            run_identity.get('ScanMode') != 'normal'):
        raise ValueError(f'round {i}: stale, foreign or incomplete native AOI metric receipt')

    dbfile = root / f'benchmark-{i}.db'
    conn = sqlite3.connect(dbfile.resolve().as_uri() + '?mode=ro', uri=True)
    by_kind = {}
    raw_counts = {}
    duplicate_locations = {}
    try:
        for kind, server, record_key, point_index, updated_at, raw in conn.execute(
                'SELECT kind,server_id,record_key,point_index,updated_at,data_json FROM map_records'):
            if server != terminal['serverId']:
                raise ValueError(f'round {i}: foreign server {server} in {kind}')
            if not isinstance(record_key, str) or not record_key.strip():
                raise ValueError(f'round {i}: missing SQLite record_key')
            row = json.loads(raw)
            if not isinstance(row, dict):
                raise ValueError(f'round {i}: malformed {kind} data_json')
            if row.get('serverId', server) != server:
                raise ValueError(f'round {i}: SQLite/JSON server conflict')
            if str(row.get('recordKey', record_key)) != record_key:
                raise ValueError(f'round {i}: SQLite/JSON record key conflict')
            x, y = row.get('x'), row.get('y')
            if not (type(x) in (int, float) and type(y) in (int, float) and
                    0 <= x <= 999 and 0 <= y <= 999):
                raise ValueError(f'round {i}: invalid {kind} coordinates')
            if row.get('worldId', 0) != 0:
                raise ValueError(f'round {i}: foreign world')
            if point_index is not None and row.get('pointIndex', point_index) != point_index:
                raise ValueError(f'round {i}: SQLite/JSON point index conflict')
            if not isinstance(updated_at, int) or updated_at <= 0:
                raise ValueError(f'round {i}: missing observation timestamp')
            identity = row.get('ownerUid') if kind == 'city' else record_key
            if identity is None or str(identity).strip() in ('', '0'):
                raise ValueError(f'round {i}: missing {kind} identity')
            identity = str(identity)
            raw_counts[kind] = raw_counts.get(kind, 0) + 1
            collection = by_kind.setdefault(kind, {})
            if identity in collection:
                if kind != 'city':
                    raise ValueError(f'round {i}: duplicate {kind} identity')
                # Historical FOV20 City trials retained an old and current point
                # for one owner. Keep a counted, unqualified duplicate instead
                # of aborting the rest of this independent evidence audit.
                duplicate_locations[kind] = duplicate_locations.get(kind, 0) + 1
                if collection[identity]['observedAt'] > updated_at:
                    continue
                if (collection[identity]['observedAt'] == updated_at and
                        collection[identity]['pointIndex'] != point_index):
                    raise ValueError(f'round {i}: ambiguous same-time City relocation')
            collection[identity] = {'fields': row, 'recordKey': record_key,
                                    'pointIndex': point_index, 'observedAt': updated_at,
                                    'serverId': server}
    finally:
        conn.close()
    selected = set(terminal['selectedTypes'])
    counts = run.get('summary', {}).get('counts', {})
    for kind, collection in by_kind.items():
        if kind not in selected:
            raise ValueError(f'round {i}: unselected stored kind {kind}')
        if kind in counts and counts[kind] != raw_counts[kind]:
            raise ValueError(f'round {i}: SQLite/engine {kind} count mismatch')
    for kind in selected:
        if counts.get(kind, 0) != raw_counts.get(kind, 0):
            raise ValueError(f'round {i}: selected {kind} count mismatch')
    return by_kind, {'seconds': run['seconds'], 'selected': sorted(selected),
                     'serverId': terminal['serverId'],
                     'coverageAois': metrics['uniqueCoveredAois'],
                     'duplicateOwnerLocations': duplicate_locations,
                     'rawCounts': raw_counts,
                     'counts': {kind: len(records) for kind, records in by_kind.items()}}


def audit(root: Path):
    measurements = json.loads((root / 'benchmark.json').read_text(encoding='utf8'))
    if len(measurements) != 6:
        raise ValueError('three complete alternating paired rounds required')
    populations, runs = [], []
    for i, run in enumerate(measurements):
        population, summary = _read_run(root, i, run)
        populations.append(population)
        runs.append(summary)
    if len({tuple(run['selected']) for run in runs}) != 1 or len({r['serverId'] for r in runs}) != 1:
        raise ValueError('measurements changed type selection or server')
    pairs = []
    for i in (0, 2, 4):
        kinds = {}
        for kind in runs[i]['selected']:
            base, candidate = populations[i].get(kind, {}), populations[i + 1].get(kind, {})
            a, b = set(base), set(candidate)
            common = a & b
            fields = STABLE_FIELDS.get(kind, ('pointId', 'x', 'y', 'level', 'uuid'))
            movements = [
                {
                    'priorCoordinate': [base[ident]['fields'].get('x'), base[ident]['fields'].get('y')],
                    'laterCoordinate': [candidate[ident]['fields'].get('x'), candidate[ident]['fields'].get('y')],
                    'priorObservedAtMs': base[ident]['observedAt'],
                    'laterObservedAtMs': candidate[ident]['observedAt'],
                    'laterObservationIsNewer': candidate[ident]['observedAt'] > base[ident]['observedAt'],
                }
                for ident in common
                if (base[ident]['fields'].get('x'), base[ident]['fields'].get('y')) !=
                   (candidate[ident]['fields'].get('x'), candidate[ident]['fields'].get('y'))
            ]
            kinds[kind] = {
                'baseline': len(a), 'candidate': len(b), 'common': len(common),
                'missingFromCandidate': len(a - b), 'newInCandidate': len(b - a),
                'fieldChangesAmongShared': {
                    field: sum(base[ident]['fields'].get(field) != candidate[ident]['fields'].get(field)
                               for ident in common) for field in fields},
                'positionMovement': {
                    'count': len(movements),
                    'laterObservationNewer': sum(movement['laterObservationIsNewer'] for movement in movements),
                    'samplesWithoutPlayerIdentity': movements[:5],
                },
                'observationTimeRangesMs': {
                    'baseline': [min((v['observedAt'] for v in base.values()), default=None),
                                 max((v['observedAt'] for v in base.values()), default=None)],
                    'candidate': [min((v['observedAt'] for v in candidate.values()), default=None),
                                  max((v['observedAt'] for v in candidate.values()), default=None)]},
            }
        pairs.append({'pair': i // 2 + 1, 'kinds': kinds})
    baseline = statistics.median(r['seconds'] for r in runs[::2])
    candidate = statistics.median(r['seconds'] for r in runs[1::2])
    return {'source': str(root), 'serverId': runs[0]['serverId'],
            'selection': runs[0]['selected'],
            'baselineMedianSeconds': round(baseline, 3),
            'candidateMedianSeconds': round(candidate, 3),
            'timeReductionPercent': round((1 - candidate / baseline) * 100, 2),
            'runs': runs, 'pairs': pairs,
            'accuracyQualified': False,
            'limitations': 'Paired snapshots are sequential; mobile expiry/spawn, server-time correlation, and independent native source population require separate evidence. AOI coverage does not establish march coverage or worldwide City recall.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('root', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.root)
    output = json.dumps(result, indent=2)
    if args.output:
        args.output.write_text(output, encoding='utf8')
    print(output)
