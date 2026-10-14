"""Read-only audit of a real task-owned mixed Treasure+Other scan receipt.

Checks native provenance, independent category placement, server/coordinates,
published SQLite identity, real camera restoration and owned cleanup. The
absence of an unspawned seasonal event is never treated as a positive sample.
"""
import argparse
import json
import sqlite3
from collections import Counter
from pathlib import Path


def audit(root: Path) -> dict:
    final = json.loads((root / 'final.json').read_text(encoding='utf8'))
    stop = json.loads((root / 'stop.json').read_text(encoding='utf8'))
    run = json.loads((root / 'treasure-other-native-report.json').read_text(encoding='utf8'))
    if (final.get('managedExit') != 0 or final.get('scenario') != 'treasure-other'
            or final.get('error') is not None or final.get('exactStopSucceeded') is not True
            or final.get('originalScriptsRestored') is not True
            or final.get('externalAccountActions') != 0 or stop.get('ok') is not True):
        raise ValueError('Native owner/exit/installed-script cleanup was not proven')
    terminal = run['terminal']
    if (terminal.get('phase') != 'completed' or terminal.get('completedBlocks') != 2500
            or terminal.get('failedBlocks') != 0 or terminal.get('unreadBlocks') != 0
            or set(terminal.get('selectedTypes', [])) != {'treasure', 'other'}):
        raise ValueError('A successful explicit mixed Other/Treasure scan is absent')
    if run['before']['cameraSnapshot'] != run['after']['cameraSnapshot']:
        raise ValueError('The full camera was not exactly restored')
    metrics = json.loads((root / 'live-resource/map-fullworld-probe-metrics.json').read_text(encoding='utf8'))
    if (metrics.get('uniqueCoveredAois') != 10000 or metrics.get('phase') != 'prepared'
            or metrics.get('identity', {}).get('RunId') != terminal.get('scanRunId')
            or metrics.get('identity', {}).get('ServerId') != terminal.get('serverId')
            or metrics.get('identity', {}).get('SelectedTypesKey') != 'treasure'):
        raise ValueError('Native shared one-pass Treasure AOI evidence mismatched the owned run')
    server = terminal['serverId']
    connection = sqlite3.connect((root / 'map-014.db').resolve().as_uri() + '?mode=ro', uri=True)
    counts = Counter()
    identities = set()
    types = Counter()
    try:
        for kind, found_server, key, payload in connection.execute(
                'SELECT kind,server_id,record_key,data_json FROM map_records'):
            if found_server != server or kind not in ('treasure', 'other'):
                raise ValueError('Unexpected server/kind in published mixed Other scan')
            if (found_server, key) in identities:
                raise ValueError('The same native key was duplicated across Treasure/Other')
            identities.add((found_server, key))
            data = json.loads(payload)
            x, y = data.get('x'), data.get('y')
            if (type(x) not in (int, float) or type(y) not in (int, float)
                    or not 0 <= x <= 999 or not 0 <= y <= 999):
                raise ValueError('Stored row is outside inclusive 0..999 bounds')
            if kind == 'other':
                subtype = data.get('otherSubtype')
                if (subtype == 'event:27000' and data.get('eventId') == 27000
                        and data.get('treasureType', 0) > 0 and data.get('pointType') == 21):
                    pass
                elif (data.get('pointType') == 27 and
                      data.get('suppliesType', 0) > 0 and
                      subtype == 'supplies:' + str(data['suppliesType'])):
                    if data['suppliesType'] != 1 and data.get('otherNameKey'):
                        raise ValueError('Unverified Supplies subtype has a fabricated native name')
                else:
                    raise ValueError('Other row does not have the exact source event subtype')
                types[subtype] += 1
            elif data.get('pointType') == 27 or data.get('eventId') == 27000:
                raise ValueError('Classified Other event leaked into ordinary Treasure')
            counts[kind] += 1
    finally:
        connection.close()
    if counts['treasure'] + counts['other'] != len(identities):
        raise ValueError('Stored kinds do not reconcile to their unique native keys')
    return {
        'source': str(root), 'serverId': server, 'nativeAoiCoverage': 10000,
        'oneNativeTreasureCensus': True, 'counts': dict(counts),
        'otherSubtypes': dict(types), 'categoryCollisionCount': 0,
        'cameraExactlyRestored': True, 'originalScriptsRestored': True,
        'unseenEvent27000LivePositive': types['event:27000'] > 0,
        'seasonalMissingIsNotFabricated': True,
    }


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('directory', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.directory)
    output = json.dumps(result, indent=2)
    if args.output:
        args.output.write_text(output, encoding='utf8')
    print(output)
