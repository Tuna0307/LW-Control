#!/usr/bin/env python3
"""A10 isolated startup-stage fault matrix for the production Overview helper.

This test never launches LastWar. It executes run_overview_bridge.run_start and
run_stop against temporary client files, preserving the real backup, recovery
journal, candidate install, rollback and recovery-clear implementation. Only
external process/readiness edges and the candidate payload itself are mocked.
"""
from __future__ import annotations

import hashlib
import json
import sys
import tempfile
from pathlib import Path
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parent
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))
import run_overview_bridge as ov

PROFILE = 'a10_profile'
STARTED_AT = '2026-09-20T07:30:00.0000000Z'
GAME_PID = 42420
LAUNCHER_PID = 42421


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def triplet(paths: dict[str, Path]) -> dict[str, str]:
    return {key: sha256(paths[key]) for key in ('data', 'metadata', 'version')}


def require(value: bool, message: str) -> None:
    if not value:
        raise AssertionError(message)


def fixture(td: str) -> dict[str, Path]:
    root = Path(td)
    install = root / 'install'
    scripts = root / 'scripts'
    runtime = root / 'runtime'
    backup_root = root / 'backups'
    evidence_root = root / 'evidence'
    game = install / 'Game' / 'LastWar.exe'
    launcher = install / 'LastWarLauncher.exe'
    xlua = install / 'Game' / 'LastWar_Data' / 'Plugins' / 'x86_64' / 'xlua.dll'
    assembly = install / 'Game' / 'LastWar_Data' / 'Assemblies' / 'Assembly-CSharp.rdl'
    for file in (game, launcher, xlua, assembly):
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_bytes(('fixture:' + file.name).encode())
    scripts.mkdir(parents=True, exist_ok=True)
    (scripts / 'LWScripts.data').write_bytes(b'baseline-data-v19')
    (scripts / 'LWScripts.txt').write_bytes(b'baseline-metadata')
    (scripts / 'version.txt').write_bytes(b'19')
    return {
        'launcher': launcher, 'game': game, 'xlua': xlua, 'assembly': assembly,
        'data': scripts / 'LWScripts.data', 'metadata': scripts / 'LWScripts.txt',
        'version': scripts / 'version.txt', 'runtime': runtime,
        'backup_root': backup_root, 'evidence_root': evidence_root,
    }


def fake_verify(paths: dict[str, Path], expected: dict[str, str]) -> dict[str, object]:
    now = triplet(paths)
    require(now == expected, f'client triplet differs from baseline: {now} != {expected}')
    return {'packageSha256': now['data'], 'packageSize': paths['data'].stat().st_size}


def fake_candidate(paths: dict[str, Path], directory: Path) -> dict[str, object]:
    directory.mkdir(parents=True, exist_ok=True)
    (directory / paths['data'].name).write_bytes(b'candidate-data')
    (directory / paths['metadata'].name).write_bytes(b'candidate-metadata')
    (directory / paths['version'].name).write_bytes(b'candidate-version')
    return {'fixture': True, 'candidateSha256': sha256(directory / paths['data'].name)}


class FakeLauncher:
    def __init__(self) -> None:
        self.pid = LAUNCHER_PID


