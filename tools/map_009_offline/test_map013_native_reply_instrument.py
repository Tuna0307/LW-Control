import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from luaparser import ast
from native_reply_instrument_013 import instrument_native_reply
import run_map013_owned_observer as observer


class NativeReplyInstrumentTests(unittest.TestCase):
    def test_private_source_preserves_default_and_is_packagable(self):
        original = observer.overview.resource_probe_source()
        modified = instrument_native_reply(original)
        ast.parse(modified.decode())
        self.assertIn(b'M._map013Begin(request,server_lod)', modified)
        self.assertIn(b'details.map013NativeReplyState == "matched"', modified)
        self.assertIn(b'details.responseFlagsTransitioned == true', modified)
        self.assertIn(b'"_splitLastAOIRequest") ~= true', modified)
        packed = b'\n'.join(line.rstrip() for line in modified.splitlines()
                            if line.strip() and not line.lstrip().startswith(b'--')) + b'\n'
        self.assertLess(len(observer.overview.lr.encode_lenc(packed)), 65536)
        self.assertEqual(observer.overview.resource_probe_source(), original)

    def test_source_drift_refuses_instrumentation(self):
        original = observer.overview.resource_probe_source()
        with self.assertRaisesRegex(ValueError, 'anchor drift'):
            instrument_native_reply(original.replace(b'if request_completed then', b'if other then'))

    def test_camera_free_is_explicit_and_preserves_real_direct_sender(self):
        original = observer.overview.resource_probe_source()
        default = instrument_native_reply(original)
        modified = instrument_native_reply(original, camera_free_direct=True)
        ast.parse(modified.decode())
        self.assertNotIn(b'if request.requestMode ~= "direct" and\n', default)
        self.assertIn(b'if request.requestMode ~= "direct" and\n', modified)
        self.assertIn(b'"SendAoiRequest", 0, server_lod,', modified)
        self.assertIn(b'bulk_aoi_added_indices[#bulk_aoi_added_indices + 1] = index', modified)
