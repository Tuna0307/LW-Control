"""Sole-controller staged FEATURE018 queue client for the R14 City proof.

This tool does not launch/control windows or edit installed scripts. Invoke it
only against the primary controller's already running owned staged-proof app.
Every mutation is sent once; timeout aborts rather than retransmitting. City
restoration uses the settled native job's immutable original-layout witness.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import itertools
import json
import os
from pathlib import Path
import time
import uuid

UID = '4799432499002212'
SERVER = 2212
HISTORICAL_UNACKNOWLEDGED_CITY_UUID = '1392040865799139725'


class StageQueue:
    def __init__(self, directory: Path, result_path: Path | None = None):
        self.directory = directory.resolve(strict=True)
        self.queue = self.directory / 'feature018-live-request.json'
        self.result_path = result_path or self.directory / 'native-ui-map.json'
        stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
        self.evidence_path = self.directory / f'feature018-city-driver-{stamp}.json'
        self.evidence = {'schemaVersion': 2, 'state': 'running', 'operations': []}
        self.save()

    def save(self):
        self.evidence_path.write_text(json.dumps(self.evidence, indent=2), encoding='utf-8')

    def state(self):
        try:
            value = json.loads(self.result_path.read_text(encoding='utf-8-sig'))
        except (OSError, ValueError):
            return None
        game = value.get('game', {})
        if str(game.get('uid')) != UID or game.get('serverId') != SERVER:
            raise RuntimeError('Staged app does not report the exact authorized 2212 UID')
        return value

    def request(self, kind, *, timeout=40, **fields):
        if (str(fields.get('command', '')).startswith('mini_land_') or
                fields.get('command') == 'monopoly_cell_open'):
            raise RuntimeError('R14 forbids all live Land Cell requests')
        state = self.state()
        if state is None or state.get('state') != 'running':
            raise RuntimeError('Owned staged proof is not currently running')
        if self.queue.exists():
            prior = json.loads(self.queue.read_text(encoding='utf-8-sig'))
            if not any(op.get('id') == prior.get('id') for op in state.get('operations', [])):
                raise RuntimeError('Another staged request is outstanding; queue left untouched')
        ident = 'citydriver' + uuid.uuid4().hex
        request = {'id': ident, 'kind': kind, **fields}
        temporary = self.directory / (ident + '.tmp')
        temporary.write_text(json.dumps(request), encoding='utf-8')
        # Atomic single-file replacement; never replay after an ambiguous wait.
        os.replace(temporary, self.queue)
        entry = {'request': request, 'state': 'sent_once'}
        self.evidence['operations'].append(entry)
        self.save()
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            state = self.state()
            if state:
                found = next((op for op in state.get('operations', []) if op.get('id') == ident), None)
                if found is not None:
                    entry['response'] = found
                    entry['state'] = found.get('state')
                    self.save()
                    if found.get('state') != 'returned':
                        raise RuntimeError(f'Native staged operation failed: {found}')
                    return found.get('result')
                if state.get('state') != 'running':
                    break
            time.sleep(.05)
        entry['state'] = 'uncertain_timeout_no_retry'
        self.save()
        raise RuntimeError(f'No correlated result for {ident}; never resend this action')

    def command(self, command, payload=None):
        return self.request('command', command=command, payload=payload or {})

    def snapshot(self):
        return self.command('city_layout_snapshot_get')

    def await_job(self, expected_job_id=None, timeout=45):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            status = self.command('city_layout_apply_status')
            if expected_job_id is not None and status.get('jobId') != expected_job_id:
                raise RuntimeError(
                    f'Native city job identity changed while waiting for {expected_job_id}: {status}')
            if status.get('state') not in ('running', 'cancelling'):
                return status
            time.sleep(.1)
        raise RuntimeError('Native city job did not settle; no mutation retried')


def positions(snapshot):
    return {str(row['uuid']): row['pointId'] for row in snapshot['buildings']}


def placement_payload(snapshot, desired):
    return {'baseRevision': snapshot['layoutRevision'], 'placements': [
        {'uuid': key, 'targetPointId': point} for key, point in sorted(desired.items())]}


def require_exact_positions(snapshot, expected, reason):
    observed = positions(snapshot)
    if observed != expected:
        raise RuntimeError(f'{reason}: expected {expected}, observed {observed}')


def find_single(client, snapshot, excluded):
    """Choose one source-discovered legal empty destination and revalidate it."""
    attempts = 0
    for candidate in snapshot.get('moveCandidates', []):
        uuid_text = str(candidate.get('uuid', ''))
        if uuid_text in excluded:
            continue
        from_point = candidate.get('fromPointId')
        target_point = candidate.get('targetPointId')
        if not isinstance(from_point, int) or not isinstance(target_point, int) or from_point == target_point:
            continue
        attempts += 1
        if attempts > 30:
            break
        desired = {uuid_text: target_point}
        payload = placement_payload(snapshot, desired)
        validation = client.command('city_layout_validate', payload)
        if any(issue.get('code') == 'CITY_LAYOUT_STALE'
               for issue in validation.get('issues', [])):
            raise RuntimeError('City snapshot changed during single-move candidate search; no movement sent')
        planned = validation.get('plannedMoves', [])
        if (validation.get('valid') and validation.get('totalMoves') == 1 and
                validation.get('temporaryMoves') == 0 and len(planned) == 1 and
                str(planned[0].get('uuid')) == uuid_text and
                planned[0].get('fromPointId') == from_point and
                planned[0].get('pointId') == target_point and
                planned[0].get('temporary') is False):
            return desired, payload, validation
    raise RuntimeError('No source-valid single-building City move found in bounded search')


def find_swap(client, snapshot, excluded):
    candidates = [row for row in snapshot['buildings']
                  if row.get('movable') and str(row['uuid']) not in excluded]
    # Bound the read-only validation search. Native zone/secondary ranges,
    # occupied footprints and a real empty buffer remain the authority.
    attempts = 0
    for first, second in itertools.combinations(candidates, 2):
        if (first.get('tileX'), first.get('tileY')) != (second.get('tileX'), second.get('tileY')):
            continue
        attempts += 1
        if attempts > 30:
            break
        desired = {str(first['uuid']): second['pointId'], str(second['uuid']): first['pointId']}
        payload = placement_payload(snapshot, desired)
        validated = client.command('city_layout_validate', payload)
        if validated.get('valid') and validated.get('totalMoves', 0) >= 3 and validated.get('temporaryMoves', 0) >= 1:
            return desired, payload, validated
        if any(issue.get('code') == 'CITY_LAYOUT_STALE' for issue in validated.get('issues', [])):
            raise RuntimeError('City snapshot changed during candidate search; no movement sent')
    raise RuntimeError('No source-valid same-size swap/buffer found in bounded search')


def started_job(result, label):
    if not isinstance(result, dict):
        raise RuntimeError(f'{label} returned a malformed native City result: {result}')
    status = result.get('status', {})
    job_id = status.get('jobId')
    if (result.get('accepted') is not True or result.get('requestDispatched') is not True or
            not isinstance(job_id, str) or not job_id):
        raise RuntimeError(f'{label} did not dispatch an attributable native City job: {result}')
    return job_id


def completed_evidence_count(status):
    """Count only source-attributed native completion evidence.

    Raw reply correlation is strongest.  Current v26 can bypass diagnostic
    reply wrappers, so the City producer also accepts an exact server-applied
    BuildingMove event + authoritative poststate without claiming raw-reply
    causality.  The producer records those paths separately; every completed
    step must be accounted for by exactly one of them.
    """
    raw = status.get('matchingNativeReplies', 0)
    server = status.get('serverAppliedPostStateCount', 0)
    if not isinstance(raw, int) or not isinstance(server, int) or raw < 0 or server < 0:
        raise RuntimeError(f'Native City completion evidence counters are invalid: {status}')
    return raw + server


def apply_and_confirm(client, payload, desired, expected_before, evidence_prefix):
    started = client.command('city_layout_apply_start', payload)
    job_id = started_job(started, evidence_prefix)
    status = client.await_job(job_id)
    if (status.get('state') != 'completed' or status.get('inFlight') or
            status.get('jobId') != job_id or
            status.get('completedMoves') != status.get('totalMoves') or
            status.get('dispatchedMoves') != status.get('totalMoves') or
            completed_evidence_count(status) != status.get('totalMoves')):
        raise RuntimeError(f'{evidence_prefix} native completion was not fully confirmed: {status}')
    expected_after = dict(expected_before)
    expected_after.update(desired)
    after = client.snapshot()
    require_exact_positions(after, expected_after,
                            f'{evidence_prefix} authoritative full-layout comparison failed')
    client.evidence[evidence_prefix + 'Started'] = started
    client.evidence[evidence_prefix + 'Status'] = status
    client.evidence[evidence_prefix + 'After'] = after
    client.save()
    return job_id, after


def restore(client, original, parent_job_id, evidence_prefix):
    """Restore only through the exact settled job's immutable native baseline."""
    preflight = client.command('city_layout_restore_preflight', {'jobId': parent_job_id})
    if (preflight.get('jobId') != parent_job_id or preflight.get('ready') is not True or
            preflight.get('pendingNativeMove') is True or
            preflight.get('historicalUncertaintyInPlan') is True):
        raise RuntimeError(f'{evidence_prefix} original-layout restore preflight rejected: {preflight}')
    if preflight.get('restorationRequired') is not True:
        final = client.snapshot()
        require_exact_positions(final, original,
                                f'{evidence_prefix} claimed no restore while layout differed')
        if preflight.get('originalLayoutConfirmed') is not True:
            raise RuntimeError(
                f'{evidence_prefix} original layout was not affirmatively confirmed: {preflight}')
        client.evidence[evidence_prefix + 'RestorePreflight'] = preflight
        client.evidence[evidence_prefix + 'RestoredSnapshot'] = final
        client.save()
        return final
    if not preflight.get('plannedMoves'):
        raise RuntimeError(f'{evidence_prefix} required restoration without a native plan: {preflight}')
    started = client.command('city_layout_restore_start', {
        'jobId': parent_job_id, 'restoreOptIn': True})
    restore_job_id = started_job(started, evidence_prefix + ' restore')
    start_status = started.get('status', {})
    if (start_status.get('restoration') is not True or
            start_status.get('restoreOfJobId') != parent_job_id):
        raise RuntimeError(f'{evidence_prefix} restore job lost parent provenance: {started}')
    status = client.await_job(restore_job_id)
    if (status.get('state') != 'completed' or status.get('inFlight') or
            status.get('jobId') != restore_job_id or
            status.get('restoration') is not True or
            status.get('restoreOfJobId') != parent_job_id or
            status.get('completedMoves') != status.get('totalMoves') or
            status.get('dispatchedMoves') != status.get('totalMoves') or
            completed_evidence_count(status) != status.get('totalMoves')):
        raise RuntimeError(f'{evidence_prefix} original-layout restoration unconfirmed: {status}')
    final = client.snapshot()
    require_exact_positions(final, original,
                            f'{evidence_prefix} full original city anchor comparison failed')
    final_preflight = client.command(
        'city_layout_restore_preflight', {'jobId': restore_job_id})
    if (final_preflight.get('jobId') != restore_job_id or
            final_preflight.get('ready') is not True or
            final_preflight.get('restorationRequired') is not False or
            final_preflight.get('originalLayoutConfirmed') is not True or
            final_preflight.get('pendingNativeMove') is True):
        raise RuntimeError(
            f'{evidence_prefix} restored native job did not confirm the original layout: {final_preflight}')
    client.evidence[evidence_prefix + 'RestorePreflight'] = preflight
    client.evidence[evidence_prefix + 'RestoreStarted'] = started
    client.evidence[evidence_prefix + 'RestoreStatus'] = status
    client.evidence[evidence_prefix + 'RestoreConfirmed'] = final_preflight
    client.evidence[evidence_prefix + 'RestoredSnapshot'] = final
    client.save()
    return final


