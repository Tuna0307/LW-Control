"""Inert checks for the R14 staged City live driver orchestration.

These checks exercise driver sequencing only. Native City placement rules,
serializer/reply attribution and Lua state-machine behavior remain covered by
feature018_city_game_checks.py and feature018_city_swap_checks.py.
"""
from __future__ import annotations

import json
from pathlib import Path
import tempfile

import feature018_city_live_driver as driver


A = '1392040807787722942'
B = '1392040807787722943'
ORIGINAL = {A: 4242, B: 4442}


def check(condition, message):
    if not condition:
        raise AssertionError('FEATURE018_CITY_LIVE_DRIVER: ' + message)


class FakeClient:
    def __init__(self):
        self.current = dict(ORIGINAL)
        self.evidence = {'operations': []}
        self.jobs = {}
        self.sequence = 0
        self.swap_starts = 0
        self.command_history = []
        self.tab_requests = []
        self.saves = 0

    def save(self):
        self.saves += 1

    def request(self, kind, **fields):
        if kind == 'cityApplyCancel':
            started = self.command('city_layout_apply_start', fields['payload'])
            job_id = started['status']['jobId']
            cancelled = self.command('city_layout_apply_cancel', {'jobId': job_id})
            return {'started': started, 'cancelled': cancelled,
                    'immediateCancel': True}
        self.tab_requests.append((kind, fields))
        return True

    def revision(self):
        return f'rev-{self.current[A]}-{self.current[B]}'

    def snapshot(self):
        return {
            'layoutRevision': self.revision(),
            'isInCity': True,
            'buildings': [
                {'uuid': A, 'pointId': self.current[A], 'movable': True,
                 'tileX': 2, 'tileY': 2},
                {'uuid': B, 'pointId': self.current[B], 'movable': True,
                 'tileX': 2, 'tileY': 2},
            ],
            'moveCandidates': [
                {'uuid': A, 'fromPointId': self.current[A], 'targetPointId': 4244},
                {'uuid': B, 'fromPointId': self.current[B], 'targetPointId': 4444},
            ] if self.current == ORIGINAL else [],
        }

    def _swap_plan(self):
        return [
            {'index': 1, 'uuid': A, 'fromPointId': 4242, 'pointId': 4244,
             'temporary': True},
            {'index': 2, 'uuid': B, 'fromPointId': 4442, 'pointId': 4242,
             'temporary': False},
            {'index': 3, 'uuid': A, 'fromPointId': 4244, 'pointId': 4442,
             'temporary': False},
        ]

    def _validation(self, payload):
        if payload.get('baseRevision') != self.revision():
            return {'valid': False, 'issues': [{'code': 'CITY_LAYOUT_STALE'}],
                    'totalMoves': 0, 'temporaryMoves': 0, 'plannedMoves': []}
        placements = {str(row['uuid']): row['targetPointId']
                      for row in payload.get('placements', [])}
        if self.current == ORIGINAL and placements == {A: 4244}:
            return {'valid': True, 'issues': [], 'totalMoves': 1,
                    'temporaryMoves': 0, 'plannedMoves': [{
                        'index': 1, 'uuid': A, 'fromPointId': 4242,
                        'pointId': 4244, 'temporary': False}]}
        if self.current == ORIGINAL and placements == {A: 4442, B: 4242}:
            return {'valid': True, 'issues': [], 'totalMoves': 3,
                    'temporaryMoves': 1, 'plannedMoves': self._swap_plan()}
        return {'valid': False, 'issues': [{'code': 'CITY_LAYOUT_OVERLAP'}],
                'totalMoves': 0, 'temporaryMoves': 0, 'plannedMoves': []}

    def _new_job(self, prefix, original, plan, terminal):
        self.sequence += 1
        job_id = f'{prefix}{self.sequence}'
        self.jobs[job_id] = {'original': dict(original), 'plan': list(plan),
                             'terminal': terminal}
        return job_id

    @staticmethod
    def _completed(job_id, count, *, restoration=False, restore_of=None):
        return {
            'jobId': job_id, 'state': 'completed', 'inFlight': False,
            'totalMoves': count, 'completedMoves': count,
            'dispatchedMoves': count, 'matchingNativeReplies': count,
            'restoration': restoration, 'restoreOfJobId': restore_of,
        }

    def command(self, command, payload=None):
        payload = payload or {}
        self.command_history.append((command, json.loads(json.dumps(payload))))
        if command == 'city_layout_validate':
            return self._validation(payload)
        if command == 'city_layout_apply_start':
            validation = self._validation(payload)
            check(validation['valid'], 'fake received an invalid generic apply')
            before = dict(self.current)
            placements = {str(row['uuid']): row['targetPointId']
                          for row in payload['placements']}
            if len(placements) == 1:
                self.current.update(placements)
                job_id = self._new_job(
                    'apply', before, validation['plannedMoves'],
                    self._completed('pending', 1))
                self.jobs[job_id]['terminal']['jobId'] = job_id
            else:
                self.swap_starts += 1
                if self.swap_starts == 1:
                    self.current.update(placements)
                    job_id = self._new_job(
                        'apply', before, validation['plannedMoves'],
                        self._completed('pending', 3))
                    self.jobs[job_id]['terminal']['jobId'] = job_id
                else:
                    # The Cancel proof starts with exactly the first native
                    # Move in flight; its authoritative effect is observed when
                    # the subsequent Cancel settles in this inert coordinator.
                    job_id = self._new_job('cancel', before,
                                           validation['plannedMoves'], None)
            return {'accepted': True, 'requestDispatched': True, 'status': {
                'jobId': job_id, 'state': 'running', 'inFlight': True,
                'totalMoves': len(validation['plannedMoves']),
                'completedMoves': 0, 'dispatchedMoves': 1,
                'matchingNativeReplies': 0,
            }}
        if command == 'city_layout_apply_cancel':
            job_id = payload['jobId']
            job = self.jobs[job_id]
            first = job['plan'][0]
            self.current[str(first['uuid'])] = first['pointId']
            job['terminal'] = {
                'jobId': job_id, 'state': 'cancelled', 'inFlight': False,
                'totalMoves': len(job['plan']), 'completedMoves': 1,
                'dispatchedMoves': 1, 'matchingNativeReplies': 1,
                'cancelRequested': True, 'cancelRequestedWhileInFlight': True,
            }
            return {'accepted': True, 'status': {
                'jobId': job_id, 'state': 'cancelling', 'inFlight': True,
                'totalMoves': len(job['plan']), 'completedMoves': 0,
                'dispatchedMoves': 1, 'matchingNativeReplies': 0,
                'cancelRequested': True, 'cancelRequestedWhileInFlight': True,
            }}
        if command == 'city_layout_restore_preflight':
            job_id = payload['jobId']
            job = self.jobs[job_id]
            original = job['original']
            changed = [uuid for uuid, point in original.items()
                       if self.current.get(uuid) != point]
            if not changed:
                return {'jobId': job_id, 'ready': True,
                        'restorationRequired': False,
                        'originalLayoutConfirmed': True,
                        'pendingNativeMove': False,
                        'historicalUncertaintyInPlan': False,
                        'plannedMoves': []}
            plan = [{'index': index + 1, 'uuid': uuid,
                     'fromPointId': self.current[uuid], 'pointId': original[uuid],
                     'temporary': False}
                    for index, uuid in enumerate(changed)]
            return {'jobId': job_id, 'ready': True,
                    'restorationRequired': True,
                    'originalLayoutConfirmed': False,
                    'pendingNativeMove': False,
                    'historicalUncertaintyInPlan': False,
                    'plannedMoves': plan, 'totalMoves': len(plan)}
        if command == 'city_layout_restore_start':
            parent_id = payload['jobId']
            check(payload.get('restoreOptIn') is True,
                  'restore_start lacked explicit boolean opt-in')
            parent = self.jobs[parent_id]
            preflight = self.command('city_layout_restore_preflight', {'jobId': parent_id})
            check(preflight['restorationRequired'], 'restore_start received an already-original layout')
            before = dict(self.current)
            self.current = dict(parent['original'])
            count = len(preflight['plannedMoves'])
            child_id = self._new_job('restore', parent['original'],
                                     preflight['plannedMoves'], None)
            terminal = self._completed(child_id, count, restoration=True,
                                       restore_of=parent_id)
            self.jobs[child_id]['terminal'] = terminal
            return {'accepted': True, 'requestDispatched': True, 'status': {
                'jobId': child_id, 'state': 'running', 'inFlight': True,
                'totalMoves': count, 'completedMoves': 0,
                'dispatchedMoves': 1, 'matchingNativeReplies': 0,
                'restoration': True, 'restoreOfJobId': parent_id,
            }}
        if command == 'city_layout_apply_status':
            raise AssertionError('FakeClient.await_job should own status polling')
        raise AssertionError('Unexpected fake command: ' + command)

    def await_job(self, expected_job_id=None, timeout=45):
        del timeout
        check(expected_job_id in self.jobs, 'driver waited for an unknown job')
        terminal = self.jobs[expected_job_id]['terminal']
        check(terminal is not None, 'driver waited before the inert job had settled')
        return dict(terminal)


