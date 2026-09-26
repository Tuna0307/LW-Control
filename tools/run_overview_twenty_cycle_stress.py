#!/usr/bin/env python3
"""Run A09: twenty consecutive production Home lifecycle cycles.

The existing --live-overview-home-proof performs two real production lifecycle
cycles: one manual Start/Close and one startup-reconcile Start/Close. This
runner repeats that proven path and correlates every returned session to the
host start/stop evidence written by OverviewLifecycleService.

No gameplay action is issued. A failed cycle is not retried by this runner;
only the product's own bounded launch policy applies.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def require(value: bool, message: str) -> None:
    if not value:
        raise RuntimeError(message)


def running_lastwar_processes() -> list[str]:
    # tasklist is available on supported Windows clients and needs no extra
    # Python dependency. Keep only process names; never persist user paths.
    result = subprocess.run(
        ['tasklist', '/FO', 'CSV', '/NH'],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
        timeout=20, check=False)
    if result.returncode != 0:
        raise RuntimeError('tasklist failed: ' + result.stderr.strip())
    names: list[str] = []
    for line in result.stdout.splitlines():
        if not line.startswith('"'):
            continue
        name = line.split('","', 1)[0].strip('"')
        lower = name.lower()
        if lower == 'lastwar.exe' or 'lastwar' in lower and 'launcher' in lower:
            names.append(name)
    return names


def file_identity(path: Path) -> dict[str, object]:
    require(path.is_file(), f'missing lifecycle baseline file: {path.name}')
    return {'name': path.name, 'size': path.stat().st_size, 'sha256': sha256(path)}


def parse_proof_output(stdout: str) -> dict:
    text = stdout.strip()
    require(bool(text), 'live overview proof returned empty stdout')
    try:
        value = json.loads(text)
    except json.JSONDecodeError as exc:
        raise RuntimeError('live overview proof stdout was not one JSON document') from exc
    require(isinstance(value, dict) and value.get('diagnosticOk') is True,
            'live overview proof did not report diagnosticOk=true')
    results = value.get('results')
    require(isinstance(results, list) and len(results) == 2,
            'live overview proof must return exactly manual + auto-start results')
    return value


def check_session(evidence_root: Path, result: dict, baseline: dict[str, dict[str, object]]) -> dict:
    mode = result.get('mode')
    session = str(result.get('instanceId') or '')
    pid = int(result.get('pid') or 0)
    require(mode in ('manual', 'auto-start'), f'unsupported proof mode: {mode!r}')
    require(len(session) == 32, f'{mode}: invalid owned session id')
    require(pid > 0, f'{mode}: invalid game pid')
    require(result.get('connectionState') == 'connected', f'{mode}: did not reach connected')

    directory = evidence_root / session
    start_path = directory / 'host-start.json'
    stop_path = directory / 'host-stop.json'
    require(start_path.is_file(), f'{mode}: host-start evidence missing')
    require(stop_path.is_file(), f'{mode}: host-stop evidence missing')
    start = json.loads(start_path.read_text(encoding='utf-8'))
    stop = json.loads(stop_path.read_text(encoding='utf-8'))
    require(start.get('sessionId') == session and int(start.get('gamePid') or 0) == pid,
            f'{mode}: host-start identity mismatch')
    helper = stop.get('helper') or {}
    require(stop.get('sessionId') == session and helper.get('sessionId') == session,
            f'{mode}: host-stop identity mismatch')
    require(helper.get('ok') is True and helper.get('mode') == 'overview_exact_pid_close_restore',
            f'{mode}: stop helper did not report exact owned close/restore')
    require(int(helper.get('gamePid') or 0) == pid, f'{mode}: stop pid mismatch')
    close = helper.get('close') or {}
    restore = helper.get('restore') or {}
    require(close.get('processExited') is True, f'{mode}: owned game did not exit')
    require(restore.get('restored') is True, f'{mode}: original client was not restored')
    require(helper.get('gameRunning') is False, f'{mode}: helper still saw game running after close')
    require(helper.get('installedFilesChanged') is False,
            f'{mode}: installed client remained modified after close')
    require(restore.get('packageSha256') == baseline['LWScripts.data']['sha256'],
            f'{mode}: restored package hash differs from baseline')
    originals = restore.get('originalFiles') or {}
    restored = restore.get('restoredFiles') or {}
    key_by_name = {'LWScripts.data': 'data', 'LWScripts.txt': 'metadata', 'version.txt': 'version'}
    for name, key in key_by_name.items():
        expected = baseline[name]
        require((originals.get(key) or {}).get('sha256') == expected['sha256'],
                f'{mode}: original {name} hash differs from baseline')
        require((restored.get(key) or {}).get('sha256') == expected['sha256'],
                f'{mode}: restored {name} hash differs from baseline')
    return {
        'mode': mode,
        'profileId': start.get('profileId'),
        'sessionId': session,
        'gamePid': pid,
        'connectionState': result.get('connectionState'),
        'startRecordedAtUtc': start.get('recordedAtUtc'),
        'stopRecordedAtUtc': stop.get('recordedAtUtc'),
        'closeMethod': close.get('method'),
        'processExited': close.get('processExited'),
        'restored': restore.get('restored'),
        'restoredPackageSha256': restore.get('packageSha256'),
        'installedFilesChangedAfterClose': helper.get('installedFilesChanged'),
        'helperGameRunningAfterClose': helper.get('gameRunning'),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('--cycles', type=int, default=20)
    parser.add_argument('--checks-exe', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    require(args.cycles > 0 and args.cycles % 2 == 0,
            'cycles must be a positive even number because each proof run owns manual + auto-start cycles')

    root = Path(__file__).resolve().parents[1]
    checks_exe = (args.checks_exe or (
        root / 'tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.exe')).resolve()
    require(checks_exe.is_file(), f'missing Release checks executable: {checks_exe}')
    desktop_dll = checks_exe.with_name('LWBridge.Desktop.dll')
    helper = checks_exe.parent / 'OverviewBridge/run_overview_bridge_current.py'
    require(desktop_dll.is_file() and helper.is_file(), 'Release lifecycle runtime files are missing')

    local_app_data = Path(os.environ['LOCALAPPDATA'])
    local_low = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow'
    evidence_root = local_app_data / 'LWBridgeRebuild/overview-evidence'
    scripts_root = local_low / 'FunFly/Last War-Survival Game/lwScripts'
    baseline_paths = [scripts_root / 'LWScripts.data', scripts_root / 'LWScripts.txt', scripts_root / 'version.txt']
    baseline = {path.name: file_identity(path) for path in baseline_paths}

    initial_processes = running_lastwar_processes()
    require(not initial_processes, 'Last War/launcher must be stopped before A09: ' + ', '.join(initial_processes))

    started = datetime.now(timezone.utc)
    cycles: list[dict] = []
    seen_sessions: set[str] = set()
    proof_runs: list[dict] = []
    failure: dict | None = None

    for iteration in range(1, args.cycles // 2 + 1):
        print(f'A09 proof run {iteration}/{args.cycles // 2}: starting manual + auto-start cycles', flush=True)
        run_started = time.monotonic()
        proc = subprocess.run(
            [str(checks_exe), '--live-overview-home-proof'],
            cwd=str(root), capture_output=True, text=True, encoding='utf-8', errors='replace',
            timeout=11 * 60, check=False)
        elapsed = round(time.monotonic() - run_started, 3)
        if proc.returncode != 0:
            failure = {
                'proofRun': iteration,
                'exitCode': proc.returncode,
                'stderrTail': proc.stderr.strip()[-1200:],
                'stdoutTail': proc.stdout.strip()[-1200:],
            }
            break
        try:
            proof = parse_proof_output(proc.stdout)
            run_cycles = []
            for result in proof['results']:
                cycle = check_session(evidence_root, result, baseline)
                require(cycle['sessionId'] not in seen_sessions, 'session id was reused across lifecycle cycles')
                seen_sessions.add(cycle['sessionId'])
                cycle['cycle'] = len(cycles) + 1
                cycles.append(cycle)
                run_cycles.append(cycle['cycle'])
            residual = running_lastwar_processes()
            require(not residual, 'residual Last War/launcher process after proof run: ' + ', '.join(residual))
            for path in baseline_paths:
                now = file_identity(path)
                require(now == baseline[path.name], f'{path.name} changed after proof run {iteration}')
            proof_runs.append({'proofRun': iteration, 'elapsedSeconds': elapsed, 'cycles': run_cycles})
            print(f'A09 proof run {iteration}: PASS cycles {run_cycles[0]}-{run_cycles[-1]} ({elapsed:.1f}s)', flush=True)
        except Exception as exc:
            failure = {'proofRun': iteration, 'error': str(exc)}
            break

    final_processes = running_lastwar_processes()
    final_files = {path.name: file_identity(path) for path in baseline_paths}
    completed = len(cycles) == args.cycles and failure is None
    result = {
        'ok': completed,
        'findingId': 'LWB-R7-089',
        'acceptanceCase': 'A09',
        'title': 'Twenty consecutive Home lifecycle cycles',
        'startedAtUtc': started.isoformat(),
        'finishedAtUtc': datetime.now(timezone.utc).isoformat(),
        'requestedCycles': args.cycles,
        'completedCycles': len(cycles),
        'proofRuns': proof_runs,
        'cycles': cycles,
        'sourceIdentity': {
            'checksExe': {'size': checks_exe.stat().st_size, 'sha256': sha256(checks_exe)},
            'desktopDll': {'size': desktop_dll.stat().st_size, 'sha256': sha256(desktop_dll)},
            'overviewHelper': {'size': helper.stat().st_size, 'sha256': sha256(helper)},
        },
        'clientBaseline': baseline,
        'clientFinal': final_files,
        'allSessionsUnique': len(seen_sessions) == len(cycles),
        'finalProcesses': final_processes,
        'failure': failure,
        'acceptance': {
            'everyStartReachedConnected': completed and all(c['connectionState'] == 'connected' for c in cycles),
            'everyStopExitedOwnedProcess': completed and all(c['processExited'] is True for c in cycles),
            'everyStopRestoredOriginalFiles': completed and all(c['restored'] is True and c['installedFilesChangedAfterClose'] is False for c in cycles),
            'noResidualGameOrLauncher': not final_processes,
            'baselineFilesExactAfterStress': final_files == baseline,
        },
        'safety': {
            'gameplayActionsPerformed': False,
            'mapScanPerformed': False,
            'claimOrCollectPerformed': False,
            'plunderOrAttackPerformed': False,
            'messageOrSharePerformed': False,
        },
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({
        'ok': result['ok'],
        'completedCycles': result['completedCycles'],
        'requestedCycles': result['requestedCycles'],
        'finalProcesses': result['finalProcesses'],
        'failure': result['failure'],
        'acceptance': result['acceptance'],
    }, indent=2), flush=True)
    return 0 if completed else 1


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(json.dumps({'ok': False, 'fatal': str(exc)}, indent=2), file=sys.stderr)
        raise
