"""Read-only lead replay of native accuracy proof acceptance boundaries.

Copies archived witness JSON only; the original DB and native evidence are never
modified. No game, desktop input, native provider or live process is invoked.
Exit 0 means the reproduced observations were saved, not product acceptance.
"""
import argparse
import json
import pathlib
import subprocess
import sys
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--db', required=True, type=pathlib.Path)
    parser.add_argument('--native', required=True, type=pathlib.Path)
    parser.add_argument('--run', required=True)
    parser.add_argument('--server', required=True, type=int)
    parser.add_argument('--owner-receipt', required=True, type=pathlib.Path)
    parser.add_argument('--out', required=True, type=pathlib.Path)
    args = parser.parse_args()
    originals = [json.loads(line) for line in args.native.read_text(encoding='utf-8').splitlines() if line.strip()]
    cases = []
    with tempfile.TemporaryDirectory(prefix='map008-lead-audit-') as directory:
        root = pathlib.Path(directory)
        for variant in ('unchanged_archive', 'uniform_wrong_owner', 'missing_method_and_defer_contract'):
            documents = json.loads(json.dumps(originals))
            if variant == 'uniform_wrong_owner':
                for document in documents:
                    document['profileId'] = 'lead-controlled-wrong-profile'
                    document['launchSessionId'] = 'lead-controlled-wrong-session'
                    document['gamePid'] = 2147483000
            elif variant == 'missing_method_and_defer_contract':
                for document in documents:
                    document.pop('requestMethod', None)
                    document.pop('deferRestoreUntilResponse', None)
            witness = root / 'witness.jsonl'
            witness.write_text('\n'.join(json.dumps(document) for document in documents) + '\n', encoding='utf-8')
            result_path = root / 'result.json'
            result = subprocess.run([
                sys.executable, 'tools/map_008_scan_checks/audit_native_fields.py',
                '--db', str(args.db), '--native', str(witness), '--run', args.run,
                '--server', str(args.server), '--require-deferred', '--out', str(result_path),
                '--owner-receipt', str(args.owner_receipt),
            ], capture_output=True, text=True)
            if variant == 'unchanged_archive' and result.returncode != 0:
                raise RuntimeError('Archived baseline no longer matches current publication: ' + result.stderr[-1000:])
            cases.append({
                'case': variant,
                'expected': 'PASS' if variant == 'unchanged_archive' else 'REJECT',
                'actual': 'PASS' if result.returncode == 0 else 'REJECT',
                'exitCode': result.returncode,
                'summary': result.stdout.strip(),
            })
    payload = {
        'reviewedCheckpoint': '473d64836f75c226ad6e4f3db1fd6ec004485351',
        'proofType': 'read-only archived native audit with disposable witness mutations',
        'sourceNativeResponseCount': len(originals),
        'runId': args.run,
        'serverId': args.server,
        'cases': cases,
        'unexpectedAcceptances': sum(case['actual'] != case['expected'] for case in cases),
        'externalGameActions': 0,
        'originalInputsModified': False,
        'limitations': 'Auditor acceptance proof only; not a live scanner or full-world oracle.',
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    if args.out.exists():
        raise FileExistsError('Do not overwrite a previous lead observation')
    args.out.write_text(json.dumps(payload, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(payload, indent=2))


if __name__ == '__main__':
    main()
