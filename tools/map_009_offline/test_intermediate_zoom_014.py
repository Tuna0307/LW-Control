import sys
import unittest
from pathlib import Path

BASE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(BASE))
from luaparser import ast
import run_overview_bridge as bridge
from map_009_offline.native_reply_instrument_013 import instrument_native_reply
from map_009_offline.intermediate_zoom_instrument_014 import instrument_intermediate_zoom


class IntermediateZoomIsolation(unittest.TestCase):
    def test_explicit_mid_zoom_preserves_original_production_bytes(self):
        original = bridge.resource_probe_source()
        mid = instrument_intermediate_zoom(instrument_native_reply(original))
        ast.parse(mid.decode("utf-8"))
        self.assertIn(b"target_zoom = math.min(", mid)
        self.assertIn(b"original_zoom + 30", mid)
        self.assertNotIn(b"camera_manager.Zoom = zoom_max end)", mid)
        self.assertEqual(original, bridge.resource_probe_source())
        packed = b"\n".join(line.rstrip() for line in mid.splitlines()
                             if line.strip() and not line.lstrip().startswith(b"--")) + b"\n"
        self.assertLess(len(bridge.lr.encode_lenc(packed)), 65536)

    def test_anchor_replacement_fails_closed(self):
        original = bridge.resource_probe_source()
        with self.assertRaisesRegex(ValueError, "source anchor drift"):
            instrument_intermediate_zoom(original.replace(
                b"camera_manager.Zoom = zoom_max", b"camera_manager.Zoom = zoom_max + 1"))


if __name__ == "__main__":
    unittest.main()