def test_full_task6_sequence():
    client = FakeClient()
    driver.city(client, set())
    check(client.current == ORIGINAL, 'full Task 6 flow did not end at exact original anchors')
    check(client.tab_requests == [],
          'City mutation controller opened a UI tab and created a competing live command producer')
    check(client.evidence.get('task6EndToEndProven') is True and
          client.evidence.get('restoredOriginalPositions') is True and
          client.evidence.get('cancelInFlightProven') is True,
          'end-to-end evidence flags were not earned')
    commands = [name for name, _ in client.command_history]
    check(commands.count('city_layout_apply_start') == 3,
          'Task 6 should send one single Apply, one swap Apply and one cancel-test Apply')
    check(commands.count('city_layout_restore_start') == 3,
          'each mutating Task 6 phase must use an owner-bound native restore')
    check(not any(name.startswith('mini_land_') or name == 'monopoly_cell_open'
                  for name in commands), 'City driver attempted a Land Cell operation')
    apply_payloads = [payload for name, payload in client.command_history
                      if name == 'city_layout_apply_start']
    check(len(apply_payloads[0]['placements']) == 1 and
          len(apply_payloads[1]['placements']) == 2 and
          apply_payloads[1]['placements'] == apply_payloads[2]['placements'],
          'generic Apply calls were not exactly single/swap/cancel-swap')
    check(all(payload.get('restoreOptIn') is True
              for name, payload in client.command_history
              if name == 'city_layout_restore_start'),
          'native restore did not carry fresh explicit consent')


