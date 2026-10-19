"""Controlled packaged startup proof; no window, game, or input is opened."""
from pathlib import Path
import argparse
import hashlib
import json
import sqlite3
import subprocess

ROOT = Path(__file__).resolve().parent.parent


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('publish', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    exe = (args.publish / 'LWBridge.Desktop.exe').resolve(strict=True)
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=False)
    source = ROOT / 'artifacts/map014-lead-ui/native-20261013-104453/profiles/map014-native-ui/map-data/map-data.db'
    preserved = []
    databases = []
    for profile in ('startup-A', 'startup-B'):
        folder = out / 'profiles' / profile
        (folder / 'map-data').mkdir(parents=True)
        (folder / 'runtime').mkdir()
        config = folder / 'runtime/config.json'
        config.write_text(json.dumps({'hotkeys': {'attack': False, 'frontlineReinforceCount': 17},
            'tasks': {'monsterSweep': {'enabled': False, 'strategies': [{'id': 'keep-shared-profile'}],
                'allianceDrill': {'enabled': False}}, 'staminaPotion': {'enabled': True}}}), encoding='utf-8')
        preserved.append(config)
        profile_db = folder / 'profile.db'
        with sqlite3.connect(profile_db) as db:
            db.execute('CREATE TABLE preservation_witness (value TEXT)')
            db.execute("INSERT INTO preservation_witness VALUES ('saved-city-draft-and-settings')")
        preserved.append(profile_db)
        target = folder / 'map-data/map-data.db'
        with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as original, sqlite3.connect(target) as db:
            original.backup(db)
            # Explicit controlled seed for a second server; not native scan evidence.
            db.execute('INSERT OR IGNORE INTO map_records SELECT kind,99999,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json FROM map_records WHERE server_id<>99999')
            assert db.execute('SELECT count(DISTINCT server_id) FROM map_records').fetchone()[0] >= 2
        databases.append(target)
    controller = out / 'controller.db'
    with sqlite3.connect(controller) as db:
        db.execute('CREATE TABLE profile_preservation (id TEXT)')
        db.executemany('INSERT INTO profile_preservation VALUES (?)', [('startup-A',), ('startup-B',)])
    preserved.append(controller)
    app_config = out / 'config.json'
    app_config.write_text(json.dumps({'profileId': 'startup-A', 'autoLaunchGame': False}), encoding='utf-8')
    preserved.append(app_config)
    hashes = {str(p.relative_to(out)): digest(p) for p in preserved}
    before = {}
    settings = {}
    for path in databases:
        with sqlite3.connect(path) as db:
            before[str(path.relative_to(out))] = db.execute('SELECT count(*) FROM map_records').fetchone()[0]
            settings[str(path)] = db.execute('SELECT * FROM app_settings ORDER BY key').fetchall()
    for launch in range(2):
        subprocess.run([str(exe), '--isolated-root', str(out), '--startup-state-proof', str(out / f'startup-{launch}.json')], check=True, timeout=60)
        assert all(digest(out / p) == sha for p, sha in hashes.items()), 'startup modified durable settings/profile files'
        for path in databases:
            with sqlite3.connect(path) as db:
                for table in ('map_records', 'scan_records', 'scan_runs', 'scan_blocks', 'treasure_claim_states'):
                    assert db.execute(f'SELECT count(*) FROM {table}').fetchone()[0] == 0, table
                assert db.execute('SELECT * FROM app_settings ORDER BY key').fetchall() == settings[str(path)]
                if launch == 0:
                    # A later session's scan must also be cleared at the next start.
                    db.execute("INSERT INTO map_records(kind,server_id,record_key,updated_at,data_json) VALUES ('city',12345,'restart-witness',1,'{}')")
    receipt = {'state': 'proven', 'controlled': True, 'nativeGameplay': False, 'packagedExe': str(exe),
        'oldRows': before, 'twoConsecutiveStartsCleared': True, 'durableFileHashesPreserved': hashes,
        'mapSettingsPreserved': True, 'gCountPreserved': 17}
    (out / 'receipt.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
    print('FEATURE018_R18_PACKAGED_STARTUP_PASS', json.dumps(receipt))


if __name__ == '__main__':
    main()