def run_case(name: str) -> dict[str, object]:
    with tempfile.TemporaryDirectory(prefix=f'lwbridge-a10-{name}-') as td:
        paths = fixture(td)
        baseline = triplet(paths)
        session1 = f'{name}_failure'
        session2 = f'{name}_retry'
        challenge = 'a' * 64
        game_running = False
        launcher_running = False
        fail_enabled = True
        restore_failures = 0
        launcher_close_calls = 0
        game_close_calls = 0
        stage_history: list[str] = []
        backup_paths: list[Path] = []
        helper_error_files: list[str] = []

        original_make_backup = ov.lr.make_backup
        original_arm_recovery = ov.lr.arm_recovery
        original_install_candidate = ov.lr.install_candidate
        original_restore_backup = ov.lr.restore_backup
        original_update_stage = ov.lr.update_recovery_stage

        def selected(_):
            if game_running:
                return [{'pid': GAME_PID, 'path': str(paths['game']), 'startedAtUtc': STARTED_AT}]
            return []

        def make_backup(p):
            nonlocal fail_enabled
            if name == 'backup' and fail_enabled:
                fail_enabled = False
                raise ov.OverviewBridgeError('A10 injected backup failure')
            value = original_make_backup(p)
            backup_paths.append(value)
            return value

        def arm_recovery(p, backup, request_id):
            nonlocal fail_enabled
            if name == 'arm_recovery' and fail_enabled:
                fail_enabled = False
                raise ov.OverviewBridgeError('A10 injected recovery-arm failure')
            return original_arm_recovery(p, backup, request_id)

        def update_stage(p, state, stage):
            stage_history.append(stage)
            return original_update_stage(p, state, stage)

        def install_candidate(p, candidate, on_stage=None):
            nonlocal fail_enabled
            if name not in {'install_data', 'install_metadata', 'install_version'} or not fail_enabled:
                return original_install_candidate(p, candidate, on_stage)
            stop_after = {'install_data': 1, 'install_metadata': 2, 'install_version': 3}[name]
            for index, key in enumerate(('data', 'metadata', 'version'), start=1):
                ov.lr.copy_atomic(candidate / p[key].name, p[key])
                if on_stage is not None:
                    on_stage(index, key)
                if index == stop_after:
                    fail_enabled = False
                    raise ov.OverviewBridgeError(f'A10 injected failure after install {key}')

        def restore_backup(p, backup, on_stage=None):
            nonlocal restore_failures, fail_enabled
            if name == 'pending_recovery' and fail_enabled:
                restore_failures += 1
                fail_enabled = False
                raise ov.OverviewBridgeError('A10 injected interrupted-recovery restore failure')
            return original_restore_backup(p, backup, on_stage)

        def verify_current(p):
            nonlocal fail_enabled
            if name == 'verify_current' and fail_enabled:
                fail_enabled = False
                raise ov.OverviewBridgeError('A10 injected current-client verification failure')
            return fake_verify(p, baseline)

        def make_candidate(p, directory):
            nonlocal fail_enabled
            if name == 'candidate_build' and fail_enabled:
                fail_enabled = False
                raise ov.OverviewBridgeError('A10 injected candidate-build failure')
            return fake_candidate(p, directory)

        def popen(*args, **kwargs):
            nonlocal launcher_running, fail_enabled
            if name == 'launcher_spawn' and fail_enabled:
                fail_enabled = False
                raise OSError('A10 injected launcher spawn failure')
            launcher_running = True
            return FakeLauncher()

        def close_launcher(_):
            nonlocal launcher_running, launcher_close_calls
            launcher_close_calls += 1
            launcher_running = False

        def await_game(p, deadline, log_offset):
            nonlocal game_running, launcher_running, fail_enabled
            if name == 'game_spawn' and fail_enabled:
                fail_enabled = False
                raise ov.OverviewBridgeError('A10 injected game-spawn failure')
            game_running = True
            # The official launcher is only the handoff owner; once the matching
            # game appears it is no longer a residual process in this fixture.
            launcher_running = False
            return {'pid': GAME_PID, 'path': str(paths['game']), 'startedAtUtc': STARTED_AT}

        def await_ready(p, profile, session, nonce, pid, deadline):
            nonlocal fail_enabled
            if name == 'readiness' and fail_enabled:
                fail_enabled = False
                raise ov.OverviewBridgeError('A10 injected bridge-readiness failure')
            return {
                'schemaVersion': 1, 'bridgeVersion': ov.BRIDGE_VERSION,
                'profileId': profile, 'sessionId': session, 'challenge': nonce,
                'gamePid': pid, 'ready': True, 'messageVisible': True,
                'messageText': ov.MESSAGE, 'readyAt': 1789890000,
            }

        def close_game(p, identity):
            nonlocal game_running, game_close_calls
            game_close_calls += 1
            require(game_running, 'close requested without synthetic game ownership')
            game_running = False
            return {
                'method': 'A10.synthetic.exact-owned-close', 'pid': identity['pid'],
                'path': identity['path'], 'startedAtUtc': identity['startedAtUtc'],
                'accepted': True, 'processExited': True,
            }

        # Create a valid but interrupted prior operation for the pre-start recovery stage.
        if name == 'pending_recovery':
            backup = original_make_backup(paths)
            backup_paths.append(backup)
            state = original_arm_recovery(paths, backup, 'prior_interrupted_session')
            ov.lr.copy_atomic((backup / paths['data'].name), paths['data'])
            paths['data'].write_bytes(b'interrupted-candidate-data')
            original_update_stage(paths, state, 'installed_1_data')
            require(triplet(paths) != baseline, 'pending-recovery fixture did not mutate client bytes')

        if name == 'unmanaged_game':
            game_running = True

        before_failure = triplet(paths)
        failure_error = None
        failure_journal_stage = None

        with patch.object(ov, 'overview_paths', return_value=paths), \
             patch.object(ov.lr, 'selected_game_processes', side_effect=selected), \
             patch.object(ov.lr, 'verify_current', side_effect=verify_current), \
             patch.object(ov.lr, 'make_backup', side_effect=make_backup), \
             patch.object(ov.lr, 'arm_recovery', side_effect=arm_recovery), \
             patch.object(ov.lr, 'update_recovery_stage', side_effect=update_stage), \
             patch.object(ov.lr, 'install_candidate', side_effect=install_candidate), \
             patch.object(ov.lr, 'restore_backup', side_effect=restore_backup), \
             patch.object(ov, 'make_candidate', side_effect=make_candidate), \
             patch.object(ov, 'launcher_log_offset', return_value=0), \
             patch.object(ov.lr.subprocess, 'Popen', side_effect=popen), \
             patch.object(ov, 'close_owned_launcher_process', side_effect=close_launcher), \
             patch.object(ov, 'await_owned_game_process_update_aware', side_effect=await_game), \
             patch.object(ov, 'await_ready', side_effect=await_ready), \
             patch.object(ov.lr, 'close_owned_game_process_for_restore', side_effect=close_game):

            try:
                ov.run_start(PROFILE, session1, challenge, 5, None)
            except Exception as exc:
                failure_error = f'{type(exc).__name__}: {exc}'
            else:
                raise AssertionError(f'{name}: injected failure unexpectedly succeeded')

            journal = ov.lr.read_json(ov.lr.recovery_path(paths))
            if isinstance(journal, dict):
                failure_journal_stage = journal.get('stage')
            helper_error = paths['evidence_root'] / session1 / 'helper-start-error.json'
            if helper_error.is_file():
                helper_error_files.append(helper_error.name)
            failure_game_running = game_running
            failure_launcher_running = launcher_running
            failure_launcher_close_calls = launcher_close_calls
            failure_game_close_calls = game_close_calls

            if name == 'unmanaged_game':
                require(failure_game_running, 'unmanaged process must be preserved by the ownership gate')
                # Release only the synthetic external fixture before the required retry.
                game_running = False
            after_failure = triplet(paths)
            if name == 'pending_recovery':
                require(after_failure != baseline, 'interrupted recovery failure must preserve uncertain bytes/journal')
                require(failure_journal_stage == 'restoring_interrupted_operation',
                        f'pending recovery failure journal stage was {failure_journal_stage!r}')
            else:
                require(after_failure == baseline, f'{name}: failure cleanup did not restore exact baseline')
                require(journal is None, f'{name}: recovery journal remained after clean failure rollback')
                require(not game_running, f'{name}: synthetic owned game leaked after failure')
                require(not launcher_running, f'{name}: synthetic launcher leaked after failure')

            # The second attempt is the required successful retry. The one-shot
            # failure has been consumed; pending recovery is now restored first.
            retry = ov.run_start(PROFILE, session2, challenge, 5, None)
            require(retry.get('ok') is True and retry.get('gameRunning') is True,
                    f'{name}: subsequent retry did not reach owned ready state')
            require(game_running, f'{name}: retry did not own the synthetic game')
            stop = ov.run_stop(PROFILE, session2, GAME_PID, str(paths['game']), None, STARTED_AT)
            require(stop.get('ok') is True and stop.get('gameRunning') is False,
                    f'{name}: successful retry did not close cleanly')
            require(triplet(paths) == baseline, f'{name}: retry close did not restore baseline')
            require(ov.lr.read_json(ov.lr.recovery_path(paths)) is None,
                    f'{name}: retry close left recovery journal')
            require(not game_running and not launcher_running,
                    f'{name}: retry close left synthetic process ownership')

        final_manifests = []
        for backup in backup_paths:
            manifest = ov.lr.read_json(backup / 'manifest.json')
            if isinstance(manifest, dict):
                final_manifests.append({'stage': manifest.get('stage'), 'originalFiles': manifest.get('originalFiles')})
        require(final_manifests and final_manifests[-1]['stage'] == 'restored',
                f'{name}: final retry backup manifest is not restored')
        return {
            'stage': name,
            'failureObserved': failure_error,
            'beforeFailureSha256': before_failure,
            'afterFailureSha256': after_failure,
            'failureJournalStage': failure_journal_stage,
            'helperStartErrorEvidence': helper_error_files,
            'failureProcessState': {
                'gameRunning': failure_game_running,
                'launcherRunning': failure_launcher_running,
                'launcherCloseCalls': failure_launcher_close_calls,
                'ownedGameCloseCalls': failure_game_close_calls,
                'unmanagedProcessPreserved': name == 'unmanaged_game' and failure_game_running,
            },
            'stageHistory': stage_history,
            'successfulSubsequentRetry': True,
            'retryClosedAndRestored': True,
            'finalSha256': triplet(paths),
            'finalRecoveryJournalPresent': False,
            'syntheticGameRunning': game_running,
            'syntheticLauncherRunning': launcher_running,
            'finalBackupManifestStage': final_manifests[-1]['stage'],
            'pendingRecoveryRestoreFailures': restore_failures,
        }


def main() -> int:
    stages = [
        'unmanaged_game',
        'pending_recovery',
        'verify_current',
        'backup',
        'arm_recovery',
        'candidate_build',
        'install_data',
        'install_metadata',
        'install_version',
        'launcher_spawn',
        'game_spawn',
        'readiness',
    ]
    results = [run_case(stage) for stage in stages]
    out = {
        'ok': True,
        'scope': 'A10 isolated production-helper startup failure matrix; no LastWar/launcher process launched',
        'stages': results,
        'stageCount': len(results),
        'allSuccessfulSubsequentRetry': all(item['successfulSubsequentRetry'] for item in results),
        'allFinalRecoveryClear': all(not item['finalRecoveryJournalPresent'] for item in results),
        'allSyntheticProcessesStopped': all(
            not item['syntheticGameRunning'] and not item['syntheticLauncherRunning'] for item in results),
    }
    print(json.dumps(out, indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
