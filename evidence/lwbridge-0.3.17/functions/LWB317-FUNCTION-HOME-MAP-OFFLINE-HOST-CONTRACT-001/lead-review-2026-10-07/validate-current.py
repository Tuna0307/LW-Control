"""Headless lead validation of worker provenance; no game/desktop operations."""
import hashlib
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[5]
packet = Path(__file__).resolve().parent.parent
manifest = json.loads((packet / 'checkpoint-a-source-hashes.json').read_text(encoding='utf-8-sig'))

def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()

verified = {}
for relative, expected in manifest['sources'].items():
    current = digest(root / relative)
    assert current == expected, f'Source drift: {relative}'
    verified[relative] = current
reference = manifest['referenceExe']
assert digest(Path(reference['path'])) == reference['sha256']
for name, record in manifest['currentClient'].items():
    assert digest(Path(record['path'])) == record['sha256'], f'Current artifact drift: {name}'
native = json.loads((packet / 'checkpoint-a-original-0317-native-handlers.json').read_text(encoding='utf-8-sig'))
assert native['source']['sha256'] == reference['sha256']
changed = subprocess.check_output(['git', 'diff', '--name-only', 'bc44275b..f970b614'], cwd=root, text=True).splitlines()
assert all(path.startswith(('docs/', 'evidence/')) for path in changed), 'Worker changed product/test source'
results = json.loads((packet / 'lead-review-2026-10-07/reader-results.json').read_text(encoding='utf-8-sig'))
assert len(results['cases']) == 6 and results['isolatedRootRemoved'] and results['connectCalls'] == 0
print(json.dumps({'workerCommit': 'f970b6143cdf5cb3d186738a769c736901a3394d',
                  'verifiedSources': verified, 'referenceSha256': reference['sha256'],
                  'currentArtifactsVerified': list(manifest['currentClient']),
                  'nativeEvidenceReferenceMatches': True, 'workerOnlyDocsEvidence': True,
                  'readerCases': len(results['cases']), 'connectCalls': 0,
                  'liveXluaConversion': 'UNKNOWN', 'liveTesting': 'ON_HOLD_BY_OWNER'}, indent=2))
