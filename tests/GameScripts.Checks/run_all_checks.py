"""Run every inert game-script check in this folder.

No check launches the game, edits installed game files or sends network traffic.
Each child's complete output goes to a separate log in --output.

Usage:
  python tests/GameScripts.Checks/run_all_checks.py --output <new-log-dir>
      [--original-dir <hash-verified original game package dir>]

Checks that need Windows (msvcrt), the local artifacts/map-013-lua-site Lua
build, or --original-dir are reported as SKIP when their prerequisite is
missing; pass --strict to treat those as failures (use on the Windows dev box).
"""
from pathlib import Path
import argparse
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
PATTERNS = ('feature018_*_checks.py', 'home_*_checks.py', 'r29_*_checks.py', 'check_*.py', 'test_*.py')
SKIP_MARKERS = ("No module named 'msvcrt'", 'map-013-lua-site', 'No module named \'lua53\'')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-dir', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--strict', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    checks = sorted({path for pattern in PATTERNS for path in HERE.glob(pattern)})
    failed = skipped = 0
    for path in checks:
        command = [sys.executable, str(path)]
        if path.name == 'feature018_package_checks.py':
            if args.original_dir is None:
                print('SKIP ' + path.name + ' (needs --original-dir)', flush=True)
                skipped += 1
                continue
            command += ['--original-dir', str(args.original_dir.resolve(strict=True))]
        log_path = args.output / (path.stem + '.log')
        with log_path.open('w', encoding='utf-8') as log:
            result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
        if result.returncode == 0:
            print('PASS ' + path.name, flush=True)
            continue
        text = log_path.read_text(encoding='utf-8', errors='replace')
        if not args.strict and any(marker in text for marker in SKIP_MARKERS):
            print('SKIP ' + path.name + ' (Windows/local prerequisite missing)', flush=True)
            skipped += 1
        else:
            print('FAIL ' + path.name, flush=True)
            failed += 1
    print(f'GAME_SCRIPT_CHECKS total={len(checks)} failed={failed} skipped={skipped}', flush=True)
    raise SystemExit(1 if failed else 0)


if __name__ == '__main__':
    main()
