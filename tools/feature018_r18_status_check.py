"""R18 status-only AFK and Equipment Scheme checks through an owned app queue.

Never sends keys, selects foreign accounts, unlocks land, or adds a game feature.
Saves a native equipment baseline, applies one-slot removal and restores it.
Every mutation is issued once; subsequent polls use the existing transaction.
"""
from pathlib import Path
import argparse
import copy
import json
import time
from feature018_live_queue import submit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    args = parser.parse_args()
    root = args.directory.resolve(strict=True)
    output = root / 'r18-task5-status.json'
    if output.exists():
        raise FileExistsError('Keep the existing one-shot action receipt; do not replay')
    evidence = {'state': 'running', 'operations': []}

    def save():
        output.write_text(json.dumps(evidence, indent=2), encoding='utf-8')

    def operation(kind, command, payload=None, required=True):
        result = submit(root, {'kind': kind, 'command': command, 'payload': payload or {}}, timeout=40)
        evidence['operations'].append(result)
        save()
        if required and result.get('state') != 'returned':
            raise RuntimeError(json.dumps(result))
        return result.get('result') if required else result

    def slots(snapshot):
        return {(s['squadIndex'], p['position'], p['heroUuid']): sorted(
            (e['slot'], e['equipUuid']) for e in p['equips'])
            for s in snapshot['squads'] for p in s['positions']}

    def apply(slot):
        result = operation('command', 'feature018_hotkey_action', {'action': 'equipment', 'slot': slot})
        deadline = time.monotonic() + 30
        while result.get('equipmentApplied') is not True:
            if time.monotonic() >= deadline:
                raise TimeoutError('Equipment transaction remains unresolved; no action replayed')
            result = operation('command', 'feature018_hotkey_action',
                {'action': 'equipment', 'slot': slot, 'queryOnly': True})
            time.sleep(.1)
        assert result['completedSteps'] == result['plannedSteps']
        return result

    try:
        original = operation('command', 'feature018_equipment_snapshot')
        evidence['equipmentOriginal'] = original
        desired = copy.deepcopy(original['squads'])
        selected = next(p for s in desired for p in s['positions'] if p['equips'])
        removed = selected['equips'].pop(0)
        config = {'equipmentPresets': [
            {'id': 'r18-one-slot', 'name': 'R18 one-slot scheme', 'squads': desired},
            {'id': 'r18-original', 'name': 'R18 exact original', 'squads': original['squads']},
        ]}
        evidence['equipmentRemovedForTest'] = removed
        operation('equipmentConfig', 'equipment_config_save', config)
        try:
            evidence['equipmentApply'] = apply(1)
            changed = operation('command', 'feature018_equipment_snapshot')
            evidence['equipmentAfterApply'] = changed
            assert slots(changed) == slots({'squads': desired}), 'scheme after-state differs'
        finally:
            current = operation('command', 'feature018_equipment_snapshot')
            if slots(current) != slots(original):
                evidence['equipmentRestore'] = apply(2)
            final = operation('command', 'feature018_equipment_snapshot')
            evidence['equipmentFinal'] = final
            evidence['allEquipmentSlotsRestored'] = slots(final) == slots(original)
            assert evidence['allEquipmentSlotsRestored'], 'native equipment was not restored'
            assert {i['equipUuid']: i['heroUuid'] for i in final['items']} == {
                i['equipUuid']: i['heroUuid'] for i in original['items']}, 'inventory wearer differs'
        profile = {'id': 'r18-ordinary-monster', 'name': 'R18 ordinary monster', 'kind': 'farm',
            'enabled': True, 'attackEnabled': True, 'joinEnabled': False, 'rally': False,
            'squadIndexes': [1], 'executionLimit': 1, 'targetKey': 'ordinary-monster',
            'continuousAttack': False, 'levelFilterEnabled': False, 'distanceFilterEnabled': False}
        config = {'enabled': True, 'strategies': [profile],
            'allianceDrill': {'enabled': False, 'activeRally': False, 'squadIndexes': [1]}}
        operation('profileCommand', 'monster_afk_config_save', config)
        evidence['monsterAfkStart'] = operation('command', 'automation_start',
            {'taskName': 'monsterSweep'}, required=False)
        evidence['monsterAfkStatus'] = operation('profileCommand', 'automation_status')['tasks']['monsterSweep']
        config['enabled'] = False
        config['strategies'] = []
        operation('profileCommand', 'monster_afk_config_save', config)
        evidence['monsterAfkDisabledAfterTest'] = operation('profileCommand', 'automation_status')['tasks']['monsterSweep']
        evidence['state'] = 'status_check_completed'
    except Exception as error:
        evidence['state'] = 'partial'
        evidence['error'] = str(error)
        raise
    finally:
        save()
        print(output, flush=True)


if __name__ == '__main__':
    main()
