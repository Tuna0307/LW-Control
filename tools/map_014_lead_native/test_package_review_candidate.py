"""No-game release manifest validation; never creates a package."""
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

from package_review_candidate import (
    permitted, excluded_non_target_runtime, NON_TARGET_RUNTIME_FILES,
    TOP_FILES, TOOLS_FILES, UI_FILES, SOURCE, map016_support_files, review_support_files,
)

# This shared ignored publish fixture was reused after MAP-017 and contains
# these nine later FEATURE-018 scripts. Their bytes are historical, so this
# file-name gate must not present it as a source-current R30 package proof.
# Keep this exact fixture exception TEST-ONLY: the historical MAP packager's
# permitted() remains unchanged and still refuses every one of these files.
LATER_FIXTURE_CONTENT = frozenset('OverviewBridge/' + name for name in (
    'feature018_attack_actions.lua', 'feature018_city_game.lua',
    'feature018_equipment_actions.lua', 'feature018_game_bridge.lua',
    'feature018_hotkey_actions.lua', 'feature018_hotkey_game.lua',
    'feature018_mini_game.lua', 'feature018_relocation_actions.lua',
    'feature018_sheep_solver.lua',
))


def fixture_content_is_declared(relative):
    return relative.as_posix() in LATER_FIXTURE_CONTENT


class ReleaseManifestChecks(unittest.TestCase):
    def test_later_fixture_content_is_exact_declared_project_content_only(self):
        project = SOURCE.parents[2] / 'src/LWBridge.Desktop/LWBridge.Desktop.csproj'
        declared = set()
        for content in ET.parse(project).iter('Content'):
            if (content.attrib.get('Link') == 'OverviewBridge\\%(Filename)%(Extension)' and
                    content.attrib.get('CopyToPublishDirectory') == 'PreserveNewest'):
                declared.update('OverviewBridge/' + name.replace('\\', '/').rsplit('/', 1)[-1]
                                for name in content.attrib.get('Include', '').split(';'))
        self.assertTrue(LATER_FIXTURE_CONTENT <= declared)
        for name in LATER_FIXTURE_CONTENT:
            self.assertTrue(fixture_content_is_declared(Path(name)), name)
            self.assertFalse(permitted(Path(name)), name)
        for name in ('OverviewBridge/feature018_credentials.lua',
                     'OverviewBridge/private/feature018_attack_actions.lua',
                     'LiveResourceProbe/feature018_attack_actions.lua',
                     'OverviewBridge/credentials.json'):
            self.assertFalse(fixture_content_is_declared(Path(name)), name)
            self.assertFalse(permitted(Path(name)), name)

    def test_map017_source_identity_and_report(self):
        files = review_support_files('MAP017', '2' * 40)
        self.assertEqual(files['LW-Control-UI/SOURCE-COMMIT.txt'],
                         ('2' * 40 + '\r\n').encode('ascii'))
        readme = files['LW-Control-UI/README-RELEASE.txt'].decode('utf-8')
        self.assertIn('MAP-017', readme)
        self.assertIn('docs/MAP_017_WORKER_REPORT.md', readme)
        self.assertNotIn('MAP_016_WORKER_REPORT', readme)

    def test_map016_release_has_packaged_proof_identity_and_no_old_actions(self):
        commit = '1' * 40
        files = map016_support_files(commit)
        self.assertEqual(set(files), {
            'LW-Control-UI/SOURCE-COMMIT.txt',
            'LW-Control-UI/Launch LWBridge.cmd',
            'LW-Control-UI/README-RELEASE.txt',
        })
        self.assertEqual(files['LW-Control-UI/SOURCE-COMMIT.txt'],
                         (commit + '\r\n').encode('ascii'))
        readme = files['LW-Control-UI/README-RELEASE.txt'].decode('utf-8')
        self.assertIn('UNPUBLISHED', readme)
        self.assertIn('removes scheduled/automatic plunder', readme)
        self.assertIn('remote raw world reply decoding remain unverified', readme)
        with self.assertRaises(ValueError):
            map016_support_files('not-a-real-commit')

    def test_required_runtime_roots_and_known_resources(self):
        for name in sorted(TOP_FILES | TOOLS_FILES | UI_FILES):
            with self.subTest(name=name):
                self.assertTrue(permitted(Path(name)))
        for name in ('WebUi/assets/index-CqTIRj8_.js',
                     'ProductionUi/assets/index-B5AeiHlM.css',
                     'ProductionUi/assets/dot-online-gjSbTzBh.png'):
            self.assertTrue(permitted(Path(name)), name)

    def test_unreviewed_private_paths_cannot_enter_package(self):
        for name in ('credentials.json', 'tokens.py',
                     'OverviewBridge/credentials.json',
                     'LiveResourceProbe/owner.json',
                     'ProductionUi/profile-key.json',
                     'WebUi/assets/credentials.json',
                     'WebUi/secret.txt',
                     'profiles/a/map-data.db',
                     'ProductionUi/assets/file-ABCDEFGH.json',
                     'runtimes/win-x64/native/unknown.dll',
                     'runtimes/linux-x64/native/private-key.json',
                     'runtimes/linux-x64/native/unexpected.so'):
            with self.subTest(name=name):
                self.assertFalse(permitted(Path(name)))
                self.assertFalse(excluded_non_target_runtime(Path(name)))

    def test_only_known_off_platform_runtime_assets_are_excluded(self):
        self.assertEqual(len(NON_TARGET_RUNTIME_FILES), 23)
        for name in NON_TARGET_RUNTIME_FILES:
            with self.subTest(name=name):
                self.assertTrue(excluded_non_target_runtime(Path(name)))
                self.assertFalse(permitted(Path(name)))
        for name in ('runtimes/win-x64/native/e_sqlite3.dll',
                     'runtimes/win-x64/native/WebView2Loader.dll'):
            self.assertTrue(permitted(Path(name)))
            self.assertFalse(excluded_non_target_runtime(Path(name)))

    def test_existing_shared_publish_fixture_has_only_explicit_known_content(self):
        if not SOURCE.is_dir():
            self.skipTest('No local published Windows tree exists')
        names = []
        for file in SOURCE.rglob('*'):
            if not file.is_file():
                continue
            relative = file.relative_to(SOURCE)
            if relative.suffix.lower() in ('.pdb', '.pyc') or '__pycache__' in relative.parts:
                continue
            self.assertTrue(permitted(relative) or excluded_non_target_runtime(relative) or
                            fixture_content_is_declared(relative), str(relative))
            if permitted(relative):
                names.append(relative.as_posix())
        self.assertIn('LWBridge.Desktop.exe', names)
        self.assertIn('ProductionUi/index.html', names)
        self.assertIn('runtimes/win-x64/native/e_sqlite3.dll', names)
        self.assertGreaterEqual(len(names), 60)


if __name__ == '__main__':
    unittest.main()
