"""Sole-controller staged FEATURE018 queue client and bounded City/Land proofs.

This tool does not launch/control windows or edit installed scripts. Invoke it
only against the primary controller's already running owned staged-proof app.
Every mutation is sent once; timeout aborts rather than retransmitting. City
restoration is attempted only after the current native job has settled.
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

UID = '2553945035002288'
SERVER = 2288
HISTORICAL_UNACKNOWLEDGED_CITY_UUID = '1392040865799139725'


class StageQueue:
    def __init__(self, directory: Path, result_path: Path | None = None):
        self.directory = directory.resolve(strict=True)
        self.queue = self.directory / 'feature018-live-request.json'
        self.result_path = result_path or self.directory / 'native-ui-map.json'
        stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
        self.evidence_path = self.directory / f'feature018-city-driver-{stamp}.json'
        self.evidence = {'schemaVersion': 1, 'state': 'running', 'operations': []}
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
            raise RuntimeError('Staged app does not report the exact authorized 2288 UID')
        return value

    def request(self, kind, *, timeout=40, **fields):
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

    def await_job(self, timeout=45):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            status = self.command('city_layout_apply_status')
            if status.get('state') not in ('running', 'cancelling'):
                return status
            time.sleep(.1)
        raise RuntimeError('Native city job did not settle; no mutation retried')


def positions(snapshot):
    return {str(row['uuid']): row['pointId'] for row in snapshot['buildings']}


def placement_payload(snapshot, desired):
    return {'baseRevision': snapshot['layoutRevision'], 'placements': [
        {'uuid': key, 'targetPointId': point} for key, point in desired.items()]}


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


def restore(client, original, touched):
    latest = client.snapshot()
    current = positions(latest)
    desired = {key: original[key] for key in touched if current.get(key) != original[key]}
    if desired:
        payload = placement_payload(latest, desired)
        validation = client.command('city_layout_validate', payload)
        if not validation.get('valid'):
            raise RuntimeError(f'Original-layout restoration validation rejected: {validation}')
        client.command('city_layout_apply_start', payload)
        status = client.await_job()
        if status.get('state') != 'completed' or status.get('inFlight'):
            raise RuntimeError(f'Original-layout restoration unconfirmed: {status}')
    final = client.snapshot()
    after = positions(final)
    if any(after.get(key) != value for key, value in original.items()):
        raise RuntimeError('Full original city anchor comparison failed after restoration')
    client.evidence['restoredOriginalPositions'] = True
    client.save()
    return final


def city(client, excluded):
    client.request('tab', index=4)
    original_snapshot = client.snapshot()
    original = positions(original_snapshot)
    desired, payload, validation = find_swap(client, original_snapshot, excluded)
    touched = set(desired)
    client.evidence['cityBefore'] = original_snapshot
    client.evidence['citySwapValidation'] = validation
    client.save()
    settled = False
    try:
        client.command('city_layout_apply_start', payload)
        status = client.await_job()
        settled = not status.get('inFlight', True)
        after = client.snapshot()
        client.evidence['citySwapAfter'] = after
        client.evidence['citySwapStatus'] = status
        if status.get('state') != 'completed' or any(positions(after).get(k) != v for k, v in desired.items()):
            raise RuntimeError('Swap authoritative poststate did not match the validated plan')
        restored = restore(client, original, touched)
        # Use the same validated pair, fresh revision. Submit Cancel immediately
        # on start result; timing is observed, never invented or forced by mocks.
        _, cancel_payload, cancel_validation = find_swap(client, restored, excluded)
        if {p['uuid'] for p in cancel_payload['placements']} != touched:
            raise RuntimeError('Restored candidate pair changed; no cancel-test mutation sent')
        client.evidence['cityCancelValidation'] = cancel_validation
        settled = False
        started = client.command('city_layout_apply_start', cancel_payload)
        cancelled = client.command('city_layout_apply_cancel', {'jobId': started['status']['jobId']})
        status = client.await_job()
        settled = not status.get('inFlight', True)
        client.evidence['cityCancelReturned'] = cancelled
        client.evidence['cityCancelStatus'] = status
        client.evidence['cityCancelAfter'] = client.snapshot()
        client.evidence['cancelInFlightProven'] = (
            cancelled.get('status', {}).get('cancelRequestedWhileInFlight') is True and
            status.get('state') == 'cancelled' and
            status.get('dispatchedMoves', 0) < status.get('totalMoves', 0))
        client.save()
    finally:
        if settled:
            restore(client, original, touched)
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
    parser.add_argument('--stage', choices=('city', 'land'), required=True)
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
