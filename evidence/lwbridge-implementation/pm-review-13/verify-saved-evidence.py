"""Read-only audit of saved live proof, present source hashes and persisted rows.
No game launch, injection, restoration or native process control.
"""
from pathlib import Path
import hashlib
import json
import sqlite3
import os

ROOT = Path(__file__).resolve().parents[3]
EVIDENCE = ROOT / 'evidence/lwbridge-implementation'

def read(name):
    return json.loads((EVIDENCE / name).read_text(encoding='utf-8-sig'))

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

finding = read('2026-09-10-pm12-resource-occupancy-source.json')
proof_path = EVIDENCE / '2026-09-10-pm12-resource-occupancy-live-proof.json'
proof = json.loads(proof_path.read_text(encoding='utf-8-sig'))
assert digest(proof_path) == finding['liveProof']['sha256']
assert digest(ROOT / 'tools/current_live_resource_probe.lua') == finding['liveProof']['probeSourceSha256']
assert proof['stage'] == 'complete'
runs = []
for label in ('first', 'second'):
    item = proof[label]
    result, helper, search = item['result'], item['helper'], item['search']
    assert result['requestId'] == helper['requestId'] == item['status']['scanRunId']
    assert all(result[k] == helper[k] for k in ('profileId', 'launchSessionId', 'gamePid'))
    assert helper['ok'] and helper['restore']['restored'] and not helper['installedFilesChanged']
    assert search['total'] == 1 and len(search['rows']) == 1
    row = search['rows'][0]
    point = result['point_records'][0]
    assert all(row[k] == point[k] for k in ('serverId', 'pointId', 'x', 'y', 'level'))
    assert row['rebuildGatherOccupancyKnown'] is True and row['rebuildGatherOccupied'] is False
    runs.append({k: result[k] for k in ('requestId', 'capturedAt', 'gamePid')})
assert runs[0]['requestId'] != runs[1]['requestId']
assert runs[0]['capturedAt'] < runs[1]['capturedAt']

db = Path(proof['mapDatabase'])
config = json.loads((Path(os.environ['LOCALAPPDATA']) / 'LWBridgeRebuild/config.json').read_text(encoding='utf-8-sig'))
assert config['profileId'] == proof['first']['result']['profileId']
with sqlite3.connect(db.as_uri() + '?mode=ro', uri=True) as connection:
    counts = connection.execute('SELECT kind,server_id,COUNT(*) FROM map_records GROUP BY kind,server_id').fetchall()

report = {
    'scope': 'Saved historical live-proof audit; current read-only source/database inspection, not a new acquisition',
    'savedProofHashAndSourceMatch': True,
    'savedTwoReadCorrelationsMatch': True,
    'runs': runs,
    'databaseExists': db.exists(),
    'currentSelectedProfileMatchesSavedProof': True,
    'persistedCounts': [{'kind': k, 'serverId': s, 'count': n} for k, s, n in counts],
    'currentSourceHashes': {name: digest(ROOT / name) for name in (
        'src/LWBridge.Desktop/LWBridgeWindow.cs',
        'src/LWBridge.Desktop/LiveResourceProbeCommandService.cs',
        'src/LWBridge.Desktop/FirstLiveResultImporter.cs',
        'tools/run_live_resource_probe.py',
        'tools/current_live_resource_probe.lua')},
}
print(json.dumps(report, indent=2))
