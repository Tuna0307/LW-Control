"""No-game release manifest validation; never creates a package."""
import unittest
from pathlib import Path

from package_review_candidate import permitted, TOP_FILES, TOOLS_FILES, UI_FILES, SOURCE


class ReleaseManifestChecks(unittest.TestCase):
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
                     'runtimes/win-x64/native/unknown.dll'):
            with self.subTest(name=name):
                self.assertFalse(permitted(Path(name)))

    def test_existing_publish_is_entirely_allowlisted(self):
        if not SOURCE.is_dir():
            self.skipTest('No local published Windows tree exists')
        names = []
        for file in SOURCE.rglob('*'):
            if not file.is_file():
                continue
            relative = file.relative_to(SOURCE)
            if relative.suffix.lower() in ('.pdb', '.pyc') or '__pycache__' in relative.parts:
                continue
            self.assertTrue(permitted(relative), str(relative))
            names.append(relative.as_posix())
        self.assertIn('LWBridge.Desktop.exe', names)
        self.assertIn('ProductionUi/index.html', names)
        self.assertGreaterEqual(len(names), 60)


if __name__ == '__main__':
    unittest.main()