class RejectingRestoreClient:
    def __init__(self):
        self.evidence = {}
        self.commands = []

    def command(self, command, payload=None):
        self.commands.append(command)
        if command == 'city_layout_restore_preflight':
            return {'jobId': 'job1', 'ready': False,
                    'restorationRequired': True, 'pendingNativeMove': False,
                    'historicalUncertaintyInPlan': False, 'plannedMoves': []}
        raise AssertionError('unsafe restore command reached after rejected preflight')

    def save(self):
        pass


def test_restore_preflight_blocks_send():
    client = RejectingRestoreClient()
    try:
        driver.restore(client, ORIGINAL, 'job1', 'blocked')
    except RuntimeError as error:
        check('restore preflight rejected' in str(error),
              'rejected restore produced the wrong failure')
    else:
        raise AssertionError('FEATURE018_CITY_LIVE_DRIVER: rejected restore was accepted')
    check(client.commands == ['city_layout_restore_preflight'],
          'restore_start escaped after a rejected read-only preflight')


def test_land_commands_never_enter_stage_queue():
    with tempfile.TemporaryDirectory() as temporary:
        root = Path(temporary)
        result = root / 'native-ui-map.json'
        result.write_text(json.dumps({
            'state': 'running',
            'game': {'uid': driver.UID, 'serverId': driver.SERVER},
            'operations': [],
        }), encoding='utf-8')
        client = driver.StageQueue(root, result)
        for command in ('mini_land_status', 'monopoly_cell_open'):
            try:
                client.command(command, {'expectedCellId': 6})
            except RuntimeError as error:
                check('forbids all live Land Cell requests' in str(error),
                      'Land guard returned the wrong failure')
            else:
                raise AssertionError('FEATURE018_CITY_LIVE_DRIVER: Land command entered staged queue')
        check(not (root / 'feature018-live-request.json').exists(),
              'blocked Land command created a staged request file')


def main():
    test_full_task6_sequence()
    test_restore_preflight_blocks_send()
    test_land_commands_never_enter_stage_queue()
    print('FEATURE018_CITY_LIVE_DRIVER_CHECKS_PASS')


if __name__ == '__main__':
    main()
