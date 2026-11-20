"""Controlled compatibility admission; no installed game reads or writes."""
import hashlib
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'src/LWBridge.GameScripts'))
import current_client_compat as compat


class RuntimeChecks(unittest.TestCase):
    def test_v36_family_allows_content_updates_with_matching_runtime_and_anchors(self):
        with tempfile.TemporaryDirectory() as folder:
            paths = {name: Path(folder) / name for name in
                     ('launcher', 'game', 'xlua', 'assembly', 'data', 'metadata', 'version')}
            for path in paths.values():
                path.write_bytes(b'data')
            paths['metadata'].write_text('4|0')
            paths['version'].write_text('36')
            keys = (paths['game'], paths['xlua'], paths['assembly'])
            digests = dict(zip(keys, compat.RUNTIME_1106))
            digests[paths['data']] = '1' * 64
            entries = [(name, bytes.fromhex(digest)) for name, digest in compat.V36_CRITICAL_ENTRIES.items()]
            reader = SimpleNamespace(ORIGINAL_LUA_ENTRY='preserved-marker', sha256_file=digests.__getitem__,
                crc32_file=lambda _: 0, read_lwlf=lambda _: (3, 36, entries))
            with patch.object(compat, '_sha256_bytes', bytes.hex):
                self.assertTrue(compat.inspect_current(reader, paths)['ok'])
                # Official updates can change the package hash and leave the marker behind.
                digests[paths['data']] = '2' * 64
                reader.read_lwlf = lambda _: (3, 37, entries)
                updated = compat.inspect_current(reader, paths)
                self.assertTrue(updated['ok'], updated['problems'])
                self.assertEqual(updated['observed']['packageSha256'], '2' * 64)
                self.assertEqual(updated['observed']['versionMarker'], 36)
                self.assertFalse(updated['observed']['versionMarkerMatchesContentVersion'])
                paths['metadata'].write_text('4|1')
                self.assertFalse(compat.inspect_current(reader, paths)['ok'])
                paths['metadata'].write_text('4|0')
                paths['version'].write_text('38')
                self.assertFalse(compat.inspect_current(reader, paths)['ok'])
                paths['version'].write_text('35')
                reader.read_lwlf = lambda _: (3, 35, entries)
                self.assertFalse(compat.inspect_current(reader, paths)['ok'])
                paths['version'].write_text('36')
                reader.read_lwlf = lambda _: (3, 36, entries)
                digests.update(zip(keys, (compat.EXPECTED_GAME_SHA256, compat.EXPECTED_XLUA_SHA256,
                                          compat.EXPECTED_ASSEMBLY_SHA256)))
                self.assertFalse(compat.inspect_current(reader, paths)['ok'])
                digests.update(zip(keys, compat.RUNTIME_1106))
                index = next(i for i, (name, _) in enumerate(entries) if name == 'Global/ConstDefine.luac')
                name, _ = entries[index]
                entries[index] = (name, bytes.fromhex(compat.CRITICAL_ENTRIES[name]))
                self.assertFalse(compat.inspect_current(reader, paths)['ok'])

    def test_coherent_runtimes_and_integrity(self):
        old = (compat.EXPECTED_GAME_SHA256, compat.EXPECTED_XLUA_SHA256, compat.EXPECTED_ASSEMBLY_SHA256)
        new = ('4bd47493eb3fda5d806c0df973839a96c41f8807596505762b54af7391009666',
               '43c33c9311570bb4161d1e61aefd0ff04148709e365332e8f7dd45d39a920a09',
               'dec38a33a63ab2d29e02adbfa2032c4ab3f7f910443cba62a1f8895b809e80d9')
        with tempfile.TemporaryDirectory() as folder:
            paths = {name: Path(folder) / name for name in
                     ('launcher', 'game', 'xlua', 'assembly', 'data', 'metadata', 'version')}
            for path in paths.values():
                path.write_bytes(b'data')
            paths['metadata'].write_text('4|0')
            paths['version'].write_text('31')
            anchor = b'controlled anchor'
            entries = [(name, anchor) for name in compat.CRITICAL_ENTRIES]
            expected = {name: hashlib.sha256(anchor).hexdigest() for name, _ in entries}
            runtime = {}
            reader = SimpleNamespace(ORIGINAL_LUA_ENTRY='preserved-marker',
                sha256_file=lambda path: runtime.get(path, hashlib.sha256(path.read_bytes()).hexdigest()),
                crc32_file=lambda _: 0, read_lwlf=lambda _: (3, 31, entries))
            with patch.object(compat, 'CRITICAL_ENTRIES', expected):
                for mask in range(8):
                    runtime.update({paths[key]: (new if mask & (1 << i) else old)[i]
                                    for i, key in enumerate(('game', 'xlua', 'assembly'))})
                    result = compat.inspect_current(reader, paths)
                    self.assertEqual(result['ok'], mask in (0, 7), (mask, result['problems']))
                runtime[paths['game']] = '0' * 64
                self.assertFalse(compat.inspect_current(reader, paths)['ok'])
                runtime[paths['game']] = new[0]
                paths['metadata'].write_text('wrong')
                self.assertFalse(compat.inspect_current(reader, paths)['ok'])
                paths['metadata'].write_text('4|0')
                entries[0] = (entries[0][0], b'changed critical entry')
                self.assertFalse(compat.inspect_current(reader, paths)['ok'])


if __name__ == '__main__':
    unittest.main()
