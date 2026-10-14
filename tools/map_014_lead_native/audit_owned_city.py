"""Read-only post-run audit of an actual owned City engine/SQLite receipt.

This checks what the owned run published and its native coverage. It never
assumes a fixed server population or certifies unobserved remote players.
"""
import argparse
import json
import sqlite3
from collections import Counter
from pathlib import Path


def audit(root: Path) -> dict:
    finished = json.loads((root / 'final.json').read_text(encoding='utf8'))
    if (finished.get('scenario') != 'city' or finished.get('managedExit') != 0
            or finished.get('error') is not None
            or finished.get('exactStopSucceeded') is not True
            or finished.get('originalScriptsRestored') is not True
            or finished.get('externalAccountActions') != 0):
        raise ValueError('owned City session was not cleanly finished/restored')
    stop = json.loads((root / 'stop.json').read_text(encoding='utf8'))
    if stop.get('ok') is not True or stop.get('installedFilesChanged') is not False:
        raise ValueError('installed scripts/stop ownership have no exact receipt')
    log = (root / 'managed-native.log').read_text(encoding='utf8')
    if 'city completed completed=2500 acquisition=100' not in log:
        raise ValueError('engine scan did not complete 2500 map blocks')
    metrics = json.loads((root / 'live-resource/map-fullworld-probe-metrics.json').read_text(encoding='utf8'))
    if metrics.get('uniqueCoveredAois') != 10000:
        raise ValueError('native City probe has less than 10000 AOIs')
    sqlite_path = root / 'map-014.db'
    with sqlite3.connect(sqlite_path.resolve().as_uri() + '?mode=ro', uri=True) as connection:
        rows = connection.execute('''SELECT kind,server_id,record_key,point_index,updated_at,data_json
             FROM map_records''').fetchall()
        scanned = connection.execute('SELECT server_id,status,completed_blocks,failed_blocks FROM scan_runs').fetchall()
    if not scanned or any(row[1] != 'completed' or row[2] != 2500 or row[3] != 0 for row in scanned):
        raise ValueError('scan_runs does not show a successfully published full scan')
    server = scanned[-1][0]
    owners = set()
    positions = set()
    stamps = []
    bounds = {'xMin': 1000, 'xMax': -1, 'yMin': 1000, 'yMax': -1}
    for kind, db_server, key, point_index, observed_at, payload in rows:
        if kind != 'city' or db_server != server:
            raise ValueError('published run has unexpected kind or foreign server')
        data = json.loads(payload)
        owner = data.get('ownerUid')
        if not isinstance(owner, str) or owner.strip() in ('', '0'):
            raise ValueError('published City has no stable player identity')
        identity = (db_server, owner)
        if identity in owners:
            raise ValueError('published City retains two locations for one owner')
        owners.add(identity)
        x, y = data.get('x'), data.get('y')
        if (type(x) is not int or type(y) is not int or not (0 <= x <= 999 and 0 <= y <= 999)
                or not isinstance(point_index, int) or point_index != y * 1000 + x + 1
                or str(point_index) != key or data.get('pointId') != point_index):
            raise ValueError('published City has invalid point/coordinate geometry')
        if not isinstance(observed_at, int) or observed_at <= 0 or observed_at != data.get('updatedAt'):
            raise ValueError('published City lacks an exact observation time')
        positions.add((x, y))
        stamps.append(observed_at)
        bounds['xMin'] = min(bounds['xMin'], x)
        bounds['xMax'] = max(bounds['xMax'], x)
        bounds['yMin'] = min(bounds['yMin'], y)
        bounds['yMax'] = max(bounds['yMax'], y)
    return dict(source=str(root), serverId=server, qualifiedPublishedOwnerUniqueness=True,
                populationClaim=False, nativeAoiCoverage=10000,
                persistedCities=len(rows), uniqueOwnerIds=len(owners),
                uniqueCoordinates=len(positions), **bounds,
                observedAtRangeMs=[min(stamps, default=None), max(stamps, default=None)],
                scans=Counter(entry[1] for entry in scanned),
                exactOwnedStop=True, originalScriptsRestored=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('root', type=Path)
    parser.add_argument('--output', type=Path)
    options = parser.parse_args()
    result = audit(options.root)
    content = json.dumps(result, indent=2)
    if options.output:
        options.output.write_text(content, encoding='utf8')
    print(content)
