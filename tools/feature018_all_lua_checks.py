"""Run every inert FEATURE018 check, including the original-package roundtrip.

This runner never launches the game. Each child keeps its complete output in a
separate log, and failure stops the gate before a commit can be considered.
"""
from pathlib import Path
import argparse
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-dir', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    original = args.original_dir.resolve(strict=True)
    args.output.mkdir(parents=True, exist_ok=False)
    checks = sorted(path for path in (ROOT / 'tools').glob('feature018_*_checks.py')
                    if path.resolve() != Path(__file__).resolve())
    for path in checks:
        command = [sys.executable, str(path)]
        if path.name == 'feature018_package_checks.py':
            command += ['--original-dir', str(original)]
        with (args.output / (path.stem + '.log')).open('w', encoding='utf-8') as log:
            result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
        print(('PASS ' if result.returncode == 0 else 'FAIL ') + path.name, flush=True)
        if result.returncode:
            raise SystemExit(result.returncode)
    print(f'FEATURE018_ALL_INERT_SUITES_PASS count={len(checks)}', flush=True)


if __name__ == '__main__':
    main()
