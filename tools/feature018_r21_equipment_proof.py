"""One-shot native Equipment Schemes save/apply/restore proof; no desktop input."""
import argparse
import copy
import json
import time
from pathlib import Path
from feature018_live_queue import submit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    root = parser.parse_args().directory.resolve(strict=True)
    output = root / 'r21-equipment-proof.json'
    if output.exists():
        raise FileExistsError('Preserve the one-shot proof; no action replay')
    evidence = {'state': 'running', 'operations': []}

    def save():
        output.write_text(json.dumps(evidence, indent=2), encoding='utf-8')

    def operation(kind, command, payload=None):
        result = submit(root, {'kind': kind, 'command': command, 'payload': payload or {}}, timeout=40)
        evidence['operations'].append(result)
        save()
        if result.get('state') != 'returned':
            raise RuntimeError(json.dumps(result))
        return result['result']

    def slots(snapshot):
        return {(s['squadIndex'], p['position'], p['heroUuid']): sorted(
            (e['slot'], e['equipUuid']) for e in p['equips'])
            for s in snapshot['squads'] for p in s['positions']}

    def execute(command, payload=None):
        payload = payload or {}
        result = operation('command', command, payload)
        deadline = time.monotonic() + 35
        while not result.get('equipmentApplied'):
            if time.monotonic() >= deadline:
                raise TimeoutError('Unresolved equipment send; never replayed')
            time.sleep(.15)
            result = operation('command', command, dict(payload, queryOnly=True))
        assert result['completedSteps'] == result['plannedSteps']
        return result

    try:
        original = operation('command', 'feature018_equipment_snapshot')
        evidence['original'] = original
        desired = copy.deepcopy(original['squads'])
        selected = next(p for s in desired for p in s['positions'] if p['equips'])
        evidence['removedForProof'] = selected['equips'].pop(0)
        config = {'equipmentPresets': [
            {'id': f'r21-native-slot-{index}', 'name': f'Native scheme {index}',
             'squads': desired if index == 1 else original['squads']}
            for index in range(1, 5)],
            'initialEquipmentConfig': {'ownerUid': original['ownerUid'],
                'serverId': original['serverId'], 'squads': original['squads']}}
        evidence['saved'] = operation('equipmentConfig', 'equipment_config_save', config)
        assert evidence['saved'] == config
        evidence['applied'] = execute('feature018_hotkey_action', {'action': 'equipment', 'slot': 1})
        evidence['afterApply'] = operation('command', 'feature018_equipment_snapshot')
        assert slots(evidence['afterApply']) == slots({'squads': desired})
        evidence['restored'] = execute('feature018_equipment_restore')
        evidence['final'] = operation('command', 'feature018_equipment_snapshot')
        evidence['savedAfterRestore'] = operation('equipmentConfig', 'equipment_config_get')
        evidence['allSlotsRestored'] = slots(evidence['final']) == slots(original)
        evidence['allInventoryWearersRestored'] = {
            i['equipUuid']: i['heroUuid'] for i in evidence['final']['items']} == {
            i['equipUuid']: i['heroUuid'] for i in original['items']}
        assert evidence['allSlotsRestored'] and evidence['allInventoryWearersRestored']
        assert evidence['savedAfterRestore'] == config, 'Restore changed saved ordinals'
        evidence['state'] = 'proven'
    except Exception as error:
        evidence['state'] = 'partial'
        evidence['error'] = str(error)
        # Preserve any ambiguous native transaction for query-only inspection.
        # Do not launch a fresh restore while a previous send is unresolved.
        raise
    finally:
        save()
        print(output, flush=True)


if __name__ == '__main__':
    main()
