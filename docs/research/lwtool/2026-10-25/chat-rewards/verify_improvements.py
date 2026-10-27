"""Verify retained evidence and inert current-source regressions; never launch a game."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[5]
HERE = Path(__file__).resolve().parent


def main():
    evidence = json.loads((HERE / 'IMPROVEMENT_EVIDENCE.json').read_text('utf-8'))
    product = evidence['productSource']
    assert not subprocess.check_output(['git', 'diff', product, '--', 'src', 'tools'], cwd=ROOT), \
        'Product inputs changed: review/rebuild before treating the retained package as current'
    for anchor in evidence['sourceAnchors']:
        original = subprocess.check_output(['git', 'show', product + ':' + anchor['path']], cwd=ROOT).decode('utf-8')
        assert anchor['contains'] in original.splitlines()[anchor['line'] - 1], anchor
    found = 0
    for item in evidence['retainedEvidence']:
        path = ROOT / item['path']
        if path.is_file():
            assert hashlib.sha256(path.read_bytes()).hexdigest() == item['sha256'], item['path']
            found += 1
    # Private artifacts are intentionally optional on a fresh checkout. Their
    # absence never changes an unavailable live result into a passing result.
    for script in ['feature018_chat_engine_checks.py', 'feature018_chat_outcome_checks.py',
                   'feature018_gold_egg_checks.py', 'feature018_cross_chat_checks.py']:
        subprocess.run([sys.executable, str(ROOT / 'tools' / script)], cwd=ROOT, check=True)
    subprocess.run(['node', 'src/LWBridge.UI-0.3.17/scripts/check-chat-status-hub.mjs'], cwd=ROOT, check=True)
    print('CHAT_IMPROVEMENT_EVIDENCE_PASS', len(evidence['sourceAnchors']), 'source anchors;',
          found, 'retained local receipts; no live claim implied')


if __name__ == '__main__':
    main()