def expected_after_planned_moves(original, planned_moves, completed_moves):
    expected = dict(original)
    if not isinstance(completed_moves, int) or completed_moves < 0 or completed_moves > len(planned_moves):
        raise RuntimeError('Cancelled City job reported an invalid completed-move count')
    for step in planned_moves[:completed_moves]:
        expected[str(step['uuid'])] = step['pointId']
    return expected


def city(client, excluded):
    # Do not click the product City tab from the mutation controller. Mounting
    # CityLayoutPage immediately starts its own snapshot/draft/status requests,
    # which would create a second live command producer racing this sole-driver
    # queue. UI/screenshots are captured separately after settled backend state.
    original_snapshot = client.snapshot()
    original = positions(original_snapshot)
    client.evidence['cityBefore'] = original_snapshot
    client.evidence['cityOriginalPositions'] = original
    client.save()

    single_desired, single_payload, single_validation = find_single(
        client, original_snapshot, excluded)
    client.evidence['citySingleValidation'] = single_validation
    client.save()
    single_job_id, _ = apply_and_confirm(
        client, single_payload, single_desired, original, 'citySingle')
    restored = restore(client, original, single_job_id, 'citySingle')

    desired, payload, validation = find_swap(client, restored, excluded)
    touched = set(desired)
    client.evidence['citySwapValidation'] = validation
    client.save()
    swap_job_id, _ = apply_and_confirm(
        client, payload, desired, original, 'citySwap')
    restored = restore(client, original, swap_job_id, 'citySwap')

    # Use the same source-valid pair from the fully restored layout, with a fresh
    # revision. Submit Cancel immediately after start so the receipt records
    # whether a real native Move was in flight at the time Cancel was consumed.
    _, cancel_payload, cancel_validation = find_swap(client, restored, excluded)
    if {p['uuid'] for p in cancel_payload['placements']} != touched:
        raise RuntimeError('Restored candidate pair changed; no cancel-test mutation sent')
    client.evidence['cityCancelValidation'] = cancel_validation
    client.save()
    settled = False
    cancel_job_id = None
    try:
        combined = client.request('cityApplyCancel', payload=cancel_payload)
        started = combined.get('started')
        cancelled = combined.get('cancelled')
        if combined.get('immediateCancel') is not True:
            raise RuntimeError(f'City immediate Cancel harness result malformed: {combined}')
        cancel_job_id = started_job(started, 'cityCancel')
        status = client.await_job(cancel_job_id)
        settled = not status.get('inFlight', True)
        client.evidence['cityCancelReturned'] = cancelled
        client.evidence['cityCancelStatus'] = status
        after = client.snapshot()
        client.evidence['cityCancelAfter'] = after
        client.evidence['cancelInFlightProven'] = (
            cancelled.get('accepted') is True and
            cancelled.get('status', {}).get('jobId') == cancel_job_id and
            cancelled.get('status', {}).get('cancelRequestedWhileInFlight') is True and
            status.get('state') == 'cancelled' and
            status.get('jobId') == cancel_job_id and not status.get('inFlight') and
            status.get('cancelRequested') is True and
            status.get('completedMoves') == status.get('dispatchedMoves') and
            completed_evidence_count(status) == status.get('completedMoves') and
            0 < status.get('dispatchedMoves', 0) < status.get('totalMoves', 0))
        if not client.evidence['cancelInFlightProven']:
            raise RuntimeError(f'Cancel did not stop a real in-flight multi-move City job: {cancelled}; {status}')
        expected_cancelled = expected_after_planned_moves(
            original, cancel_validation.get('plannedMoves', []), status['completedMoves'])
        require_exact_positions(after, expected_cancelled,
                                'Cancelled City authoritative partial layout mismatch')
        client.save()
    finally:
        if settled and cancel_job_id is not None:
            restore(client, original, cancel_job_id, 'cityCancel')
            client.evidence['restoredOriginalPositions'] = True
            client.evidence['task6EndToEndProven'] = client.evidence.get('cancelInFlightProven') is True
            client.save()
        else:
            client.evidence['restorationState'] = 'PENDING_UNSETTLED_NATIVE_JOB_NO_CONFLICTING_SEND'
            client.save()


