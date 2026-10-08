"""Lead review provenance; reads reference bytes and immutable committed records only."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'AGENTS.md').exists())
OUT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / 'tools/lwbridge317'))
import home009_native as n

instructions = [{"rva": hex(i.address - n.BASE), "mnemonic": i.mnemonic, "operands": i.op_str}
                for i in n.dis_range(0x1DD114, 0x1DD1D0)]
assert any(i['rva'] == '0x1dd152' and i['mnemonic'] == 'call' for i in instructions)
assert any(i['rva'] == '0x1dd198' and i['mnemonic'] == 'call' for i in instructions)
(OUT / 'original-refresh-error.json').write_text(json.dumps({
    'referenceSha256': hashlib.sha256(n.raw).hexdigest(),
    'scope': 'hash-gated original static instructions; no original execution',
    'interpretation': 'refresh_pending failure enters launched-game termination rather than successful readiness; see home009_launch_contract.py assertions',
    'instructions': instructions,
}, indent=2) + '\n', encoding='utf-8')

checkpoint = '79617a18d493fbb722f4cc2179279bff954472be'
base = 'evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009/'
paths = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', checkpoint, '--', base], cwd=ROOT, text=True).splitlines()
records = []
for path in paths:
    committed = subprocess.check_output(['git', 'show', f'{checkpoint}:{path}'], cwd=ROOT)
    current = (ROOT / path).read_bytes()
    # Windows checkout line-ending conversion is separated from content preservation.
    exact = current == committed
    normalized = current.replace(b'\r\n', b'\n') == committed.replace(b'\r\n', b'\n')
    if not normalized:
        raise AssertionError(f'historical evidence changed: {path}')
    records.append({'path': path, 'committedSha256': hashlib.sha256(committed).hexdigest(),
                    'workingSha256': hashlib.sha256(current).hexdigest(), 'byteExact': exact,
                    'contentPreserved': normalized})
checks = json.loads((OUT / 'final-checks.json').read_text(encoding='utf-8'))
assert len(checks) == 38 and all(c['exitCode'] == 0 for c in checks)
negative = json.loads((OUT / 'refresh-error-negative.json').read_text(encoding='utf-8'))
assert [c['actual'] for c in negative['cases']] == ['PIPE_REGISTRATION_INVALID', 'SUCCESS']
assert negative['tempRootRemoved'] and negative['newGameLaunches'] == 0
admission = json.loads((OUT / 'reconcile-current.json').read_text(encoding='utf-8'))
assert len(admission['cases']) == 4 and not any(c['mismatch'] for c in admission['cases'])
scratch = Path(checks[-1]['command'][-1]).parent
assert not scratch.exists(), f'task verification scratch remains: {scratch}'
result = {'reviewedCheckpoint': checkpoint, 'referenceSha256': hashlib.sha256(n.raw).hexdigest(),
          'integratedCommands': 38, 'commandFailures': 0, 'admissionComparisons': 4,
          'refreshErrorComparisons': 2, 'demonstratedRefreshErrorMismatches': 1,
          'verificationScratchRemoved': True, 'historicalRecordCount': len(records),
          'historicalRecords': records, 'leadProductCodeChanges': 0, 'leadGameLaunches': 0,
          'desktopCaptureOrInput': False, 'fullOriginalParity': 'PARTIAL'}
(OUT / 'review-integrity.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'historicalRecords'}))