def land(client, excluded):
    client.request('tab', index=6)
    before = client.command('mini_land_status')
    target = before.get('cellId')
    if before.get('state') != 'locked' or not isinstance(target, int) or target in excluded:
        raise RuntimeError(f'Fresh land target is unavailable/excluded: {before}')
    exact = client.command('mini_land_status', {'expectedCellId': target})
    if exact.get('state') != 'locked' or not exact.get('exactCellLookup'):
        raise RuntimeError('Exact fresh target is no longer locked')
    client.evidence['landBefore'] = exact
    client.save()
    sent = client.command('monopoly_cell_open', {'expectedCellId': target})
    client.evidence['landRequest'] = sent
    client.save()
    deadline = time.monotonic() + 20
    while time.monotonic() < deadline:
        after = client.command('mini_land_status', {'expectedCellId': target})
        client.evidence['landAfter'] = after
        client.save()
        if after.get('state') == 'unlocked':
            client.evidence['landCausalityProven'] = after.get('requestProof', {}).get('requestCausalityConfirmed') is True
            break
        time.sleep(.2)
    # Event observation is read-only. A thrown unsupported result is retained
    # in the native staged receipt with its native reason; never starts a game.
    try:
        client.evidence['foodHouse'] = client.command('mini_sheep_status')
    except RuntimeError as error:
        client.evidence['foodHouseError'] = str(error)
    client.save()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--result-path', type=Path)
    parser.add_argument('--stage', choices=('city',), required=True)
    parser.add_argument('--exclude-city-uuid', action='append', default=[HISTORICAL_UNACKNOWLEDGED_CITY_UUID])
    parser.add_argument('--exclude-land-id', action='append', type=int, default=[5])
    args = parser.parse_args()
    client = StageQueue(args.directory, args.result_path)
    try:
        if args.stage == 'city':
            city(client, set(args.exclude_city_uuid))
        else:
            land(client, set(args.exclude_land_id))
        client.evidence['state'] = 'returned'
    except Exception as error:
        client.evidence['state'] = 'partial'
        client.evidence['error'] = str(error)
        raise
    finally:
        client.save()
        print(client.evidence_path)


if __name__ == '__main__':
    main()
